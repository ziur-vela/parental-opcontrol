// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Globalization;
using System.Text.RegularExpressions;

namespace OpControl.Parental.Contracts;

public enum LunaPictureKind
{
    Bar,
    Bars,
    Pie,
    Line
}

public sealed record LunaFraction(int Numerator, int Denominator);

public sealed record LunaPicture(
    LunaPictureKind Kind,
    IReadOnlyList<LunaFraction> Terms,
    LunaFraction? Answer,
    string CaptionLatex,
    string? AnswerLatex);

public sealed record LunaChatPart(string? Text, LunaPicture? Picture);

/// <summary>A deterministic drawing plan shared by the Avalonia renderer and its tests.</summary>
public sealed record LunaPictureShape(
    string Kind,
    double X,
    double Y,
    double Width,
    double Height,
    string ColorRole,
    bool Filled,
    double StartAngle = 0,
    double EndAngle = 0);

public sealed record LunaPictureLayout(
    double Width,
    double Height,
    IReadOnlyList<LunaPictureShape> Shapes,
    string CaptionLatex,
    string? AnswerLatex);

public static class LunaPictures
{
    public const int MaxDenominator = 12;
    public const double BarWidth = 300;
    public const double BarHeight = 54;
    public const double BarGap = 10;
    public const double BarCorner = 12;
    public const double PieSize = 120;
    public const double LineWidth = 300;
    public const double LineHeight = 54;

