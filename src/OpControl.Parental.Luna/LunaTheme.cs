// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia.Media;

namespace OpControl.Parental.Luna;

public sealed record LunaSubjectStyle(string Id, Color Tile, Color Ink, Color Shadow, Color Dot);

public sealed class LunaPalette
{
    public required string Key { get; init; }
    public required Color Bg { get; init; }
    public required Color Border { get; init; }
    public required Color Text { get; init; }
    public required Color Muted { get; init; }
    public required Color Soft { get; init; }
    public required Color Accent { get; init; }
    public required Color AccentInk { get; init; }
    public required Color Surface { get; init; }
    public required Color Surface2 { get; init; }
    public required Color InputBg { get; init; }
    public required Color LunaBubble { get; init; }
    public required Color LunaBubbleBorder { get; init; }
    public required Color ChildBubble { get; init; }
    public required Color ChildBubbleBorder { get; init; }
    public required Color Danger { get; init; }
    public required Color Sunken { get; init; }
    public required Color Success { get; init; }
    public IReadOnlyDictionary<string, LunaSubjectStyle> Subjects { get; init; } = new Dictionary<string, LunaSubjectStyle>();

    public LunaSubjectStyle Subject(string id) =>
        Subjects.TryGetValue(id, out var value) ? value : Subjects["curious"];
}

public static class LunaThemes
{
    public static readonly FontFamily Display = new("avares://OpControl.Parental.Luna/Assets/Fonts#Fredoka");
    public static readonly FontFamily Body = new("avares://OpControl.Parental.Luna/Assets/Fonts#Nunito");

    public static readonly LunaPalette Night = new()
    {
        Key = "night",
        Bg = C("#1b1442"), Border = C("#3a2f7a"), Text = C("#f4f1ff"), Muted = C("#c9c0f5"),
        Soft = C("#b3a9ea"), Accent = C("#f6dc7e"), AccentInk = C("#15103a"),
        Surface = C("#2a2160"), Surface2 = C("#4a3d94"), InputBg = C("#2a2160"),
        LunaBubble = C("#3a2f7a"), LunaBubbleBorder = C("#4a3d94"),
        ChildBubble = C("#4a3d94"), ChildBubbleBorder = C("#6b5fc4"),
        Danger = C("#ffb547"), Sunken = C("#1f1846"), Success = C("#3ddc97"),
        Subjects = new Dictionary<string, LunaSubjectStyle>
        {
            ["math"] = new("math", C("#5b8cff"), C("#15103a"), C("#3a63c9"), C("#5b8cff")),
            ["science"] = new("science", C("#3ddc97"), C("#15103a"), C("#22a46d"), C("#3ddc97")),
            ["history"] = new("history", C("#ffb547"), C("#15103a"), C("#d18a1f"), C("#ffb547")),
            ["dictionary"] = new("dictionary", C("#ff8fb1"), C("#15103a"), C("#d9638a"), C("#ff8fb1")),
            ["languages"] = new("languages", C("#b18cff"), C("#15103a"), C("#8a62dd"), C("#b18cff")),
            ["curious"] = new("curious", C("#f6dc7e"), C("#15103a"), C("#cfae45"), C("#f6dc7e"))
        }
    };

    public static readonly LunaPalette Gray = new()
    {
        Key = "gray",
        Bg = C("#eceef2"), Border = C("#cfd3dc"), Text = C("#23273a"), Muted = C("#565c73"),
        Soft = C("#7a8196"), Accent = C("#f6dc7e"), AccentInk = C("#23273a"),
        Surface = C("#f7f8fa"), Surface2 = C("#e2e5eb"), InputBg = C("#ffffff"),
        LunaBubble = C("#ffffff"), LunaBubbleBorder = C("#d7dbe3"),
        ChildBubble = C("#e3d6ff"), ChildBubbleBorder = C("#b9a3ef"),
        Danger = C("#b5650a"), Sunken = C("#e2e5eb"), Success = C("#14845a"),
        Subjects = new Dictionary<string, LunaSubjectStyle>
        {
            ["math"] = new("math", C("#cfe0ff"), C("#23273a"), C("#9db8f0"), C("#2f5bd3")),
            ["science"] = new("science", C("#c9f2de"), C("#23273a"), C("#8fd6b3"), C("#14845a")),
            ["history"] = new("history", C("#ffe2b8"), C("#23273a"), C("#efb46a"), C("#b5650a")),
            ["dictionary"] = new("dictionary", C("#ffd6e3"), C("#23273a"), C("#f0a3bb"), C("#c23a6b")),
            ["languages"] = new("languages", C("#e3d6ff"), C("#23273a"), C("#b9a3ef"), C("#6a3fd6")),
            ["curious"] = new("curious", C("#fff0b3"), C("#23273a"), C("#e8cc5c"), C("#8a6500"))
        }
    };

    public static LunaPalette From(string? key) =>
        string.Equals(key, "gray", StringComparison.OrdinalIgnoreCase) ? Gray : Night;

    public static LunaPalette Toggle(string? key) =>
        string.Equals(key, "gray", StringComparison.OrdinalIgnoreCase) ? Night : Gray;

    private static Color C(string hex) => Color.Parse(hex);
}

public static class LunaBrushes
{
    public static IBrush B(Color c) => new SolidColorBrush(c);
}
