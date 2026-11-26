using UnityEngine;

namespace AuteTurno.Conquest
{
    public sealed partial class ConquestPrototype
    {
        private float flareTime = -10;
        private int shownRollTurn = -1;
        private bool MyTurn => !onlineMode || myPlayer==game.ActivePlayer;
        private bool CanAct => MyTurn && !game.AwaitingRoll && !game.Finished && !sendingAction;

        private static int Illustration(Territory z)
        {
            if(z.IsCapital) return 8+Mathf.Clamp(z.Owner,0,1);
            if(z.Land==LandType.Plain) return z.HasBuilding?11:10;
            return (int)z.Land-1+(z.HasBuilding?4:0);
        }
        private static string LandTitle(Territory z)
        {
            switch(z.Land)
            {
                case LandType.Farm: return "Huerto";
                case LandType.Mine: return "Mina";
                case LandType.Factory: return "Fábrica";
                case LandType.Barracks: return "Cuartel";
                case LandType.Capital: return "Capital";
                default: return "Pradera";
            }
        }
        private static string Income(Territory z, int level)
        {
            int amount = ConquestGame.Production(z.Land, level);
            switch(z.Land)
            {
                case LandType.Farm: return amount+" de comida";
                case LandType.Mine: return amount+" de oro";
                case LandType.Factory: return amount+" materiales";
                case LandType.Barracks: return amount+" tropas";
                default: return "";
            }
        }
        public void SelectTerritoryAt(int x,int y)
        {
            if(screen!=AppScreen.Match || game==null || x<0 || x>=ConquestGame.Width || y<0 || y>=ConquestGame.Height) return;
            var zone=game.Board[x,y];
            if(selected!=null && selected.Owner==game.ActivePlayer && selected!=zone && MyTurn && game.IsAdjacent(selected,zone))
            { target=zone; return; }
            selected=zone; target=null;
        }
        private void MatchView()
        {
            int player=game.ActivePlayer;
            var p=game.Players[player];
            Fill(R(0,0,1280,720),Night);
            Frame(R(10,10,1260,74),Brass,Night);
            Emblem(R(25,18,49,58),player,"J"+(player+1));
            Label(R(85,16,244,24),Realm(player).ToUpperInvariant(),14,Honey,false,true);
            Label(R(84,40,240,38),p.Name,23,Ivory,true,true);
            Fill(R(332,23,1,48),Brass);
            Label(R(344,18,100,23),"RONDA",13,Mist,false,true,Center);
            Label(R(344,41,100,31),(game.CompletedTurns/2+1)+" / 30",20,Ivory,true,true,Center);
            DrawRoll();
            Resource(735,"ORO",p.Gold,Brass);
            Resource(909,"COMIDA",p.Food,C(0xc0a95e));
            Resource(1083,"MATERIALES",p.Materials,C(0xaabcc2));
            if(!game.AwaitingRoll && shownRollTurn!=game.CompletedTurns){shownRollTurn=game.CompletedTurns;flareTime=Time.unscaledTime;}
            Island();
            Inspector();
            Fill(R(16,644,1248,1),Brass);
            string status=game.AwaitingRoll ? p.Name+" puede lanzar los dados." : game.Message;
            if(onlineMode && networkStatusIsError) status=networkStatus;
            Label(R(26,650,902,63),status,16,Mist);
            bool end=CanAct;
            if(Button(R(956,655,298,52),MyTurn?"Terminar turno":"Esperando al rival",true,end))
            {
                if(onlineMode) SendAction("end"); else game.EndTurn();
                selected=target=null;
            }
        }
        private bool networkStatusIsError;
        private void Resource(int x,string title,int amount,Color tint)
        {
            Fill(R(x,24,2,44),tint);
            Label(R(x+16,16,150,26),title,13,Mist,false,true);
            Label(R(x+15,39,148,33),amount.ToString(),27,Ivory,true,true);
        }
        private void DrawRoll()
        {
            bool ready=game.AwaitingRoll && MyTurn && !onlineMode;
            var r=R(458,19,252,56);
            Frame(r,Brass,ready?Brass:Navy);
            Label(R(461,23,100,48),ready?"Tirar dados":"Tirada",17,ready?Night:Ivory,false,true,Center);
            Die(R(565,29,35,35),game.AwaitingRoll?0:game.DiceOne);
            Die(R(607,29,35,35),game.AwaitingRoll?0:game.DiceTwo);
            Label(R(644,22,59,48),game.AwaitingRoll?"—":"= "+game.DiceTotal,19,ready?Night:Honey,false,true,Center);
            if(ready && GUI.Button(r,GUIContent.none,GUIStyle.none)) game.RollForTurn();
        }
        private void Die(Rect r,int v)
        {
            Frame(r,Sepia,Ivory,1);
            if(v==0) { Label(r,"?",23,Ink,true,true,Center); return; }
            if(v%2==1) Dot(r,.5f,.5f);
            if(v>=2){Dot(r,.25f,.25f);Dot(r,.75f,.75f);}
            if(v>=4){Dot(r,.25f,.75f);Dot(r,.75f,.25f);}
            if(v==6){Dot(r,.25f,.5f);Dot(r,.75f,.5f);}
        }
        private void Dot(Rect r,float x,float y) => Tint(R(r.x+r.width*x-3,r.y+r.height*y-3,6,6),pip,Ink);

