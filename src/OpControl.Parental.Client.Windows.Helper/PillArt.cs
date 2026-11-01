// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Drawing.Drawing2D;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Helper;

/// <summary>Layout and painting for the Luna-style lock countdown pill.</summary>
internal static class PillArt
{
    public const float Height = 46f;
    public const float Shadow = 4f;
    private const float FaceSize = 30f;
    private const float PadLeft = 7f;
    private const float Gap = 8f;
    private const float PadRight = 46f;
    private const float Expose = 74f;

    private static readonly Font TimeFont = Theme.PillTime(18);
    private static readonly Font LabelFont = Theme.PillLabel(13);

    private static readonly Color Ink = C("#2b2f3a");
    private static readonly Color Moon = C("#f6dc7e");
    private static readonly Color MoonLine = C("#e2bd52");
    private static readonly Color Crater = C("#ecc964");
    private static readonly Color Red = C("#e8553f");

    public readonly record struct Layout(
        float Width,
        float FaceX,
        float FaceY,
        float LabelX,
        float TimeX,
        float LabelWidth,
        float TimeWidth);

    public readonly record struct Visual(
        string Theme,
        LockTimerPhase Phase,
        string Label,
        string Time,
        float Slide,
        bool Reduced,
        double NowSeconds,
        DateTime Clock);

    public readonly record struct Palette(Color Bg, Color Border, Color Text, Color Label, Color Shadow, bool Stars);

    private static Color C(string hex) => ColorTranslator.FromHtml(hex);

    public static Palette Colors(string theme, LockTimerPhase phase)
    {
        if (string.Equals(theme, "gray", StringComparison.OrdinalIgnoreCase))
        {
            return phase switch
            {
                LockTimerPhase.Warn => new(C("#ffffff"), C("#c98a00"), C("#8a5600"), C("#565c73"), C("#c98a00"), false),
                LockTimerPhase.Last => new(C("#ffffff"), C("#b3261e"), C("#b3261e"), C("#565c73"), C("#b3261e"), false),
                _ => new(C("#ffffff"), C("#d6dae3"), C("#23273a"), C("#565c73"), C("#e2bd52"), false)
            };
        }

        return phase switch
        {
            LockTimerPhase.Warn => new(C("#1b1442"), C("#ffb547"), C("#ffb547"), C("#c9c0f5"), C("#0e0a26"), true),
            LockTimerPhase.Last => new(C("#1b1442"), C("#ff8f80"), C("#ff8f80"), C("#c9c0f5"), C("#0e0a26"), true),
            _ => new(C("#1b1442"), C("#4a3d94"), C("#fff6c8"), C("#c9c0f5"), C("#0e0a26"), true)
        };
    }

    public static Layout Measure(Graphics graphics, string label, string time)
    {
        var labelWidth = MeasureText(graphics, label, LabelFont);
        var timeWidth = MeasureText(graphics, time, TimeFont);
        var labelX = PadLeft + FaceSize + Gap;
        var timeX = labelX + labelWidth + Gap;
        var width = timeX + timeWidth + PadRight;
        return new Layout(width, PadLeft, (Height - FaceSize) / 2f, labelX, timeX, labelWidth, timeWidth);
    }

