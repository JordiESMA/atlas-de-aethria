using System;

namespace AuteTurno
{
    public enum MiniGame { ConnectFour, DotsAndBoxes, Yahtzee, Memory, Battleship, Blackjack, Brisca }
    public enum PlayMode { Local, Bot }
    public enum GamePace { Fast, Normal, Slow }

    [Serializable]
    public sealed class PlayerInfo
    {
        public string Name;
        public UnityEngine.Color Color;
        public bool IsBot;
        public PlayerInfo(string name, UnityEngine.Color color, bool isBot = false)
        { Name = name; Color = color; IsBot = isBot; }
    }

    public static class GameRules
    {
        // Returns width, height for board games according to the GDD pace table.
        public static (int width, int height) BoardSize(MiniGame game, GamePace pace)
        {
            int p = (int)pace;
            switch (game)
            {
                case MiniGame.ConnectFour: return new[] { (6, 5), (7, 6), (8, 7) }[p];
                case MiniGame.DotsAndBoxes: return new[] { (4, 4), (5, 5), (6, 6) }[p];
                case MiniGame.Memory: return new[] { (4, 4), (6, 6), (8, 8) }[p];
                case MiniGame.Battleship: return new[] { (7, 7), (10, 10), (12, 12) }[p];
                default: return (0, 0);
            }
        }

        public static string DisplayName(MiniGame game)
        {
            switch (game)
            {
                case MiniGame.ConnectFour: return "4 en Raya";
                case MiniGame.DotsAndBoxes: return "Timbiriche";
                case MiniGame.Yahtzee: return "Generala / Yahtzee";
                case MiniGame.Memory: return "Memoria";
                case MiniGame.Battleship: return "Hundir la flota";
                case MiniGame.Blackjack: return "Blackjack";
                default: return "Brisca";
            }
        }
    }
}
