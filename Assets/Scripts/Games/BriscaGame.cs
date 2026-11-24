using System;
using System.Collections.Generic;

namespace AuteTurno.Games
{
    public enum SpanishSuit { Oro, Copa, Espada, Basto }
    public enum SpanishRank { Two=2, Three=3, Four=4, Five=5, Six=6, Seven=7, Sota=10, Caballo=11, Rey=12, As=14 }
    [Serializable]
    public struct SpanishCard
    {
        public SpanishSuit Suit; public SpanishRank Rank;
        public SpanishCard(SpanishSuit suit,SpanishRank rank){Suit=suit;Rank=rank;}
        public int Points=>Rank==SpanishRank.As?11:Rank==SpanishRank.Three?10:Rank==SpanishRank.Rey?4:Rank==SpanishRank.Caballo?3:Rank==SpanishRank.Sota?2:0;
        public int Strength=>Rank==SpanishRank.As?10:Rank==SpanishRank.Three?9:Rank==SpanishRank.Rey?8:Rank==SpanishRank.Caballo?7:Rank==SpanishRank.Sota?6:Rank==SpanishRank.Seven?5:Rank==SpanishRank.Six?4:Rank==SpanishRank.Five?3:Rank==SpanishRank.Four?2:1;
        public override string ToString()=>Rank+" de "+Suit;
    }

    public sealed class BriscaGame
    {
        public readonly List<SpanishCard>[] Hands={new List<SpanishCard>(),new List<SpanishCard>()};
        public readonly List<SpanishCard>[] Captured={new List<SpanishCard>(),new List<SpanishCard>()};
        public readonly List<SpanishCard> Played=new List<SpanishCard>();
        public SpanishCard TrumpSample { get; private set; }
        public SpanishSuit TrumpSuit=>TrumpSample.Suit;
        public int CurrentPlayer { get; private set; }
        public int FirstPlayer { get; private set; }
        public int CardsInTrick=>Played.Count;
        public bool CanExchange=>DrawPending&&CardsInTrick==0&&Deck.Count>0&&!exchangeUsed;
        public bool DrawPending { get; private set; }
        public bool IsFinished { get; private set; }
        public int Winner=>!IsFinished?-2:Points(0)==Points(1)?-1:Points(0)>Points(1)?0:1;
        private readonly List<SpanishCard> Deck=new List<SpanishCard>();
        private readonly int[] trickPlayers=new int[2];
        private readonly int[] trickCards=new int[2];
        private readonly Random random;
        private bool exchangeUsed;
        public BriscaGame(int startingPlayer=0,int? seed=null)
        {
            CurrentPlayer=FirstPlayer=startingPlayer==1?1:0;random=seed.HasValue?new Random(seed.Value):new Random();
            foreach(SpanishSuit s in Enum.GetValues(typeof(SpanishSuit)))foreach(SpanishRank r in Enum.GetValues(typeof(SpanishRank)))Deck.Add(new SpanishCard(s,r));
            Shuffle(Deck); foreach(int p in new[]{0,1})for(int i=0;i<3;i++)Hands[p].Add(Draw()); TrumpSample=Draw();
        }
        private void Shuffle(List<SpanishCard> cards){for(int i=cards.Count-1;i>0;i--){int j=random.Next(i+1);var t=cards[i];cards[i]=cards[j];cards[j]=t;}}
        private SpanishCard Draw(){int last=Deck.Count-1;var c=Deck[last];Deck.RemoveAt(last);return c;}
        public bool Exchange(int handIndex)
        {
            if(!CanExchange||!DrawPending||handIndex<0||handIndex>=Hands[CurrentPlayer].Count)return false;var card=Hands[CurrentPlayer][handIndex];
            bool eligible=card.Suit==TrumpSuit&&((card.Rank==SpanishRank.Seven&&TrumpSample.Points>=2)||(card.Rank==SpanishRank.Two&&TrumpSample.Points==0));if(!eligible)return false;
            Hands[CurrentPlayer][handIndex]=TrumpSample;TrumpSample=card;exchangeUsed=true;return true;
        }
        public bool PlayCard(int handIndex)
        {
            if(IsFinished||DrawPending||handIndex<0||handIndex>=Hands[CurrentPlayer].Count)return false;
            var c=Hands[CurrentPlayer][handIndex];Hands[CurrentPlayer].RemoveAt(handIndex);trickPlayers[CardsInTrick]=CurrentPlayer;trickCards[CardsInTrick]=Played.Count;Played.Add(c);
            if(CardsInTrick==1)CurrentPlayer=1-CurrentPlayer;else CompleteTrick();return true;
        }
        private void CompleteTrick()
        {
            var a=Played[trickCards[0]];var b=Played[trickCards[1]];int winner=trickPlayers[BriscaWinner(a,b,TrumpSuit)];
            Captured[winner].Add(a);Captured[winner].Add(b);Played.Clear();
            CurrentPlayer=FirstPlayer=winner;DrawPending=true;exchangeUsed=false;
            if(Deck.Count==0)CompleteDraws();
        }
        public void CompleteDraws()
        {
            if(!DrawPending)return;int winner=CurrentPlayer;if(Deck.Count>0)Hands[winner].Add(Draw());if(Deck.Count>0)Hands[1-winner].Add(Draw());DrawPending=false;
            if(Hands[0].Count==0&&Hands[1].Count==0)IsFinished=true;
        }
        private static int BriscaWinner(SpanishCard a,SpanishCard b,SpanishSuit trump)
        {if(a.Suit==b.Suit)return a.Strength>=b.Strength?0:1;if(a.Suit==trump)return 0;if(b.Suit==trump)return 1;return 0;}
        public int Points(int player){int total=0;foreach(var c in Captured[player])total+=c.Points;return total;}
        public int RemainingDeck=>Deck.Count;
    }
}
