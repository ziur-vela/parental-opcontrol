// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpControl.Parental.Contracts.Games;

public enum MoonTowerMode
{
    Make10 = 0,
    Make20 = 1
}

public enum MoonTowerSymbol
{
    Sun = 0,
    Moon = 1,
    Star = 2,
    Comet = 3
}

public enum MoonTowerPower
{
    None = 0,
    Rocket = 1,
    Meteor = 2,
    Eclipse = 3,
    DoubleStar = 4
}

public enum MoonTowerPlayer
{
    Child = 0,
    Luna = 1
}

public enum MoonTowerDifficulty
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}

public enum MoonTowerMoveKind
{
    Place = 0,
    Rocket = 1,
    Meteor = 2,
    Swap = 3
}

public enum MoonTowerResult
{
    Win = 0,
    Loss = 1,
    Draw = 2
}

public enum MoonTowerEventKind
{
    Placed = 0,
    LevelComplete = 1,
    SymbolBonus = 2,
    PowerUsed = 3,
    BagEmpty = 4,
    TowerComplete = 5,
    GameOver = 6,
    Meteor = 7,
    Swap = 8
}

public sealed record MoonTowerBlock(
    int Id,
    int Length,
    MoonTowerSymbol Symbol,
    MoonTowerPower Power = MoonTowerPower.None)
{
    public bool IsWildcard => Power == MoonTowerPower.Eclipse;
    public bool IsPlaceable => Power is MoonTowerPower.None or MoonTowerPower.Eclipse or MoonTowerPower.DoubleStar;
}

public sealed class MoonTowerPlacedBlock
{
    public int BlockId { get; set; }
    public int Length { get; set; }
    public MoonTowerSymbol Symbol { get; set; }
    public MoonTowerPower Power { get; set; }
    public int Level { get; set; }
    public int Start { get; set; }
    public MoonTowerPlayer Owner { get; set; }

    public bool Covers(int column) => column >= Start && column < Start + Length;

    public MoonTowerPlacedBlock Clone() => (MoonTowerPlacedBlock)MemberwiseClone();
}

public sealed record MoonTowerEvent(MoonTowerEventKind Kind, MoonTowerPlayer Player, int Level = 0, int Value = 0);

public sealed record MoonTowerMove(
    MoonTowerMoveKind Kind,
    int HandIndex,
    int Level = 0,
    int Start = 0,
    int TargetBlockId = 0);

public sealed record MoonTowerPlacementScore(
    int Base,
    int LevelBonus,
    int SymbolBonus,
    int Total,
    bool Completed,
    int LevelSumBefore,
    int Needed);

public sealed record MoonTowerHint(MoonTowerMove Move, int Level, int LevelSum, int Needed, int Points);

/// <summary>The rule constants live here and nowhere else, so tuning is one edit.</summary>
public static class MoonTowerConfig
{
    public const int HandSize = 5;
    public const int Levels = 6;
    public const int LevelBonus = 10;
    public const int SymbolBonus = 2;
    public const int MaxBagPowerCopies = 2;
    public const int Make10NormalCopies = 3;
    public const int Make20NormalCopies = 2;

    public static int Width(MoonTowerMode mode) => mode == MoonTowerMode.Make20 ? 20 : 10;

    public static int MaxLength(MoonTowerMode mode) => mode == MoonTowerMode.Make20 ? 8 : 5;

    public static MoonTowerMode ParseMode(string? value) =>
        string.Equals(value, LunaGameModes.Make20, StringComparison.OrdinalIgnoreCase)
            ? MoonTowerMode.Make20
            : MoonTowerMode.Make10;
}

public static class MoonTowerEngine
{
    private static readonly JsonSerializerOptions SaveJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static MoonTowerState NewGame(MoonTowerMode mode, int seed, MoonTowerPlayer first = MoonTowerPlayer.Child)
    {
        var state = new MoonTowerState
        {
            Mode = mode,
            Width = MoonTowerConfig.Width(mode),
            Levels = MoonTowerConfig.Levels,
            MaxBlockLength = MoonTowerConfig.MaxLength(mode),
            Seed = seed,
            Turn = first,
            Bag = BuildBag(mode),
            ChildHand = [],
            LunaHand = [],
            Placed = [],
            ChildScore = 0,
            LunaScore = 0,
            StartedUtc = DateTime.UtcNow
        };

        var rng = new Random(seed);
        Shuffle(state.Bag, rng);
        for (var i = 0; i < MoonTowerConfig.HandSize; i++)
        {
            Draw(state, MoonTowerPlayer.Child, rng);
            Draw(state, MoonTowerPlayer.Luna, rng);
        }
        state.RngState = rng.Next();
        return state;
    }

