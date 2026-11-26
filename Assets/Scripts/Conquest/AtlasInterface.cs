using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuteTurno.Conquest
{
    public sealed partial class ConquestPrototype
    {
        private static readonly Color Night = C(0x101e29), Navy = C(0x1b3040), Brass = C(0xc49b60),
            Honey = C(0xf1d49e), Ivory = C(0xfff6dc), Mist = C(0xc2ced0), Ink = C(0x262b27),
            Paper = C(0xf1e5c9), Sepia = C(0x61563e), Azure = C(0x245580), Crimson = C(0x853f43);
        private Texture2D cover, domains, coast, shield, medallion, pip, shade, pennant;
        private Font displayFont, bodyFont;
        private GUIStyle inputStyle;
        private readonly Dictionary<(int, bool, bool, TextAnchor), GUIStyle> type = new Dictionary<(int, bool, bool, TextAnchor), GUIStyle>();
        private bool roomBusy, joining, inviteCopied;
        private string inviteInput = "", lobbyCode = "";
        private const TextAnchor Center = TextAnchor.MiddleCenter, Left = TextAnchor.MiddleLeft;
        private static Color C(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        private static Rect R(float x,float y,float w,float h) => new Rect(x,y,w,h);
        private static Rect Inside(Rect r,float inset) => R(r.x+inset,r.y+inset,r.width-inset*2,r.height-inset*2);
        private static Color Alpha(Color c,float a) { c.a=a; return c; }
        private static Color Team(int i) => i==0 ? Azure : Crimson;
        private static string Realm(int i) => i==0 ? "Reino del Alba" : "Reino de Umbra";

        private void LoadArtwork()
        {
            if (inputStyle != null) return;
            cover=Resources.Load<Texture2D>("AtlasArt/Citadel");
            domains=Resources.Load<Texture2D>("AtlasArt/Domains");
            coast=Resources.Load<Texture2D>("AtlasArt/Coast");
            displayFont = Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Cambria","Times New Roman"},40);
            bodyFont = Font.CreateDynamicFontFromOSFont(new[]{"Segoe UI","Arial"},20);
            pip = Mask(64,(x,y)=> x*x+y*y < .9f);
            pennant = Mask(64,(x,y)=>x < .45f + .55f*Mathf.Abs(y));
            shield = Mask(96,(x,y)=> Mathf.Abs(x)<.91f && y<.91f && y>-.93f+Mathf.Pow(Mathf.Abs(x),1.6f)*.55f);
            medallion = Mask(96,(x,y)=> Mathf.Abs(x)+.22f*Mathf.Abs(y)<1.03f && Mathf.Abs(y)<.94f);
            shade = new Texture2D(128,1,TextureFormat.RGBA32,false) {wrapMode=TextureWrapMode.Clamp};
            for(int i=0;i<128;i++) shade.SetPixel(i,0,Alpha(Night,Mathf.Lerp(.96f,.08f,Mathf.SmoothStep(0,1,i/127f))));
            shade.Apply();
            inputStyle = new GUIStyle(GUI.skin.textField) {font=bodyFont,fontSize=20,alignment=Left,
                padding=new RectOffset(12,12,0,0), richText=false,clipping=TextClipping.Clip};
            foreach (GUIStyleState state in new[]{inputStyle.normal,inputStyle.focused,inputStyle.hover,inputStyle.active})
            { state.background=Texture2D.whiteTexture; state.textColor=Ivory; }
        }

        private static Texture2D Mask(int size,Func<float,float,bool> contains)
        {
            var t=new Texture2D(size,size,TextureFormat.RGBA32,false) {wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
                pixels[y*size+x]=contains(2f*(x+.5f)/size-1,2f*(y+.5f)/size-1)?Color.white:Color.clear;
            t.SetPixels32(pixels); t.Apply(); return t;
        }

        private void OnGUI()
        {
            LoadArtwork();
            var saved=GUI.matrix;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            Fill(R(0,0,Screen.width,Screen.height),Night);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),
                Quaternion.identity,new Vector3(scale,scale,1));
            if(screen==AppScreen.Match) MatchView();
            else
            {
                GUI.DrawTexture(R(0,0,1280,720),cover,ScaleMode.ScaleAndCrop);
                GUI.DrawTexture(R(0,0,1280,720),shade);
                switch(screen)
                {
                    case AppScreen.HowTo: GuideView(); break;
                    case AppScreen.Menu: NamesView(); break;
                    case AppScreen.OnlineSetup: OnlineView(); break;
                    case AppScreen.Lobby: LobbyView(); break;
                    case AppScreen.Result: ResultView(); break;
                }
            }
            GUI.matrix=saved;
        }

        private static void Tint(Rect r,Texture texture,Color color)
        {
            var previous=GUI.color; GUI.color=color; GUI.DrawTexture(r,texture); GUI.color=previous;
        }
        private static void Fill(Rect r,Color color) => Tint(r,Texture2D.whiteTexture,color);
        private static void Frame(Rect r,Color edge,Color fill,float width=1)
        { Fill(r,edge); Fill(Inside(r,width),fill); }
        private void Label(Rect r,string value,int size,Color color,bool serif=false,bool bold=false,TextAnchor align=Left)
        {
            var key=(size,serif,bold,align);
            if(!type.TryGetValue(key,out var style))
            {
                style=new GUIStyle {font=serif?displayFont:bodyFont,fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,
                    alignment=align,wordWrap=true,richText=false,clipping=TextClipping.Clip};
                type[key]=style;
            }
            style.normal.textColor=color; GUI.Label(r,value,style);
        }
        private bool Button(Rect r,string title,bool primary=false,bool enabled=true)
        {
            bool hot=enabled && r.Contains(Event.current.mousePosition);
            Frame(r,enabled?(hot?Ivory:Brass):C(0x58606a),enabled?(primary?(hot?Honey:Brass):(hot?C(0x324c59):Navy)):C(0x37444a));
            Label(Inside(r,3),title,17,enabled?(primary?Night:Ivory):C(0xb6bbbc),false,true,Center);
            return enabled && GUI.Button(r,GUIContent.none,GUIStyle.none);
        }
        private string Field(Rect r,string id,string value,int limit)
        {
            Fill(r,GUI.GetNameOfFocusedControl()==id?Honey:Brass);
            GUI.SetNextControlName(id); GUI.backgroundColor=Night;
            string result=GUI.TextField(Inside(r,2),value,limit,inputStyle);
            GUI.backgroundColor=Color.white; return result;
        }

        // The atlas rows are wilderness, developed land and heraldic landmarks.
        private void Art(Rect r,int index,bool crop=false,bool mirror=false)
        {
            float u=.25f-.003f, v=1f/3-.004f;
            float x=index%4*.25f+.0015f, y=(2-index/4)/3f+.002f;
            if(crop)
            {
                float aspect=r.width/r.height;
                if(aspect>1) { float newV=v/aspect; y+=(v-newV)*.5f; v=newV; }
                else {float newU=u*aspect; x+=(u-newU)*.5f; u=newU;}
            }
            GUI.DrawTextureWithTexCoords(r,domains,R(mirror?x+u:x,y,mirror?-u:u,v));
        }
        private void Emblem(Rect r,int player,string value)
        {
            Tint(r,shield,Honey); Tint(Inside(r,2),shield,Team(player));
            Label(R(r.x,r.y-2,r.width,r.height),value,Mathf.RoundToInt(r.height*.39f),Ivory,true,true,Center);
        }
        private void Masthead(string section)
        {
            Fill(R(48,32,32,2),Brass);
            Label(R(90,19,590,29),"ATLAS DE AETHRIA   /   "+section,14,Honey,false,true);
        }
        private void GuideView()
        {
            Masthead("GUÍA DE JUEGO");
            Label(R(48,77,670,166),"Atlas de\nAethria",77,Ivory,true);
            Label(R(52,251,435,70),"Conquista la capital rival o lidera la isla al cabo de 30 rondas.",23,Ivory);
            Fill(R(50,394,1180,253),Alpha(Night,.96f));
            Fill(R(50,394,1180,2),Brass);
            Label(R(72,407,700,41),"Cómo jugar",31,Honey,true);
            GuideStep(72,"I","Lanza los dados","Tu capital gana 1 tropa, incluso con un 7. Los terrenos de ambos reinos producen si sale su número.");
            GuideStep(466,"II","Haz crecer tu reino","Compra por 5 de oro. El terreno produce desde nivel 1. Cada mejora añade una unidad, hasta nivel 3.");
            GuideStep(859,"III","Prepara el avance","Un terreno nuevo puede atacar en tu próximo turno. Sigue jugando o pulsa Terminar turno.");
            if(Button(R(924,648,306,51),"Preparar partida   ›",true)) screen=AppScreen.Menu;
        }
        private void GuideStep(int x,string index,string title,string desc)
        {
            Fill(R(x,468,2,140),Brass);
            Label(R(x+16,462,45,28),index,19,Brass,true,true);
            Label(R(x+16,494,337,38),title,27,Ivory,true);
            Label(R(x+16,536,328,88),desc,18,Mist);
        }
        private void NamesView()
        {
            Masthead("PREPARACIÓN");
            Label(R(50,68,750,73),"Elige tus reinos",58,Ivory,true);
            Label(R(52,146,590,40),"Cada jugador dirige una capital de la isla.",22,Ivory);
            NameCard(50,0); NameCard(658,1);
            Frame(R(50,526,586,131),Brass,Night);
            Frame(R(658,526,572,131),Brass,Night);
            Label(R(74,538,535,37),"Dos jugadores en este equipo",20,Ivory);
            Label(R(682,538,520,37),"Cada jugador en su ordenador",20,Ivory);
            if(Button(R(74,588,538,48),"Partida local",true)) StartMatch();
            if(Button(R(682,588,524,48),"Jugar online · P2P")) {networkStatus=""; joining=false; screen=AppScreen.OnlineSetup;}
            if(Button(R(50,666,160,39),"Cómo jugar")) screen=AppScreen.HowTo;
        }
        private void NameCard(int x,int player)
        {
            var w=player==0?586:572;
            Frame(R(x,228,w,269),Brass,Night);
            Art(R(x+2,230,204,265),8+player,true);
            Fill(R(x+205,230,4,265),Team(player));
            Emblem(R(x+227,248,44,51),player,"J"+(player+1));
            Label(R(x+284,248,265,51),player==0?"JUGADOR AZUL":"JUGADOR ROJO",15,Honey,false,true);
            Label(R(x+228,309,w-243,52),Realm(player),31,Ivory,true);
            Label(R(x+228,376,300,26),"Nombre del jugador",16,Mist);
            names[player]=Field(R(x+228,410,w-248,52),"name"+player,names[player],18);
        }
        private void OnlineView()
        {
            Masthead("MULTIJUGADOR");
            Frame(R(50,76,655,575),Brass,Night);
            Label(R(82,99,585,66),"Partida online",46,Ivory,true);
            if(Button(R(82,183,283,46),"Crear sala",!joining,!roomBusy)) { joining=false; networkStatus=""; }
            if(Button(R(375,183,296,46),"Unirse con invitación",joining,!roomBusy)) { joining=true; networkStatus=""; }
            Label(R(82,248,575,30),"Tu nombre",16,Honey,false,true);
            names[0]=Field(R(82,283,589,45),"onlineName",names[0],18);
            if(joining)
            {
                Label(R(82,340,588,24),"Enlace de invitación o dirección del anfitrión",16,Mist);
                inviteInput=Field(R(82,369,589,43),"invite",inviteInput,400);
                Label(R(82,419,588,26),"Código de sala para la unión manual",16,Mist);
                joinCode=Field(R(82,450,254,42),"code",joinCode,6).ToUpperInvariant();
                if(Button(R(82,515,589,50),"Unirse a la partida",true,!roomBusy))
                {
                    if(TryReadInvite(inviteInput,out var addr,out var code)){serverUrl=addr;joinCode=code;}
                    else serverUrl=inviteInput.Trim();
                    JoinOnlineRoom();
                }
            }
            else
            {
                Label(R(82,350,575,92),"Abre una sala y comparte el enlace con tu rival. La partida empieza cuando se conecte.",22,Ivory);
                if(Button(R(82,470,589,58),"Iniciar sala",true,!roomBusy)) {inviteCopied=false; CreateOnlineRoom();}
            }
            Label(R(82,576,585,60),networkStatus,16,Honey);
            if(joining && roomBusy)
            {
                if(Button(R(50,664,241,40),"Cancelar conexión"))
                { LeaveOnlineRoom(); networkStatus="Conexión cancelada."; }
            }
            else if(Button(R(50,664,187,40),"Volver al menú",false,!roomBusy)) screen=AppScreen.Menu;
        }
        private void LobbyView()
        {
            Masthead("SALA DE PARTIDA");
            Frame(R(50,82,670,553),Brass,Night);
            Label(R(82,105,608,68),"Esperando al rival",44,Ivory,true);
            Label(R(82,189,605,31),"CÓDIGO DE SALA",15,Honey,false,true);
            Label(R(80,220,607,94),lobbyCode,62,Ivory,true,true);
            Label(R(82,328,595,30),"Dirección que se incluirá en la invitación",17,Mist);
            shareAddress=Field(R(82,368,604,50),"share",shareAddress,100);
            Label(R(82,428,604,66),string.IsNullOrWhiteSpace(shareAddress)
                ? "No se detectó la dirección local. Escribe http:// seguido de la IPv4 del anfitrión y :3080 para compartir la sala."
                : "En la misma Wi-Fi se usa la dirección local. Desde otra red, el anfitrión necesita una dirección pública y el puerto TCP 3080 accesible.",17,Mist);
            if(Button(R(82,506,604,55),"Copiar enlace para invitar",true))
            {
                if(ValidServerUrl(shareAddress)){
                    GUIUtility.systemCopyBuffer="aethria://join?address="+Uri.EscapeDataString(shareAddress.Trim())+"&code="+lobbyCode;
                    inviteCopied=true;
                } else {inviteCopied=false; networkStatus="Introduce una dirección válida para compartir.";}
            }
            Label(R(82,572,600,41),inviteCopied?"Enlace copiado. Envíalo a tu rival.":networkStatus,16,Honey);
            if(Button(R(50,660,195,42),"Salir de la sala")) {LeaveOnlineRoom();screen=AppScreen.Menu;}
        }
        private void ResultView()
        {
            Masthead("RESULTADO");
            Frame(R(50,82,735,557),Brass,Night);
            Label(R(82,100,674,81),game.Winner<0?"Empate":"Victoria del "+Realm(game.Winner),40,Ivory,true);
            Label(R(82,184,667,85),game.Message,20,Mist);
            for(int i=0;i<2;i++)
            {
                int y=294+111*i;
                Frame(R(82,y,669,95),Team(i),Navy,2); Emblem(R(98,y+19,46,55),i,"J"+(i+1));
                Label(R(163,y+8,346,40),game.Players[i].Name,24,Ivory,true,true);
                int count=game.CountTerritories(i);
                Label(R(164,y+49,352,32),count==1?"1 territorio":count+" territorios",16,Mist);
                Label(R(523,y+9,213,71),game.Score(i)+" puntos",28,Honey,true,true,Center);
            }
            if(Button(R(82,551,669,55),"Volver al menú",true)) {LeaveOnlineRoom();game=null;selected=target=null;screen=AppScreen.Menu;}
        }
    }
}
