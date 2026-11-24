using System;

namespace AuteTurno.Games
{
    public sealed class DotsAndBoxesGame
    {
        public readonly int PointCount;
        private readonly bool[,] horizontal, vertical;
        private readonly int[,] owners;
        public int CurrentPlayer { get; private set; }
        public int[] Scores { get; } = new int[2];
        public bool IsFinished { get; private set; }
        public int Winner => IsFinished ? (Scores[0] == Scores[1] ? -1 : (Scores[0] > Scores[1] ? 0 : 1)) : -2;
        public int BoxOwner(int x, int y) => owners[x, y];
        public bool HorizontalLine(int x, int y) => horizontal[x, y];
        public bool VerticalLine(int x, int y) => vertical[x, y];
        public bool BestMove(out bool isHorizontal,out int moveX,out int moveY)
        {
            int best=int.MinValue;isHorizontal=true;moveX=moveY=0;
            for(int orient=0;orient<2;orient++)
            {
                int maxX=orient==0?PointCount-1:PointCount,maxY=orient==0?PointCount:PointCount-1;
                for(int x=0;x<maxX;x++)for(int y=0;y<maxY;y++)
                {
                    if(orient==0?horizontal[x,y]:vertical[x,y])continue;int value=0;
                    int bx0=orient==0?x:Math.Max(0,x-1),bx1=orient==0?x:Math.Min(PointCount-2,x);
                    int by0=orient==0?Math.Max(0,y-1):y,by1=orient==0?Math.Min(PointCount-2,y):y;
                    for(int bx=bx0;bx<=bx1;bx++)for(int by=by0;by<=by1;by++)if(owners[bx,by]==0)
                    {int edges=(horizontal[bx,by]?1:0)+(horizontal[bx,by+1]?1:0)+(vertical[bx,by]?1:0)+(vertical[bx+1,by]?1:0);if(edges==3)value+=100;else if(edges==2)value-=12;}
                    if(value>best){best=value;isHorizontal=orient==0;moveX=x;moveY=y;}
                }
            }
            return best!=int.MinValue;
        }

        public DotsAndBoxesGame(int pointCount, int startingPlayer = 0)
        { if (pointCount < 2) throw new ArgumentOutOfRangeException(nameof(pointCount)); PointCount=pointCount; horizontal=new bool[pointCount-1,pointCount]; vertical=new bool[pointCount,pointCount-1]; owners=new int[pointCount-1,pointCount-1]; CurrentPlayer=startingPlayer==1?1:0; }

        public bool PlayHorizontal(int x,int y) => Play(x,y,true);
        public bool PlayVertical(int x,int y) => Play(x,y,false);
        private bool Play(int x,int y,bool isHorizontal)
        {
            if(IsFinished) return false;
            if(isHorizontal ? x<0||x>=PointCount-1||y<0||y>=PointCount||horizontal[x,y] : x<0||x>=PointCount||y<0||y>=PointCount-1||vertical[x,y]) return false;
            if(isHorizontal) horizontal[x,y]=true; else vertical[x,y]=true;
            int closed=0;
            if(isHorizontal)
            { for(int by=Math.Max(0,y-1);by<=Math.Min(PointCount-2,y);by++) if(CloseBox(x,by)) closed++; }
            else
            { for(int bx=Math.Max(0,x-1);bx<=Math.Min(PointCount-2,x);bx++) if(CloseBox(bx,y)) closed++; }
            int lines=0; foreach(bool b in horizontal) if(b) lines++; foreach(bool b in vertical) if(b) lines++;
            IsFinished=lines==(PointCount-1)*PointCount+PointCount*(PointCount-1);
            if(closed==0) CurrentPlayer=1-CurrentPlayer;
            return true;
        }
        private bool CloseBox(int x,int y)
        { if(owners[x,y]!=0||!horizontal[x,y]||!horizontal[x,y+1]||!vertical[x,y]||!vertical[x+1,y])return false;owners[x,y]=CurrentPlayer+1;Scores[CurrentPlayer]++;return true; }
    }
}