    private static List<MoonTowerBlock> BuildBag(MoonTowerMode mode)
    {
        var bag = new List<MoonTowerBlock>();
        var nextId = 1;
        var copies = mode == MoonTowerMode.Make20 ? MoonTowerConfig.Make20NormalCopies : MoonTowerConfig.Make10NormalCopies;
        for (var length = 1; length <= MoonTowerConfig.MaxLength(mode); length++)
        {
            foreach (var symbol in Enum.GetValues<MoonTowerSymbol>())
            {
                for (var c = 0; c < copies; c++)
                    bag.Add(new MoonTowerBlock(nextId++, length, symbol));
            }
        }

        for (var c = 0; c < MoonTowerConfig.MaxBagPowerCopies; c++)
        {
            bag.Add(new MoonTowerBlock(nextId++, 0, MoonTowerSymbol.Star, MoonTowerPower.Rocket));
            bag.Add(new MoonTowerBlock(nextId++, 0, MoonTowerSymbol.Comet, MoonTowerPower.Meteor));
            bag.Add(new MoonTowerBlock(nextId++, 2, MoonTowerSymbol.Moon, MoonTowerPower.Eclipse));
            bag.Add(new MoonTowerBlock(nextId++, 1, MoonTowerSymbol.Star, MoonTowerPower.DoubleStar));
        }

        return bag;
    }

