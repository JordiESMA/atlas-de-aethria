using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuteTurno.Conquest
{
    /// <summary>Isometric 5x4 map made from softly beveled square territory tiles.</summary>
    public sealed class ConquestBoardVisual : MonoBehaviour
    {
        private const int MapLayer = 31;
        private readonly Dictionary<Territory, TileView> views = new Dictionary<Territory, TileView>();
        private readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        private readonly Dictionary<LandType, Material> terrainMaterials = new Dictionary<LandType, Material>();
        private readonly Dictionary<LandType, Texture2D> terrainTextures = new Dictionary<LandType, Texture2D>();
        private Camera mapCamera;
        private ConquestPrototype controller;
        private Transform mapRoot;
        private Material waterMaterial;
        private Font runtimeFont;

        private sealed class TileView
        {
            public Territory territory;
            public Transform root, structures;
            public MeshRenderer ground, border;
            public MeshCollider collider;
            public Material groundMat, borderMat;
            public int owner = -99, troops = -99, level = -99;
            public bool built;
        }

        public static ConquestBoardVisual Create(ConquestPrototype owner, ConquestGame game)
        {
            var existing = owner.GetComponent<ConquestBoardVisual>();
            if (existing != null) Destroy(existing.gameObject);
            var go = new GameObject("Mapa isométrico · Aethria");
            go.transform.SetParent(owner.transform, false);
            var visual = go.AddComponent<ConquestBoardVisual>();
            visual.Build(owner, game);
            return visual;
        }

        public void SetVisible(bool visible)
        {
            if (mapCamera != null) mapCamera.enabled = visible;
            if (mapRoot != null) mapRoot.gameObject.SetActive(visible);
        }

        public void Select(int x, int y)
        {
            if (controller != null) controller.SelectTerritoryAt(x, y);
        }

        public void Refresh(ConquestGame game, Territory selected, Territory target)
        {
            if (game == null) return;
            foreach (var item in views)
            {
                Territory z = item.Key; TileView v = item.Value;
                if (v.owner != z.Owner || v.troops != z.Troops || v.built != z.HasBuilding || v.level != z.Level)
                {
                    v.owner = z.Owner; v.troops = z.Troops; v.built = z.HasBuilding; v.level = z.Level;
                    UpdateTile(v);
                }
                bool activated = !game.AwaitingRoll && game.DiceTotal != 7 && z.ProductionNumber == game.DiceTotal;
                Color teamColor = game.ActivePlayer == 0 ? new Color(.15f, .48f, .98f) : new Color(.91f, .24f, .20f);
                Color terrain = terrainTextures.ContainsKey(z.Land) ? Color.white : TerrainColor(z.Land);
                v.groundMat.color = activated ? Color.Lerp(terrain, teamColor, .30f) : terrain;
                Color rim = activated
                    ? new Color(.98f, .78f, .28f)
                    : z.Owner == 0 ? new Color(.19f, .58f, .97f) : z.Owner == 1 ? new Color(.95f, .31f, .25f) : new Color(.47f, .46f, .35f);
                if (z == selected) rim = new Color(1f, .83f, .32f);
                if (z == target) rim = new Color(1f, .96f, .69f);
                v.borderMat.color = rim;
            }
        }

        private void Build(ConquestPrototype owner, ConquestGame game)
        {
            controller = owner;
            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            terrainTextures[LandType.Plain] = Resources.Load<Texture2D>("TerrainTiles/Grass");
            terrainTextures[LandType.Farm] = Resources.Load<Texture2D>("TerrainTiles/Farm");
            terrainTextures[LandType.Factory] = Resources.Load<Texture2D>("TerrainTiles/Factory");
            terrainTextures[LandType.Mine] = Resources.Load<Texture2D>("TerrainTiles/Mine");
            terrainTextures[LandType.Barracks] = Resources.Load<Texture2D>("TerrainTiles/Forest");
            terrainTextures[LandType.Capital] = terrainTextures[LandType.Plain];
            waterMaterial = MakeMaterial(new Color(.17f, .40f, .51f));
            mapRoot = new GameObject("Isla y territorios").transform; mapRoot.SetParent(transform, false);
            CreateCamera();
            CreateOcean();
            CreateIslandBase(game);
            for (int y = 0; y < ConquestGame.Height; y++)
            for (int x = 0; x < ConquestGame.Width; x++)
            {
                Territory z = game.Board[x, y];
                var root = new GameObject("Territorio " + x + "," + y).transform;
                root.SetParent(mapRoot, false); root.localPosition = Position(x, y);
                Mesh mesh = BuildBeveledSquarePrism(.90f, .3f, .12f);
                var mf = root.gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
                var mr = root.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = new Material(TerrainMaterial(z.Land));
                var mc = root.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mesh;
                var click = root.gameObject.AddComponent<ConquestTerritoryClick>(); click.Setup(this, z.X, z.Y);
                Transform border = CreateRing("Frontera", root, .88f, .026f, .154f);
                var borderRenderer = border.GetComponent<MeshRenderer>();
                borderRenderer.sharedMaterial = new Material(borderRenderer.sharedMaterial);
                var structures = new GameObject("Relieve, edificio y tropas").transform; structures.SetParent(root, false); structures.localPosition = Vector3.up * .17f;
                var view = new TileView { territory = z, root = root, ground = mr, collider = mc, structures = structures,
                    border = borderRenderer, groundMat = mr.sharedMaterial, borderMat = borderRenderer.sharedMaterial };
                views[z] = view; UpdateTile(view);
            }
            SetLayerRecursively(mapRoot.gameObject, MapLayer);
            Refresh(game, null, null);
        }

        private static Vector3 Position(int x, int y)
        {
            return new Vector3((x - 2f) * 1.92f, 0, (1.5f - y) * 1.76f);
        }

        private void CreateCamera()
        {
            Camera backgroundCamera = null;
            Camera[] sceneCameras = FindObjectsByType<Camera>();
            foreach (Camera sceneCamera in sceneCameras)
            {
                if (sceneCamera == null) continue;
                if (backgroundCamera == null) backgroundCamera = sceneCamera;
                else sceneCamera.enabled = false;
            }
            if (backgroundCamera == null)
            {
                var backgroundObject = new GameObject("Fondo de interfaz");
                backgroundCamera = backgroundObject.AddComponent<Camera>();
            }
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color(.055f, .12f, .14f);
            backgroundCamera.cullingMask = 0;
            backgroundCamera.depth = 0;

            var go = new GameObject("Cámara isométrica del tablero"); go.transform.SetParent(transform, false);
            mapCamera = go.AddComponent<Camera>(); mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(.10f, .42f, .54f); mapCamera.orthographic = true;
            mapCamera.orthographicSize = 3.75f; mapCamera.nearClipPlane = .1f; mapCamera.farClipPlane = 100f;
            mapCamera.rect = new Rect(.015f, .105f, .70f, .78f); mapCamera.depth = 1; mapCamera.cullingMask = 1 << MapLayer;
            go.transform.position = new Vector3(-.05f, 11f, -12f); go.transform.rotation = Quaternion.Euler(42f, 0, 0);
            go.transform.LookAt(new Vector3(0, 0, 0));
            var light = new GameObject("Luz cálida del mapa"); light.transform.SetParent(transform, false);
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0);
            var sun = light.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.15f; sun.color = new Color(1f, .91f, .73f);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .62f;
            RenderSettings.ambientLight = new Color(.62f, .69f, .64f);
        }

        private void CreateOcean()
        {
            GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Plane); sea.name = "Mar de Aethria"; sea.transform.SetParent(mapRoot, false);
            sea.transform.localPosition = new Vector3(0, -.62f, 0); sea.transform.localScale = new Vector3(3.2f, 1, 3.1f);
            sea.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            Texture2D waterTexture = CreateWaterTexture(); waterMaterial.mainTexture = waterTexture;
            if (waterMaterial.HasProperty("_BaseMap")) waterMaterial.SetTexture("_BaseMap", waterTexture);
            Destroy(sea.GetComponent<Collider>());
        }

        private void CreateIslandBase(ConquestGame game)
        {
            var baseObject = new GameObject("Costa elevada · perfil natural"); baseObject.transform.SetParent(mapRoot, false);
            baseObject.transform.localPosition = new Vector3(0, 0, 0);
            baseObject.AddComponent<MeshFilter>().sharedMesh = BuildCoastMesh();
            baseObject.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                MakeMaterial(new Color(.42f,.50f,.34f)), MakeMaterial(new Color(.36f,.30f,.22f))
            };
            for (int i = 0; i < 18; i++)
            {
                float angle = i * 2.399f; float r = 3.4f + (i % 5) * .35f;
                Vector3 at = new Vector3(Mathf.Cos(angle) * r, -.03f, Mathf.Sin(angle) * r);
                CreateRock(mapRoot, at, .18f + (i % 3) * .08f, new Color(.44f, .49f, .40f));
            }
        }

        private void UpdateTile(TileView view)
        {
            Territory z = view.territory;
            // Keep the sides of the island tile earthy and its top clearly tied to its terrain.
            view.groundMat.color = TerrainColor(z.Land);
            view.groundMat.SetColor("_BaseColor", TerrainColor(z.Land));
            for (int i = view.structures.childCount - 1; i >= 0; i--) Destroy(view.structures.GetChild(i).gameObject);
            if (!terrainTextures.TryGetValue(z.Land, out Texture2D terrainTexture) || terrainTexture == null)
                BuildTerrain(view.structures, z);
            if (!z.IsCapital && z.ProductionNumber > 0)
                CreateText(view.structures, z.ProductionNumber.ToString(), new Vector3(-.62f, .22f, .58f), .045f, new Color(.10f, .15f, .16f), new Color(1f, .94f, .72f));
            if (z.IsCapital) BuildCastle(view.structures, z.Owner);
            else if (z.HasBuilding) BuildBuilding(view.structures, z.Land);
            BuildTroops(view.structures, z.Owner, z.Troops);
            if (z.Owner >= 0 && !z.IsCapital)
            {
                Color team = z.Owner == 0 ? new Color(.18f, .47f, .86f) : new Color(.79f, .25f, .22f);
                CreateText(view.structures, "N" + z.Level, new Vector3(.62f, .22f, -.58f), .031f, Color.white, team);
            }
            SetLayerRecursively(view.structures.gameObject, MapLayer);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private void BuildTerrain(Transform parent, Territory z)
        {
            if (z.Land == LandType.Farm)
            {
                for (int row = 0; row < 4; row++)
                {
                    float zRow = -.50f + row * .32f;
                    CreateBlock(parent, "Surco de cultivo", new Vector3(0, .025f, zRow), new Vector3(1.28f, .025f, .035f), row % 2 == 0 ? new Color(.46f, .35f, .21f) : new Color(.55f, .42f, .25f));
                }
                for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 3; row++)
                    CreateCrop(parent, new Vector3(side * .46f, .04f, -.32f + row * .32f), new Color(.40f, .60f, .27f));
            }
            else if (z.Land == LandType.Factory)
            {
                for (int i = 0; i < 3; i++) CreateRock(parent, new Vector3(-.48f + i * .4f, .12f, -.36f), .16f, new Color(.43f, .44f, .39f));
            }
            else if (z.Land == LandType.Mine)
            {
                for (int i = 0; i < 4; i++) CreateRock(parent, new Vector3(-.45f + (i % 2) * .67f, .16f, -.22f + (i / 2) * .36f), .25f, new Color(.49f, .48f, .42f));
                CreatePrimitive(parent, PrimitiveType.Sphere, "Boca arqueada de la mina", new Vector3(.02f, .16f, .37f), new Vector3(.40f, .26f, .08f), new Color(.15f, .16f, .15f));
                CreateBlock(parent, "Dintel de madera", new Vector3(.02f, .30f, .37f), new Vector3(.48f, .055f, .12f), new Color(.39f, .27f, .16f));
            }
            else if (z.Land == LandType.Barracks)
            {
                CreateTree(parent, new Vector3(-.43f, 0, -.40f), .56f, new Color(.17f, .40f, .24f));
                CreateTree(parent, new Vector3(.45f, 0, -.33f), .44f, new Color(.20f, .48f, .25f));
            }
            else if (!z.IsCapital)
            {
                CreatePlant(parent, new Vector3(-.48f, .05f, -.40f), new Color(.53f, .73f, .35f));
                CreatePlant(parent, new Vector3(.42f, .05f, -.30f), new Color(.42f, .66f, .31f));
            }
        }

        private void BuildBuilding(Transform p, LandType land)
        {
            switch (land)
            {
                case LandType.Farm: BuildFarmhouse(p); break;
                case LandType.Factory: BuildFactory(p); break;
                case LandType.Mine: BuildMineWorks(p); break;
                case LandType.Barracks: BuildBarracks(p); break;
            }
        }

        private void BuildFarmhouse(Transform p)
        {
            CreateBlock(p, "Casa de la granja", new Vector3(0, .40f, .05f), new Vector3(.70f, .62f, .60f), new Color(.73f, .59f, .39f));
            CreateRoof(p, new Vector3(0, .77f, .05f), .52f, .57f, new Color(.54f, .26f, .16f));
            CreateBlock(p, "Granero", new Vector3(.43f, .24f, .46f), new Vector3(.30f, .32f, .27f), new Color(.67f, .43f, .26f));
        }

        private void BuildFactory(Transform p)
        {
            CreateBlock(p, "Fábrica · nave", new Vector3(0, .40f, .10f), new Vector3(.98f, .70f, .76f), new Color(.48f, .51f, .47f));
            CreateBlock(p, "Tejado industrial", new Vector3(0, .80f, .10f), new Vector3(1.08f, .11f, .84f), new Color(.36f, .40f, .39f));
            for (int i = 0; i < 2; i++)
            {
                CreateBlock(p, "Chimenea", new Vector3(.30f + i * .22f, 1.0f, -.12f), new Vector3(.14f, .48f + i * .16f, .15f), new Color(.35f, .36f, .34f));
                CreateRock(p, new Vector3(.30f + i * .22f, 1.28f + i * .16f, -.12f), .14f, new Color(.72f, .76f, .68f));
            }
            CreateBlock(p, "Portón", new Vector3(-.12f, .22f, .49f), new Vector3(.27f, .38f, .045f), new Color(.24f, .29f, .29f));
        }

        private void BuildMineWorks(Transform p)
        {
            CreateBlock(p, "Torre de extracción", new Vector3(0, .64f, .04f), new Vector3(.16f, .98f, .16f), new Color(.42f, .29f, .18f));
            CreateBlock(p, "Viga de mina", new Vector3(0, 1.02f, .04f), new Vector3(.84f, .12f, .16f), new Color(.44f, .31f, .20f));
            CreateBlock(p, "Cabaña minera", new Vector3(-.40f, .30f, .15f), new Vector3(.42f, .42f, .44f), new Color(.49f, .37f, .25f));
            CreateRoof(p, new Vector3(-.40f, .55f, .15f), .31f, .30f, new Color(.35f, .27f, .21f));
            for (int i = 0; i < 3; i++) CreateRock(p, new Vector3(.30f + i * .18f, .15f, .40f), .16f, new Color(.53f, .52f, .47f));
        }

        private void BuildBarracks(Transform p)
        {
            CreateBlock(p, "Cuartel", Vector3.up * .36f, new Vector3(.86f, .58f, .57f), new Color(.58f, .40f, .27f));
            CreateRoof(p, new Vector3(0, .70f, 0), .57f, .51f, new Color(.35f, .29f, .22f));
            CreateBlock(p, "Torre de guardia", new Vector3(.47f, .33f, -.22f), new Vector3(.22f, .55f, .22f), new Color(.67f, .52f, .34f));
        }

        private void BuildCastle(Transform p, int owner)
        {
            Color stone = owner == 0 ? new Color(.63f, .72f, .77f) : new Color(.76f, .56f, .48f);
            Color roof = owner == 0 ? new Color(.16f, .36f, .64f) : new Color(.63f, .22f, .18f);
            CreateBlock(p, "Alcázar central", new Vector3(0, .48f, -.02f), new Vector3(.56f, .72f, .54f), stone);
            CreateRoof(p, new Vector3(0, .88f, -.02f), .36f, .34f, roof);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 at = new Vector3(x * .42f, .32f, z * .37f);
                CreateBlock(p, "Torreón", at, new Vector3(.22f, .52f, .22f), stone);
                CreateCone(p, at + Vector3.up * .28f, .16f, .25f, roof);
            }
            CreateBlock(p, "Muralla frontal", new Vector3(0, .25f, .40f), new Vector3(.60f, .28f, .12f), stone);
            CreateBlock(p, "Puerta de la fortaleza", new Vector3(0, .22f, .47f), new Vector3(.16f, .24f, .035f), new Color(.31f, .23f, .17f));
        }

        private void BuildTroops(Transform p, int owner, int count)
        {
            if (owner < 0 || count <= 0) return;
            Color cloth = owner == 0 ? new Color(.12f, .40f, .90f) : new Color(.86f, .22f, .18f);
            int shown = Mathf.Min(count, 4);
            for (int i = 0; i < shown; i++)
            {
                float x = i % 2 == 0 ? -.38f : .38f;
                float z = i < 2 ? -.48f : -.05f;
                CreateSoldier(p, new Vector3(x, 0, z), cloth);
            }
            CreateText(p, "" + count, new Vector3(.52f, .19f, .43f), .041f, Color.white, cloth);
        }

        private void CreateSoldier(Transform p, Vector3 at, Color cloth)
        {
            CreatePrimitive(p, PrimitiveType.Capsule, "Soldado", at + Vector3.up * .26f, new Vector3(.16f, .24f, .16f), cloth);
            CreatePrimitive(p, PrimitiveType.Sphere, "Casco y cabeza", at + Vector3.up * .58f, new Vector3(.16f, .13f, .16f), new Color(.84f, .69f, .49f));
            CreatePrimitive(p, PrimitiveType.Cylinder, "Casco", at + Vector3.up * .67f, new Vector3(.11f, .035f, .11f), new Color(.83f, .68f, .34f));
            CreatePrimitive(p, PrimitiveType.Cylinder, "Lanza", at + new Vector3(.13f, .43f, 0), new Vector3(.025f, .36f, .025f), new Color(.32f, .27f, .21f));
        }

        private void CreateTree(Transform parent, Vector3 at, float size, Color leaves)
        {
            CreatePrimitive(parent, PrimitiveType.Cylinder, "Tronco", at + Vector3.up * size * .34f, new Vector3(size * .10f, size * .35f, size * .10f), new Color(.36f, .25f, .15f));
            CreateCone(parent, at + Vector3.up * size * .24f, size * .43f, size * .67f, leaves);
            CreateCone(parent, at + Vector3.up * size * .55f, size * .33f, size * .62f, Color.Lerp(leaves, new Color(.17f, .34f, .22f), .22f));
            CreateCone(parent, at + Vector3.up * size * .84f, size * .23f, size * .55f, Color.Lerp(leaves, Color.white, .10f));
        }

        private void CreateCone(Transform parent, Vector3 at, float radius, float height, Color color)
        {
            var go = new GameObject("Copa facetada"); go.transform.SetParent(parent, false); go.transform.localPosition = at;
            var filter = go.AddComponent<MeshFilter>(); filter.sharedMesh = BuildCone(radius, height, 6);
            go.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(color);
        }

        private void CreatePlant(Transform parent, Vector3 at, Color color)
        {
            CreatePrimitive(parent, PrimitiveType.Sphere, "Matorral", at + Vector3.up * .12f, new Vector3(.24f, .22f, .22f), color);
        }

        private void CreateCrop(Transform parent, Vector3 at, Color color)
        {
            CreatePrimitive(parent, PrimitiveType.Cylinder, "Tallo de cultivo", at + Vector3.up * .095f, new Vector3(.025f, .11f, .025f), new Color(.35f, .42f, .20f));
            CreatePrimitive(parent, PrimitiveType.Sphere, "Hoja de cultivo", at + new Vector3(-.055f, .13f, 0), new Vector3(.10f, .045f, .075f), color);
            CreatePrimitive(parent, PrimitiveType.Sphere, "Hoja de cultivo", at + new Vector3(.055f, .16f, .015f), new Vector3(.095f, .05f, .075f), Color.Lerp(color, new Color(.75f, .72f, .34f), .2f));
        }

        private void CreateRock(Transform parent, Vector3 at, float size, Color color)
        {
            CreatePrimitive(parent, PrimitiveType.Sphere, "Roca facetada", at + Vector3.up * size * .36f, new Vector3(size, size * .75f, size * .83f), color);
        }

        private void CreateBlock(Transform parent, string label, Vector3 at, Vector3 size, Color color)
        { CreatePrimitive(parent, PrimitiveType.Cube, label, at, size, color); }

        private void CreateRoof(Transform parent, Vector3 at, float width, float depth, Color color)
        {
            GameObject roof = new GameObject("Tejado a dos aguas"); roof.transform.SetParent(parent, false); roof.transform.localPosition = at;
            var filter = roof.AddComponent<MeshFilter>(); filter.sharedMesh = BuildRoof(width, depth);
            roof.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(color);
        }

        private GameObject CreatePrimitive(Transform parent, PrimitiveType type, string label, Vector3 at, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = label; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color);
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            return go;
        }

        private Transform CreateRing(string label, Transform parent, float outer, float width, float height)
        {
            var ring = new GameObject(label).transform; ring.SetParent(parent, false); ring.localPosition = Vector3.up * height;
            var filter = ring.gameObject.AddComponent<MeshFilter>(); filter.sharedMesh = BuildRing(outer, outer - width);
            ring.gameObject.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(Color.white);
            return ring;
        }

        private void CreateText(Transform parent, string value, Vector3 at, float size, Color color, Color backing)
        {
            Quaternion faceCamera = Quaternion.FromToRotation(Vector3.up, -mapCamera.transform.forward);
            float badgeSize = size >= .045f ? .36f : .25f;
            GameObject rim = CreatePrimitive(parent, PrimitiveType.Cylinder, "Borde del medallón", at, new Vector3(badgeSize, .035f, badgeSize), new Color(.31f,.24f,.12f));
            rim.transform.rotation = faceCamera;
            GameObject badge = CreatePrimitive(parent, PrimitiveType.Cylinder, "Medallón numérico", at - mapCamera.transform.forward * .018f, new Vector3(badgeSize * .87f, .035f, badgeSize * .87f), backing);
            badge.transform.rotation = faceCamera;
            var go = new GameObject("Número visible"); go.transform.SetParent(parent, false); go.transform.localPosition = at - mapCamera.transform.forward * .043f;
            go.transform.rotation = mapCamera.transform.rotation;
            var text = go.AddComponent<TextMesh>(); text.text = value; text.font = runtimeFont; text.fontSize = 96; text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = color;
            if (runtimeFont != null) go.GetComponent<MeshRenderer>().sharedMaterial = runtimeFont.material;
        }

        private static Mesh BuildBeveledSquarePrism(float halfSize, float depth, float bevel)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            Vector2[] points = BeveledSquarePoints(halfSize, bevel);
            var topCenter = new Vector3(0, depth * .5f, 0); vertices.Add(topCenter); uv.Add(new Vector2(.5f,.5f));
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = points[i];
                vertices.Add(new Vector3(p.x, depth * .5f, p.y)); uv.Add(new Vector2(.5f + p.x / (halfSize * 2f),.5f + p.y / (halfSize * 2f)));
            }
            for (int i = 0; i < points.Length; i++) { triangles.Add(0); triangles.Add((i + 1) % points.Length + 1); triangles.Add(i + 1); }
            int bottom = vertices.Count; vertices.Add(new Vector3(0, -depth * .5f, 0)); uv.Add(new Vector2(.5f,.5f));
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = points[i]; vertices.Add(new Vector3(p.x, -depth * .5f, p.y));
                uv.Add(new Vector2(.5f + p.x / (halfSize * 2f),.5f + p.y / (halfSize * 2f)));
            }
            for (int i = 0; i < points.Length; i++)
            {
                int n = (i + 1) % points.Length;
                triangles.Add(bottom); triangles.Add(bottom + n + 1); triangles.Add(bottom + i + 1);
                int topA = i + 1, topB = n + 1, lowA = bottom + i + 1, lowB = bottom + n + 1;
                triangles.Add(topA); triangles.Add(lowA); triangles.Add(topB);
                triangles.Add(topB); triangles.Add(lowA); triangles.Add(lowB);
            }
            var mesh = new Mesh { name = "Casilla cuadrada biselada con relieve" }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); return mesh;
        }

        private static Vector2[] BeveledSquarePoints(float halfSize, float bevel)
        {
            float cut = Mathf.Clamp(bevel, .01f, halfSize * .45f);
            return new[]
            {
                new Vector2(-halfSize + cut, -halfSize), new Vector2(halfSize - cut, -halfSize),
                new Vector2(halfSize, -halfSize + cut), new Vector2(halfSize, halfSize - cut),
                new Vector2(halfSize - cut, halfSize), new Vector2(-halfSize + cut, halfSize),
                new Vector2(-halfSize, halfSize - cut), new Vector2(-halfSize, -halfSize + cut)
            };
        }

        private Material TerrainMaterial(LandType land)
        {
            if (terrainMaterials.TryGetValue(land, out Material cached)) return cached;
            terrainTextures.TryGetValue(land, out Texture2D texture);
            var material = new Material(MakeMaterial(texture != null ? Color.white : TerrainColor(land)));
            if (texture == null) texture = CreateTerrainTexture(land);
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            terrainMaterials[land] = material; return material;
        }

        private static Texture2D CreateTerrainTexture(LandType land)
        {
            const int size = 128; var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float broad = Mathf.PerlinNoise(u * 8f + (int)land * 4.7f, v * 8f + (int)land * 3.1f);
                float grain = Mathf.PerlinNoise(u * 47f, v * 47f);
                float shade = .72f + broad * .20f + grain * .08f;
                if (land == LandType.Farm)
                {
                    float furrow = Mathf.Abs(Mathf.Sin((u * .86f + v * .50f) * 30f));
                    shade *= .78f + furrow * .22f;
                }
                else if (land == LandType.Mine)
                {
                    float seam = Mathf.PerlinNoise(u * 23f + .4f, v * 23f + 1.7f);
                    shade *= seam < .24f ? .67f : 1f;
                }
                else if (land == LandType.Factory)
                {
                    float track = Mathf.Abs(Mathf.Sin((u + v * .10f) * 42f));
                    shade *= track < .07f ? .64f : 1f;
                }
                pixels[y * size + x] = new Color(shade, shade, shade, 1f);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            texture.SetPixels(pixels); texture.Apply(true); return texture;
        }

        private static Texture2D CreateWaterTexture()
        {
            const int size = 256;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float swell = Mathf.PerlinNoise(u * 5.5f + .4f, v * 5.5f + .8f);
                float ripple = Mathf.Sin((u * .72f + v * .28f) * 52f + swell * 4f) * .035f;
                float grain = (Mathf.PerlinNoise(u * 36f, v * 36f) - .5f) * .08f;
                float shade = .92f + ripple + grain + swell * .08f;
                pixels[y * size + x] = new Color(shade, shade, shade, 1f);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            texture.SetPixels(pixels); texture.Apply(true); return texture;
        }

        private static Mesh BuildCoastMesh()
        {
            const int sides = 20; const float topY = -.24f, bottomY = -.62f;
            var vertices = new List<Vector3> { new Vector3(0, topY, 0) };
            var topRing = new List<Vector3>(); var bottomRing = new List<Vector3>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float variation = .93f + .07f * Mathf.Sin(i * 2.17f) + .035f * Mathf.Sin(i * 4.91f);
                float x = Mathf.Cos(a) * 5.18f * variation, z = Mathf.Sin(a) * 4.12f * variation;
                topRing.Add(new Vector3(x, topY, z)); bottomRing.Add(new Vector3(x * .97f, bottomY, z * .97f));
            }
            vertices.AddRange(topRing); vertices.AddRange(bottomRing);
            var topTriangles = new List<int>(); var sideTriangles = new List<int>(); int bottomStart = 1 + sides;
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                topTriangles.Add(0); topTriangles.Add(next + 1); topTriangles.Add(i + 1);
                int top = i + 1, nextTop = next + 1, low = bottomStart + i, nextLow = bottomStart + next;
                sideTriangles.Add(top); sideTriangles.Add(nextTop); sideTriangles.Add(low);
                sideTriangles.Add(nextTop); sideTriangles.Add(nextLow); sideTriangles.Add(low);
            }
            var mesh = new Mesh { name = "Costa orgánica low-poly", subMeshCount = 2 };
            mesh.SetVertices(vertices); mesh.SetTriangles(topTriangles, 0); mesh.SetTriangles(sideTriangles, 1); mesh.RecalculateNormals(); return mesh;
        }

        private static Mesh BuildRing(float outer, float inner)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Vector2[] outside = BeveledSquarePoints(outer, outer * .14f);
            Vector2[] inside = BeveledSquarePoints(inner, inner * .14f);
            for (int i = 0; i < outside.Length; i++)
            {
                vertices.Add(new Vector3(outside[i].x, 0, outside[i].y)); vertices.Add(new Vector3(inside[i].x, 0, inside[i].y));
            }
            for (int i = 0; i < outside.Length; i++)
            {
                int n = (i + 1) % outside.Length, a = i * 2, b = n * 2;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
            }
            var mesh = new Mesh { name = "Marco de territorio biselado" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); return mesh;
        }

        private static Mesh BuildRoof(float width, float depth)
        {
            float w = width, d = depth, e = .22f;
            Vector3[] v = { new Vector3(-w, 0, -d), new Vector3(w, 0, -d), new Vector3(w, 0, d), new Vector3(-w, 0, d),
                new Vector3(0, e, -d), new Vector3(0, e, d) };
            int[] t = { 0,4,5, 0,5,3, 4,1,2, 4,2,5, 0,1,4, 3,5,2 };
            var mesh = new Mesh { name = "Tejado low-poly" }; mesh.vertices = v; mesh.triangles = t; mesh.RecalculateNormals(); return mesh;
        }

        private static Mesh BuildCone(float radius, float height, int sides)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
            }
            int apex = vertices.Count; vertices.Add(new Vector3(0, height, 0)); int center = vertices.Count; vertices.Add(Vector3.zero);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                triangles.Add(i); triangles.Add(apex); triangles.Add(next);
                triangles.Add(center); triangles.Add(next); triangles.Add(i);
            }
            var mesh = new Mesh { name = "Copa cónica low-poly" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); return mesh;
        }

        private static Color TerrainColor(LandType land)
        {
            switch (land)
            {
                case LandType.Farm: return new Color(.64f, .72f, .34f);
                case LandType.Factory: return new Color(.48f, .65f, .53f);
                case LandType.Mine: return new Color(.55f, .57f, .53f);
                case LandType.Barracks: return new Color(.38f, .63f, .40f);
                case LandType.Capital: return new Color(.69f, .72f, .51f);
                default: return new Color(.48f, .69f, .37f);
            }
        }

        private Material MakeMaterial(Color color)
        {
            int key = color.GetHashCode();
            if (materials.TryGetValue(key, out Material cached)) return cached;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            // Shader.Find can return null in a player build when Unity strips a shader
            // that is only requested dynamically. This Resources material keeps a
            // direct shader reference so the player includes a valid map shader.
            Material material;
            if (shader != null) material = new Material(shader);
            else
            {
                Material fallback = Resources.Load<Material>("AethriaMapBase");
                if (fallback == null || fallback.shader == null)
                {
                    Debug.LogError("No hay un shader disponible para dibujar el mapa. Revisa los shaders incluidos en el build.");
                    return null;
                }
                material = new Material(fallback);
            }
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .18f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            materials[key] = material;
            return material;
        }
    }

    public sealed class ConquestTerritoryClick : MonoBehaviour
    {
        private ConquestBoardVisual board; private int x, y;
        public void Setup(ConquestBoardVisual visual, int tileX, int tileY) { board = visual; x = tileX; y = tileY; }
        private void OnMouseUpAsButton() { if (board != null) board.Select(x, y); }
    }
}
