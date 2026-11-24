using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AuteTurno.Games;

namespace AuteTurno.UI
{
    /// <summary>Runtime prototype UI. All rule state lives in the independent game models.</summary>
    public sealed class ATuTurnoPrototype : MonoBehaviour
    {
        private enum Page { Home, Setup, Preview, Game, Result, Rps, Help, Settings, Profile }
        private Page page;
        private PlayMode mode;
        private bool seriesMode, randomSeries=true, chooseEachRound, previewReady, botBusy;
        private GamePace pace=GamePace.Normal;
        private int rounds=3, difficulty=1, quickGameIndex, color0, color1=1, timeout=15;
        private string name0="Jugador 1", name1="Jugador 2";
        private readonly HashSet<MiniGame> selectedGames=new HashSet<MiniGame>((MiniGame[])Enum.GetValues(typeof(MiniGame)));
        private readonly Color[] palette={new Color(.91f,.18f,.25f),new Color(.15f,.38f,.91f),new Color(.12f,.64f,.3f),new Color(.02f,.68f,.78f),new Color(.79f,.18f,.68f),new Color(.92f,.71f,.1f),new Color(.95f,.36f,.12f),new Color(.2f,.68f,.53f),new Color(.39f,.25f,.8f),new Color(.76f,.28f,.37f),new Color(.86f,.57f,.1f),new Color(.15f,.56f,.68f)};
        private Vector2 scroll;
        private GUIStyle title, centered, cell;
        private SeriesSession series;
        private int gameStarter, gameWinner=-2, rpsWins0,rpsWins1,rpsStage,rpsChoice0=-1,theme,statsFilter=-1;
        private bool soundEnabled=true,vibrationEnabled=true,confirmExit,orientationReady=true,requestedLandscape;
        private string resultMessage="";
        private float turnStart;
        private bool horizontal=true, handoff;
        private readonly Dictionary<int,int> memorySeen=new Dictionary<int,int>();
        private struct Outcome { public MiniGame Game; public int Winner; public Outcome(MiniGame game,int winner){Game=game;Winner=winner;} }
        private readonly List<Outcome> pendingOutcomes=new List<Outcome>();
        private bool pendingCommitted;

        private ConnectFourGame connect4;
        private DotsAndBoxesGame dots;
        private YahtzeeGame dice;
        private MemoryGame memory;
        private BattleshipGame fleet;
        private BlackjackGame blackjack;
        private BriscaGame brisca;
        private AudioSource audioSource;
        private AudioClip uiBeep;

        // Kept as an archived prototype for the original GDD; the conquest game is the active entry point.
        /*[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        { if(FindObjectOfType<ATuTurnoPrototype>()==null)new GameObject("¡A tu turno!").AddComponent<ATuTurnoPrototype>(); }*/