    private static void Shuffle<T>(IList<T> items, Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    public static IReadOnlyList<MoonTowerBlock> Hand(MoonTowerState state, MoonTowerPlayer player) =>
        player == MoonTowerPlayer.Child ? state.ChildHand : state.LunaHand;

    public static List<MoonTowerMove> LegalMoves(MoonTowerState state, MoonTowerPlayer player)
    {
        var moves = new List<MoonTowerMove>();
        if (state.Finished) return moves;
        var hand = Hand(state, player);
        for (var i = 0; i < hand.Count; i++)
        {
            var block = hand[i];
            if (block.IsPlaceable)
            {
                for (var level = 0; level < state.Levels; level++)
                {
                    var maxStart = state.Width - block.Length;
                    for (var start = 0; start <= maxStart; start++)
                    {
                        if (IsPlacementLegal(state, level, start, block.Length))
                            moves.Add(new MoonTowerMove(MoonTowerMoveKind.Place, i, level, start));
                    }
                }
            }
            else if (block.Power == MoonTowerPower.Rocket)
            {
                moves.Add(new MoonTowerMove(MoonTowerMoveKind.Rocket, i));
            }
            else if (block.Power == MoonTowerPower.Meteor)
            {
                foreach (var top in TopBlocks(state))
                    moves.Add(new MoonTowerMove(MoonTowerMoveKind.Meteor, i, top.Level, top.Start, top.BlockId));
            }
        }

        if (state.Bag.Count > 0)
        {
            for (var i = 0; i < hand.Count; i++)
                moves.Add(new MoonTowerMove(MoonTowerMoveKind.Swap, i));
        }

        return moves;
    }

    public static bool IsPlacementLegal(MoonTowerState state, int level, int start, int length)
    {
        if (level < 0 || level >= state.Levels || length <= 0) return false;
        if (start < 0 || start + length > state.Width) return false;
        for (var column = start; column < start + length; column++)
        {
            if (BlockAt(state, level, column) is not null) return false;
            if (level > 0 && BlockAt(state, level - 1, column) is null) return false;
        }
        return true;
    }

    public static MoonTowerPlacedBlock? BlockAt(MoonTowerState state, int level, int column) =>
        state.Placed.FirstOrDefault(p => p.Level == level && p.Covers(column));

    public static int RowSum(MoonTowerState state, int level) =>
        Enumerable.Range(0, state.Width).Count(c => BlockAt(state, level, c) is not null);

    public static bool IsLevelComplete(MoonTowerState state, int level) => RowSum(state, level) == state.Width;

    public static bool IsTowerComplete(MoonTowerState state) =>
        Enumerable.Range(0, state.Levels).All(level => IsLevelComplete(state, level));

    public static IReadOnlyList<MoonTowerPlacedBlock> TopBlocks(MoonTowerState state) =>
        state.Placed.Where(block => !state.Placed.Any(other =>
            other.Level == block.Level + 1 &&
            Enumerable.Range(block.Start, block.Length).Any(other.Covers))).ToList();

    public static MoonTowerPlacementScore ScorePlacement(MoonTowerState state, int level, int start, MoonTowerBlock block)
    {
        var sumBefore = RowSum(state, level);
        var completed = !IsLevelComplete(state, level) && sumBefore + block.Length == state.Width;
        var points = block.Length;
        var levelBonus = completed ? MoonTowerConfig.LevelBonus : 0;
        var symbolBonus = 0;
        if (level > 0)
        {
            for (var column = start; column < start + block.Length; column++)
            {
                var below = BlockAt(state, level - 1, column);
                if (below is not null && SymbolsMatch(block.Symbol, below.Symbol, block.IsWildcard, below.Power == MoonTowerPower.Eclipse))
                {
                    symbolBonus = MoonTowerConfig.SymbolBonus;
                    break;
                }
            }
        }
        var total = points + levelBonus + symbolBonus;
        if (block.Power == MoonTowerPower.DoubleStar) total *= 2;
        return new MoonTowerPlacementScore(points, levelBonus, symbolBonus, total, completed, sumBefore,
            Math.Max(0, state.Width - sumBefore));
    }

    private static bool SymbolsMatch(MoonTowerSymbol a, MoonTowerSymbol b, bool aWild, bool bWild) =>
        aWild || bWild || a == b;

    public static List<MoonTowerEvent> Apply(MoonTowerState state, MoonTowerPlayer player, MoonTowerMove move)
    {
        if (player != state.Turn) throw new InvalidOperationException("moon-tower:not-your-turn");
        var legal = LegalMoves(state, player);
        if (!legal.Any(m => SameMove(m, move))) throw new InvalidOperationException("moon-tower:illegal-move");

        var events = new List<MoonTowerEvent>();
        var hand = player == MoonTowerPlayer.Child ? state.ChildHand : state.LunaHand;
        var block = hand[move.HandIndex];
        var extraTurn = false;

        switch (move.Kind)
        {
            case MoonTowerMoveKind.Place:
            {
                hand.RemoveAt(move.HandIndex);
                var score = ScorePlacement(state, move.Level, move.Start, block);
                state.Placed.Add(new MoonTowerPlacedBlock
                {
                    BlockId = block.Id,
                    Length = block.Length,
                    Symbol = block.Symbol,
                    Power = block.Power,
                    Level = move.Level,
                    Start = move.Start,
                    Owner = player
                });
                AddScore(state, player, score.Total);
                state.LastMovePoints = score.Total;
                events.Add(new MoonTowerEvent(MoonTowerEventKind.Placed, player, move.Level, score.Total));
                if (score.Completed)
                {
                    events.Add(new MoonTowerEvent(MoonTowerEventKind.LevelComplete, player, move.Level,
                        MoonTowerConfig.LevelBonus));
                }
                if (score.SymbolBonus > 0)
                    events.Add(new MoonTowerEvent(MoonTowerEventKind.SymbolBonus, player, move.Level, score.SymbolBonus));
                if (block.Power != MoonTowerPower.None)
                    events.Add(new MoonTowerEvent(MoonTowerEventKind.PowerUsed, player, move.Level, (int)block.Power));
                Draw(state, player);
                break;
            }
            case MoonTowerMoveKind.Rocket:
                hand.RemoveAt(move.HandIndex);
                Draw(state, player);
                extraTurn = true;
                state.LastMovePoints = 0;
                events.Add(new MoonTowerEvent(MoonTowerEventKind.PowerUsed, player, 0, (int)MoonTowerPower.Rocket));
                break;

            case MoonTowerMoveKind.Meteor:
            {
                hand.RemoveAt(move.HandIndex);
                var target = state.Placed.FirstOrDefault(p => p.BlockId == move.TargetBlockId)
                    ?? throw new InvalidOperationException("moon-tower:illegal-move");
                state.Placed.Remove(target);
                Draw(state, player);
                state.LastMovePoints = 0;
                events.Add(new MoonTowerEvent(MoonTowerEventKind.Meteor, player, target.Level, target.Length));
                break;
            }
            case MoonTowerMoveKind.Swap:
                hand.RemoveAt(move.HandIndex);
                Draw(state, player);
                state.LastMovePoints = 0;
                events.Add(new MoonTowerEvent(MoonTowerEventKind.Swap, player));
                break;
        }

        state.TurnCount++;
        state.LastEvents = events;
        if (state.Bag.Count == 0 && events.All(e => e.Kind != MoonTowerEventKind.BagEmpty))
        {
            // Only announce once, on the move that empties the bag.
            if (state.BagEmptyAnnounced == false)
            {
                state.BagEmptyAnnounced = true;
                events.Add(new MoonTowerEvent(MoonTowerEventKind.BagEmpty, player));
            }
        }

        if (IsTowerComplete(state))
        {
            Finish(state, events);
            return events;
        }

        AdvanceTurn(state, extraTurn, events);
        state.UpdatedUtc = DateTime.UtcNow;
        return events;
    }

    private static void AdvanceTurn(MoonTowerState state, bool extraTurn, List<MoonTowerEvent> events)
    {
        if (state.Finished) return;
        if (extraTurn) return;

        var next = Other(state.Turn);
        if (state.Bag.Count == 0)
        {
            var nextHasMove = LegalMoves(state, next).Count > 0;
            var currentHasMove = LegalMoves(state, state.Turn).Count > 0;
            if (!nextHasMove && !currentHasMove)
            {
                Finish(state, events);
                return;
            }
            if (!nextHasMove) next = state.Turn;
        }
        state.Turn = next;
    }

    private static void Finish(MoonTowerState state, List<MoonTowerEvent> events)
    {
        state.Finished = true;
        state.Winner = state.ChildScore > state.LunaScore
            ? MoonTowerPlayer.Child
            : state.LunaScore > state.ChildScore ? MoonTowerPlayer.Luna : null;
        state.FinishedUtc = DateTime.UtcNow;
        if (IsTowerComplete(state))
            events.Add(new MoonTowerEvent(MoonTowerEventKind.TowerComplete, state.Turn));
        events.Add(new MoonTowerEvent(MoonTowerEventKind.GameOver, state.Turn,
            0, state.Winner is { } winner ? (int)winner : -1));
    }

    /// <summary>Ends a loaded or hand-built state when the rules say the game is over.</summary>
    public static bool CheckEnd(MoonTowerState state)
    {
        if (state.Finished) return true;
        if (IsTowerComplete(state))
        {
            Finish(state, []);
            return true;
        }
        if (state.Bag.Count == 0
            && LegalMoves(state, MoonTowerPlayer.Child).Count == 0
            && LegalMoves(state, MoonTowerPlayer.Luna).Count == 0)
        {
            Finish(state, []);
            return true;
        }
        return false;
    }

    public static MoonTowerResult ResultFor(MoonTowerState state, MoonTowerPlayer player) =>
        !state.Finished ? MoonTowerResult.Draw
        : state.Winner is null ? MoonTowerResult.Draw
        : state.Winner == player ? MoonTowerResult.Win : MoonTowerResult.Loss;

    public static MoonTowerPlayer Other(MoonTowerPlayer player) =>
        player == MoonTowerPlayer.Child ? MoonTowerPlayer.Luna : MoonTowerPlayer.Child;

    private static void AddScore(MoonTowerState state, MoonTowerPlayer player, int points)
    {
        if (player == MoonTowerPlayer.Child) state.ChildScore += points;
        else state.LunaScore += points;
    }

    private static void Draw(MoonTowerState state, MoonTowerPlayer player) =>
        Draw(state, player, new Random(state.RngState == 0 ? state.Seed : state.RngState));

    private static void Draw(MoonTowerState state, MoonTowerPlayer player, Random rng)
    {
        if (state.Bag.Count == 0) return;
        var index = rng.Next(state.Bag.Count);
        var block = state.Bag[index];
        state.Bag.RemoveAt(index);
        if (player == MoonTowerPlayer.Child) state.ChildHand.Add(block);
        else state.LunaHand.Add(block);
        state.RngState = rng.Next();
    }

    public static bool SameMove(MoonTowerMove a, MoonTowerMove b) =>
        a.Kind == b.Kind && a.HandIndex == b.HandIndex && a.Level == b.Level && a.Start == b.Start
        && a.TargetBlockId == b.TargetBlockId;

    public static MoonTowerHint? Hint(MoonTowerState state, MoonTowerPlayer player) =>
        MoonTowerAi.BestHint(state, player);

    public static MoonTowerState Clone(MoonTowerState state) => new()
    {
        Mode = state.Mode,
        Width = state.Width,
        Levels = state.Levels,
        MaxBlockLength = state.MaxBlockLength,
        Seed = state.Seed,
        RngState = state.RngState,
        Turn = state.Turn,
        Bag = state.Bag.Select(CloneBlock).ToList(),
        ChildHand = state.ChildHand.Select(CloneBlock).ToList(),
        LunaHand = state.LunaHand.Select(CloneBlock).ToList(),
        Placed = state.Placed.Select(p => p.Clone()).ToList(),
        ChildScore = state.ChildScore,
        LunaScore = state.LunaScore,
        TurnCount = state.TurnCount,
        Finished = state.Finished,
        Winner = state.Winner,
        BagEmptyAnnounced = state.BagEmptyAnnounced,
        LastMovePoints = state.LastMovePoints,
        StartedUtc = state.StartedUtc,
        UpdatedUtc = state.UpdatedUtc,
        FinishedUtc = state.FinishedUtc,
        LastEvents = state.LastEvents.ToList()
    };

    private static MoonTowerBlock CloneBlock(MoonTowerBlock block) => block;

    public static string Serialize(MoonTowerState state) => JsonSerializer.Serialize(state, SaveJson);

    public static MoonTowerState? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<MoonTowerState>(json, SaveJson);
        }
        catch
        {
            return null;
        }
    }

    public static string DescribeState(MoonTowerState state)
    {
        var lines = new List<string>
        {
            $"Game: Moon Tower ({state.Mode}), width {state.Width}, levels {state.Levels}.",
            $"Score: child {state.ChildScore}, Luna {state.LunaScore}. Blocks in bag: {state.Bag.Count}.",
            $"Turn: {(state.Turn == MoonTowerPlayer.Child ? "child" : "Luna")}."
        };
        for (var level = 0; level < state.Levels; level++)
            lines.Add($"Level {level + 1}: {RowSum(state, level)}/{state.Width}.");
        var hand = Hand(state, state.Turn);
        if (hand.Count > 0)
            lines.Add("Hand: " + string.Join(", ", hand.Select(b => b.Power == MoonTowerPower.None
                ? $"{b.Symbol} {b.Length}"
                : b.Power.ToString())));
        return string.Join("\n", lines);
    }
}