    private static readonly Regex TagPattern = new(@"\[\[([^\[\]\r\n]{1,80})\]\]", RegexOptions.Compiled);
    private static readonly Regex BarPattern = new(@"^bar\s+(\d+)\s*/\s*(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BarsPattern = new(@"^bars\s+(\d+)\s*/\s*(\d+)\s*\+\s*(\d+)\s*/\s*(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PiePattern = new(@"^pie\s+(\d+)\s*/\s*(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LinePattern = new(@"^line\s+(\d+)\s*/\s*(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NumberPairPattern = new(@"(\d+)\s*/\s*(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<LunaChatPart> Split(string? markdown)
    {
        var parts = new List<LunaChatPart>();
        var text = markdown ?? "";
        var position = 0;
        foreach (Match match in TagPattern.Matches(text))
        {
            if (match.Index > position)
                AddText(parts, text[position..match.Index]);
            if (TryParse(match.Groups[1].Value, out var picture))
            {
                parts.Add(new LunaChatPart(null, picture));
            }
            else
            {
                AddText(parts, FallbackText(match.Groups[1].Value));
            }
            position = match.Index + match.Length;
        }
        if (position < text.Length) AddText(parts, text[position..]);
        if (parts.Count == 0) parts.Add(new LunaChatPart("", null));
        return parts;
    }

    public static bool TryParse(string inner, out LunaPicture picture)
    {
        picture = null!;
        inner = (inner ?? "").Trim();
        var bar = BarPattern.Match(inner);
        if (bar.Success)
        {
            var n = Int(bar, 1);
            var d = Int(bar, 2);
            if (!ValidDenominator(d) || n < 0 || n > d * 2) return false;
            var term = new LunaFraction(n, d);
            picture = new LunaPicture(LunaPictureKind.Bar, [term], null,
                FractionLatex(n, d), null);
            return true;
        }

        var bars = BarsPattern.Match(inner);
        if (bars.Success)
        {
            var a = Int(bars, 1);
            var b = Int(bars, 2);
            var c = Int(bars, 3);
            var d = Int(bars, 4);
            if (!ValidDenominator(b) || !ValidDenominator(d) || b != d) return false;
            if (a < 0 || c < 0 || a > b || c > b || a + c > b) return false;
            var answer = new LunaFraction(a + c, b);
            picture = new LunaPicture(LunaPictureKind.Bars,
                [new LunaFraction(a, b), new LunaFraction(c, b)], answer,
                $"{FractionLatex(a, b)} + {FractionLatex(c, b)}", FractionLatex(answer.Numerator, answer.Denominator));
            return true;
        }

        var pie = PiePattern.Match(inner);
        if (pie.Success)
        {
            var n = Int(pie, 1);
            var d = Int(pie, 2);
            if (!ValidDenominator(d) || n < 0 || n > d) return false;
            picture = new LunaPicture(LunaPictureKind.Pie, [new LunaFraction(n, d)], null,
                FractionLatex(n, d), null);
            return true;
        }

        var line = LinePattern.Match(inner);
        if (line.Success)
        {
            var n = Int(line, 1);
            var d = Int(line, 2);
            if (!ValidDenominator(d) || n < 0 || n > d) return false;
            picture = new LunaPicture(LunaPictureKind.Line, [new LunaFraction(n, d)], null,
                FractionLatex(n, d), null);
            return true;
        }

        return false;
    }

    public static string FallbackText(string inner)
    {
        var pairs = NumberPairPattern.Matches(inner ?? "");
        if (pairs.Count >= 1)
        {
            var latex = new List<string>();
            foreach (Match pair in pairs)
            {
                if (!int.TryParse(pair.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ||
                    !int.TryParse(pair.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var d))
                    break;
                latex.Add(FractionLatex(n, d));
            }
            if (latex.Count > 0) return "$" + string.Join(" + ", latex) + "$";
        }
        return "[[" + (inner ?? "") + "]]";
    }

    public static string FractionLatex(int numerator, int denominator) =>
        $"\\frac{{{numerator}}}{{{denominator}}}";

    public static LunaPictureLayout Layout(LunaPicture picture)
    {
        return picture.Kind switch
        {
            LunaPictureKind.Bar => LayoutBar(picture),
            LunaPictureKind.Bars => LayoutBars(picture),
            LunaPictureKind.Pie => LayoutPie(picture),
            LunaPictureKind.Line => LayoutLine(picture),
            _ => throw new ArgumentOutOfRangeException(nameof(picture))
        };
    }

    private static LunaPictureLayout LayoutBar(LunaPicture picture)
    {
        var term = picture.Terms[0];
        var shapes = new List<LunaPictureShape>();
        var bars = term.Numerator > term.Denominator ? 2 : 1;
        for (var i = 0; i < bars; i++)
        {
            var filledNumerator = i == 0 ? Math.Min(term.Numerator, term.Denominator)
                : Math.Max(0, term.Numerator - term.Denominator);
            var y = i * (BarHeight + BarGap);
            shapes.Add(new LunaPictureShape("bar", 0, y, BarWidth, BarHeight, "empty", false));
            if (filledNumerator > 0)
                shapes.Add(new LunaPictureShape("bar", 0, y, BarWidth * filledNumerator / term.Denominator,
                    BarHeight, "term1", true));
            AddBarDividers(shapes, 0, y, BarWidth, BarHeight, term.Denominator);
        }
        return new LunaPictureLayout(BarWidth, bars * BarHeight + (bars - 1) * BarGap,
            shapes, picture.CaptionLatex, picture.AnswerLatex);
    }

    private static LunaPictureLayout LayoutBars(LunaPicture picture)
    {
        var term1 = picture.Terms[0];
        var term2 = picture.Terms[1];
        var shapes = new List<LunaPictureShape>();
        for (var i = 0; i < 2; i++)
        {
            var term = picture.Terms[i];
            var y = i * (BarHeight + BarGap);
            shapes.Add(new LunaPictureShape("bar", 0, y, BarWidth, BarHeight, "empty", false));
            if (term.Numerator > 0)
                shapes.Add(new LunaPictureShape("bar", 0, y, BarWidth * term.Numerator / term.Denominator,
                    BarHeight, i == 0 ? "term1" : "term2", true));
            AddBarDividers(shapes, 0, y, BarWidth, BarHeight, term.Denominator);
        }
        var answerY = 2 * (BarHeight + BarGap);
        shapes.Add(new LunaPictureShape("bar", 0, answerY, BarWidth, BarHeight, "answer-outline", false));
        var x = 0.0;
        var width1 = BarWidth * term1.Numerator / term1.Denominator;
        var width2 = BarWidth * term2.Numerator / term2.Denominator;
        if (width1 > 0)
            shapes.Add(new LunaPictureShape("bar", x, answerY, width1, BarHeight, "term1", true));
        x += width1;
        if (width2 > 0)
            shapes.Add(new LunaPictureShape("bar", x, answerY, width2, BarHeight, "term2", true));
        AddBarDividers(shapes, 0, answerY, BarWidth, BarHeight, term1.Denominator);
        return new LunaPictureLayout(BarWidth, answerY + BarHeight, shapes,
            picture.CaptionLatex, picture.AnswerLatex);
    }

    private static LunaPictureLayout LayoutPie(LunaPicture picture)
    {
        var term = picture.Terms[0];
        var shapes = new List<LunaPictureShape>
        {
            new("pie", 0, 0, PieSize, PieSize, "empty", false)
        };
        var sweep = 360.0 / term.Denominator;
        for (var i = 0; i < term.Denominator; i++)
        {
            var filled = i < term.Numerator;
            shapes.Add(new LunaPictureShape("pie-slice", PieSize / 2, PieSize / 2, PieSize, PieSize,
                filled ? "term1" : "empty", filled, -90 + i * sweep, -90 + (i + 1) * sweep));
        }
        for (var i = 0; i < Math.Min(term.Numerator, 3); i++)
        {
            shapes.Add(new LunaPictureShape("topping", 38 + i * 22, 44 + (i % 2) * 18, 6, 6,
                "topping", true));
        }
        return new LunaPictureLayout(PieSize, PieSize, shapes, picture.CaptionLatex, picture.AnswerLatex);
    }

    private static LunaPictureLayout LayoutLine(LunaPicture picture)
    {
        var term = picture.Terms[0];
        var shapes = new List<LunaPictureShape>
        {
            new("line", 0, LineHeight / 2, LineWidth, 3, "answer-outline", true),
            new("line-mark", LineWidth * term.Numerator / term.Denominator, 10, 4, LineHeight - 20,
                "term1", true)
        };
        return new LunaPictureLayout(LineWidth, LineHeight, shapes, picture.CaptionLatex, picture.AnswerLatex);
    }

    private static void AddBarDividers(List<LunaPictureShape> shapes, double x, double y,
        double width, double height, int denominator)
    {
        for (var piece = 1; piece < denominator; piece++)
        {
            shapes.Add(new LunaPictureShape("bar-divider",
                x + width * piece / denominator, y, 1.5, height, "divider", true));
        }
    }

    private static bool ValidDenominator(int d) => d is >= 1 and <= MaxDenominator;

    private static int Int(Match match, int group) =>
        int.TryParse(match.Groups[group].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value : -1;

    private static void AddText(List<LunaChatPart> parts, string text)
    {
        if (text.Length == 0) return;
        if (parts.Count > 0 && parts[^1].Picture is null)
            parts[^1] = parts[^1] with { Text = (parts[^1].Text ?? "") + text };
        else
            parts.Add(new LunaChatPart(text, null));
    }
}
