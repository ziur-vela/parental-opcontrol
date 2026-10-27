// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Luna;

/// <summary>
/// The sleeping moon is the visible quarter of a circle whose centre is outside
/// the top-left corner of the window. Only that yellow sliver may wake Luna or
/// take clicks; the transparent square around it (and the floating Z's) must
/// pass input to the window underneath.
/// </summary>
public static class LunaSleepGeometry
{
    public const double CenterOffsetDip = -20;
    public const double RadiusDip = 48;

    public static bool IsInsideSliver(double x, double y, double sleepScale)
    {
        var scale = sleepScale <= 0 ? 1 : sleepScale;
        var dx = x - CenterOffsetDip * scale;
        var dy = y - CenterOffsetDip * scale;
        var radius = RadiusDip * scale;
        return dx * dx + dy * dy <= radius * radius;
    }

    public static (int Left, int Top, int Right, int Bottom) EllipseBounds(double sleepScale, double renderScaling)
    {
        var scale = sleepScale <= 0 ? 1 : sleepScale;
        var scaling = renderScaling <= 0 ? 1 : renderScaling;
        return (
            (int)Math.Floor((CenterOffsetDip - RadiusDip) * scale * scaling),
            (int)Math.Floor((CenterOffsetDip - RadiusDip) * scale * scaling),
            (int)Math.Ceiling((CenterOffsetDip + RadiusDip) * scale * scaling),
            (int)Math.Ceiling((CenterOffsetDip + RadiusDip) * scale * scaling));
    }
}