    private static float MeasureText(Graphics graphics, string text, Font font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var size = graphics.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic);
        return size.Width + 1;
    }

    /// <summary>Left cap round, right end square (it runs past the screen edge).</summary>
    public static GraphicsPath Path(float left, float top, float right, float bottom, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(right - left, bottom - top));
        var r = diameter / 2f;
        if (diameter <= 1)
        {
            path.AddRectangle(new RectangleF(left, top, right - left, bottom - top));
            return path;
        }

        path.AddArc(left, top, diameter, diameter, 90, 180);
        path.AddLine(left + r, top, right, top);
        path.AddLine(right, bottom, left + r, bottom);
        path.CloseFigure();
        return path;
    }

    public static void Draw(Graphics g, Layout layout, Visual visual)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var palette = Colors(visual.Theme, visual.Phase);
        var offset = SlideOffset(layout, visual.Slide);
        var height = Height;
        var right = layout.Width + 4;
        var radius = height / 2f;

        using (var shadowPath = Path(offset, Shadow, right, height + Shadow, radius))
        using (var shadow = new SolidBrush(palette.Shadow))
            g.FillPath(shadow, shadowPath);

        using (var pillPath = Path(offset, 0, right, height, radius))
        {
            using (var fill = new SolidBrush(palette.Bg))
                g.FillPath(fill, pillPath);
            if (palette.Stars)
                DrawStars(g, pillPath, offset, layout.Width, visual.NowSeconds);
            using (var pen = new Pen(palette.Border, 2f))
                g.DrawPath(pen, pillPath);
        }

        DrawFace(g, layout.FaceX + offset, layout.FaceY, FaceSize, visual);

        var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        using var labelBrush = new SolidBrush(palette.Label);
        using var timeBrush = new SolidBrush(palette.Text);
        g.DrawString(visual.Label, LabelFont, labelBrush,
            new RectangleF(layout.LabelX + offset, 0, layout.LabelWidth + 24, height), format);
        g.DrawString(visual.Time, TimeFont, timeBrush,
            new RectangleF(layout.TimeX + offset, 0, layout.TimeWidth + 24, height), format);
    }

    public static float SlideOffset(Layout layout, float slide)
    {
        var travel = Math.Max(0, layout.Width - Expose);
        return travel * Math.Clamp(slide, 0f, 1f);
    }

    private static void DrawStars(Graphics g, GraphicsPath clip, float offset, float width, double now)
    {
        var state = g.Save();
        g.SetClip(clip, CombineMode.Intersect);
        var twinkle = Math.Sin(now * 1.7);
        for (var i = 0; i < 26; i++)
        {
            var x = offset + 10 + (i * 47 % Math.Max(60, (int)width));
            var y = i % 3 switch { 0 => 9f, 1 => 21f, _ => 33f };
            var size = 1.15f + (float)((Math.Sin(i * 2.1) + 1) * 0.25);
            var color = (i % 5) switch
            {
                0 => Color.FromArgb(235, 255, 255, 255),
                1 => Color.FromArgb(210, 217, 210, 255),
                2 => Color.FromArgb(200, 255, 246, 200),
                _ => Color.FromArgb(170, 255, 255, 255)
            };
            if (i % 4 == 0) color = Color.FromArgb((int)(170 + 60 * ((twinkle + 1) / 2)), color.R, color.G, color.B);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, x, y, size, size);
        }
        g.Restore(state);
    }

    private static void DrawFace(Graphics g, float x, float y, float size, Visual visual)
    {
        var state = g.Save();
        g.TranslateTransform(x, y);
        g.ScaleTransform(size / 172f, size / 172f);

        var slide = visual.Slide;
        var clockMix = visual.Reduced ? (slide > 0.5f ? 1f : 0f) : Math.Clamp((slide - 0.35f) / 0.65f, 0f, 1f);
        var moonMix = 1f - clockMix;

        if (moonMix > 0f)
        {
            var scale = 1f - 0.4f * clockMix;
            var s2 = g.Save();
            g.TranslateTransform(86, 86);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-86, -86);
            switch (visual.Phase)
            {
                case LockTimerPhase.Warn:
                    DrawGrimace(g, visual);
                    break;
                case LockTimerPhase.Last:
                    DrawFear(g, visual);
                    break;
                default:
                    DrawCalm(g, visual);
                    break;
            }
            g.Restore(s2);
        }

        if (clockMix > 0f)
        {
            var s3 = g.Save();
            g.TranslateTransform(86, 86);
            g.RotateTransform(-90f * (1f - clockMix));
            var scale = 0.6f + 0.4f * clockMix;
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-86, -86);
            DrawClock(g, visual);
            g.Restore(s3);
        }

        g.Restore(state);
    }

    private static void MoonBase(Graphics g, Color? fill = null)
    {
        using var body = new SolidBrush(fill ?? Moon);
        using var pen = new Pen(MoonLine, 4);
        g.FillEllipse(body, 3, 3, 166, 166);
        g.DrawEllipse(pen, 3, 3, 166, 166);
    }

    private static void Eye(Graphics g, float cx, float cy, float rx, float ry, Color? fill = null)
    {
        using var brush = new SolidBrush(fill ?? Ink);
        g.FillEllipse(brush, cx - rx, cy - ry, rx * 2, ry * 2);
    }

    private static void DrawCalm(Graphics g, Visual visual)
    {
        MoonBase(g);
        using (var crater = new SolidBrush(Crater))
            g.FillEllipse(crater, 41, 37, 22, 22);

        var blink = visual.Reduced ? 1f : BlinkScale(visual.NowSeconds);
        Eye(g, 64, 78, 9, 12 * blink);
        Eye(g, 108, 78, 9, 12 * blink);

        using var pen = new Pen(Ink, 9) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var path = new GraphicsPath();
        path.AddBezier(60, 106, 74, 124, 98, 124, 112, 106);
        g.DrawPath(pen, path);
    }

    private static void DrawGrimace(Graphics g, Visual visual)
    {
        var tilt = visual.Reduced ? 0f : Keyframe(visual.NowSeconds, 3.2, [0, 0, 4, 4, 0, 0]);
        var look = visual.Reduced ? 0f : Keyframe(visual.NowSeconds, 3.2, [0, 0, 16, 16, 0, 0]);
        var s = g.Save();
        g.TranslateTransform(86, 86);
        g.RotateTransform(tilt);
        g.TranslateTransform(-86, -86);

        MoonBase(g);
        using (var crater = new SolidBrush(Crater))
            g.FillEllipse(crater, 41, 37, 22, 22);

        var shift = look * 172f / 30f;
        var s2 = g.Save();
        g.TranslateTransform(shift, 0);
        Eye(g, 62, 70, 10, 14);
        Eye(g, 110, 70, 10, 14);

        using (var fill = new SolidBrush(Color.White))
        using (var pen = new Pen(Ink, 7))
        {
            var rect = new RectangleF(42, 100, 88, 36);
            using var path = Rounded(rect, 16);
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }
        using (var pen = new Pen(Ink, 5))
        {
            g.DrawLine(pen, 45, 118, 127, 118);
            g.DrawLine(pen, 64, 103, 64, 133);
            g.DrawLine(pen, 86, 103, 86, 133);
            g.DrawLine(pen, 108, 103, 108, 133);
        }
        g.Restore(s2);
        g.Restore(s);
    }

    private static void DrawFear(Graphics g, Visual visual)
    {
        var shake = visual.Reduced ? (X: 0f, Y: 0f, Rotation: 0f) : ShakeState(visual.NowSeconds);
        var s = g.Save();
        g.TranslateTransform(86 + shake.X, 86 + shake.Y);
        g.RotateTransform(shake.Rotation);
        g.TranslateTransform(-86, -86);

        var rect = new RectangleF(3, 3, 166, 166);
        using (var gradient = new LinearGradientBrush(rect, C("#5b9be8"), Moon, 90f))
        {
            gradient.InterpolationColors = new ColorBlend
            {
                Colors = [C("#5b9be8"), C("#a9cdf2"), Moon, Moon],
                Positions = [0f, 0.48f, 0.62f, 1f]
            };
            g.FillEllipse(gradient, rect);
        }
        using (var pen = new Pen(MoonLine, 4))
            g.DrawEllipse(pen, 3, 3, 166, 166);

        using (var pen = new Pen(Ink, 8) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawBezier(pen, 40, 52, 48, 51, 62, 45, 72, 40);
            g.DrawBezier(pen, 132, 52, 124, 51, 110, 45, 100, 40);
        }

        using (var white = new SolidBrush(Color.White))
        using (var pen = new Pen(Ink, 5))
        {
            g.FillEllipse(white, 43, 60, 34, 40);
            g.DrawEllipse(pen, 43, 60, 34, 40);
            g.FillEllipse(white, 95, 60, 34, 40);
            g.DrawEllipse(pen, 95, 60, 34, 40);
        }
        Eye(g, 60, 82, 8, 8);
        Eye(g, 112, 82, 8, 8);

        using (var mouth = new SolidBrush(Ink))
            g.FillEllipse(mouth, 68, 112, 36, 40);
        using (var tongue = new SolidBrush(C("#e8706a")))
            g.FillEllipse(tongue, 75, 132, 22, 16);

        DrawSweat(g, visual.Reduced ? 0.0 : visual.NowSeconds);
        g.Restore(s);
    }

    private static void DrawSweat(Graphics g, double now)
    {
        var p = now % 1.6 / 1.6;
        var drop = p <= 0.8 ? 0f : (float)((p - 0.8) / 0.2) * 16f;
        var alpha = p <= 0.8 ? 1f : 1f - (float)((p - 0.8) / 0.2);
        using var path = new GraphicsPath();
        path.AddBezier(152, 36 + drop, 162, 54 + drop, 162, 54 + drop, 152, 60 + drop);
        path.AddBezier(152, 60 + drop, 142, 54 + drop, 142, 54 + drop, 152, 36 + drop);
        path.CloseFigure();
        using var fill = new SolidBrush(Color.FromArgb((int)(255 * alpha), 124, 198, 255));
        using var pen = new Pen(Color.FromArgb((int)(255 * alpha), 43, 110, 168), 3);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
    }

    private static void DrawClock(Graphics g, Visual visual)
    {
        using (var body = new SolidBrush(Moon))
            g.FillEllipse(body, 4, 4, 164, 164);
        using (var pen = new Pen(Ink, 9))
            g.DrawEllipse(pen, 4, 4, 164, 164);

        using (var pen = new Pen(Ink, 9) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(pen, 86, 18, 86, 32);
            g.DrawLine(pen, 86, 140, 86, 154);
            g.DrawLine(pen, 18, 86, 32, 86);
            g.DrawLine(pen, 140, 86, 154, 86);
        }

        var clock = visual.Clock;
        var hourAngle = (clock.Hour % 12 + clock.Minute / 60f) * 30f;
        var minuteAngle = clock.Minute * 6f;
        var secondAngle = clock.Second * 6f;

        using var hand = new Pen(Ink, 11) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(hand, new PointF(86, 86), PointOnCircle(86, 86, 40, hourAngle));
        g.DrawLine(hand, new PointF(86, 86), PointOnCircle(86, 86, 56, minuteAngle));

        if (!visual.Reduced)
        {
            using var second = new Pen(Red, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(second, PointOnCircle(86, 86, -10, secondAngle), PointOnCircle(86, 86, 56, secondAngle));
        }

        using var center = new SolidBrush(Ink);
        g.FillEllipse(center, 78, 78, 16, 16);
    }

    private static PointF PointOnCircle(float cx, float cy, float radius, float degrees)
    {
        var radians = (degrees - 90) * Math.PI / 180.0;
        return new PointF(cx + radius * (float)Math.Cos(radians), cy + radius * (float)Math.Sin(radians));
    }

    private static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static float BlinkScale(double now)
    {
        var p = now % 4.2 / 4.2;
        if (p < 0.92) return 1f;
        if (p < 0.95) return Lerp(1f, 0.1f, (float)((p - 0.92) / 0.03));
        return Lerp(0.1f, 1f, (float)((p - 0.95) / 0.05));
    }

    /// <summary>Linear keyframes at 0, 30, 42, 72, 85 and 100 percent.</summary>
    private static float Keyframe(double now, double period, float[] values)
    {
        var p = now % period / period;
        double[] stops = [0, 0.30, 0.42, 0.72, 0.85, 1.0];
        for (var i = 0; i < stops.Length - 1; i++)
        {
            if (p > stops[i + 1]) continue;
            var t = (float)((p - stops[i]) / (stops[i + 1] - stops[i]));
            return Lerp(values[i], values[i + 1], Math.Clamp(t, 0f, 1f));
        }
        return values[^1];
    }

    private static (float X, float Y, float Rotation) ShakeState(double now)
    {
        var p = now % 0.5 / 0.5;
        double[] stops = [0, 0.12, 0.25, 0.38, 0.50, 0.62, 0.75, 0.88, 1.0];
        (float X, float Y, float Rotation)[] values =
        [
            (0, 0, 0), (-1.5f, 0, -12), (1.5f, -0.5f, 11), (-1f, 0.5f, -10),
            (1f, 0, 9), (-1f, 0, -7), (1f, 0, 6), (0, 0, -3), (0, 0, 0)
        ];
        for (var i = 0; i < stops.Length - 1; i++)
        {
            if (p > stops[i + 1]) continue;
            var t = Math.Clamp((float)((p - stops[i]) / (stops[i + 1] - stops[i])), 0f, 1f);
            var a = values[i];
            var b = values[i + 1];
            return (Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t), Lerp(a.Rotation, b.Rotation, t));
        }
        return (0, 0, 0);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
