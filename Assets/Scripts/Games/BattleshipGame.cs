using System;
using System.Collections.Generic;

namespace AuteTurno.Games
{
    public sealed class BattleshipBoard
    {
        public readonly int Size;
        public readonly int[,] Ships;
        public readonly byte[,] Shots;
        public static readonly int[] Fleet={5,4,3,3,2};
        public int ShipsPlaced { get; private set; }
        public BattleshipBoard(int size){Size=size;Ships=new int[size,size];Shots=new byte[size,size];}
        public string Place(int x,int y,bool horizontal)
        {
            if(ShipsPlaced>=Fleet.Length)return "La flota ya está completa.";
            int length=Fleet[ShipsPlaced];
            for(int i=0;i<length;i++)
            {
                int cx=x+(horizontal?i:0),cy=y+(horizontal?0:i);
                if(cx<0||cy<0||cx>=Size||cy>=Size)return "El barco sale del tablero.";
                for(int nx=Math.Max(0,cx-1);nx<=Math.Min(Size-1,cx+1);nx++)for(int ny=Math.Max(0,cy-1);ny<=Math.Min(Size-1,cy+1);ny++)if(Ships[nx,ny]!=0)return "Los barcos no pueden tocarse ni solaparse.";
            }
            for(int i=0;i<length;i++)Ships[x+(horizontal?i:0),y+(horizontal?0:i)]=ShipsPlaced+1;
            ShipsPlaced++;return null;
        }
        public bool HasBeenShot(int x,int y)=>x<0||y<0||x>=Size||y>=Size||Shots[x,y]!=0;
        public bool AllSunk{get{for(int x=0;x<Size;x++)for(int y=0;y<Size;y++)if(Ships[x,y]!=0&&Shots[x,y]!=1)return false;return true;}}
        public int RemainingShipCells{get{int count=0;for(int x=0;x<Size;x++)for(int y=0;y<Size;y++)if(Ships[x,y]!=0&&Shots[x,y]!=1)count++;return count;}}
        public bool IsShipSunk(int ship){if(ship<=0)return false;for(int x=0;x<Size;x++)for(int y=0;y<Size;y++)if(Ships[x,y]==ship&&Shots[x,y]!=1)return false;return true;}
    }

    public sealed class BattleshipGame
    {
        public readonly BattleshipBoard[] Boards;
        public int CurrentPlayer { get; private set; }
        public int PlacementPlayer { get; private set; }
        public bool CombatStarted { get; private set; }
        public bool IsFinished { get; private set; }
        public int Winner { get; private set; }=-1;
        public string LastMessage { get; private set; }="";
        private readonly int initialPlayer;
        public BattleshipGame(int size,int startingPlayer=0){Boards=new[]{new BattleshipBoard(size),new BattleshipBoard(size)};initialPlayer=startingPlayer==1?1:0;PlacementPlayer=initialPlayer;}
        public string PlaceShip(int x,int y,bool horizontal)
        {
            if(CombatStarted)return "La colocación ya terminó.";
            string error=Boards[PlacementPlayer].Place(x,y,horizontal);if(error!=null)return error;
            if(Boards[PlacementPlayer].ShipsPlaced==BattleshipBoard.Fleet.Length)
            {if(Boards[1-PlacementPlayer].ShipsPlaced==BattleshipBoard.Fleet.Length){CombatStarted=true;CurrentPlayer=initialPlayer;}else PlacementPlayer=1-PlacementPlayer;}
            return null;
        }
        public void AutoPlaceFleet(int player,Random random)
        {
            if(player<0||player>1)throw new ArgumentOutOfRangeException(nameof(player));var board=Boards[player];
            while(board.ShipsPlaced<BattleshipBoard.Fleet.Length)
            {bool ok=false;for(int tries=0;tries<10000&&!ok;tries++)ok=board.Place(random.Next(board.Size),random.Next(board.Size),random.Next(2)==0)==null;if(!ok)throw new InvalidOperationException("No se pudo colocar la flota.");}
            if(PlacementPlayer==player){if(Boards[1-player].ShipsPlaced==BattleshipBoard.Fleet.Length){CombatStarted=true;CurrentPlayer=initialPlayer;}else PlacementPlayer=1-player;}
        }
        /// <returns>-1 illegal, 0 water, 1 hit, 2 sunk, 3 win</returns>
        public int Fire(int x,int y)
        {
            if(!CombatStarted||IsFinished)return -1;var target=Boards[1-CurrentPlayer];if(target.HasBeenShot(x,y))return -1;
            int ship=target.Ships[x,y];target.Shots[x,y]=(byte)(ship==0?2:1);
            if(ship==0){CurrentPlayer=1-CurrentPlayer;LastMessage="Agua";return 0;}
            if(target.IsShipSunk(ship)){LastMessage="¡Hundido!";if(target.AllSunk){IsFinished=true;Winner=CurrentPlayer;return 3;}return 2;}
            LastMessage="¡Tocado!";return 1;
        }
    }
}
