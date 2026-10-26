// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OpControl.Parental.Luna;

internal enum LunaGreetingTailSide
{
    Left,
    Right
}

internal sealed record LunaGreetingStyle(Color Fill, Color Outline, Color Shadow, Color Ink);

internal sealed record LunaGreetingPlacement(double Left, double Top, LunaGreetingTailSide TailSide);

/// <summary>
/// The comic-book speech bubble shown next to the awake moon. It draws its own
/// rounded box, tail and hard shadow so the tail joins the outline without a
/// seam, and sizes itself to the two greeting lines.
/// </summary>
public sealed class LunaGreetingBubble : Control
{
    internal const double TailDepth = 16;
    internal const double TailHalfBase = 9;
    internal const double CornerRadius = 18;
    internal const double OutlineThickness = 4;
    internal const double ShadowOffset = 5;
    internal const double PadLeft = 18;
    internal const double PadRight = 20;
    internal const double PadVertical = 14;
    internal const double LineGap = 3;
    internal const double FirstLineSize = 24;
    internal const double SecondLineSize = 15;

    private static readonly Typeface FirstFace = new(
        LunaThemes.Display, FontStyle.Normal, FontWeight.Bold);

    private static readonly Typeface SecondFace = new(
        LunaThemes.Body, FontStyle.Normal, FontWeight.ExtraBold);

    private readonly List<FormattedText> _lines = [];
    private readonly List<string> _lineTexts = [];
    private LunaGreetingStyle _style = ForTheme("night");
    private LunaGreetingTailSide _tailSide = LunaGreetingTailSide.Left;
    private string _themeKey = "night";
    private string _firstLine = string.Empty;
    private string _secondLine = string.Empty;

    public LunaGreetingBubble()
    {
        IsHitTestVisible = false;
        Configure("night", string.Empty, string.Empty);
    }

    internal string FirstLine => _firstLine;
    internal string SecondLine => _secondLine;
    internal string ThemeKey => _themeKey;
    internal int LineCount => _lines.Count;
    internal double TextWidth => _lines.Count == 0 ? 0 : _lines.Max(line => line.WidthIncludingTrailingWhitespace);
    internal double TextHeight => _lines.Sum(line => line.Height) + LineGap * Math.Max(0, _lines.Count - 1);
    internal IReadOnlyList<double> LineWidths => _lines.Select(line => line.WidthIncludingTrailingWhitespace).ToList();
    internal IReadOnlyList<double> LineHeights => _lines.Select(line => line.Height).ToList();
    internal IReadOnlyList<string> Lines => _lineTexts;
    internal double BodyWidth => Math.Ceiling(TextWidth) + PadLeft + PadRight + 1;
    internal double BodyHeight => Math.Ceiling(TextHeight) + PadVertical * 2;
    internal double TotalWidth => BodyWidth + TailDepth + OutlineThickness;
    internal double TotalHeight => BodyHeight + OutlineThickness + ShadowOffset;
    internal LunaGreetingStyle Style => _style;
    internal LunaGreetingTailSide TailSide => _tailSide;
    internal double TailTipX => _tailSide == LunaGreetingTailSide.Left
        ? OutlineThickness / 2
        : TotalWidth - OutlineThickness / 2;
    internal double TailTipY => OutlineThickness / 2 + BodyHeight / 2;

    internal static LunaGreetingStyle ForTheme(string? theme) =>
        string.Equals(theme, "gray", StringComparison.OrdinalIgnoreCase)
            ? new LunaGreetingStyle(
                Color.Parse("#ffffff"), Color.Parse("#23273a"), Color.Parse("#e2bd52"), Color.Parse("#23273a"))
            : new LunaGreetingStyle(
                Color.Parse("#fffdf3"), Color.Parse("#15103a"), Color.Parse("#e8dcc6"), Color.Parse("#15103a"));

