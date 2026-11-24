using System;
using System.Linq;

namespace AuteTurno.Games
{
    public enum YahtzeeCategory { Ones, Twos, Threes, Fours, Fives, Sixes, ShortStraight, LongStraight, FullHouse, FourOfAKind, Yahtzee, Chance }
    public sealed class YahtzeeGame
    {
        public readonly int[][] Dice = { new int[5], new int[5] };
        public readonly bool[][] Held = { new bool[5], new bool[5] };
        public readonly int?[,] Sheet = new int?[2,12];
        public int CurrentPlayer { get; private set; }
        public int RollsThisTurn { get; private set; }
        public bool HasRolled => RollsThisTurn>0;
        public bool IsFinished { get; private set; }
        private readonly Random random;
        public YahtzeeGame(int startingPlayer=0,int? seed=null){CurrentPlayer=startingPlayer==1?1:0;random=seed.HasValue?new Random(seed.Value):new Random();}
        public bool Roll()
        { if(IsFinished||RollsThisTurn>=3)return false; for(int i=0;i<5;i++)if(!Held[CurrentPlayer][i])Dice[CurrentPlayer][i]=random.Next(1,7); RollsThisTurn++;return true; }
        public bool ToggleHold(int index)
        { if(!HasRolled||RollsThisTurn>=3||index<0||index>=5)return false;Held[CurrentPlayer][index]=!Held[CurrentPlayer][index];return true; }
        public int Score(YahtzeeCategory category)=>ScoreDice(Dice[CurrentPlayer],category);
        public static int ScoreDice(int[] dice,YahtzeeCategory category)
        {
            if(dice==null||dice.Length!=5)throw new ArgumentException("Se necesitan cinco dados.");
            int[] counts=new int[7];foreach(int d in dice){if(d<1||d>6)throw new ArgumentException("Dado fuera de rango.");counts[d]++;}
            int sum=dice.Sum(), max=counts.Max();
            switch(category)
            {
                case YahtzeeCategory.Ones:case YahtzeeCategory.Twos:case YahtzeeCategory.Threes:case YahtzeeCategory.Fours:case YahtzeeCategory.Fives:case YahtzeeCategory.Sixes: int face=(int)category+1; return counts[face]*face;
                case YahtzeeCategory.LongStraight: return IsSequence(counts,1,2,3,4,5)||IsSequence(counts,2,3,4,5,6)?30:0;
                case YahtzeeCategory.ShortStraight:
                    int best=0; for(int missing=1;missing<=6;missing++){if(counts[missing]==0)continue;var unique=Enumerable.Range(1,6).Where(n=>counts[n]>0).ToArray();for(int i=0;i<=unique.Length-4;i++){var run=unique.Skip(i).Take(4).ToArray();if(run.Length==4&&run[3]-run[0]==3&&run.Distinct().Count()==4){int leftover=0;var left=counts.ToArray();foreach(int v in run)left[v]--;for(int v=1;v<=6;v++)if(left[v]>0)leftover=Math.Max(leftover,v);best=Math.Max(best,20+leftover);}}}return best;
                case YahtzeeCategory.FullHouse:return counts.Contains(3)&&counts.Contains(2)?35:0;
                case YahtzeeCategory.FourOfAKind:
                    if(max<4)return 0;
                    int repeated=Array.FindIndex(counts,0,n=>n==max);
                    int kicker=max==5?repeated:Array.FindIndex(counts,0,n=>n==1);
                    return 40+kicker;
                case YahtzeeCategory.Yahtzee:return max==5?50:0;
                default:return sum;
            }
        }
        private static bool IsSequence(int[] counts,int a,int b,int c,int d,int e)=>counts[a]>0&&counts[b]>0&&counts[c]>0&&counts[d]>0&&counts[e]>0;
        public bool KeepScore(YahtzeeCategory category)
        {
            int c=(int)category;if(IsFinished||!HasRolled||Sheet[CurrentPlayer,c].HasValue)return false;
            Sheet[CurrentPlayer,c]=Score(category);int p=CurrentPlayer;CurrentPlayer=1-CurrentPlayer;RollsThisTurn=0;Array.Clear(Held[p],0,5);
            IsFinished=true;for(int player=0;player<2;player++)for(int i=0;i<12;i++)if(!Sheet[player,i].HasValue)IsFinished=false;
            return true;
        }
        public int Total(int player)
        {int total=0,upper=0;for(int i=0;i<12;i++){int value=Sheet[player,i]??0;total+=value;if(i<6)upper+=value;}return total+(upper>=63?35:0);}
        public int Winner=>!IsFinished?-2:Total(0)==Total(1)?-1:Total(0)>Total(1)?0:1;
    }
}
