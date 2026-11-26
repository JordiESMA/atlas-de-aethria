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
        public int X, Y, Owner = -1, Troops, ProductionNumber, Level, AttackReadyTurn;
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
    public sealed class RemoteTerritoryState { public int x, y, owner, troops, productionNumber, level, attackReadyTurn; public string land; public bool hasBuilding; }
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
        public const int MaxLevel = 3;
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

        private static readonly LandType[] EconomicSites = { LandType.Farm, LandType.Factory, LandType.Mine };

        public ConquestGame(int seed = -1)
        {
            if (seed >= 0) UnityEngine.Random.InitState(seed);
            Board[0, 1] = new Territory(0, 1, LandType.Capital) { Owner = 0, Troops = 4, HasBuilding = true, Level = 2 };
            Board[Width - 1, Height - 2] = new Territory(Width - 1, Height - 2, LandType.Capital) { Owner = 1, Troops = 4, HasBuilding = true, Level = 2 };
            var sites = new List<Territory>();
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                if (Board[x, y] != null) continue;
                Board[x, y] = new Territory(x, y, LandType.Barracks);
                sites.Add(Board[x, y]);
            }
            Shuffle(sites);
            var blueCapital = Board[0, 1];
            var redCapital = Board[Width - 1, Height - 2];
            var blueCamp = sites.Find(z => IsAdjacent(z, blueCapital));
            var redCamp = Board[Width - 1 - blueCamp.X, Height - 1 - blueCamp.Y];
            blueCamp.ProductionNumber = UnityEngine.Random.Range(0, 2) == 0 ? 5 : 9;
            redCamp.ProductionNumber = 14 - blueCamp.ProductionNumber;
            sites.Remove(blueCamp);
            sites.Remove(redCamp);
            // One 5 and one 9 belong to the two starting camps.
            var numbers = new List<int> { 2, 3, 3, 4, 4, 5, 6, 6, 8, 8, 9, 10, 10, 11, 11, 12 };
            Shuffle(numbers);
            int inlandCamps = 0, economicSites = 0;
            for (int i = 0; i < sites.Count; i++)
            {
                var z = sites[i];
                z.ProductionNumber = numbers[i];
                if (inlandCamps < 4 && !IsAdjacent(z, blueCapital) && !IsAdjacent(z, redCapital)) inlandCamps++;
                else z.Land = EconomicSites[economicSites++ % EconomicSites.Length];
            }
            ActivePlayer = UnityEngine.Random.Range(0, 2);
            AwaitingRoll = true;
            Message = Players[ActivePlayer].Name + " empieza. Lanza los dados para activar la producción.";
        }

        private static void Shuffle<T>(List<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T item = items[i]; items[i] = items[j]; items[j] = item;
            }
        }

        public static int Production(LandType land, int level)
        {
            if (level < 1 || land == LandType.Plain || land == LandType.Capital) return 0;
            return (land == LandType.Mine ? 3 : 2) + Mathf.Min(level, MaxLevel) - 1;
        }

        public static int UpgradeGold(int level) => level == 1 ? 5 : 7;
        public static int UpgradeMaterials(int level) => level == 1 ? 2 : 3;

        public bool RollForTurn()
        {
            if (Finished || !AwaitingRoll) return false;
            AwaitingRoll = false;
            Message = BeginTurn();
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
            if (!CanAct(zone, ActivePlayer) || zone.IsCapital || zone.Level < 1 || zone.Level >= MaxLevel)
                return Fail("Elige un territorio propio de nivel 1 o 2 para mejorarlo.");
            ConquestPlayer p = Players[ActivePlayer];
            int gold = UpgradeGold(zone.Level), materials = UpgradeMaterials(zone.Level);
            if (p.Gold < gold || p.Materials < materials) return Fail("Mejorar cuesta " + gold + " de oro y " + materials + " materiales.");
            p.Gold -= gold; p.Materials -= materials; zone.HasBuilding = true; zone.Level++;
            return Spend("Territorio mejorado a nivel " + zone.Level + ".");
        }

        public bool PurchaseTerritory(Territory from, Territory to)
        {
            if (!CanAct(from, ActivePlayer) || to == null || !IsAdjacent(from, to) || to.Owner >= 0)
                return Fail("Puedes comprar una zona neutral vecina a una zona tuya.");
            ConquestPlayer p = Players[ActivePlayer];
            if (p.Gold < 5) return Fail("Comprar una zona cuesta 5 monedas.");
            p.Gold -= 5; to.Owner = ActivePlayer; to.Troops = 1; to.Level = 1;
            to.AttackReadyTurn = CompletedTurns + 2;
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
            if (from != null && from.Owner == ActivePlayer && from.AttackReadyTurn > CompletedTurns)
                return Fail("Este territorio podrá atacar en tu próximo turno.");
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
                to.AttackReadyTurn = CompletedTurns + 2;
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

        private static string ProductionSummary(int gold, int food, int materials, int troops)
        {
            var produced = new List<string>();
            if (gold > 0) produced.Add("+" + gold + " oro");
            if (food > 0) produced.Add("+" + food + " comida");
            if (materials > 0) produced.Add("+" + materials + " materiales");
            if (troops > 0) produced.Add("+" + troops + " tropas");
            return produced.Count == 0 ? "sin producción" : string.Join(" · ", produced);
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
                    { Owner = remote.owner, Troops = remote.troops, HasBuilding = remote.hasBuilding, AttackReadyTurn = remote.attackReadyTurn,
                        ProductionNumber = remote.productionNumber, Level = remote.level > 0 ? remote.level : remote.owner < 0 ? 0 : remote.hasBuilding ? 2 : 1 };
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
        private string BeginTurn()
        {
            ActionsLeft = 1;
            DiceOne = UnityEngine.Random.Range(1, 7); DiceTwo = UnityEngine.Random.Range(1, 7);
            int[] garrison = new int[2];
            int[] gold = { Players[0].Gold, Players[1].Gold }, food = { Players[0].Food, Players[1].Food };
            int[] materials = { Players[0].Materials, Players[1].Materials };
            foreach (Territory z in Board)
            {
                if (z.Owner < 0) continue;
                if (z.IsCapital) { if (z.Owner == ActivePlayer) z.Troops++; continue; }
                if (z.ProductionNumber != DiceTotal || DiceTotal == 7) continue;
                int amount = Production(z.Land, z.Level);
                ConquestPlayer owner = Players[z.Owner];
                if (z.Land == LandType.Mine) owner.Gold += amount;
                else if (z.Land == LandType.Farm) owner.Food += amount;
                else if (z.Land == LandType.Factory) owner.Materials += amount;
                else if (z.Land == LandType.Barracks) { z.Troops += amount; garrison[z.Owner] += amount; }
            }
            string reinforcement = "Tirada " + DiceTotal + " · Capital J" + (ActivePlayer + 1) + " +1 tropa.";
            if (DiceTotal == 7) return reinforcement + " Los terrenos descansan.";
            var summaries = new string[2];
            for (int i = 0; i < Players.Length; i++)
                summaries[i] = ProductionSummary(Players[i].Gold - gold[i], Players[i].Food - food[i], Players[i].Materials - materials[i], garrison[i]);
            return reinforcement + " Terrenos J1: " + summaries[0] + " · J2: " + summaries[1] + ".";
        }
        private void ResolveScore()
        {
            int blueScore = Score(0), redScore = Score(1);
            Finished = true; Winner = blueScore == redScore ? -1 : (blueScore > redScore ? 0 : 1);
            Message = Winner < 0 ? "¡Empate! Se acabaron las 30 rondas." : Players[Winner].Name + " gana por puntuación (" + blueScore + "–" + redScore + ").";
        }
    }
}