        private void Island()
        {
            var world=R(12,95,880,535);
            // Map the grassy shelf to the four board rows, leaving the cliff and water visible below.
            GUI.DrawTextureWithTexCoords(R(world.x,world.y,world.width,41),coast,R(0,.90f,1,.10f));
            GUI.DrawTextureWithTexCoords(R(world.x,world.y+41,world.width,425),coast,R(0,.35f,1,.55f));
            GUI.DrawTextureWithTexCoords(R(world.x,world.y+466,world.width,69),coast,R(0,0,1,.35f));
            for(int y=0;y<ConquestGame.Height;y++)
            {
                float width=148+4*y, start=452-2.5f*width;
                for(int x=0;x<ConquestGame.Width;x++)
                {
                    var z=game.Board[x,y];
                    var tile=R(start+x*width,139+y*105,width-3,102);
                    DrawDomain(tile,z);
                    if(GUI.Button(tile,GUIContent.none,GUIStyle.none)) SelectTerritoryAt(x,y);
                }
            }
        }
        private void DrawDomain(Rect r,Territory z)
        {
            bool active=!game.AwaitingRoll && (z.IsCapital ? z.Owner==game.ActivePlayer :
                game.DiceTotal!=7 && z.ProductionNumber>0 && z.ProductionNumber==game.DiceTotal);
            Color rim=z.Owner<0?C(0x827b57):z.Owner==0?C(0x5bb5ff):C(0xf66f64);
            if(active) rim=C(0xffcf51);
            Fill(R(r.x+1,r.y+3,r.width,r.height),C(0x463d29));
            Fill(r,rim);
            Rect face=Inside(r,active||z.Owner>=0?3:1.5f);
            bool mirror=!z.IsCapital && (z.X+z.Y*3)%4>=2;
            Art(face,Illustration(z),false,mirror);
            if(active)
            {
                float t=Mathf.Clamp01(1-(Time.unscaledTime-flareTime)/1.6f);
                Fill(face,Alpha(game.ActivePlayer==0?C(0x509aff):C(0xff635f),.45f*t));
            }
            if(z==selected || z==target || r.Contains(Event.current.mousePosition))
            {
                var focus=Inside(r,4);
                Color c=z==target?Ivory:z==selected?Honey:Alpha(Ivory,.8f);
                for(int side=0;side<2;side++) for(int end=0;end<2;end++)
                {
                    float x=side==0?focus.x:focus.xMax-13, y=end==0?focus.y:focus.yMax-2;
                    Fill(R(x,y,13,2),c);
                    Fill(R(side==0?x:x+11,end==0?y:y-11,2,13),c);
                }
            }
            if(z.Owner>=0)
            {
                if(z.Level==ConquestGame.MaxLevel && !z.IsCapital) DrawPennant(r.x+12,r.y+30);
                Emblem(R(r.xMax-47,r.y+3,44,41),z.Owner,"N"+z.Level);
                Rect army=R(r.x+4,r.y+4,40,21);
                Fill(army,Alpha(Night,.94f)); Label(army,"T "+z.Troops,13,Ivory,false,true,Center);
            }
            Rect plate=R(r.center.x-(z.IsCapital?44:23),r.yMax-27,z.IsCapital?88:46,25);
            Tint(plate,medallion,Ink); Tint(Inside(plate,2),medallion,active?Honey:Paper);
            Label(plate,z.IsCapital?"CAPITAL":z.ProductionNumber.ToString(),z.IsCapital?13:19,Ink,!z.IsCapital,true,Center);
        }
        private void DrawPennant(float x,float y)
        {
            Fill(R(x-1,y-3,5,34),Ink);
            Fill(R(x,y-2,2,31),Honey);
            Tint(R(x+2,y,27,16),pennant,Ink);
            Tint(R(x+3,y+1,24,13),pennant,C(0xf6c256));
            Fill(R(x+7,y+1,3,13),Ivory);
        }
        private void Inspector()
        {
            Frame(R(904,95,364,535),Brass,Paper,2);
            var z=target??selected;
            Label(R(925,106,322,24),z==null?"ATLAS DE LA ISLA":target!=null?"TERRITORIO DE DESTINO":"TERRITORIO SELECCIONADO",13,Sepia,false,true);
            Label(R(923,132,323,49),z==null?"La isla de Aethria":LandTitle(z),33,Ink,true,true);
            var picture=R(925,191,322,122);
            Fill(picture,Brass);
            if(z==null) GUI.DrawTexture(Inside(picture,2),cover,ScaleMode.ScaleAndCrop);
            else
            {
                Art(Inside(picture,2),Illustration(z),true);
                if(z.Owner>=0 && z.Level==ConquestGame.MaxLevel && !z.IsCapital) DrawPennant(picture.x+16,picture.y+15);
            }
            if(z==null)
            {
                Label(R(926,326,322,44),game.AwaitingRoll?"Tu primera tirada":"Elige un territorio",24,Ink,true,true);
                Label(R(926,372,321,75),game.AwaitingRoll?"Tira los dados. Tu capital gana 1 tropa y los terrenos propios de ambos reinos producen con su número.":"Selecciona una casilla de tu reino y después una vecina para ver las acciones posibles.",18,Ink);
                Fill(R(926,464,318,1),Brass);
                Label(R(926,480,320,118),"Cartela · número de producción\nEscudo N1 / N2 / N3 · nivel\nT · tropas en la casilla\nOro · número activado o capital reforzada",16,Sepia);
                return;
            }
            Color band=z.Owner<0?C(0x495848):Team(z.Owner);
            Fill(R(926,323,320,35),band);
            string owner=z.Owner<0?"Territorio neutral":"J"+(z.Owner+1)+"   ·   Nivel "+z.Level+"   ·   "+(z.IsCapital?"Capital":z.HasBuilding?"Desarrollado":"Adquirido");
            Label(R(932,323,306,35),owner,16,Ivory,false,true,Center);
            string garrison=z.Troops==1?"1 tropa":z.Troops+" tropas";
            Label(R(926,369,322,32),garrison+(z.IsCapital?"   ·   Sede del reino":"   ·   Tirada "+z.ProductionNumber),19,Ink,true,true);
            string income = z.IsCapital ? "+1 tropa en cada tirada de su dueño."
                : z.Owner < 0 ? "Al reclamarla, produce "+Income(z,1)+"."
                : "Produce "+Income(z,z.Level)+" con un "+z.ProductionNumber+".";
            Label(R(926,400,320,42),income,16,Sepia);
            bool available=CanAct && selected.Owner==game.ActivePlayer;
            if(target!=null)
            {
                if(target.Owner==game.ActivePlayer)
                { if(Button(R(926,458,320,44),"Mover una tropa",true,available && selected.Troops>=2)) DoAction("move"); }
                else
                {
                    if(target.Owner<0 && Button(R(926,449,320,42),"Comprar   ·   5 oro",true,available && game.Players[game.ActivePlayer].Gold>=5)) DoAction("buy");
                    bool preparing=selected.AttackReadyTurn>game.CompletedTurns;
                    if(target!=null && Button(R(926,500,320,42),preparing?"Ataca en tu próximo turno":"Atacar",false,available && !preparing && selected.Troops>=2)) DoAction("attack");
                }
                if(Button(R(926,560,320,41),"Cancelar destino")) target=null;
            }
            else if(z.Owner==game.ActivePlayer && MyTurn)
            {
                var p=game.Players[game.ActivePlayer];
                if(!z.IsCapital && z.Level<ConquestGame.MaxLevel)
                {
                    int gold=ConquestGame.UpgradeGold(z.Level), mat=ConquestGame.UpgradeMaterials(z.Level);
                    Label(R(926,439,320,26),"N"+(z.Level+1)+" producirá "+Income(z,z.Level+1)+".",15,Sepia);
                    if(Button(R(926,469,320,40),"Mejorar a N"+(z.Level+1)+" · "+gold+" oro + "+mat+" mat.",true,available && p.Gold>=gold && p.Materials>=mat)) DoAction("build");
                }
                if((z.IsCapital || (z.HasBuilding && z.Land==LandType.Barracks)) &&
                    Button(R(926,516,320,43),"Entrenar   ·   2 oro + 2 comida",false,available && p.Gold>=2 && p.Food>=2)) DoAction("recruit");
                if(!z.IsCapital && z.Level==ConquestGame.MaxLevel)
                    Label(R(926,449,320,44),"Nivel máximo alcanzado",19,Ink,true,true);
                Label(R(926,567,320,44),z.AttackReadyTurn>game.CompletedTurns?"Recién adquirido. Podrá atacar en tu próximo turno.":"Elige una vecina para mover, comprar o atacar.",15,Sepia);
            }
            else Label(R(926,458,320,80),MyTurn?"Para actuar aquí, selecciona primero un territorio propio vecino.":"Puedes inspeccionar la isla mientras juega el rival.",18,Ink);
        }
        private void DoAction(string action)
        {
            if(!CanAct || selected==null) return;
            var destination=target;
            if(onlineMode){SendAction(action,selected,destination); return;}
            bool ok;
            switch(action)
            {
                case "buy": ok=game.PurchaseTerritory(selected,destination); break;
                case "move": ok=game.Move(selected,destination); break;
                case "attack": ok=game.Attack(selected,destination); break;
                case "build": ok=game.Build(selected); break;
                default: ok=game.Recruit(selected); break;
            }
            if(ok && destination!=null){selected=destination;target=null;}
        }
    }
}
