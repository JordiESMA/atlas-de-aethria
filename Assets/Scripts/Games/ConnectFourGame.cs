using System;
using System.Collections.Generic;

namespace AuteTurno.Games
{
    /// <summary>Rules-only 4 en Raya model. Player IDs are 0 and 1; row zero is the bottom.</summary>
    public sealed class ConnectFourGame
    {
        public readonly int Width, Height;
        public int[,] Cells { get; private set; }
        public int CurrentPlayer { get; private set; }
        public int Winner { get; private set; } = -1;
        public bool IsDraw { get; private set; }
        public bool IsFinished { get { return Winner >= 0 || IsDraw; } }

        public ConnectFourGame(int width, int height, int startingPlayer = 0)
        {
            if (width < 4 || height < 4) throw new ArgumentOutOfRangeException("Board must be at least 4x4.");
            Width = width; Height = height; Cells = new int[width, height];
            CurrentPlayer = startingPlayer == 1 ? 1 : 0;
        }

        public bool CanPlay(int column) => !IsFinished && column >= 0 && column < Width && Cells[column, Height - 1] == 0;

        public bool Play(int column)
        {
            if (!CanPlay(column)) return false;
            int row = 0;
            while (row < Height && Cells[column, row] != 0) row++;
            Cells[column, row] = CurrentPlayer + 1;
            if (HasFour(column, row)) Winner = CurrentPlayer;
            else if (IsBoardFull()) IsDraw = true;
            else CurrentPlayer = 1 - CurrentPlayer;
            return true;
        }

        public bool PassTurnAfterTimeout()
        {
            if (IsFinished) return false;
            CurrentPlayer = 1 - CurrentPlayer;
            return true;
        }

        public List<int> LegalColumns()
        {
            var moves = new List<int>();
            for (int c = 0; c < Width; c++) if (CanPlay(c)) moves.Add(c);
            return moves;
        }

        public bool WouldWin(int column, int player)
        {
            if (!CanPlay(column)) return false;
            int row = 0; while (row < Height && Cells[column, row] != 0) row++;
            Cells[column, row] = player + 1;
            bool result = HasFour(column, row);
            Cells[column, row] = 0;
            return result;
        }

        private bool HasFour(int x, int y)
        {
            int token = Cells[x, y];
            return Count(x, y, 1, 0, token) >= 4 || Count(x, y, 0, 1, token) >= 4 ||
                   Count(x, y, 1, 1, token) >= 4 || Count(x, y, 1, -1, token) >= 4;
        }
        private int Count(int x, int y, int dx, int dy, int token)
        {
            int total = 1;
            total += Ray(x, y, dx, dy, token); total += Ray(x, y, -dx, -dy, token);
            return total;
        }
        private int Ray(int x, int y, int dx, int dy, int token)
        {
            int n = 0; x += dx; y += dy;
            while (x >= 0 && x < Width && y >= 0 && y < Height && Cells[x, y] == token)
            { n++; x += dx; y += dy; }
            return n;
        }
        private bool IsBoardFull()
        { for (int c = 0; c < Width; c++) if (Cells[c, Height - 1] == 0) return false; return true; }
    }
}