    public void Configure(string themeKey, string firstLine, string secondLine)
    {
        _themeKey = string.Equals(themeKey, "gray", StringComparison.OrdinalIgnoreCase) ? "gray" : "night";
        _style = ForTheme(_themeKey);
        _firstLine = firstLine ?? string.Empty;
        _secondLine = secondLine ?? string.Empty;

        _lines.Clear();
        _lineTexts.Clear();
        _lines.Add(MakeText(_firstLine, FirstFace, FirstLineSize));
        _lineTexts.Add(_firstLine);
        foreach (var part in SplitSecondLine(_secondLine))
        {
            _lines.Add(MakeText(part, SecondFace, SecondLineSize));
            _lineTexts.Add(part);
        }

        Width = TotalWidth;
        Height = TotalHeight;
        RenderTransformOrigin = new RelativePoint(TailTipX / TotalWidth, TailTipY / TotalHeight, RelativeUnit.Relative);
        InvalidateMeasure();
        InvalidateVisual();
    }

    internal void SetTailSide(LunaGreetingTailSide side)
    {
        if (_tailSide == side)
        {
            return;
        }

        _tailSide = side;
        RenderTransformOrigin = new RelativePoint(TailTipX / TotalWidth, TailTipY / TotalHeight, RelativeUnit.Relative);
        InvalidateVisual();
    }

    /// <summary>
    /// Breaks the Nunito line after the question mark when there is more text,
    /// as allowed by the design ("the second line can break after 'question?'").
    /// </summary>
    internal static IReadOnlyList<string> SplitSecondLine(string text)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return [string.Empty];
        }

        var mark = text.IndexOf('?');
        if (mark >= 0 && mark < text.Length - 1)
        {
            var head = text[..(mark + 1)].Trim();
            var tail = text[(mark + 1)..].Trim();
            if (head.Length > 0 && tail.Length > 0)
            {
                return [head, tail];
            }
        }

        return [text];
    }

    private FormattedText MakeText(string text, Typeface face, double size) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size,
            new SolidColorBrush(_style.Ink));

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bodyLeft = OutlineThickness / 2 + (_tailSide == LunaGreetingTailSide.Left ? TailDepth : 0);
        var bodyTop = OutlineThickness / 2;
        var bodyRight = bodyLeft + BodyWidth;
        var bodyBottom = bodyTop + BodyHeight;

        var shadow = BuildGeometry(bodyLeft, bodyTop + ShadowOffset, bodyRight, bodyBottom + ShadowOffset, _tailSide);
        context.DrawGeometry(new SolidColorBrush(_style.Shadow), null, shadow);

        var body = BuildGeometry(bodyLeft, bodyTop, bodyRight, bodyBottom, _tailSide);
        var pen = new Pen(new SolidColorBrush(_style.Outline), OutlineThickness)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round
        };
        context.DrawGeometry(new SolidColorBrush(_style.Fill), pen, body);

        var y = bodyTop + PadVertical;
        var x = bodyLeft + PadLeft;
        foreach (var line in _lines)
        {
            context.DrawText(line, new Point(x, y));
            y += line.Height + LineGap;
        }
    }

    private StreamGeometry BuildGeometry(
        double left, double top, double right, double bottom, LunaGreetingTailSide side)
    {
        var radius = CornerRadius;
        var tailY = top + BodyHeight / 2;
        var baseTop = tailY - TailHalfBase;
        var baseBottom = tailY + TailHalfBase;
        var tipInset = OutlineThickness / 2;
        var tipX = side == LunaGreetingTailSide.Left ? tipInset : TotalWidth - tipInset;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(left + radius, top), isFilled: true);

            // Top edge and top-right corner.
            ctx.LineTo(new Point(right - radius, top), true);
            ctx.QuadraticBezierTo(new Point(right, top), new Point(right, top + radius), true);

            if (side == LunaGreetingTailSide.Right)
            {
                // Right edge with the tail pointing at Luna on the left.
                ctx.LineTo(new Point(right, baseTop), true);
                ctx.LineTo(new Point(tipX, tailY), true);
                ctx.LineTo(new Point(right, baseBottom), true);
                ctx.LineTo(new Point(right, bottom - radius), true);
            }
            else
            {
                ctx.LineTo(new Point(right, bottom - radius), true);
            }

            // Bottom-right corner, bottom edge and bottom-left corner.
            ctx.QuadraticBezierTo(new Point(right, bottom), new Point(right - radius, bottom), true);
            ctx.LineTo(new Point(left + radius, bottom), true);
            ctx.QuadraticBezierTo(new Point(left, bottom), new Point(left, bottom - radius), true);

            if (side == LunaGreetingTailSide.Left)
            {
                // Left edge with the tail pointing at Luna on the left.
                ctx.LineTo(new Point(left, baseBottom), true);
                ctx.LineTo(new Point(tipX, tailY), true);
                ctx.LineTo(new Point(left, baseTop), true);
                ctx.LineTo(new Point(left, top + radius), true);
            }
            else
            {
                ctx.LineTo(new Point(left, top + radius), true);
            }

            // Top-left corner back to the start.
            ctx.QuadraticBezierTo(new Point(left, top), new Point(left + radius, top), true);
            ctx.EndFigure(true);
        }

        return geometry;
    }
}

