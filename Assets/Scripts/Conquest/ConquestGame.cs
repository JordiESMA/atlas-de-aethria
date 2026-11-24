using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuteTurno.Conquest
{
    public enum LandType { Plain, Farm, Factory, Mine, Barracks, Capital }
    public enum GameAction { None, Build, Recruit, Attack, Move }

    [Serializable]
    public sealed class Territory
    {
        public int X, Y, Owner = -1, Troops, ProductionNumber, Level;
        public LandType Land;
        public bool HasBuilding;
        public bool IsCapital => Land == LandType.Capital;
        public Territory(int x, int y, LandType land) { X = x; Y = y; Land = land; }
    }

    [Serializable]
    public sealed class ConquestPlayer
    {
        public string Name;
        public int Gold = 12, Food = 8, Materials = 6;
        public ConquestPlayer(string name) { Name = name; }
    }

    [Serializable]
    public sealed class RemotePlayerState { public string name; public int gold, food, materials; }
    [Serializable]
    public sealed class RemoteTerritoryState { public int x, y, owner, troops, productionNumber, level; public string land; public bool hasBuilding; }
    [Serializable]
    public sealed class RemoteGameState
    {
        public string code, status, message;
        public int yourPlayer, activePlayer, actionsLeft, completedTurns, maxTurns, winner, version, diceOne, diceTwo, diceTotal;
        public bool finished;
        public RemotePlayerState[] players;
        public RemoteTerritoryState[] board;
    }
    [Serializable]
    public sealed class RemoteGameEnvelope { public string token, error; public RemoteGameState state; }
    [Serializable]
    public sealed class RemoteActionRequest
    {
        public string token, action;
        public int x, y, toX, toY, expectedVersion;
    }

    /// <summary>Rules and state for a two-player local territory-conquest match.</summary>
    public sealed class ConquestGame
    {
        public const int Width = 5, Height = 4, MaxTurns = 60;
        public readonly Territory[,] Board = new Territory[Width, Height];
        public readonly ConquestPlayer[] Players = { new ConquestPlayer("Jugador azul"), new ConquestPlayer("Jugador rojo") };
        public int ActivePlayer { get; private set; }
        public int ActionsLeft { get; private set; }
        public int CompletedTurns { get; private set; }
        public int DiceOne { get; private set; }
        public int DiceTwo { get; private set; }
        public int DiceTotal => DiceOne + DiceTwo;
        public bool AwaitingRoll { get; private set; }
        public bool Finished { get; private set; }
        public int Winner { get; private set; } = -1;
        public string Message { get; private set; } = "Conquista zonas y construye tu economía.";

        private static readonly LandType[] Specialties = { LandType.Farm, LandType.Factory, LandType.Mine, LandType.Barracks };
        private static readonly int[] ProductionNumbers = { 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 4, 5, 6, 8, 9, 3, 10, 5, 6, 8 };

        public ConquestGame(int seed = -1)
        {
            if (seed >= 0) UnityEngine.Random.InitState(seed);
            int numberIndex = 0;
            int[] numbers = (int[])ProductionNumbers.Clone();
            for (int i = numbers.Length - 1; i > 0; i--)
            { int j = UnityEngine.Random.Range(0, i + 1); int swap = numbers[i]; numbers[i] = numbers[j]; numbers[j] = swap; }
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                LandType land = Specialties[UnityEngine.Random.Range(0, Specialties.Length)];
                Board[x, y] = new Territory(x, y, land) { ProductionNumber = numbers[numberIndex++] };
            }
            Board[0, 1] = new Territory(0, 1, LandType.Capital) { Owner = 0, Troops = 4, HasBuilding = true, Level = 2 };
            Board[Width - 1, Height - 2] = new Territory(Width - 1, Height - 2, LandType.Capital) { Owner = 1, Troops = 4, HasBuilding = true, Level = 2 };
            ActivePlayer = UnityEngine.Random.Range(0, 2);
            AwaitingRoll = true;
            Message = Players[ActivePlayer].Name + " empieza. Lanza los dados para activar la producción.";
        }

        public bool RollForTurn()
        {
            if (Finished || !AwaitingRoll) return false;
            AwaitingRoll = false;
            BeginTurn();
            Message = "Turno de " + Players[ActivePlayer].Name + ". " + TurnDiceMessage();
            return true;
        }

        public bool IsAdjacent(Territory a, Territory b)
        {
            if (a == null || b == null) return false;
            // Territory cells are now beveled square tiles in an isometric 5x4 grid.
            // Keep movement and attack adjacency aligned with the visible four-neighbor layout.
            return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) == 1;
        }

        public void EndTurn(string reason = "")
        {
            if (Finished || AwaitingRoll) return;
            CompletedTurns++;
            if (CompletedTurns >= MaxTurns) { ResolveScore(); return; }
            ActivePlayer = 1 - ActivePlayer;
            AwaitingRoll = true;
            ActionsLeft = 0;
            Message = (string.IsNullOrEmpty(reason) ? "" : reason + " ") + "Turno de " + Players[ActivePlayer].Name + ". Lanza los dados para activar la producción.";
        }

        public bool Build(Territory zone)
        {
            if (!CanAct(zone, ActivePlayer) || zone.HasBuilding || zone.IsCapital) return Fail("Elige una zona propia libre para construir.");
            ConquestPlayer p = Players[ActivePlayer];
            if (p.Gold < 5 || p.Materials < 2) return Fail("Construir cuesta 5 monedas y 2 materiales.");
            p.Gold -= 5; p.Materials -= 2; zone.HasBuilding = true; zone.Level = 2;
            return Spend("Territorio desarrollado a nivel 2: ahora produce recursos al coincidir los dados.");
        }

        public bool PurchaseTerritory(Territory from, Territory to)
        {
            if (!CanAct(from, ActivePlayer) || to == null || !IsAdjacent(from, to) || to.Owner >= 0)
                return Fail("Puedes comprar una zona neutral vecina a una zona tuya.");
            ConquestPlayer p = Players[ActivePlayer];
            if (p.Gold < 5) return Fail("Comprar una zona cuesta 5 monedas.");
            p.Gold -= 5; to.Owner = ActivePlayer; to.Troops = 1; to.Level = 1;
            return Spend("Territorio comprado: nivel 1.");
        }

        public bool Recruit(Territory zone)
        {
            if (!CanAct(zone, ActivePlayer) || (!zone.IsCapital && (!zone.HasBuilding || zone.Land != LandType.Barracks)))
                return Fail("Solo puedes entrenar tropas en tu capital o en un cuartel propio.");
            ConquestPlayer p = Players[ActivePlayer];
            if (p.Food < 2 || p.Gold < 2) return Fail("Entrenar cuesta 2 alimentos y 2 monedas.");
            p.Food -= 2; p.Gold -= 2; zone.Troops++;
            return Spend("Has entrenado una tropa.");
        }

        public bool Attack(Territory from, Territory to)
        {
            if (!CanAct(from, ActivePlayer) || to == null || !IsAdjacent(from, to) || to.Owner == ActivePlayer || from.Troops < 2)
                return Fail("Ataca una zona vecina neutral o rival con al menos 2 tropas.");
            int attackingTroops = from.Troops;
            int attackDie = UnityEngine.Random.Range(1, 7);
            int defenseDie = UnityEngine.Random.Range(1, 7);
            int attack = from.Troops + attackDie;
            int defendingTroops = to.Owner < 0 ? Mathf.Max(1, to.Troops) : to.Troops;
            int defense = defendingTroops + defenseDie;
            if (attack > defense)
            {
                int oldOwner = to.Owner;
                to.Owner = ActivePlayer;
                to.Level = Mathf.Max(1, to.Level);
                to.Troops = Mathf.Max(1, from.Troops - 1);
                from.Troops = 1;
                if (to.IsCapital && oldOwner != ActivePlayer) { Finished = true; Winner = ActivePlayer; Message = Players[Winner].Name + " gana al conquistar la capital rival. Combate: " + attackingTroops + " + d6 " + attackDie + " contra " + defendingTroops + " + d6 " + defenseDie + "."; return true; }
                return Spend("Combate: " + attackingTroops + " tropas + d6 " + attackDie + " contra " + defendingTroops + " + d6 " + defenseDie + ". ¡Victoria!");
            }
            from.Troops = Mathf.Max(1, from.Troops - 1);
            if (to.Owner < 0) to.Troops = 1;
            return Spend("Combate: " + (from.Troops + 1) + " tropas + d6 " + attackDie + " contra " + defendingTroops + " + d6 " + defenseDie + ". Derrota: pierdes 1 tropa.");
        }

        public bool Move(Territory from, Territory to)
        {
            if (!CanAct(from, ActivePlayer) || to == null || !IsAdjacent(from, to) || to.Owner != ActivePlayer || from.Troops < 2)
                return Fail("Mueve una tropa a una zona propia vecina.");
            from.Troops--; to.Troops++;
            return Spend("Has movido una tropa.");
        }

        public static string LandName(LandType land)
        {
            switch (land)
            {
                case LandType.Farm: return "huerto";
                case LandType.Factory: return "fábrica";
                case LandType.Mine: return "mina";
                case LandType.Barracks: return "cuartel";
                case LandType.Capital: return "capital";
                default: return "llanura";
            }
        }

        public string ProductionSummary(int player)
        {
            if (DiceTotal == 7) return "ningún edificio produce con 7";
            int gold = 0, food = 0, materials = 0, troops = 0;
            foreach (Territory zone in Board)
            {
                if (zone.Owner != player || !zone.HasBuilding || zone.IsCapital || zone.ProductionNumber != DiceTotal || DiceTotal == 7) continue;
                if (zone.Land == LandType.Mine) gold += 3;
                if (zone.Land == LandType.Farm) food += 2;
                if (zone.Land == LandType.Factory) materials += 2;
                if (zone.Land == LandType.Barracks) troops++;
            }
            var produced = new List<string>();
            if (gold > 0) produced.Add("+" + gold + " oro");
            if (food > 0) produced.Add("+" + food + " comida");
            if (materials > 0) produced.Add("+" + materials + " materiales");
            if (troops > 0) produced.Add("+" + troops + " tropas");
            return produced.Count == 0 ? "sin producción" : string.Join(" · ", produced);
        }

        private string TurnDiceMessage()
        {
            return DiceTotal == 7
                ? "Salió 7 (" + DiceOne + "+" + DiceTwo + "): ningún edificio produce."
                : "Salió " + DiceTotal + " (" + DiceOne + "+" + DiceTwo + "). Producción · J1: " + ProductionSummary(0) + " · J2: " + ProductionSummary(1) + ".";
        }

        public int CountTerritories(int player)
        { int count = 0; foreach (Territory z in Board) if (z.Owner == player) count++; return count; }

        public int Score(int player)
        {
            int score = 0;
            foreach (Territory z in Board)
                if (z.Owner == player) score += 1 + z.Troops + (z.HasBuilding ? 2 : 0) + (z.IsCapital ? 5 : 0);
            return score;
        }

        public void ApplySnapshot(RemoteGameState state)
        {
            if (state == null) return;
            if (state.players != null)
                for (int i = 0; i < Mathf.Min(Players.Length, state.players.Length); i++)
                    if (state.players[i] != null)
                    {
                        Players[i].Name = state.players[i].name;
                        Players[i].Gold = state.players[i].gold; Players[i].Food = state.players[i].food;
                        Players[i].Materials = state.players[i].materials;
                    }
            if (state.board != null)
                foreach (RemoteTerritoryState remote in state.board)
                {
                    if (remote == null || remote.x < 0 || remote.x >= Width || remote.y < 0 || remote.y >= Height) continue;
                    LandType land;
                    if (!Enum.TryParse(remote.land, true, out land)) land = LandType.Plain;
                    Board[remote.x, remote.y] = new Territory(remote.x, remote.y, land)
                    { Owner = remote.owner, Troops = remote.troops, HasBuilding = remote.hasBuilding,
                        ProductionNumber = remote.productionNumber, Level = remote.level >= 0 ? remote.level : remote.owner < 0 ? 0 : remote.hasBuilding ? 2 : 1 };
                }
            ActivePlayer = state.activePlayer; ActionsLeft = state.actionsLeft; CompletedTurns = state.completedTurns;
            DiceOne = state.diceOne; DiceTwo = state.diceTwo;
            AwaitingRoll = false; Finished = state.finished; Winner = state.winner; Message = state.message ?? "";
        }

        private bool CanAct(Territory zone, int player) => !Finished && ActionsLeft > 0 && zone != null && zone.Owner == player;
        private bool Fail(string message) { Message = message; return false; }
        private bool Spend(string message)
        {
            // The active player may take several affordable actions before finishing.
            ActionsLeft = 1;
            Message = message;
            return true;
        }
        private void BeginTurn()
        {
            ActionsLeft = 1;
            DiceOne = UnityEngine.Random.Range(1, 7); DiceTwo = UnityEngine.Random.Range(1, 7);
            foreach (Territory z in Board)
            {
                if (z.Owner < 0 || !z.HasBuilding || z.IsCapital || z.ProductionNumber != DiceTotal || DiceTotal == 7) continue;
                ConquestPlayer owner = Players[z.Owner];
                if (z.Land == LandType.Mine) owner.Gold += 3;
                else if (z.Land == LandType.Farm) owner.Food += 2;
                else if (z.Land == LandType.Factory) owner.Materials += 2;
                else if (z.Land == LandType.Barracks) z.Troops++;
            }
        }
        private void ResolveScore()
        {
            int blueScore = Score(0), redScore = Score(1);
            Finished = true; Winner = blueScore == redScore ? -1 : (blueScore > redScore ? 0 : 1);
            Message = Winner < 0 ? "¡Empate! Se acabaron las 30 rondas." : Players[Winner].Name + " gana por puntuación (" + blueScore + "–" + redScore + ").";
        }
    }
}
