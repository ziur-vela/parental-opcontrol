// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Runtime.InteropServices;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Helper;

/// <summary>Night sky / Moon gray tokens for My OpControl, shared with Luna.</summary>
internal static class KidTheme
{
    public static string Key { get; private set; } = "night";
    public static bool IsGray => Key == "gray";

    public static Color Bg;
    public static Color HeaderBg;
    public static Color SidebarBg;
    public static Color OuterBorder;
    public static Color Text;
    public static Color Muted;
    public static Color Faint;
    public static Color Label;
    public static Color Card;
    public static Color CardBorder;
    public static Color Sub;
    public static Color SubBorder;
    public static Color BarTrack;
    public static Color BarFill;
    public static Color Ok;
    public static Color Bad;
    public static Color Warn;
    public static Color DotOk;
    public static Color DotBad;
    public static Color DotWarn;
    public static Color NavActive;
    public static Color NavActiveText;
    public static Color NavActiveBorder;
    public static Color Primary;
    public static Color PrimaryInk;
    public static Color PrimaryShadow;
    public static Color Chip;
    public static Color ChipBorder;
    public static Color ChipText;
    public static Color SegBg;
    public static Color SegBorder;
    public static Color SegOn;
    public static Color SegOnInk;
    public static Color InputBg;
    public static Color InputBorder;
    public static Color OwnBubble;
    public static Color OwnBubbleInk;
    public static Color ParentBubble;
    public static Color ParentBubbleInk;
    public static Color RingTrack;
    public static Color RingFill;
    public static Color Link;
    public static Color Highlight;

    static KidTheme() => Apply("night");

    public static void Apply(string? theme)
    {
        Key = LunaThemes.Normalize(theme);
        if (Key == "gray") ApplyGray(); else ApplyNight();
    }

    private static Color C(string hex) => ColorTranslator.FromHtml(hex);

    private static void ApplyNight()
    {
        Bg = C("#1b1442"); HeaderBg = C("#191140"); SidebarBg = C("#1a1340");
        OuterBorder = C("#3a2f7a"); Text = C("#f4f1ff"); Muted = C("#c9c0f5"); Faint = C("#a99fe0");
        Label = C("#f6dc7e"); Card = C("#2a2160"); CardBorder = C("#4a3d94");
        Sub = C("#332a6b"); SubBorder = C("#4a3d94");
        BarTrack = C("#3a2f7a"); BarFill = C("#f6dc7e");
        Ok = C("#3ddc97"); Bad = C("#ff8f80"); Warn = C("#ffb547");
        DotOk = C("#3ddc97"); DotBad = C("#ff8f80"); DotWarn = C("#ffb547");
        NavActive = C("#3a2f7a"); NavActiveText = C("#fff6c8"); NavActiveBorder = C("#6a5cc4");
        Primary = C("#3ddc97"); PrimaryInk = C("#15103a"); PrimaryShadow = C("#22a46d");
        Chip = Color.Transparent; ChipBorder = C("#4a3d94"); ChipText = C("#c9c0f5");
        SegBg = C("#2a2160"); SegBorder = C("#4a3d94"); SegOn = C("#f6dc7e"); SegOnInk = C("#15103a");
        InputBg = C("#2a2160"); InputBorder = C("#4a3d94");
        OwnBubble = C("#3ddc97"); OwnBubbleInk = C("#15103a");
        ParentBubble = C("#332a6b"); ParentBubbleInk = C("#f4f1ff");
        RingTrack = C("#3a2f7a"); RingFill = C("#f6dc7e"); Link = C("#f6dc7e"); Highlight = C("#443a86");
    }

    private static void ApplyGray()
    {
        Bg = C("#eceef2"); HeaderBg = C("#f5f6f9"); SidebarBg = C("#f3f4f7");
        OuterBorder = C("#cfd3dc"); Text = C("#23273a"); Muted = C("#565c73"); Faint = C("#72788c");
        Label = C("#7a5a00"); Card = C("#ffffff"); CardBorder = C("#d6dae3");
        Sub = C("#f5f6f9"); SubBorder = C("#e1e4ea");
        BarTrack = C("#e2e5eb"); BarFill = C("#e2bd52");
        Ok = C("#14845a"); Bad = C("#b3261e"); Warn = C("#8a5600");
        DotOk = C("#14845a"); DotBad = C("#b3261e"); DotWarn = C("#c98a00");
        NavActive = C("#ffffff"); NavActiveText = C("#23273a"); NavActiveBorder = C("#e2bd52");
        Primary = C("#14845a"); PrimaryInk = C("#ffffff"); PrimaryShadow = C("#0d5e40");
        Chip = C("#ffffff"); ChipBorder = C("#d6dae3"); ChipText = C("#565c73");
        SegBg = C("#ffffff"); SegBorder = C("#d6dae3"); SegOn = C("#23273a"); SegOnInk = C("#ffffff");
        InputBg = C("#ffffff"); InputBorder = C("#d6dae3");
        OwnBubble = C("#14845a"); OwnBubbleInk = C("#ffffff");
        ParentBubble = C("#f5f6f9"); ParentBubbleInk = C("#23273a");
        RingTrack = C("#e2e5eb"); RingFill = C("#e2bd52"); Link = C("#7a5a00"); Highlight = C("#fff4cf");
    }
}

/// <summary>Windows "Show animations" setting; when off, Luna stops moving.</summary>
internal static class KidMotion
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref bool value, uint winIni);

    public static bool Reduced
    {
        get
        {
            try
            {
                var enabled = true;
                return SystemParametersInfo(SpiGetClientAreaAnimation, 0, ref enabled, 0) && !enabled;
            }
            catch
            {
                return false;
            }
        }
    }
}
