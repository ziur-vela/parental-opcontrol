// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public readonly record struct LockBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
}

/// <summary>
/// Geometry for the cozy bedroom lock scene. The two shape groups are anchored
/// to the bottom corners at 94% of the screen height; the wall between them
/// stretches. Coordinates are physical pixels.
/// </summary>
public sealed record LockSceneLayout(
    double Width,
    double Height,
    double Scale,
    double GroupHeight,
    double GroupTop,
    double FloorHeight,
    LockBox LeftGroup,
    LockBox RightGroup,
    LockBox TextColumn,
    LockBox Logo,
    LockBox PrimaryButton,
    LockBox SecondaryButton)
{
    public static LockSceneLayout For(double width, double height)
    {
        var scale = Math.Max(0.1, height * 0.94 / 900.0);
        var groupHeight = height * 0.94;
        var groupTop = height - groupHeight;
        var floor = height * 0.08;
        var left = new LockBox(0, groupTop, 560 * scale, groupHeight);
        var right = new LockBox(width - 700 * scale, groupTop, 700 * scale, groupHeight);
        var textWidth = Math.Min(600, Math.Max(260, width * 0.62));
        var textColumn = new LockBox((width - textWidth) / 2, groupTop + 0.26 * groupHeight, textWidth, 0.48 * groupHeight);
        var logoHeight = 62 * Math.Min(1.4, Math.Max(0.8, height / 900.0));
        var logoWidth = logoHeight * (330.0 / 66.0);
        var logo = new LockBox(32, height - logoHeight - 26, logoWidth, logoHeight);
        var primaryWidth = Math.Min(360 * scale, width - 48);
        var primaryHeight = Math.Min(72 * scale, height * 0.12);
        var primary = new LockBox((width - primaryWidth) / 2, groupTop + 0.66 * groupHeight, primaryWidth, primaryHeight);
        var secondaryWidth = Math.Min(300 * scale, width - 48);
        var secondaryHeight = Math.Min(52 * scale, height * 0.09);
        var secondary = new LockBox((width - secondaryWidth) / 2, primary.Bottom + 16 * scale, secondaryWidth, secondaryHeight);
        return new LockSceneLayout(width, height, scale, groupHeight, groupTop, floor,
            left, right, textColumn, logo, primary, secondary);
    }
}
