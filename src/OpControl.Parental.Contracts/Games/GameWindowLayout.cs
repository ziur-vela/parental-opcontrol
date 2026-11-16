// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts.Games;

public readonly record struct GameWindowRect(double X, double Y, double Width, double Height);

/// <summary>
/// The big game window covers 90% of the monitor work area, centered, and never the
/// taskbar. All values are device-independent pixels; the caller passes the working
/// area of the monitor the Luna window is on, already divided by the render scaling.
/// </summary>
public static class GameWindowLayout
{
    public const double WorkAreaFraction = 0.90;

    public static GameWindowRect Large(double workX, double workY, double workWidth, double workHeight)
    {
        var width = Math.Max(1, workWidth * WorkAreaFraction);
        var height = Math.Max(1, workHeight * WorkAreaFraction);
        var x = workX + (workWidth - width) / 2;
        var y = workY + (workHeight - height) / 2;
        return new GameWindowRect(x, y, width, height);
    }

    public static GameWindowRect Interpolate(GameWindowRect from, GameWindowRect to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        return new GameWindowRect(
            from.X + (to.X - from.X) * e,
            from.Y + (to.Y - from.Y) * e,
            from.Width + (to.Width - from.Width) * e,
            from.Height + (to.Height - from.Height) * e);
    }

    /// <summary>Fits a target inside the work area, for very small screens.</summary>
    public static GameWindowRect Clamp(GameWindowRect rect, double workX, double workY, double workWidth, double workHeight)
    {
        var width = Math.Min(rect.Width, workWidth);
        var height = Math.Min(rect.Height, workHeight);
        var x = Math.Clamp(rect.X, workX, workX + workWidth - width);
        var y = Math.Clamp(rect.Y, workY, workY + workHeight - height);
        return new GameWindowRect(x, y, width, height);
    }
}
