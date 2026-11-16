// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts.Games;

/// <summary>The two Play with Luna categories. The Windows client and the server share this list.</summary>
public static class LunaGameCatalog
{
    public const string SpaceBoards = "space-boards";
    public const string LunarCraft = "lunar-craft";

    public const string MoonTower = "moon-tower";
    public const string Orbit = "orbit";
    public const string CraterClash = "crater-clash";
    public const string CritterLab = "critter-lab";
    public const string CritterArena = "critter-arena";
    public const string LunarHarvest = "lunar-harvest";

    public sealed record GameCategory(string Id, string NameKey, string DetailKey);
    public sealed record Game(string Id, string CategoryId, string NameKey, string DetailKey, bool Playable);

    public static readonly IReadOnlyList<GameCategory> Categories =
    [
        new(SpaceBoards, "Games.Category.SpaceBoards", "Games.Category.SpaceBoardsDetail"),
        new(LunarCraft, "Games.Category.LunarCraft", "Games.Category.LunarCraftDetail")
    ];

    public static readonly IReadOnlyList<Game> Games =
    [
        new(MoonTower, SpaceBoards, "Games.MoonTower.Name", "Games.MoonTower.Detail", true),
        new(Orbit, SpaceBoards, "Games.Orbit.Name", "Games.Orbit.Detail", false),
        new(CraterClash, SpaceBoards, "Games.CraterClash.Name", "Games.CraterClash.Detail", false),
        new(CritterLab, LunarCraft, "Games.CritterLab.Name", "Games.CritterLab.Detail", false),
        new(CritterArena, LunarCraft, "Games.CritterArena.Name", "Games.CritterArena.Detail", false),
        new(LunarHarvest, LunarCraft, "Games.LunarHarvest.Name", "Games.LunarHarvest.Detail", false)
    ];

    public static GameCategory FindCategory(string id) =>
        Categories.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? Categories[0];

    public static IReadOnlyList<Game> InCategory(string categoryId) =>
        Games.Where(g => string.Equals(g.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase)).ToList();

    public static Game? Find(string id) =>
        Games.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string DisplayName(string id) => Find(id)?.Id switch
    {
        MoonTower => "Moon Tower",
        Orbit => "Orbit",
        CraterClash => "Crater Clash",
        CritterLab => "Critter Lab",
        CritterArena => "Critter Arena",
        LunarHarvest => "Lunar Harvest",
        _ => "Moon Tower"
    };

    public static bool IsValid(string? id) => !string.IsNullOrWhiteSpace(id) && Find(id.Trim()) is not null;

    public static string Normalize(string id) => (Find(id)?.Id ?? id).Trim().ToLowerInvariant();
}

public static class LunaGameModes
{
    public const string Make10 = "make-10";
    public const string Make20 = "make-20";
    public const string Fractions = "fractions";
    public const string TimesTables = "times-tables";

    public static readonly string[] All = [Make10, Make20, Fractions, TimesTables];

    public static bool IsPlayable(string? mode) =>
        string.Equals(mode, Make10, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, Make20, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? mode) => (mode ?? "").Trim().ToLowerInvariant() switch
    {
        Make20 => Make20,
        Fractions => Fractions,
        TimesTables => TimesTables,
        _ => Make10
    };
}

/// <summary>
/// Seam for a future "play with friends" transport. No networking exists yet; the
/// disabled button in the common game frame only needs to know that it is unavailable.
/// </summary>
public interface ILunaGameMultiplayer
{
    bool IsAvailable { get; }
    Task<bool> InviteAsync(string gameId, CancellationToken ct = default);
}

public sealed class LunaNoMultiplayer : ILunaGameMultiplayer
{
    public bool IsAvailable => false;
    public Task<bool> InviteAsync(string gameId, CancellationToken ct = default) => Task.FromResult(false);
}
