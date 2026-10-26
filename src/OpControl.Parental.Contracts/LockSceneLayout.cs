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
/// stretches. Coordinates are physical pixels. <see cref="UiScale"/> carries the
/// monitor DPI so text, buttons and the logo stay crisp and the same apparent
/// size at 100% to 200% scaling (the reference design is drawn for 900 px).
/// </summary>
public sealed record LockSceneLayout(
    double Width,
    double Height,
    double Scale,
    double UiScale,
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
    /// <summary>lockup-ondark-300h.png is 871 x 300 pixels.</summary>
    public const double LogoAspectRatio = 871.0 / 300.0;

    public static LockSceneLayout For(double width, double height, double dpiScale = 1.0)
    {
        var scale = Math.Max(0.1, height * 0.94 / 900.0);
        var uiScale = Math.Max(0.5, dpiScale) * Math.Min(1.0, height / 900.0);
        var groupHeight = height * 0.94;
        var groupTop = height - groupHeight;
        var floor = height * 0.08;
        var left = new LockBox(0, groupTop, 560 * scale, groupHeight);
        var right = new LockBox(width - 700 * scale, groupTop, 700 * scale, groupHeight);

        // The reference centres the title, reason, hint and both buttons as one
        // block, with a 40 px bottom padding. Keep that relationship at every size.
        var titleHeight = 58.8 * uiScale;
        var reasonHeight = 38.4 * uiScale;
        var hintHeight = 26.4 * uiScale;
        var gapOne = 18 * uiScale;
        var gapTwo = 10 * uiScale;
        var gapButtons = 40 * uiScale;
        var gapSecondary = 10 * uiScale;
        var primaryHeight = Math.Min(78 * uiScale, height * 0.22);
        var secondaryHeight = Math.Min(52 * uiScale, height * 0.15);
        var contentHeight = titleHeight + gapOne + reasonHeight + gapTwo + hintHeight
                            + gapButtons + primaryHeight + gapSecondary + secondaryHeight;
        var contentTop = Math.Max(0, (height - contentHeight) / 2 - 20 * uiScale);

        var textWidth = Math.Min(600 * uiScale, Math.Max(260, width * 0.62));
        var textColumn = new LockBox((width - textWidth) / 2, contentTop, textWidth,
            titleHeight + gapOne + reasonHeight + gapTwo + hintHeight);

        var logoHeight = 62 * uiScale;
        var logoWidth = logoHeight * LogoAspectRatio;
        var logo = new LockBox(32 * uiScale, height - logoHeight - 26 * uiScale, logoWidth, logoHeight);

        var primaryWidth = Math.Min(366 * uiScale, width - 48);
        var primary = new LockBox((width - primaryWidth) / 2,
            textColumn.Bottom + gapButtons, primaryWidth, primaryHeight);
        var secondaryWidth = Math.Min(210 * uiScale, width - 48);
        var secondary = new LockBox((width - secondaryWidth) / 2,
            primary.Bottom + gapSecondary, secondaryWidth, secondaryHeight);

        return new LockSceneLayout(width, height, scale, uiScale, groupHeight, groupTop, floor,
            left, right, textColumn, logo, primary, secondary);
    }
}
