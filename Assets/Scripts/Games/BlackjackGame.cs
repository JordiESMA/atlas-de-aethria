using System;
using System.Collections.Generic;

namespace AuteTurno.Games
{
    public enum BlackjackAction { Hit, Stand, Double }
    public enum BlackjackStatus { Betting, Insurance, Playing, Finished }
    public sealed class BlackjackCard
    {
        public int Rank { get; }
        public int Value => Rank==1?11:Math.Min(Rank,10);
        public BlackjackCard(int rank){Rank=rank;}
        public override string ToString()=>Rank==1?"A":Rank==11?"J":Rank==12?"Q":Rank==13?"K":Rank.ToString();
    }
    public sealed class BlackjackGame
    {
        public readonly float[] Balances={100f,100f};
        public readonly float[] Bets={0f,0f};
        public readonly float[] Insurance={0f,0f};
        public readonly List<BlackjackCard>[] Hands={new List<BlackjackCard>(),new List<BlackjackCard>()};
        public readonly List<BlackjackCard> Dealer=new List<BlackjackCard>();
        public int HandNumber { get; private set; }=1;
        public int CurrentPlayer { get; private set; }
        public BlackjackStatus Status { get; private set; }=BlackjackStatus.Betting;
        public bool DealerRevealed { get; private set; }
        public bool[] Stood={false,false};
        public bool[] Busted={false,false};
        public bool[] Natural={false,false};
        private readonly List<BlackjackCard> shoe=new List<BlackjackCard>();
        private readonly Random random;
        private readonly int firstPlayer;
        private bool dealerCheckedSeventeen;
        public BlackjackGame(int startingPlayer=0,int? seed=null){firstPlayer=startingPlayer==1?1:0;random=seed.HasValue?new Random(seed.Value):new Random();for(int d=0;d<6;d++)for(int r=1;r<=13;r++)for(int s=0;s<4;s++)shoe.Add(new BlackjackCard(r));Shuffle();}
        private void Shuffle(){for(int i=shoe.Count-1;i>0;i--){int j=random.Next(i+1);var c=shoe[i];shoe[i]=shoe[j];shoe[j]=c;}}
        private BlackjackCard Draw(){if(shoe.Count==0){for(int d=0;d<6;d++)for(int r=1;r<=13;r++)for(int s=0;s<4;s++)shoe.Add(new BlackjackCard(r));Shuffle();}int i=shoe.Count-1;var c=shoe[i];shoe.RemoveAt(i);return c;}
        public bool StartHand(float bet0,float bet1,bool insure0=false,bool insure1=false)
        {
            if(Status!=BlackjackStatus.Betting||bet0<1||bet1<1||bet0>Balances[0]||bet1>Balances[1])return false;
            Bets[0]=bet0;Bets[1]=bet1;for(int p=0;p<2;p++){Balances[p]-=Bets[p];Hands[p].Clear();Stood[p]=Busted[p]=Natural[p]=false;Insurance[p]=0;}
            Dealer.Clear();DealerRevealed=false;dealerCheckedSeventeen=false;
            Hands[0].Add(Draw());Hands[1].Add(Draw());Dealer.Add(Draw());Hands[0].Add(Draw());Hands[1].Add(Draw());Dealer.Add(Draw());
            Status=Dealer[0].Rank==1?BlackjackStatus.Insurance:BlackjackStatus.Playing;
            if(Status==BlackjackStatus.Insurance){if(insure0)BuyInsurance(0);if(insure1)BuyInsurance(1);}
            if(Status==BlackjackStatus.Playing){if(IsNatural(Dealer))SettleDealerNatural();else BeginTurns();}
            return true;
        }
        public bool BuyInsurance(int player)
        {if(Status!=BlackjackStatus.Insurance||player<0||player>1||Insurance[player]>0)return false;float amount=Bets[player]/2f;if(amount<=0||Balances[player]<amount)return false;Insurance[player]=amount;Balances[player]-=amount;return true;}
        public void ResolveInsuranceChoice()
        {
            if(Status!=BlackjackStatus.Insurance)return;
            bool dealerNatural=IsNatural(Dealer);
            for(int p=0;p<2;p++)if(Insurance[p]>0&&dealerNatural)Balances[p]+=Insurance[p]*3f;
            if(dealerNatural){SettleDealerNatural();return;}
            Status=BlackjackStatus.Playing;BeginTurns();
        }
        private void SettleDealerNatural()
        {DealerRevealed=true;for(int p=0;p<2;p++){Natural[p]=IsNatural(Hands[p]);if(Natural[p])Balances[p]+=Bets[p];}CompleteHand();}
        private void BeginTurns()
        {
            for(int p=0;p<2;p++){Natural[p]=IsNatural(Hands[p]);if(Natural[p]){Balances[p]+=Bets[p]*2.5f;Stood[p]=true;}}
            CurrentPlayer=FindNextPlayer(firstPlayer);if(CurrentPlayer<0)PlayDealerAndSettle();
        }
        public bool Act(BlackjackAction action)
        {
            if(Status!=BlackjackStatus.Playing||CurrentPlayer<0)return false;int p=CurrentPlayer;
            if(action==BlackjackAction.Double)
            {if(Hands[p].Count!=2||Balances[p]<Bets[p])return false;Balances[p]-=Bets[p];Bets[p]*=2;Hands[p].Add(Draw());if(Total(Hands[p])>21)Busted[p]=true;Stood[p]=true;}
            else if(action==BlackjackAction.Hit)
            {Hands[p].Add(Draw());int total=Total(Hands[p]);if(total>21){Busted[p]=true;Stood[p]=true;}else if(total==21)Stood[p]=true;}
            else Stood[p]=true;
            CurrentPlayer=FindNextPlayer(p+1);if(CurrentPlayer<0)PlayDealerAndSettle();return true;
        }
        private int FindNextPlayer(int from){for(int offset=0;offset<2;offset++){int p=(from+offset)%2;if(!Stood[p]&&!Busted[p])return p;}return -1;}
        public static int Total(IList<BlackjackCard> hand)
        {int total=0,aces=0;foreach(var c in hand){total+=Math.Min(c.Rank,10);if(c.Rank==1)aces++;}while(total<=11&&aces-->0)total+=10;return total;}
        public static bool IsNatural(IList<BlackjackCard> hand)=>hand.Count==2&&Total(hand)==21;
        private void PlayDealerAndSettle()
        {
            DealerRevealed=true;while(true){int total=Total(Dealer);if(total<=16){Dealer.Add(Draw());continue;}if(total==17&&!dealerCheckedSeventeen){dealerCheckedSeventeen=true;if(random.NextDouble()<.1)continue;break;}if(total==17&&dealerCheckedSeventeen)break;if(total>=18)break;}
            int dealer=Total(Dealer);for(int p=0;p<2;p++)
            {
                if(Natural[p])continue;
                if(Busted[p])continue;
                int player=Total(Hands[p]);if(dealer>21||player>dealer)Balances[p]+=Bets[p]*2f;else if(player==dealer)Balances[p]+=Bets[p];
            }
            CompleteHand();
        }
        private void CompleteHand()
        {if(HandNumber>=3||Balances[0]==0||Balances[1]==0)Status=BlackjackStatus.Finished;else{HandNumber++;Status=BlackjackStatus.Betting;}}
        public int Winner=>Status!=BlackjackStatus.Finished?-2:Math.Abs(Balances[0]-Balances[1])<0.001f?-1:Balances[0]>Balances[1]?0:1;
    }
}
