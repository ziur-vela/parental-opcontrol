// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Luna;

internal readonly record struct LunaChatDimensions(double Width, double Height);

/// <summary>Chat window sizing rules, independent of the window so they can be tested.</summary>
internal static class LunaChatSize
{
    public const double MinWidth = 520;
    public const double MinHeight = 600;
    public const double DefaultWidthRatio = 0.50;
    public const double DefaultHeightRatio = 0.80;
    public const double MaxWidthRatio = 0.60;
    public const double MaxHeightRatio = 0.90;
    public const double ScreenMargin = 12;

    /// <summary>The largest window that still fits the working area.</summary>
    public static LunaChatDimensions Max(double workWidth, double workHeight)
    {
        var w = Math.Min(workWidth * MaxWidthRatio, workWidth - ScreenMargin);
        var h = Math.Min(workHeight * MaxHeightRatio, workHeight - ScreenMargin);
        return new LunaChatDimensions(Math.Max(1, w), Math.Max(1, h));
    }

    /// <summary>
    /// 50% of the working area width and 80% of the height, but never below
    /// 520 x 600 and never above <see cref="Max"/>. On a screen that is too
    /// small for the minimum, the maximum wins so the window still fits.
    /// </summary>
    public static LunaChatDimensions Default(double workWidth, double workHeight)
    {
        var max = Max(workWidth, workHeight);
        var width = Math.Clamp(workWidth * DefaultWidthRatio,
            Math.Min(MinWidth, max.Width), max.Width);
        var height = Math.Clamp(workHeight * DefaultHeightRatio,
            Math.Min(MinHeight, max.Height), max.Height);
        return new LunaChatDimensions(width, height);
    }

    /// <summary>Clamp any remembered/desired size to the current working area.</summary>
    public static LunaChatDimensions Clamp(double width, double height, double workWidth, double workHeight)
    {
        var max = Max(workWidth, workHeight);
        return new LunaChatDimensions(
            Math.Clamp(width, Math.Min(MinWidth, max.Width), max.Width),
            Math.Clamp(height, Math.Min(MinHeight, max.Height), max.Height));
    }
}