public sealed class MoonTowerState
{
    public MoonTowerMode Mode { get; set; }
    public int Width { get; set; }
    public int Levels { get; set; }
    public int MaxBlockLength { get; set; }
    public int Seed { get; set; }
    public int RngState { get; set; }
    public MoonTowerPlayer Turn { get; set; }
    public List<MoonTowerBlock> Bag { get; set; } = [];
    public List<MoonTowerBlock> ChildHand { get; set; } = [];
    public List<MoonTowerBlock> LunaHand { get; set; } = [];
    public List<MoonTowerPlacedBlock> Placed { get; set; } = [];
    public int ChildScore { get; set; }
    public int LunaScore { get; set; }
    public int TurnCount { get; set; }
    public int LastMovePoints { get; set; }
    public bool Finished { get; set; }
    public MoonTowerPlayer? Winner { get; set; }
    public bool BagEmptyAnnounced { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    [JsonIgnore] public List<MoonTowerEvent> LastEvents { get; set; } = [];
}

public sealed class MoonTowerSave
{
    public string Game { get; set; } = LunaGameCatalog.MoonTower;
    public string ChildId { get; set; } = "";
    public int ElapsedSeconds { get; set; }
    public DateTime SavedUtc { get; set; } = DateTime.UtcNow;
    public MoonTowerState State { get; set; } = new();
}
