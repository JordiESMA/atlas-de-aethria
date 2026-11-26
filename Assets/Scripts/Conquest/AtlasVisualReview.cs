#if ATLAS_VISUAL_QA
using System.Collections;
using System.IO;
using UnityEngine;

namespace AuteTurno.Conquest
{
    public sealed partial class ConquestPrototype
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginVisualReview()
        {
            var view = FindAnyObjectByType<ConquestPrototype>();
            if(view==null) {Bootstrap(); view=FindAnyObjectByType<ConquestPrototype>();}
            view.StartCoroutine(view.CaptureViews());
        }
        private IEnumerator Snapshot(string name)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var tex=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
            tex.Apply();
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Capturas",Screen.width+"x"+Screen.height));
            Directory.CreateDirectory(dir);
            // Write uncompressed TGA so review builds need no extra Unity packages.
            using(var writer=new BinaryWriter(File.Create(Path.Combine(dir,name+".tga"))))
            {
                var header=new byte[18]; header[2]=2; header[16]=24;
                header[12]=(byte)tex.width; header[13]=(byte)(tex.width>>8);
                header[14]=(byte)tex.height; header[15]=(byte)(tex.height>>8);
                writer.Write(header);
                var data=tex.GetRawTextureData();
                for(int i=0;i<data.Length;i+=3){var red=data[i]; data[i]=data[i+2]; data[i+2]=red;}
                writer.Write(data);
            }
            Destroy(tex);
        }
        private IEnumerator CaptureViews()
        {
            Application.runInBackground=true;
            string testHost=System.Environment.GetEnvironmentVariable("ATLAS_QA_ROOM_URL");
            if(!string.IsNullOrEmpty(testHost))
            {
                yield return VerifyNetworkMatch(testHost);
                yield break;
            }
            yield return new WaitForSecondsRealtime(.5f);
            yield return Snapshot("Portada");
            names[0]="Alba"; names[1]="Oriol"; screen=AppScreen.Menu;
            yield return Snapshot("Reinos");
            screen=AppScreen.OnlineSetup; yield return Snapshot("Crear-sala");
            joining=true; yield return Snapshot("Unirse");
            shareAddress="http://192.168.1.20:3080"; networkStatus="Esperando al segundo jugador…";
            OnNetworkState(new RemoteGameState {status="waiting", code="A4F9CD"}, "");
            yield return Snapshot("Sala");
            Random.InitState(43); StartMatch();
            yield return Snapshot("Tablero");
            game.RollForTurn();
            int x=game.ActivePlayer==0?0:4, y=game.ActivePlayer==0?1:2;
            var capital=game.Board[x,y];
            SelectTerritoryAt(x,y);
            foreach(var zone in game.Board)
                if(zone.Land==LandType.Barracks && game.IsAdjacent(capital,zone))
                { SelectTerritoryAt(zone.X,zone.Y); break; }
            yield return Snapshot("Destino");
            DoAction("buy");
            if(selected.Level!=1) throw new System.Exception("Purchase did not yield level one");
            yield return Snapshot("Nivel-1");
            DoAction("build");
            if(selected.Level!=2) throw new System.Exception("Building did not yield level two");
            yield return Snapshot("Nivel-2");
            // Fund the presentation fixture so the third level can be inspected on the same board.
            game.Players[game.ActivePlayer].Gold=ConquestGame.UpgradeGold(selected.Level);
            DoAction("build");
            if(selected.Level!=3) throw new System.Exception("Upgrade did not yield level three");
            yield return Snapshot("Nivel-3");
            while(!game.Finished) { if(game.AwaitingRoll) game.RollForTurn(); game.EndTurn(); }
            screen=AppScreen.Result; yield return Snapshot("Resultado");
            Debug.Log("ATLAS_VISUAL_REVIEW_OK");
            Application.Quit();
        }

        private IEnumerator VerifyNetworkMatch(string url)
        {
            yield return null;
            if(string.IsNullOrEmpty(pendingInvite))
            {
                onlineMode=true;
                network.BaseUrl=url;
                network.CreateRoom("QA azul");
            }
            float deadline=Time.realtimeSinceStartup+90;
            int lastEnd=-1;
            bool lobbyReported=false;
            while(Time.realtimeSinceStartup<deadline)
            {
                if(screen==AppScreen.Lobby && !lobbyReported)
                { Debug.Log("ATLAS_QA_ROOM="+lobbyCode); lobbyReported=true; }
                if(screen==AppScreen.Match && CanAct && lastEnd!=game.CompletedTurns)
                { lastEnd=game.CompletedTurns; SendAction("end"); }
                if(screen==AppScreen.Result)
                {
                    bool ok=game.CompletedTurns==60 && game.Winner==-1 && game.Score(0)==42 && game.Score(1)==42;
                    Debug.Log("ATLAS_QA_ROOM_RESULT="+ok+" J"+(myPlayer+1));
                    Application.Quit(ok?0:1); yield break;
                }
                yield return null;
            }
            Debug.LogError("Atlas QA: partida incompleta. "+networkStatus); Application.Quit(1);
        }
    }
}
#endif
