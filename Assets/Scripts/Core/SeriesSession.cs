using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuteTurno
{
    /// <summary>Local series rules: 3/5/10 rounds, no consecutive repeats, alternating starters.</summary>
    public sealed class SeriesSession
    {
        public readonly PlayerInfo[] Players;
        public readonly List<MiniGame> Games;
        public readonly int[] Score = new int[2];
        public readonly int Rounds;
        public int RoundIndex { get; private set; }
        public int StartingPlayer { get; private set; }
        public bool IsComplete => RoundIndex >= Rounds;
        public MiniGame CurrentGame => Games[RoundIndex];

        public SeriesSession(PlayerInfo first, PlayerInfo second, int rounds, IEnumerable<MiniGame> gamePool, bool randomOrder)
        {
            if (rounds != 3 && rounds != 5 && rounds != 10) throw new ArgumentException("Las rondas deben ser 3, 5 o 10.");
            Players = new[] { first, second }; Rounds = rounds;
            var pool = new List<MiniGame>(gamePool);
            var originalPool = new List<MiniGame>(pool);
            if (pool.Count < 2) throw new ArgumentException("Elige al menos dos minijuegos para una serie.");
            Games = new List<MiniGame>(rounds);
            MiniGame? previous = null;
            while (Games.Count < rounds)
            {
                var choices = pool.FindAll(g => pool.Count == 1 || !previous.HasValue || g != previous.Value);
                var next = randomOrder ? choices[UnityEngine.Random.Range(0, choices.Count)] : choices[0];
                Games.Add(next); previous = next;
                if (!randomOrder) pool.Remove(next);
                if (pool.Count == 0 && Games.Count < rounds) pool.AddRange(originalPool);
            }
            StartingPlayer = UnityEngine.Random.Range(0, 2);
        }

        public int PlayerStartingThisRound => RoundIndex % 2 == 0 ? StartingPlayer : 1 - StartingPlayer;
        public void RecordResult(int winner)
        {
            if (IsComplete) throw new InvalidOperationException("La serie ya terminó.");
            if (winner == 0 || winner == 1) Score[winner]++;
            RoundIndex++;
        }
    }
}