internal static class LunaMotion
{
    private static bool _cached;
    private static long _checkedAt = long.MinValue;

    internal static bool? ForceReducedMotionForTest { get; set; }

    internal static bool ReducedMotion
    {
        get
        {
            if (ForceReducedMotionForTest is { } forced)
            {
                return forced;
            }

            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            var now = Environment.TickCount64;
            if (now - _checkedAt < 1000)
            {
                return _cached;
            }

            _checkedAt = now;
            try
            {
                var enabled = 1;
                _cached = Win32.SystemParametersInfo(Win32.SpiGetClientAreaAnimation, 0, ref enabled, 0) && enabled == 0;
            }
            catch
            {
                _cached = false;
            }

            return _cached;
        }
    }
}

internal static class LunaGreetingLayout
{
    internal const double MoonRadius = 52;
    internal const double MoonGap = 6;
    internal const double EdgeMargin = 6;

    internal static LunaGreetingPlacement Compute(
        double moonCenterX, double moonEyeY, double controlWidth, double controlHeight,
        double bodyHeight, double viewportWidth, double viewportHeight,
        double moonRadius = MoonRadius, double gap = MoonGap, double edge = EdgeMargin)
    {
        var tailInset = LunaGreetingBubble.OutlineThickness / 2;
        var rightTip = moonCenterX + moonRadius + gap;
        var leftTip = moonCenterX - moonRadius - gap;

        var rightLeft = rightTip - tailInset;
        var leftLeft = leftTip - (controlWidth - tailInset);
        var rightFits = rightLeft >= edge && rightLeft + controlWidth <= viewportWidth - edge;
        var leftFits = leftLeft >= edge && leftLeft + controlWidth <= viewportWidth - edge;

        LunaGreetingTailSide side;
        double left;
        if (rightFits)
        {
            side = LunaGreetingTailSide.Left;
            left = rightLeft;
        }
        else if (leftFits)
        {
            side = LunaGreetingTailSide.Right;
            left = leftLeft;
        }
        else
        {
            var rightRoom = viewportWidth - rightTip - edge;
            var leftRoom = leftTip - edge;
            if (leftRoom > rightRoom)
            {
                side = LunaGreetingTailSide.Right;
                left = leftLeft;
            }
            else
            {
                side = LunaGreetingTailSide.Left;
                left = rightLeft;
            }

            left = Math.Clamp(left, edge, Math.Max(edge, viewportWidth - controlWidth - edge));
        }

        var top = moonEyeY - (LunaGreetingBubble.OutlineThickness / 2 + bodyHeight / 2);
        top = Math.Clamp(top, edge, Math.Max(edge, viewportHeight - controlHeight - edge));

        return new LunaGreetingPlacement(left, top, side);
    }
}
