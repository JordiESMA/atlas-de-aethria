using System;
using System.Linq;
using AuteTurno.Conquest;
using UnityEditor;
using UnityEngine;

public static class AtlasProductionReview
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [MenuItem("Atlas/Verify production rules")]
    public static void Run()
    {
        bool sawSeven = false;
        int[,] coverage = new int[4, 3];
        for (int seed = 0; seed < 100; seed++)
        {
            var game = new ConquestGame(seed);
            var board = game.Board.Cast<Territory>().ToArray();
            var capitals = board.Where(z => z.IsCapital).OrderBy(z => z.Owner).ToArray();
            var fields = board.Where(z => !z.IsCapital).ToArray();
            Check(fields.Count(z => z.Land == LandType.Barracks) == 6, "Barracks count");
            Check(capitals.All(c => fields.Any(z => z.Land == LandType.Barracks && game.IsAdjacent(c, z))), "Barracks access");
            foreach (var land in new[] { LandType.Farm, LandType.Factory, LandType.Mine })
                Check(fields.Count(z => z.Land == land) == 4, "Economic terrain count");

            // Include all levels, both owners and neutral sites in real 60-turn games.
            for (int i = 0; i < fields.Length; i++)
            {
                var z = fields[i];
                z.Owner = (i + seed) % 5 == 0 ? -1 : (i + seed) % 2;
                z.Level = z.Owner < 0 ? 0 : 1 + (i + seed) % 3;
                z.HasBuilding = z.Level >= 2;
            }
            for (int turn = 0; turn < 60; turn++)
            {
                var troops = board.Select(z => z.Troops).ToArray();
                var gold = game.Players.Select(p => p.Gold).ToArray();
                var food = game.Players.Select(p => p.Food).ToArray();
                var materials = game.Players.Select(p => p.Materials).ToArray();
                Check(game.RollForTurn() && !game.RollForTurn(), "Exactly one roll per turn");
                sawSeven |= game.DiceTotal == 7;
                for (int i = 0; i < board.Length; i++)
                {
                    var z = board[i];
                    if (z.IsCapital && z.Owner == game.ActivePlayer) troops[i]++;
                    if (!z.IsCapital && z.Owner >= 0 && z.ProductionNumber == game.DiceTotal)
                    {
                        int amount = z.Level + (z.Land == LandType.Mine ? 2 : 1);
                        coverage[(int)z.Land - 1, z.Level - 1]++;
                        if (z.Land == LandType.Barracks) troops[i] += amount;
                        if (z.Land == LandType.Farm) food[z.Owner] += amount;
                        if (z.Land == LandType.Mine) gold[z.Owner] += amount;
                        if (z.Land == LandType.Factory) materials[z.Owner] += amount;
                    }
                    Check(z.Troops == troops[i], "Garrison after roll");
                }
                for (int i = 0; i < 2; i++)
                    Check(game.Players[i].Gold == gold[i] && game.Players[i].Food == food[i] && game.Players[i].Materials == materials[i], "Income after roll");
                game.EndTurn();
            }
            int winner = game.Score(0) == game.Score(1) ? -1 : game.Score(0) > game.Score(1) ? 0 : 1;
            Check(game.Finished && !game.RollForTurn() && game.Winner == winner, "Game over by score");
        }
        Check(sawSeven && coverage.Cast<int>().All(n => n > 0), "Every terrain and level exercised");

        var sample = new ConquestGame(42);
        var capital = sample.Board.Cast<Territory>().Single(z => z.IsCapital && z.Owner == sample.ActivePlayer);
        var barracks = sample.Board.Cast<Territory>().First(z => z.Land == LandType.Barracks && sample.IsAdjacent(capital, z));
        var player = sample.Players[sample.ActivePlayer];
        Check(!sample.PurchaseTerritory(capital, barracks), "Actions before roll");
        sample.RollForTurn();
        Check(sample.PurchaseTerritory(capital, barracks) && barracks.Level == 1 && !sample.Recruit(barracks), "Buy and training at N1");
        Check(sample.Build(barracks) && barracks.Level == 2 && player.Gold == 2 && player.Materials == 4, "N2 upgrade cost");
        Check(!sample.Build(barracks) && player.Gold == 2 && player.Materials == 4, "Insufficient funds");
        player.Gold = 9;
        Check(sample.Build(barracks) && barracks.Level == 3 && player.Gold == 2 && player.Materials == 1, "N3 upgrade cost");
        Check(!sample.Build(barracks) && !sample.Build(capital) && sample.Recruit(barracks), "Level cap and paid training");
        Check(sample.Move(capital, barracks), "Move to upgraded barracks");
        var enemy = sample.Board.Cast<Territory>().First(z => z.IsCapital && z != capital);
        var front = sample.Board.Cast<Territory>().First(z => sample.IsAdjacent(z, enemy));
        front.Owner = sample.ActivePlayer; front.Troops = 20;
        Check(sample.Attack(front, enemy) && sample.Finished && !sample.RollForTurn() && sample.Winner == sample.ActivePlayer, "Capital conquest");
        Debug.Log("ATLAS_PRODUCTION_OK: 100 maps, 6000 turns, all 12 terrain/level combinations, both win conditions.");
    }
}