        private PlayerInfo Player(int p)
        { bool bot=p==1&&mode==PlayMode.Bot;return new PlayerInfo(p==0?name0:(bot?"Bot":name1),palette[p==0?color0:color1],bot); }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(Screen.width*.04f,Screen.height*.03f,Screen.width*.92f,Screen.height*.94f));
            switch(page){case Page.Home:DrawHome();break;case Page.Setup:DrawSetup();break;case Page.Preview:DrawPreview();break;case Page.Game:DrawGame();break;case Page.Result:DrawResult();break;case Page.Rps:DrawRps();break;}
            if(page==Page.Help)DrawHelp();else if(page==Page.Settings)DrawSettings();else if(page==Page.Profile)DrawProfile();
            if(GUI.changed&&Event.current.type==EventType.MouseUp)PlayUiSound();
            GUILayout.EndArea();
        }
        private void PlayUiSound(){if(soundEnabled&&audioSource!=null&&uiBeep!=null)audioSource.PlayOneShot(uiBeep);}
        private void Awake()
        {theme=PlayerPrefs.GetInt("atu.theme",0);soundEnabled=PlayerPrefs.GetInt("atu.sound",1)!=0;vibrationEnabled=PlayerPrefs.GetInt("atu.vibration",1)!=0;name0=PlayerPrefs.GetString("atu.profile.name","Jugador 1");audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;int rate=22050,count=rate/10;uiBeep=AudioClip.Create("A Tu Turno UI",count,1,rate,false);var samples=new float[count];for(int i=0;i<count;i++)samples[i]=Mathf.Sin(2*Mathf.PI*660*i/rate)*(1f-(float)i/count)*.1f;uiBeep.SetData(samples,0);if(FindAnyObjectByType<AudioListener>()==null)new GameObject("Audio Listener").AddComponent<AudioListener>();}
        private void EnsureStyles()
        { if(title==null){title=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Screen.height/19,26,48),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};centered=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Screen.height/40,16,25),alignment=TextAnchor.MiddleCenter,wordWrap=true};cell=new GUIStyle(GUI.skin.button){fontSize=22,fontStyle=FontStyle.Bold,fixedWidth=50,fixedHeight=50,margin=new RectOffset(2,2,2,2)};}Color textColor=theme==1?Color.white:Color.black;GUI.backgroundColor=theme==1?new Color(.85f,.25f,.92f):theme==2?new Color(.35f,.8f,.95f):Color.white;GUI.skin.button.normal.textColor=textColor;GUI.skin.label.normal.textColor=textColor;title.normal.textColor=textColor;centered.normal.textColor=textColor; }
        private bool Big(string label)=>GUILayout.Button(label,GUILayout.Height(Mathf.Clamp(Screen.height*.075f,45,72)));
        private void DrawHome()
        {
            GUILayout.Space(20);GUILayout.Label("¡A tu turno!",title);GUILayout.Label("Minijuegos para dos",centered);GUILayout.Space(20);
            if(Big("Contra bot")){mode=PlayMode.Bot;seriesMode=false;page=Page.Setup;}
            if(Big("En el mismo dispositivo")){mode=PlayMode.Local;seriesMode=false;page=Page.Setup;}
            GUI.enabled=false;Big("Online · necesita configurar servidor y autenticación");GUI.enabled=true;
            GUILayout.Space(12);GUILayout.Label("7 juegos · partidas rápidas y series",centered);
            GUILayout.BeginHorizontal();if(Big("Cómo jugar"))page=Page.Help;if(Big("Ajustes"))LoadSettings();if(Big("Perfil"))page=Page.Profile;GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();GUILayout.Label("Las reglas siguen la guía de diseño del proyecto.",centered);
        }
        private void DrawSetup()
        {
            GUILayout.Label(seriesMode?"Configurar serie":"Preparar partida",title);
            scroll=GUILayout.BeginScrollView(scroll);
            name0=Edit("Nombre del jugador 1",name0);
            if(mode==PlayMode.Local)name1=Edit("Nombre del jugador 2",name1);else GUILayout.Label("Tu rival será el bot",centered);
            DrawColors();GUILayout.Space(6);
            GUILayout.BeginHorizontal();ToggleButton("Juego rápido",!seriesMode,()=>seriesMode=false);ToggleButton("Serie",seriesMode,()=>seriesMode=true);GUILayout.EndHorizontal();
            if(seriesMode){GUILayout.Label("Rondas",centered);ChoiceRow(new[]{"3","5","10"},rounds==3?0:rounds==5?1:2,i=>rounds=new[]{3,5,10}[i]);
                GUILayout.BeginHorizontal();ToggleButton("Aleatorio",randomSeries&&!chooseEachRound,()=>{randomSeries=true;chooseEachRound=false;});ToggleButton("Seleccionar",!randomSeries&&!chooseEachRound,()=>{randomSeries=false;chooseEachRound=false;});ToggleButton("Partida a partida",chooseEachRound,()=>{randomSeries=false;chooseEachRound=true;});GUILayout.EndHorizontal();
                if(!randomSeries){foreach(MiniGame g in Enum.GetValues(typeof(MiniGame))){bool on=selectedGames.Contains(g);if(GUILayout.Toggle(on,GameRules.DisplayName(g),"Button",GUILayout.Height(38))!=on){if(on)selectedGames.Remove(g);else selectedGames.Add(g);}}GUILayout.Label("Elige al menos dos juegos para la serie.",centered);}
            } else {GUILayout.Label("Juego",centered);foreach(MiniGame g in Enum.GetValues(typeof(MiniGame)))ToggleButton(GameRules.DisplayName(g),quickGameIndex==(int)g,()=>quickGameIndex=(int)g);}
            GUILayout.Label("Ritmo",centered);ChoiceRow(new[]{"Rápido","Normal","Lento"},(int)pace,i=>pace=(GamePace)i);
            if(mode==PlayMode.Bot){GUILayout.Label("Dificultad",centered);ChoiceRow(new[]{"Fácil","Normal","Difícil"},difficulty,i=>difficulty=i);}
            if(quickGameIndex==(int)MiniGame.ConnectFour){GUILayout.Label("Tiempo por turno",centered);ChoiceRow(new[]{"15 s","30 s"},timeout==15?0:1,i=>timeout=i==0?15:30);}
            GUILayout.EndScrollView();GUILayout.Space(5);
            GUI.enabled=!seriesMode||randomSeries||selectedGames.Count>=2;
            if(Big(seriesMode?"Crear serie":"Jugar"))BeginMatch();GUI.enabled=true;
            if(Big("Volver"))page=Page.Home;
        }
        private void DrawColors()
        {
            GUILayout.Label("Color del jugador 1",centered);PaletteRow(color0,0);
            if(mode==PlayMode.Local){GUILayout.Label("Color del jugador 2",centered);PaletteRow(color1,1);if(color0==color1)GUILayout.Label("Elige colores distintos.",centered);}
        }
        private void PaletteRow(int selected,int player)
        {GUILayout.BeginHorizontal();for(int i=0;i<palette.Length;i++){Color old=GUI.backgroundColor;GUI.backgroundColor=palette[i];if(GUILayout.Toggle(i==selected,(i==selected?"✓":"●"),"Button",GUILayout.Height(32))) {if(player==0)color0=i;else if(i!=color0)color1=i;}GUI.backgroundColor=old;}GUILayout.EndHorizontal();}
        private string Edit(string label,string value){GUILayout.Label(label,centered);return GUILayout.TextField(value,20,GUILayout.Height(40));}
        private void ToggleButton(string label,bool value,Action action){if(GUILayout.Toggle(value,label,"Button",GUILayout.Height(40))&&!value)action();}
        private void ChoiceRow(string[] options,int selected,Action<int> choose){GUILayout.BeginHorizontal();for(int i=0;i<options.Length;i++){int n=i;if(GUILayout.Toggle(i==selected,options[i],"Button",GUILayout.Height(40)))choose(n);}GUILayout.EndHorizontal();}

        private void BeginMatch()
        {
            pendingOutcomes.Clear();pendingCommitted=false;
            if(color0==color1&&mode==PlayMode.Local){color1=(color0+1)%palette.Length;}
            if(!seriesMode){series=null;StartGame((MiniGame)quickGameIndex,UnityEngine.Random.Range(0,2));return;}
            IEnumerable<MiniGame> pool=randomSeries?(MiniGame[])Enum.GetValues(typeof(MiniGame)):selectedGames;
            series=new SeriesSession(Player(0),Player(1),rounds,pool,randomSeries||chooseEachRound);BeginPreview();
        }
        private void BeginPreview(){previewReady=false;page=Page.Preview;StartCoroutine(PreviewDelay());}
        private IEnumerator PreviewDelay(){yield return new WaitForSeconds(5);previewReady=true;}
        private void DrawPreview()
        {
            GUILayout.Label("Tu serie",title);GUILayout.Label("Modo: "+(mode==PlayMode.Local?"local":"contra bot")+" · "+series.Rounds+" partidas",centered);
            GUILayout.Label(name0+"  vs  "+(mode==PlayMode.Bot?"Bot":name1),centered);
            GUILayout.Label("Ritmo: "+new[]{"Rápido","Normal","Lento"}[(int)pace],centered);
            for(int i=0;i<series.Games.Count;i++)GUILayout.Label((i+1)+". "+(chooseEachRound?(i==series.RoundIndex?GameRules.DisplayName(series.Games[i]):"Se elige antes de jugar"):GameRules.DisplayName(series.Games[i])),centered);
            if(chooseEachRound){GUILayout.Label("Juego de esta ronda",centered);foreach(MiniGame g in selectedGames){MiniGame choice=g;ToggleButton(GameRules.DisplayName(g),series.CurrentGame==g,()=>series.Games[series.RoundIndex]=choice);}}
            GUILayout.FlexibleSpace();if(!previewReady)GUILayout.Label("Revisa la lista un momento…",centered);
            GUI.enabled=previewReady;if(Big("Continuar"))StartGame(series.CurrentGame,series.PlayerStartingThisRound);GUI.enabled=true;
            if(Big("Cancelar serie")){series=null;page=Page.Home;}
        }

        private void StartGame(MiniGame game,int starter)
        {
            StopAllCoroutines();gameStarter=starter;memorySeen.Clear();handoff=false;botBusy=false;gameWinner=-2;confirmExit=false;
            switch(game)
            {
                case MiniGame.ConnectFour:var c=GameRules.BoardSize(game,pace);connect4=new ConnectFourGame(c.width,c.height,starter);break;
                case MiniGame.DotsAndBoxes:var d=GameRules.BoardSize(game,pace);dots=new DotsAndBoxesGame(d.width,starter);break;
                case MiniGame.Yahtzee:dice=new YahtzeeGame(starter);break;
                case MiniGame.Memory:var m=GameRules.BoardSize(game,pace);memory=new MemoryGame(m.height,m.width,starter);break;
                case MiniGame.Battleship:var b=GameRules.BoardSize(game,pace);fleet=new BattleshipGame(b.width,starter);horizontal=true;break;
                case MiniGame.Blackjack:blackjack=new BlackjackGame(starter);break;
                case MiniGame.Brisca:brisca=new BriscaGame(starter);break;
            }
            requestedLandscape=game==MiniGame.DotsAndBoxes||game==MiniGame.Battleship;orientationReady=!Application.isMobilePlatform;page=Page.Game;turnStart=Time.time;
            if(Application.isMobilePlatform){Screen.orientation=requestedLandscape?ScreenOrientation.LandscapeLeft:ScreenOrientation.Portrait;StartCoroutine(WaitForOrientation());}
            StartCoroutine(InitializeGameAfterOrientation(game));
        }
        private IEnumerator InitializeGameAfterOrientation(MiniGame game)
        {while(!orientationReady&&page==Page.Game)yield return null;if(page!=Page.Game)yield break;if(game==MiniGame.Battleship&&mode==PlayMode.Bot)PlaceBotFleet();if(game==MiniGame.Memory)StartMemoryTurnIfBot();if(game==MiniGame.Brisca)StartBriscaIfBot();if(game==MiniGame.Yahtzee)StartDiceIfBot();if(game==MiniGame.DotsAndBoxes)StartDotsIfBot();if(game==MiniGame.Battleship&&fleet.CombatStarted&&IsBotTurn())StartCoroutine(BotFleet());}
        private MiniGame ActiveGame=>series!=null&&!series.IsComplete?series.CurrentGame:(MiniGame)quickGameIndex;
        private string ActivePlayerName(int p)=>p==0?name0:mode==PlayMode.Bot?"Bot":name1;
        private int ActivePlayer
        {get{switch(ActiveGame){case MiniGame.ConnectFour:return connect4.CurrentPlayer;case MiniGame.DotsAndBoxes:return dots.CurrentPlayer;case MiniGame.Yahtzee:return dice.CurrentPlayer;case MiniGame.Memory:return memory.CurrentPlayer;case MiniGame.Battleship:return fleet.CombatStarted?fleet.CurrentPlayer:fleet.PlacementPlayer;case MiniGame.Blackjack:return blackjack.Status==BlackjackStatus.Playing?blackjack.CurrentPlayer:0;case MiniGame.Brisca:return brisca.CurrentPlayer;default:return 0;}}}
        private void DrawGame()
        {
            int p=ActivePlayer;GUILayout.Label(series==null?GameRules.DisplayName(ActiveGame):GameRules.DisplayName(ActiveGame)+" · Juego "+(series.RoundIndex+1)+"/"+series.Rounds,title);
            if(!orientationReady){GUILayout.Space(35);GUILayout.Label("Gira el dispositivo a "+(requestedLandscape?"horizontal":"vertical")+" para jugar.",centered);GUILayout.Label("El tablero aparecerá cuando la orientación esté lista.",centered);return;}
            if(handoff){GUILayout.Space(35);GUILayout.Label("Pasa el móvil a "+ActivePlayerName(p),title);GUILayout.Label("Toca cuando esté listo.",centered);if(Big("Estoy listo")){handoff=false;turnStart=Time.time;StartGameBotIfNeeded();}return;}
            if(mode==PlayMode.Bot&&p==1&&ActiveGame!=MiniGame.Blackjack&&ActiveGame!=MiniGame.Battleship){GUILayout.Label("Turno del bot…",centered);}
            switch(ActiveGame){case MiniGame.ConnectFour:DrawConnectFour();break;case MiniGame.DotsAndBoxes:DrawDots();break;case MiniGame.Yahtzee:DrawDice();break;case MiniGame.Memory:DrawMemory();break;case MiniGame.Battleship:DrawFleet();break;case MiniGame.Blackjack:DrawBlackjack();break;case MiniGame.Brisca:DrawBrisca();break;}
            GUILayout.FlexibleSpace();if(!confirmExit){if(Big("Abandonar partida"))confirmExit=true;}else{GUILayout.Label("Si sales ahora, se perderá la partida o la serie sin guardarse.",centered);GUILayout.BeginHorizontal();if(Big("Seguir jugando"))confirmExit=false;if(Big("Salir")){series=null;confirmExit=false;page=Page.Home;}GUILayout.EndHorizontal();}
        }

        private void DrawConnectFour()
        {
            if(connect4.IsFinished){EndGame(connect4.Winner);return;}
            GUILayout.Label("Turno de "+ActivePlayerName(connect4.CurrentPlayer)+" · "+Mathf.CeilToInt(Mathf.Max(0,timeout-(Time.time-turnStart)))+" s",centered);
            cell.fixedWidth=Mathf.Max(28,Mathf.Min(50,Screen.width*.82f/connect4.Width-6));cell.fixedHeight=Mathf.Max(34,Mathf.Min(50,Screen.height*.075f));
            GUILayout.BeginHorizontal();GUILayout.FlexibleSpace();for(int x=0;x<connect4.Width;x++){int col=x;GUI.enabled=!IsBotTurn();if(GUILayout.Button("↓",GUILayout.Width(50),GUILayout.Height(40)))PlayColumn(col);}GUI.enabled=true;GUILayout.FlexibleSpace();GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();GUILayout.FlexibleSpace();scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(Mathf.Min(Screen.height*.52f,connect4.Height*48+8)));GUILayout.BeginVertical();
            for(int y=connect4.Height-1;y>=0;y--){GUILayout.BeginHorizontal();for(int x=0;x<connect4.Width;x++){int token=connect4.Cells[x,y];Color old=GUI.backgroundColor;GUI.backgroundColor=token==0?new Color(.86f,.89f,.94f):palette[token-1==0?color0:color1];GUILayout.Label(token==0?"○":"●",cell);GUI.backgroundColor=old;}GUILayout.EndHorizontal();}GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.FlexibleSpace();GUILayout.EndHorizontal();
        }
        private bool IsBotTurn()=>mode==PlayMode.Bot&&ActivePlayer==1;
        private void PlayColumn(int col){if(connect4.Play(col)){turnStart=Time.time;if(connect4.IsFinished)EndGame(connect4.Winner);else if(IsBotTurn())StartCoroutine(BotConnect4());}}
        private IEnumerator BotConnect4(){yield return new WaitForSeconds(.35f);if(page!=Page.Game||connect4.IsFinished)yield break;var moves=connect4.LegalColumns();int best=-1;foreach(int c in moves)if(connect4.WouldWin(c,connect4.CurrentPlayer)){best=c;break;}if(best<0)foreach(int c in moves)if(connect4.WouldWin(c,1-connect4.CurrentPlayer)){best=c;break;}if(best<0){int center=connect4.Width/2;moves.Sort((a,b)=>Math.Abs(a-center).CompareTo(Math.Abs(b-center)));best=moves[0];}int choice=best;if(UnityEngine.Random.value>=new[]{.6f,.8f,.95f}[difficulty]&&moves.Count>1){var alternatives=moves.Where(c=>c!=best).ToList();choice=alternatives[UnityEngine.Random.Range(0,alternatives.Count)];}connect4.Play(choice);turnStart=Time.time;if(connect4.IsFinished)EndGame(connect4.Winner);}
        private void DrawDots()
        {
            GUILayout.Label("Cajas: "+name0+" "+dots.Scores[0]+"  ·  "+(mode==PlayMode.Bot?"Bot":name1)+" "+dots.Scores[1],centered);
            int n=dots.PointCount;scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(Mathf.Min(Screen.height*.66f,n*70)));GUILayout.BeginVertical();
            for(int y=n-1;y>=0;y--){GUILayout.BeginHorizontal();for(int x=0;x<n;x++){GUILayout.Label("•",GUILayout.Width(24));if(x<n-1){int xx=x,yy=y;GUI.enabled=!IsBotTurn();if(GUILayout.Button(dots.HorizontalLine(x,y)?"━━":"──",GUILayout.Width(46),GUILayout.Height(36)))PlayDot(true,xx,yy);GUI.enabled=true;}}
                GUILayout.EndHorizontal();if(y>0){GUILayout.BeginHorizontal();for(int x=0;x<n;x++){int xx=x,yy=y-1;GUI.enabled=!IsBotTurn();if(x<n-1){GUILayout.Label(dots.BoxOwner(x,yy)==1?"①":dots.BoxOwner(x,yy)==2?"②":" □ ",GUILayout.Width(70));}else GUILayout.Label("",GUILayout.Width(24));if(GUILayout.Button(dots.VerticalLine(x,yy)?"║":"│",GUILayout.Width(24),GUILayout.Height(34)))PlayDot(false,xx,yy);GUI.enabled=true;}GUILayout.EndHorizontal();}}
            GUILayout.EndVertical();GUILayout.EndScrollView();
        }
        private void PlayDot(bool h,int x,int y){bool ok=h?dots.PlayHorizontal(x,y):dots.PlayVertical(x,y);if(ok&&dots.IsFinished)EndGame(dots.Winner);else if(ok&&IsBotTurn())StartDotsIfBot();}
        private void StartDotsIfBot(){if(IsBotTurn()&&!botBusy)StartCoroutine(BotDots());}
        private IEnumerator BotDots(){botBusy=true;yield return new WaitForSeconds(.35f);if(page==Page.Game&&ActiveGame==MiniGame.DotsAndBoxes&&IsBotTurn()){bool h;int x,y;if(UnityEngine.Random.value<new[]{.6f,.8f,.95f}[difficulty]&&dots.BestMove(out h,out x,out y))PlayDot(h,x,y);else{var legal=new List<Vector3Int>();for(int xx=0;xx<dots.PointCount-1;xx++)for(int yy=0;yy<dots.PointCount;yy++)if(!dots.HorizontalLine(xx,yy))legal.Add(new Vector3Int(xx,yy,0));for(int xx=0;xx<dots.PointCount;xx++)for(int yy=0;yy<dots.PointCount-1;yy++)if(!dots.VerticalLine(xx,yy))legal.Add(new Vector3Int(xx,yy,1));if(legal.Count>0){var move=legal[UnityEngine.Random.Range(0,legal.Count)];PlayDot(move.z==0,move.x,move.y);}}if(page==Page.Game&&!dots.IsFinished&&IsBotTurn())StartCoroutine(BotDots());}botBusy=false;}
        private void DrawDice()
        {
            int p=dice.CurrentPlayer;GUILayout.Label("Turno de "+ActivePlayerName(p)+" · tirada "+dice.RollsThisTurn+"/3",centered);GUILayout.Label("Dados: "+string.Join("   ",dice.Dice[p].Select((v,i)=>dice.Held[p][i]?"["+v+"]":v.ToString()).ToArray()),centered);
            GUI.enabled=!IsBotTurn();if(dice.RollsThisTurn<3&&Big("Lanzar dados"))dice.Roll();GUI.enabled=true;
            if(dice.RollsThisTurn>0){GUILayout.Label("Toca dados para guardarlos antes de otra tirada",centered);GUILayout.BeginHorizontal();for(int i=0;i<5;i++){int die=i;GUI.enabled=!IsBotTurn();bool hold=GUILayout.Toggle(dice.Held[p][i],dice.Dice[p][i].ToString(),"Button",GUILayout.Height(48));if(hold!=dice.Held[p][i])dice.ToggleHold(die);GUI.enabled=true;}GUILayout.EndHorizontal();}
            GUILayout.Label("Puntuaciones",centered);GUILayout.BeginVertical();for(int c=0;c<12;c++){int ix=c;var cat=(YahtzeeCategory)c;string score=dice.Sheet[0,c].HasValue?dice.Sheet[0,c].Value.ToString():"—";string score2=dice.Sheet[1,c].HasValue?dice.Sheet[1,c].Value.ToString():"—";GUI.enabled=dice.HasRolled&&!IsBotTurn()&&!dice.Sheet[p,c].HasValue;if(GUILayout.Button(cat+" · "+dice.Score(cat)+" pts   ["+score+" | "+score2+"]",GUILayout.Height(32)))ScoreDice(cat);GUI.enabled=true;}GUILayout.EndVertical();
        }
        private void ScoreDice(YahtzeeCategory cat){if(dice.KeepScore(cat)&&dice.IsFinished)EndGame(dice.Winner);else if(IsBotTurn())StartCoroutine(BotDice());}
        private IEnumerator BotDice(){if(botBusy)yield break;botBusy=true;yield return new WaitForSeconds(.3f);if(page==Page.Game&&dice.CurrentPlayer==1){if(!dice.HasRolled){dice.Roll();StartCoroutine(BotDiceAgain());}else{int best=-1,bestScore=-1;for(int c=0;c<12;c++)if(!dice.Sheet[1,c].HasValue){int s=dice.Score((YahtzeeCategory)c);if(s>bestScore){bestScore=s;best=c;}}if(best>=0)ScoreDice((YahtzeeCategory)best);}}botBusy=false;}
        private IEnumerator BotDiceAgain(){yield return new WaitForSeconds(.3f);if(dice.CurrentPlayer==1){bool strategic=UnityEngine.Random.value<new[]{.6f,.8f,.95f}[difficulty];for(int i=0;i<5;i++)dice.Held[1][i]=strategic?dice.Dice[1].Count(v=>v==dice.Dice[1][i])>=2:UnityEngine.Random.value<.35f;if(dice.RollsThisTurn<3)dice.Roll();if(dice.RollsThisTurn<3)StartCoroutine(BotDiceAgain());else{int best=0,score=-1;for(int c=0;c<12;c++)if(!dice.Sheet[1,c].HasValue&&dice.Score((YahtzeeCategory)c)>score){best=c;score=dice.Score((YahtzeeCategory)c);}if(!strategic){var available=Enumerable.Range(0,12).Where(c=>!dice.Sheet[1,c].HasValue).ToArray();best=available[UnityEngine.Random.Range(0,available.Length)];}ScoreDice((YahtzeeCategory)best);}}}
        private void DrawMemory()
        {
            GUILayout.Label("Parejas: "+name0+" "+memory.Scores[0]+" · "+(mode==PlayMode.Bot?"Bot":name1)+" "+memory.Scores[1],centered);
            GUILayout.BeginHorizontal();GUILayout.FlexibleSpace();GUILayout.BeginVertical();for(int y=0;y<memory.Rows;y++){GUILayout.BeginHorizontal();for(int x=0;x<memory.Columns;x++){int ix=y*memory.Columns+x;bool shown=memory.IsVisible(ix);GUI.enabled=!IsBotTurn()&&!shown;string label=shown?memory.Cards[ix].ToString():"?";if(GUILayout.Button(label,cell,GUILayout.Width(Mathf.Min(50,Screen.width*.84f/memory.Columns-4)),GUILayout.Height(Mathf.Min(50,Screen.height*.075f))))RevealMemory(ix);GUI.enabled=true;}GUILayout.EndHorizontal();}GUILayout.EndVertical();GUILayout.FlexibleSpace();GUILayout.EndHorizontal();
        }
        private void RevealMemory(int i){if(!IsBotTurn()){memorySeen[i]=memory.Cards[i];var r=memory.Reveal(i);if(r==MemoryRevealResult.Mismatch)StartCoroutine(ResolveMemoryMismatch());else if(r==MemoryRevealResult.Match)StartMemoryTurnIfBot();else if(r==MemoryRevealResult.Finished)EndGame(memory.Winner);}}
        private IEnumerator ResolveMemoryMismatch(){yield return new WaitForSeconds(.85f);memory.ResolveMismatch();if(memory.IsFinished)EndGame(memory.Winner);else StartMemoryTurnIfBot();}
        private void StartMemoryTurnIfBot(){if(IsBotTurn()&&!botBusy)StartCoroutine(BotMemory());}
        private IEnumerator BotMemory(){botBusy=true;yield return new WaitForSeconds(.45f);if(page==Page.Game&&IsBotTurn()){bool remember=UnityEngine.Random.value<new[]{.6f,.8f,.95f}[difficulty];var known=remember?memorySeen.GroupBy(k=>k.Value).FirstOrDefault(g=>g.Count()>=2&&g.Any(x=>!memory.Matched[x.Key])):null;int first=-1,second=-1;if(known!=null){var ids=known.Where(x=>!memory.Matched[x.Key]).Select(x=>x.Key).ToArray();if(ids.Length>=2){first=ids[0];second=ids[1];}}if(first<0){var choices=Enumerable.Range(0,memory.Cards.Length).Where(i=>!memory.Matched[i]).OrderBy(_=>UnityEngine.Random.value).Take(2).ToArray();first=choices[0];second=choices[1];}memorySeen[first]=memory.Cards[first];memory.Reveal(first);yield return new WaitForSeconds(.3f);memorySeen[second]=memory.Cards[second];var r=memory.Reveal(second);if(r==MemoryRevealResult.Mismatch){yield return new WaitForSeconds(.7f);memory.ResolveMismatch();}if(memory.IsFinished)EndGame(memory.Winner);else if(IsBotTurn())StartCoroutine(BotMemory());}botBusy=false;}

        private void DrawFleet()
        {
            if(!fleet.CombatStarted){int p=fleet.PlacementPlayer;GUILayout.Label(ActivePlayerName(p)+" · coloca barco de longitud "+BattleshipBoard.Fleet[fleet.Boards[p].ShipsPlaced],centered);GUILayout.BeginHorizontal();if(Big("Girar barco ↻"))horizontal=!horizontal;GUILayout.Label(horizontal?"Horizontal":"Vertical",centered);GUILayout.EndHorizontal();DrawFleetGrid(p,true);}
            else{GUILayout.Label("Turno de "+ActivePlayerName(fleet.CurrentPlayer)+" · "+fleet.LastMessage,centered);int p=fleet.CurrentPlayer;GUILayout.BeginHorizontal();GUILayout.BeginVertical();GUILayout.Label("Disparos al rival",centered);DrawFleetGrid(p,false);GUILayout.EndVertical();GUILayout.BeginVertical();GUILayout.Label("Tu flota",centered);DrawOwnFleetGrid(p);GUILayout.EndVertical();GUILayout.EndHorizontal();}
        }
        private void DrawOwnFleetGrid(int player){var b=fleet.Boards[player];GUILayout.BeginVertical();for(int y=b.Size-1;y>=0;y--){GUILayout.BeginHorizontal();for(int x=0;x<b.Size;x++)GUILayout.Label(b.Shots[x,y]==1?"×":b.Shots[x,y]==2?"·":b.Ships[x,y]>0?"■":"○",GUILayout.Width(Mathf.Min(24,Screen.width*.45f/b.Size)),GUILayout.Height(24));GUILayout.EndHorizontal();}GUILayout.EndVertical();}
        private void PlaceBotFleet(){fleet.AutoPlaceFleet(1,new System.Random());}
        private void DrawFleetGrid(int player,bool placing)
        {int size=fleet.Boards[player].Size;scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(Mathf.Min(Screen.height*.62f,size*34)));GUILayout.BeginVertical();for(int y=size-1;y>=0;y--){GUILayout.BeginHorizontal();for(int x=0;x<size;x++){int xx=x,yy=y;var own=fleet.Boards[player];var board=placing?own:fleet.Boards[1-player];string text=placing?(own.Ships[x,y]>0?"■":"·"):board.Shots[x,y]==2?"×":board.Shots[x,y]==1?"●":"·";GUI.enabled=placing||(!IsBotTurn()&&board.Shots[x,y]==0);float cellWidth=Mathf.Min(34,Screen.width*(placing ? .8f : .44f)/size);if(GUILayout.Button(text,cell,GUILayout.Width(cellWidth),GUILayout.Height(32))){if(placing){int before=fleet.PlacementPlayer;string error=fleet.PlaceShip(xx,yy,horizontal);if(error!=null)resultMessage=error;else if(fleet.CombatStarted){turnStart=Time.time;if(mode==PlayMode.Local)handoff=true;else if(IsBotTurn())StartCoroutine(BotFleet());}else if(mode==PlayMode.Local&&before!=fleet.PlacementPlayer)handoff=true;}else FireFleet(xx,yy);}GUI.enabled=true;}GUILayout.EndHorizontal();}GUILayout.EndVertical();GUILayout.EndScrollView();if(!string.IsNullOrEmpty(resultMessage)){GUILayout.Label(resultMessage,centered);resultMessage="";}}
        private void FireFleet(int x,int y){int result=fleet.Fire(x,y);if(result<0)resultMessage="Ya disparaste a esa casilla.";else if(result==3)EndGame(fleet.Winner);else if(result==0&&mode==PlayMode.Bot)StartCoroutine(BotFleet());else if(result==0)handoff=true;}
        private IEnumerator BotFleet(){yield return new WaitForSeconds(.4f);if(page!=Page.Game||fleet.IsFinished)yield break;int s=fleet.Boards[0].Size;var list=new List<Vector2Int>();bool smart=UnityEngine.Random.value<new[]{.6f,.8f,.95f}[difficulty];if(smart)for(int x=0;x<s;x++)for(int y=0;y<s;y++)if(fleet.Boards[0].Shots[x,y]==0&&HasHitNeighbour(x,y))list.Add(new Vector2Int(x,y));if(list.Count==0)for(int x=0;x<s;x++)for(int y=0;y<s;y++)if(fleet.Boards[0].Shots[x,y]==0&&(!smart||((x+y)%2==0)))list.Add(new Vector2Int(x,y));if(list.Count==0)for(int x=0;x<s;x++)for(int y=0;y<s;y++)if(fleet.Boards[0].Shots[x,y]==0)list.Add(new Vector2Int(x,y));var p=list[UnityEngine.Random.Range(0,list.Count)];int r=fleet.Fire(p.x,p.y);if(r==3)EndGame(fleet.Winner);else if(r==1||r==2)StartCoroutine(BotFleet());else if(mode==PlayMode.Local)handoff=true;}
        private bool HasHitNeighbour(int x,int y){var b=fleet.Boards[0];return x>0&&b.Shots[x-1,y]==1||x+1<b.Size&&b.Shots[x+1,y]==1||y>0&&b.Shots[x,y-1]==1||y+1<b.Size&&b.Shots[x,y+1]==1;}
        private void DrawBlackjack()
        {
            GUILayout.Label("Mano "+blackjack.HandNumber+"/3 · fichas: "+name0+" "+blackjack.Balances[0]+"  |  "+(mode==PlayMode.Bot?"Bot":name1)+" "+blackjack.Balances[1],centered);
            GUILayout.Label("Crupier: "+string.Join("  ",blackjack.Dealer.Select((c,i)=>i==1&&!blackjack.DealerRevealed?"??":c.ToString()).ToArray())+(blackjack.DealerRevealed?" = "+BlackjackGame.Total(blackjack.Dealer):""),centered);
            for(int p=0;p<2;p++)GUILayout.Label(ActivePlayerName(p)+" ("+blackjack.Balances[p]+" fichas) apuesta "+blackjack.Bets[p]+": "+string.Join("  ",blackjack.Hands[p].Select(c=>c.ToString()).ToArray())+(blackjack.Hands[p].Count>0?" = "+BlackjackGame.Total(blackjack.Hands[p]):""),centered);
            if(blackjack.Status==BlackjackStatus.Betting){GUILayout.Label("Apuesta de cada jugador (saldo máximo: "+Mathf.Min(blackjack.Balances[0],blackjack.Balances[1])+")",centered);if(Big("Apostar 10"))StartBlackjack(10);if(Big("Apostar 25"))StartBlackjack(25);if(Big("Apostar todo"))StartBlackjack(Mathf.FloorToInt(Mathf.Min(blackjack.Balances[0],blackjack.Balances[1])));}
            else if(blackjack.Status==BlackjackStatus.Insurance){GUILayout.Label("El crupier muestra un As. El seguro cuesta la mitad de la apuesta.",centered);if(Big("Asegurar a "+name0))blackjack.BuyInsurance(0);if(mode==PlayMode.Local&&Big("Asegurar a "+name1))blackjack.BuyInsurance(1);if(Big("Continuar"))blackjack.ResolveInsuranceChoice();}
            else if(blackjack.Status==BlackjackStatus.Playing){GUILayout.Label("Turno: "+ActivePlayerName(blackjack.CurrentPlayer),centered);if(IsBotTurn()){if(!botBusy)StartCoroutine(BotBlackjack());}else{GUILayout.BeginHorizontal();if(Big("Pedir"))blackjack.Act(BlackjackAction.Hit);if(Big("Plantarse"))blackjack.Act(BlackjackAction.Stand);if(Big("Doblar"))blackjack.Act(BlackjackAction.Double);GUILayout.EndHorizontal();}}
            else{GUILayout.Label(blackjack.Winner<0?"Empate":ActivePlayerName(blackjack.Winner)+" gana",title);if(Big("Ver resultado"))EndGame(blackjack.Winner);}
        }
        private void StartBlackjack(int amount){if(amount>0){float bet0=Mathf.Min(amount,blackjack.Balances[0]),bet1=Mathf.Min(amount,blackjack.Balances[1]);blackjack.StartHand(bet0,bet1);}}
        private IEnumerator BotBlackjack(){botBusy=true;yield return new WaitForSeconds(.45f);if(blackjack.Status==BlackjackStatus.Playing&&blackjack.CurrentPlayer==1){int total=BlackjackGame.Total(blackjack.Hands[1]);bool canDouble=blackjack.Hands[1].Count==2&&blackjack.Balances[1]>=blackjack.Bets[1];var best=total<=11?(canDouble?BlackjackAction.Double:BlackjackAction.Hit):total<=16?BlackjackAction.Hit:BlackjackAction.Stand;var legal=new List<BlackjackAction>{BlackjackAction.Hit,BlackjackAction.Stand};if(canDouble)legal.Add(BlackjackAction.Double);var action=UnityEngine.Random.value<new[]{.6f,.8f,.95f}[difficulty]?best:legal[UnityEngine.Random.Range(0,legal.Count)];if(action==BlackjackAction.Double&&!blackjack.Act(action))blackjack.Act(BlackjackAction.Hit);else if(action!=BlackjackAction.Double)blackjack.Act(action);}botBusy=false;}
        private void DrawBrisca()
        {
            GUILayout.Label("Triunfo: "+brisca.TrumpSample+" · Restan "+brisca.RemainingDeck+" cartas",centered);GUILayout.Label(name0+" "+brisca.Points(0)+" pts  |  "+(mode==PlayMode.Bot?"Bot":name1)+" "+brisca.Points(1)+" pts",centered);
            if(brisca.DrawPending)
            {
                GUILayout.Label(ActivePlayerName(brisca.CurrentPlayer)+" gana la baza. Puede cambiar triunfo antes de robar.",centered);
                if(brisca.CanExchange&&!IsBotTurn())for(int i=0;i<brisca.Hands[brisca.CurrentPlayer].Count;i++){int ix=i;if(Big("Cambiar "+brisca.Hands[brisca.CurrentPlayer][i]+" por la muestra"))brisca.Exchange(ix);}
                if(!IsBotTurn()&&Big("Robar cartas y continuar")){brisca.CompleteDraws();}
                if(IsBotTurn()&&!botBusy)StartCoroutine(BotBrisca());
                return;
            }
            GUILayout.Label("Turno de "+ActivePlayerName(brisca.CurrentPlayer),centered);for(int i=0;i<brisca.Hands[brisca.CurrentPlayer].Count;i++){int ix=i;GUI.enabled=!IsBotTurn();if(Big(brisca.Hands[brisca.CurrentPlayer][i].ToString()))PlayBrisca(ix);GUI.enabled=true;}
        }
        private void PlayBrisca(int i){brisca.PlayCard(i);if(brisca.IsFinished)EndGame(brisca.Winner);else if(IsBotTurn())StartCoroutine(BotBrisca());else if(mode==PlayMode.Local)handoff=true;}
        private void StartBriscaIfBot(){if(IsBotTurn())StartCoroutine(BotBrisca());}
        private IEnumerator BotBrisca(){if(botBusy)yield break;botBusy=true;yield return new WaitForSeconds(.45f);if(page!=Page.Game||!IsBotTurn()){botBusy=false;yield break;}if(brisca.DrawPending){if(brisca.CanExchange){int ix=ChooseBriscaExchange();if(ix>=0)brisca.Exchange(ix);}brisca.CompleteDraws();}int card=ChooseBriscaCard();brisca.PlayCard(card);botBusy=false;if(brisca.IsFinished)EndGame(brisca.Winner);else if(IsBotTurn())StartCoroutine(BotBrisca());else if(mode==PlayMode.Local)handoff=true;}
        private int ChooseBriscaExchange(){if(UnityEngine.Random.value>=new[]{.6f,.8f,.95f}[difficulty])return -1;for(int i=0;i<brisca.Hands[1].Count;i++){var c=brisca.Hands[1][i];if(c.Suit==brisca.TrumpSuit&&((c.Rank==SpanishRank.Seven&&brisca.TrumpSample.Points>=2)||(c.Rank==SpanishRank.Two&&brisca.TrumpSample.Points==0)))return i;}return -1;}
        private int ChooseBriscaCard(){var hand=brisca.Hands[brisca.CurrentPlayer];int best=0;int lowestCost=int.MaxValue;if(brisca.CardsInTrick>0){var lead=brisca.Played[0];for(int i=0;i<hand.Count;i++){var c=hand[i];bool wins=c.Suit==lead.Suit?c.Strength>lead.Strength:c.Suit==brisca.TrumpSuit;if(wins&&c.Points<lowestCost){lowestCost=c.Points;best=i;}}if(lowestCost<int.MaxValue)return UnityEngine.Random.value<new[]{.6f,.8f,.95f}[difficulty]?best:UnityEngine.Random.Range(0,hand.Count);}for(int i=1;i<hand.Count;i++)if(hand[i].Points<hand[best].Points)best=i;return UnityEngine.Random.value<new[]{.6f,.8f,.95f}[difficulty]?best:UnityEngine.Random.Range(0,hand.Count);}

        private void StartGameBotIfNeeded(){if(IsBotTurn()){if(ActiveGame==MiniGame.ConnectFour)StartCoroutine(BotConnect4());else if(ActiveGame==MiniGame.Memory)StartMemoryTurnIfBot();else if(ActiveGame==MiniGame.Brisca)StartBriscaIfBot();else if(ActiveGame==MiniGame.Yahtzee)StartCoroutine(BotDice());else if(ActiveGame==MiniGame.Battleship)StartCoroutine(BotFleet());}}
        private void StartDiceIfBot(){if(IsBotTurn())StartCoroutine(BotDice());}
        private void Update()
        { if(page!=Page.Game)return;if(ActiveGame==MiniGame.ConnectFour&&!connect4.IsFinished&&!IsBotTurn()&&Time.time-turnStart>=timeout){connect4.PassTurnAfterTimeout();turnStart=Time.time;if(IsBotTurn())StartCoroutine(BotConnect4());} }
        private IEnumerator WaitForOrientation(){while(page==Page.Game&&!orientationReady){bool isLandscape=Screen.width>Screen.height;if(isLandscape==requestedLandscape){orientationReady=true;break;}yield return null;}}
        private void EndGame(int winner)
        {if(page!=Page.Game)return;gameWinner=winner;pendingOutcomes.Add(new Outcome(ActiveGame,winner));if(series!=null&&!series.IsComplete)series.RecordResult(winner);else if(series==null)CommitPendingStats();resultMessage=winner<0?"Empate":ActivePlayerName(winner)+" gana la partida";page=Page.Result;
#if UNITY_ANDROID || UNITY_IOS
            if(vibrationEnabled)Handheld.Vibrate();
#endif
        }
        private void CommitPendingStats()
        {if(pendingCommitted)return;foreach(var o in pendingOutcomes){string stem="atu.stats."+(int)o.Game+"."+(int)mode+".";string suffix=o.Winner<0?"d":o.Winner==0?"w":"l";string key=stem+suffix;PlayerPrefs.SetInt(key,PlayerPrefs.GetInt(key,0)+1);string global="atu.stats."+(int)o.Game+"."+suffix;PlayerPrefs.SetInt(global,PlayerPrefs.GetInt(global,0)+1);}PlayerPrefs.Save();pendingCommitted=true;}
        private void DrawResult()
        {
            GUILayout.FlexibleSpace();GUILayout.Label("Resultado",title);GUILayout.Label(resultMessage,centered);
            if(series==null){if(Big("Otra partida"))BeginMatch();if(Big("Menú principal"))page=Page.Home;}
            else{GUILayout.Label("Marcador de serie: "+series.Score[0]+"  —  "+series.Score[1],centered);
                if(!series.IsComplete){if(Big("Siguiente juego"))BeginPreview();}
                else if(series.Score[0]==series.Score[1]){if(Big("Desempatar · Piedra, papel o tijera")){rpsWins0=rpsWins1=0;rpsStage=0;rpsChoice0=-1;page=Page.Rps;}}
                else{CommitPendingStats();GUILayout.Label("Ganador de la serie: "+ActivePlayerName(series.Score[0]>series.Score[1]?0:1),title);if(Big("Terminar serie")){series=null;page=Page.Home;pendingOutcomes.Clear();}}
            }
            GUILayout.FlexibleSpace();
        }
        private void DrawRps()
        {
            GUILayout.FlexibleSpace();GUILayout.Label("Desempate · mejor de 3",title);GUILayout.Label(name0+" "+rpsWins0+" — "+(mode==PlayMode.Bot?"Bot":name1)+" "+rpsWins1,centered);
            string[] labels={"Piedra","Papel","Tijera"};
            if(mode==PlayMode.Local&&rpsStage==1){GUILayout.Label("Pasa el móvil a "+name1,centered);if(Big("Estoy listo"))rpsStage=2;}
            else if(rpsStage==3){GUILayout.Label(resultMessage,centered);if(Math.Max(rpsWins0,rpsWins1)<2&&Big("Siguiente ronda")){rpsStage=0;rpsChoice0=-1;}}
            else if(rpsStage<3){GUILayout.Label("Elige "+(mode==PlayMode.Local&&rpsStage==2?name1:name0)+"",centered);GUILayout.BeginHorizontal();for(int i=0;i<3;i++){int choice=i;if(GUILayout.Button(labels[i],GUILayout.Height(60)))PlayRps(choice);}GUILayout.EndHorizontal();}
            if(Math.Max(rpsWins0,rpsWins1)>=2){int w=rpsWins0>rpsWins1?0:1;CommitPendingStats();GUILayout.Label("Gana la serie "+ActivePlayerName(w),title);if(Big("Volver al menú")){series=null;page=Page.Home;pendingOutcomes.Clear();}}
            GUILayout.FlexibleSpace();
        }
        private void PlayRps(int choice)
        {
            if(mode==PlayMode.Bot){int bot=UnityEngine.Random.Range(0,3);ResolveRps(choice,bot);return;}
            if(rpsStage==0){rpsChoice0=choice;rpsStage=1;}else if(rpsStage==2){ResolveRps(rpsChoice0,choice);}
        }
        private void ResolveRps(int a,int b)
        {if(a==b)resultMessage="Empate en esta ronda: repetid.";else if((a-b+3)%3==1){rpsWins0++;resultMessage=name0+" gana esta ronda.";}else{rpsWins1++;resultMessage=name1+" gana esta ronda.";}rpsStage=3;}

        private void LoadSettings(){theme=PlayerPrefs.GetInt("atu.theme",0);soundEnabled=PlayerPrefs.GetInt("atu.sound",1)!=0;vibrationEnabled=PlayerPrefs.GetInt("atu.vibration",1)!=0;page=Page.Settings;}
        private void DrawSettings()
        {GUILayout.Label("Ajustes",title);GUILayout.Label("Estilo visual",centered);ChoiceRow(new[]{"Mesa moderna","Arcade","Cartoon"},theme,i=>theme=i);if(Big("Sonido: "+(soundEnabled?"activado":"desactivado")))soundEnabled=!soundEnabled;if(Big("Vibración: "+(vibrationEnabled?"activada":"desactivada")))vibrationEnabled=!vibrationEnabled;if(Big("Guardar y volver")){PlayerPrefs.SetInt("atu.theme",theme);PlayerPrefs.SetInt("atu.sound",soundEnabled?1:0);PlayerPrefs.SetInt("atu.vibration",vibrationEnabled?1:0);PlayerPrefs.Save();page=Page.Home;}}
        private void DrawProfile()
        {GUILayout.Label("Perfil local",title);name0=Edit("Nombre visible",name0);GUILayout.BeginHorizontal();ToggleButton("Todas",statsFilter<0,()=>statsFilter=-1);ToggleButton("Bot",statsFilter==(int)PlayMode.Bot,()=>statsFilter=(int)PlayMode.Bot);ToggleButton("Local",statsFilter==(int)PlayMode.Local,()=>statsFilter=(int)PlayMode.Local);GUILayout.EndHorizontal();int totalW=0,totalL=0,totalD=0;scroll=GUILayout.BeginScrollView(scroll);foreach(MiniGame g in Enum.GetValues(typeof(MiniGame))){string stem="atu.stats."+(int)g+(statsFilter<0?".":"."+statsFilter+".");int w=PlayerPrefs.GetInt(stem+"w",0),l=PlayerPrefs.GetInt(stem+"l",0),d=PlayerPrefs.GetInt(stem+"d",0);totalW+=w;totalL+=l;totalD+=d;GUILayout.Label(GameRules.DisplayName(g)+": "+w+" victorias · "+l+" derrotas · "+d+" empates",centered);}GUILayout.Label("Total: "+totalW+" · "+totalL+" · "+totalD,centered);GUILayout.EndScrollView();if(Big("Guardar y volver")){PlayerPrefs.SetString("atu.profile.name",name0);PlayerPrefs.Save();page=Page.Home;}}
        private void DrawHelp()
        {GUILayout.Label("Cómo jugar",title);scroll=GUILayout.BeginScrollView(scroll);GUILayout.Label("SERIES\nElige 3, 5 o 10 partidas. Una victoria da un punto de serie; empate no da puntos. Quien empieza alterna en cada partida. Si la serie empata, piedra, papel o tijera al mejor de tres.\n\n4 EN RAYA\nToca una columna para dejar caer una ficha. Gana quien conecte cuatro en línea horizontal, vertical o diagonal. El turno dura 15 o 30 segundos; si se acaba, pasa sin mover.\n\nTIMBIRICHE\nTraza líneas entre puntos adyacentes. Quien cierre una caja la gana y vuelve a jugar. Gana quien capture más cajas.\n\nGENERALA\nTienes cinco dados y hasta tres tiradas por turno. Toca dados para guardarlos y elige una categoría libre: unos, doses, treses, cuatros, cincos, seises, escalera corta, escalera larga, full, póker, generala o suma total. Las categorías vacías anotan cero. Bonus de 35 puntos al sumar 63 en la parte superior.\n\nMEMORIA\nDescubre dos cartas. Si son iguales, ganas una pareja y repites; si no, se ocultan y juega el rival. Gana quien reúne más parejas.\n\nHUNDIR LA FLOTA\nColoca barcos de longitud 5, 4, 3, 3 y 2 sin que se solapen ni se toquen. Dispara al tablero rival. Los impactos dan otro turno; el agua lo pasa.\n\nBLACKJACK\nCada persona empieza con 100 fichas virtuales y juega tres manos contra el crupier. Pide carta, plántate o dobla. El as vale 1 u 11. Blackjack natural paga 3:2; el seguro opcional se ofrece ante un as visible. No hay dinero real.\n\nBRISCA\nJuega cualquier carta; no hace falta seguir palo. El triunfo gana a los otros palos. As vale 11, tres 10, rey 4, caballo 3, sota 2. Gana quien logra más puntos; 60–60 empata.\n\nRITMO\nRápido, normal y lento cambian el tamaño de los tableros de 4 en Raya, Timbiriche, Memoria y Hundir la flota.",centered);GUILayout.EndScrollView();if(Big("Volver"))page=Page.Home;}
    }
}
