// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts.Games;

/// <summary>
/// The computer opponent. Every choice comes from <see cref="MoonTowerEngine.LegalMoves"/>,
/// so the AI can never make an illegal move, including after a save/resume.
/// </summary>
public static class MoonTowerAi
{
    public static MoonTowerMove? Choose(MoonTowerState state, MoonTowerDifficulty difficulty, Random? rng = null)
    {
        var moves = MoonTowerEngine.LegalMoves(state, state.Turn);
        if (moves.Count == 0) return null;
        var placements = moves.Where(m => m.Kind == MoonTowerMoveKind.Place).ToList();
        rng ??= new Random(state.Seed ^ (state.TurnCount * 397));

        if (placements.Count == 0)
        {
            // Prefer a Rocket (play again), then Meteor, then Swap.
            return moves.FirstOrDefault(m => m.Kind == MoonTowerMoveKind.Rocket)
                ?? moves.FirstOrDefault(m => m.Kind == MoonTowerMoveKind.Meteor)
                ?? moves.FirstOrDefault(m => m.Kind == MoonTowerMoveKind.Swap);
        }

        return difficulty switch
        {
            MoonTowerDifficulty.Easy => Easy(state, placements, rng),
            MoonTowerDifficulty.Medium => Medium(state, placements),
            _ => Hard(state, placements)
        };
    }

    public static MoonTowerHint? BestHint(MoonTowerState state, MoonTowerPlayer player)
    {
        if (state.Turn != player) return null;
        var placements = MoonTowerEngine.LegalMoves(state, player)
            .Where(m => m.Kind == MoonTowerMoveKind.Place)
            .ToList();
        if (placements.Count == 0) return null;
        var best = placements
            .Select(m => (Move: m, Points: EvaluatePlacement(state, player, m)))
            .OrderByDescending(x => x.Points)
            .ThenBy(x => x.Move.Level)
            .First();
        var block = MoonTowerEngine.Hand(state, player)[best.Move.HandIndex];
        var score = MoonTowerEngine.ScorePlacement(state, best.Move.Level, best.Move.Start, block);
        return new MoonTowerHint(best.Move, best.Move.Level, score.LevelSumBefore, score.Needed, score.Total);
    }

    private static MoonTowerMove Easy(MoonTowerState state, List<MoonTowerMove> placements, Random rng)
    {
        // Easy favors points but still picks at random: predictable enough to beat,
        // never a random illegal move.
        var scored = placements.Select(m => (Move: m, Points: EvaluatePlacement(state, state.Turn, m))).ToList();
        var total = scored.Sum(x => Math.Max(1, x.Points));
        var roll = rng.Next(Math.Max(1, total));
        foreach (var item in scored)
        {
            roll -= Math.Max(1, item.Points);
            if (roll < 0) return item.Move;
        }
        return scored[^1].Move;
    }

    private static MoonTowerMove Medium(MoonTowerState state, List<MoonTowerMove> placements)
    {
        MoonTowerMove best = placements[0];
        var bestScore = double.MinValue;
        foreach (var move in placements)
        {
            var clone = MoonTowerEngine.Clone(state);
            MoonTowerEngine.Apply(clone, state.Turn, move);
            var own = OwnPoints(state, clone, state.Turn);
            var opponent = OpponentBestPoints(clone);
            var score = own - (opponent >= MoonTowerConfig.LevelBonus ? opponent * 0.8 : opponent * 0.25);
            if (clone.Finished && clone.Winner == state.Turn) score += 10_000;
            if (score > bestScore)
            {
                bestScore = score;
                best = move;
            }
        }
        return best;
    }

    private static MoonTowerMove Hard(MoonTowerState state, List<MoonTowerMove> placements)
    {
        // One-ply lookahead: rank our moves first, then simulate the opponent's
        // greedy reply for the strongest candidates only.
        var ranked = placements
            .Select(m => (Move: m, Points: EvaluatePlacement(state, state.Turn, m)))
            .OrderByDescending(x => x.Points)
            .Take(28)
            .ToList();

        MoonTowerMove best = ranked[0].Move;
        var bestScore = double.MinValue;
        foreach (var (move, _) in ranked)
        {
            var clone = MoonTowerEngine.Clone(state);
            MoonTowerEngine.Apply(clone, state.Turn, move);
            if (clone.Finished)
            {
                var final = clone.Winner == state.Turn ? 100_000 : clone.Winner is null ? -500 : -100_000;
                if (final > bestScore)
                {
                    bestScore = final;
                    best = move;
                }
                continue;
            }

            var own = OwnPoints(state, clone, state.Turn);
            var reply = MoonTowerAi.Choose(clone, MoonTowerDifficulty.Medium, new Random(clone.Seed ^ clone.TurnCount));
            var opponentPoints = reply is null ? 0 : EvaluatePlacement(clone, clone.Turn, reply);
            var score = own - opponentPoints * 0.9;
            if (score > bestScore)
            {
                bestScore = score;
                best = move;
            }
        }
        return best;
    }

    private static int EvaluatePlacement(MoonTowerState state, MoonTowerPlayer player, MoonTowerMove move)
    {
        var clone = MoonTowerEngine.Clone(state);
        try
        {
            MoonTowerEngine.Apply(clone, player, move);
        }
        catch (InvalidOperationException)
        {
            return int.MinValue;
        }
        return OwnPoints(state, clone, player);
    }

    private static int OwnPoints(MoonTowerState before, MoonTowerState after, MoonTowerPlayer player) =>
        (player == MoonTowerPlayer.Child ? after.ChildScore : after.LunaScore)
        - (player == MoonTowerPlayer.Child ? before.ChildScore : before.LunaScore);

    private static int OpponentBestPoints(MoonTowerState state)
    {
        var opponent = MoonTowerEngine.Other(state.Turn);
        var best = 0;
        foreach (var move in MoonTowerEngine.LegalMoves(state, opponent))
        {
            if (move.Kind != MoonTowerMoveKind.Place) continue;
            var points = EvaluatePlacement(state, opponent, move);
            if (points > best) best = points;
        }
        return Math.Max(0, best);
    }
}
