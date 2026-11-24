using System;
using System.Collections.Generic;

namespace AuteTurno.Games
{
    public enum MemoryRevealResult { Invalid, FirstCard, WaitingForSecond, Match, Mismatch, Finished }

    public sealed class MemoryGame
    {
        public readonly int Rows, Columns;
        public readonly int[] Cards;
        public readonly bool[] Matched;
        public readonly int[] Scores = new int[2];
        public int CurrentPlayer { get; private set; }
        public int FirstRevealed { get; private set; } = -1;
        public int SecondRevealed { get; private set; } = -1;
        public bool IsFinished => MatchedCount == Cards.Length;
        public int MatchedCount { get; private set; }
        public bool IsVisible(int index) => Matched[index] || index == FirstRevealed || index == SecondRevealed;

        public MemoryGame(int rows, int columns, int startingPlayer = 0, int? seed = null)
        {
            if(rows*columns%2!=0) throw new ArgumentException("El tablero necesita un número par de cartas.");
            Rows=rows;Columns=columns;Cards=new int[rows*columns];Matched=new bool[Cards.Length];CurrentPlayer=startingPlayer==1?1:0;
            for(int i=0;i<Cards.Length;i++) Cards[i]=i/2;
            var random=seed.HasValue?new Random(seed.Value):new Random();
            for(int i=Cards.Length-1;i>0;i--){int j=random.Next(i+1);int t=Cards[i];Cards[i]=Cards[j];Cards[j]=t;}
        }

        public MemoryRevealResult Reveal(int index)
        {
            if(IsFinished) return MemoryRevealResult.Finished;
            if(index<0||index>=Cards.Length||Matched[index]||index==FirstRevealed||SecondRevealed>=0) return MemoryRevealResult.Invalid;
            if(FirstRevealed<0){FirstRevealed=index;return MemoryRevealResult.FirstCard;}
            SecondRevealed=index;
            if(Cards[FirstRevealed]==Cards[SecondRevealed])
            { Matched[FirstRevealed]=Matched[SecondRevealed]=true;Scores[CurrentPlayer]++;MatchedCount+=2;FirstRevealed=SecondRevealed=-1;return IsFinished?MemoryRevealResult.Finished:MemoryRevealResult.Match; }
            return MemoryRevealResult.Mismatch;
        }
        public void ResolveMismatch()
        { if(SecondRevealed<0||FirstRevealed<0||Cards[FirstRevealed]==Cards[SecondRevealed]) return; FirstRevealed=SecondRevealed=-1;CurrentPlayer=1-CurrentPlayer; }
        public int Winner => !IsFinished ? -2 : Scores[0]==Scores[1] ? -1 : Scores[0]>Scores[1] ? 0 : 1;
    }
}
