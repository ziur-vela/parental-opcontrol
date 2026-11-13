// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;

namespace OpControl.Parental.Helper;

internal sealed record MessagePopupPalette(
    string Key,
    Color Background,
    Color Text,
    Color Muted,
    Color Card,
    Color Border,
    Color Bubble,
    Color BubbleInk,
    Color Accent,
    Color AccentInk,
    Color Secondary,
    Color SecondaryInk,
    Color Shadow);

/// <summary>The child's Night sky / Moon gray palette for the message popup.</summary>
internal static class MessagePopupTheme
{
    public static MessagePopupPalette For(string? theme)
    {
        KidTheme.Apply(theme);
        if (KidTheme.IsGray)
        {
            return new MessagePopupPalette(
                "gray",
                Background: KidTheme.Bg,
                Text: KidTheme.Text,
                Muted: KidTheme.Muted,
                Card: KidTheme.Card,
                Border: KidTheme.CardBorder,
                Bubble: KidTheme.Primary,
                BubbleInk: KidTheme.PrimaryInk,
                Accent: KidTheme.Link,
                AccentInk: Color.White,
                Secondary: KidTheme.Sub,
                SecondaryInk: KidTheme.Text,
                Shadow: Color.FromArgb(38, 35, 39, 58));
        }

        return new MessagePopupPalette(
            "night",
            Background: KidTheme.Bg,
            Text: KidTheme.Text,
            Muted: KidTheme.Muted,
            Card: KidTheme.Card,
            Border: KidTheme.CardBorder,
            Bubble: KidTheme.Primary,
            BubbleInk: KidTheme.PrimaryInk,
            Accent: KidTheme.Link,
            AccentInk: KidTheme.PrimaryInk,
            Secondary: KidTheme.Sub,
            SecondaryInk: KidTheme.Text,
            Shadow: Color.FromArgb(90, 0, 0, 0));
    }

    public static string Resolve()
    {
        var local = KidThemeStore.ReadLocal();
        if (local is not null) return local;
        var summary = HelperPaths.ReadSummary();
        if (summary is not null && LunaThemes.IsValid(summary.Theme)) return LunaThemes.Normalize(summary.Theme);
        return LunaThemes.Normalize(HelperPaths.ReadState()?.Theme);
    }
}
