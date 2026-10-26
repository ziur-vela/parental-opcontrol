// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public readonly record struct LockBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
}

/// <summary>Measured line boxes for the lock text block and its two buttons.</summary>
public sealed record LockContentLayout(
    LockBox Title,
    LockBox Reason,
    LockBox Hint,
    LockBox PrimaryButton,
    LockBox SecondaryButton)
{
    public IEnumerable<(string Name, LockBox Box)> InOrder() =>
    [
        ("title", Title),
        ("reason", Reason),
        ("hint", Hint),
        ("primary", PrimaryButton),
        ("secondary", SecondaryButton)
    ];
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

    /// <summary>Reference gaps in design pixels at 900 px height.</summary>
    public const double TitleGap = 18;
    public const double ReasonGap = 10;
    public const double ButtonGap = 40;
    public const double ButtonBetweenGap = 16;

    /// <summary>Reference line boxes at 900 px height (line-height 1.05 / normal).</summary>
    public const double TitleLineHeight = 56 * 1.05;
    public const double ReasonLineHeight = 32 * 1.2;
    public const double HintLineHeight = 22 * 1.2;

    public static LockSceneLayout For(double width, double height, double dpiScale = 1.0)
    {
        var scale = Math.Max(0.1, height * 0.94 / 900.0);
        var uiScale = Math.Max(0.5, dpiScale) * Math.Min(1.0, height / 900.0);
        var groupHeight = height * 0.94;
        var groupTop = height - groupHeight;
        var floor = height * 0.08;
        var left = new LockBox(0, groupTop, 560 * scale, groupHeight);
        var right = new LockBox(width - 700 * scale, groupTop, 700 * scale, groupHeight);

        var textWidth = Math.Min(600 * uiScale, Math.Max(260, width * 0.62));
        var textColumn = new LockBox((width - textWidth) / 2, 0, textWidth, 0);

        // Default one-line estimates keep the scene fields usable without a
        // Graphics; the live overlay replaces these with measured boxes.
        var content = Arrange(width, height, uiScale,
            textWidth * 0.85, TitleLineHeight * uiScale,
            textWidth * 0.55, ReasonLineHeight * uiScale,
            textWidth * 0.62, HintLineHeight * uiScale,
            340 * uiScale, 150 * uiScale);
        textColumn = textColumn with { Y = content.Title.Y, Height = content.Hint.Bottom - content.Title.Y };

        var logoHeight = 62 * uiScale;
        var logoWidth = logoHeight * LogoAspectRatio;
        var logo = new LockBox(32 * uiScale, height - logoHeight - 26 * uiScale, logoWidth, logoHeight);

        return new LockSceneLayout(width, height, scale, uiScale, groupHeight, groupTop, floor,
            left, right, textColumn, logo, content.PrimaryButton, content.SecondaryButton);
    }

    /// <summary>
    /// Places the measured text lines and buttons as one vertically centred block.
    /// Every line is centred on the screen on its own, not aligned to a shared
    /// left edge, and the gaps follow the approved reference.
    /// </summary>
    public LockContentLayout ArrangeContent(
        double titleWidth, double titleHeight,
        double reasonWidth, double reasonHeight,
        double hintWidth, double hintHeight,
        double primaryTextWidth, double secondaryTextWidth) =>
        Arrange(Width, Height, UiScale,
            titleWidth, titleHeight, reasonWidth, reasonHeight,
            hintWidth, hintHeight, primaryTextWidth, secondaryTextWidth);

    private static LockContentLayout Arrange(
        double width, double height, double ui,
        double titleWidth, double titleHeight,
        double reasonWidth, double reasonHeight,
        double hintWidth, double hintHeight,
        double primaryTextWidth, double secondaryTextWidth)
    {
        var gapOne = TitleGap * ui;
        var gapTwo = ReasonGap * ui;
        var gapButtons = ButtonGap * ui;
        var gapBetween = ButtonBetweenGap * ui;
        var primaryHeight = Math.Min(72 * ui, height * 0.22);
        var secondaryHeight = Math.Min(52 * ui, height * 0.15);
        var contentHeight = titleHeight + gapOne + reasonHeight + gapTwo + hintHeight
                            + gapButtons + primaryHeight + gapBetween + secondaryHeight;
        var contentTop = Math.Max(0, (height - contentHeight) / 2 - 20 * ui);

        var title = Centered(width, contentTop, titleWidth, titleHeight);
        var reason = Centered(width, title.Bottom + gapOne, reasonWidth, reasonHeight);
        var hint = Centered(width, reason.Bottom + gapTwo, hintWidth, hintHeight);

        // Ref: 40 px side padding, a 32 px icon and a 14 px gap in the primary
        // pill; 30 px side padding in the secondary pill.
        var primaryWidth = Clamp(primaryTextWidth + (2 * 40 + 32 + 14) * ui, 1, Math.Max(1, width - 48));
        var secondaryWidth = Clamp(secondaryTextWidth + 2 * 30 * ui, 1, Math.Max(1, width - 48));
        var primary = new LockBox((width - primaryWidth) / 2, hint.Bottom + gapButtons, primaryWidth, primaryHeight);
        var secondary = new LockBox((width - secondaryWidth) / 2, primary.Bottom + gapBetween,
            secondaryWidth, secondaryHeight);

        return new LockContentLayout(title, reason, hint, primary, secondary);
    }

    private static LockBox Centered(double screenWidth, double top, double textWidth, double height)
    {
        var boxWidth = Math.Max(1, Math.Min(textWidth, screenWidth));
        return new LockBox((screenWidth - boxWidth) / 2, top, boxWidth, height);
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Max(min, Math.Min(max, value));
}
