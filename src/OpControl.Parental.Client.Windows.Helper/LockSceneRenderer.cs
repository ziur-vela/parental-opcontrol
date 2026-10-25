// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Drawing.Drawing2D;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Helper;

internal static class LockSceneRenderer
{
    private static readonly Color WallTop = Color.FromArgb(0x17, 0x39, 0x3d);
    private static readonly Color WallMid = Color.FromArgb(0x12, 0x30, 0x2f);
    private static readonly Color WallBottom = Color.FromArgb(0x0d, 0x23, 0x26);
    private static readonly Color Floor = Color.FromArgb(0x0b, 0x1d, 0x20);

    public static Bitmap Render(int width, int height)
    {
        var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var layout = LockSceneLayout.For(width, height);

        using (var wall = new LinearGradientBrush(new Rectangle(0, 0, width, height), WallTop, WallBottom, 90f))
        {
            var blend = new ColorBlend
            {
                Colors = [WallTop, WallMid, WallBottom],
                Positions = [0f, 0.62f, 1f]
            };
            wall.InterpolationColors = blend;
            g.FillRectangle(wall, 0, 0, width, height);
        }
        g.FillRectangle(new SolidBrush(Floor), 0, (float)(height - layout.FloorHeight), width, (float)layout.FloorHeight);

        // A warm pool of lamp light, then the two anchored groups.
        using (var glow = new GraphicsPath())
        {
            glow.AddEllipse(40 * (float)layout.Scale, 340 * (float)layout.Scale,
                330 * (float)layout.Scale, 330 * (float)layout.Scale);
            using var brush = new PathGradientBrush(glow)
            {
                CenterColor = Color.FromArgb(70, 255, 211, 138),
                SurroundColors = [Color.FromArgb(0, 242, 178, 90)]
            };
            g.FillPath(brush, glow);
        }

        DrawLeftGroup(g, layout);
        DrawRightGroup(g, layout);
        DrawCornerShade(g, layout);
        return bitmap;
    }

    private static void DrawLeftGroup(Graphics g, LockSceneLayout layout)
    {
        var state = g.Save();
        g.TranslateTransform((float)layout.LeftGroup.X, (float)layout.LeftGroup.Y);
        g.ScaleTransform((float)layout.Scale, (float)layout.Scale);
        try
        {
            // Bookshelf and books.
            Rect(g, 0, 210, 300, 14, "#5b3f2c", 4);
            Rect(g, 18, 128, 26, 82, "#2f6f8f", 3);
            Rect(g, 46, 140, 22, 70, "#c98a3a", 3);
            Rect(g, 70, 118, 28, 92, "#3e8a6b", 3);
            Rect(g, 100, 146, 20, 64, "#a9543f", 3);
            Rect(g, 124, 134, 24, 76, "#5a6fb0", 3);
            Rect(g, 168, 168, 70, 20, "#c98a3a", 3);
            Rect(g, 164, 188, 78, 22, "#2f6f8f", 3);
            Path(g, "M262 210 q-6 -40 8 -60 q10 20 2 60z", "#3e8a6b");
            Path(g, "M272 210 q8 -34 26 -44 q-2 26 -16 44z", "#4f9d78");
            Rect(g, 256, 196, 36, 16, "#a9543f", 4);

            // Desk, legs, books and lamp.
            Rect(g, 0, 600, 400, 20, "#5b3f2c", 5);
            Rect(g, 20, 620, 16, 250, "#4a3324", 0);
            Rect(g, 360, 620, 16, 250, "#4a3324", 0);
            Rect(g, 40, 560, 96, 16, "#a9543f", 3);
            Rect(g, 34, 576, 108, 24, "#2f6f8f", 3);
            Rect(g, 168, 548, 38, 52, "#3e6c7a", 6);
            Line(g, "#e7c36a", 5, 176, 548, 170, 514);
            Line(g, "#e7c36a", 5, 188, 548, 190, 508);
            Line(g, "#e7c36a", 5, 198, 548, 208, 518);

            // Chair.
            Rect(g, 262, 588, 70, 12, "#2a3a3e", 6);
            Path(g, "M296 588 L262 470 L312 410", "#2a3a3e", 10);
            Path(g, "M292 392 l66 -22 l18 64 l-70 6z", "#34494d");
            using (var lamp = new SolidBrush(Color.FromArgb(255, 226, 160)))
                g.FillEllipse(lamp, 316, 426, 52, 24);
            using (var beam = new SolidBrush(Color.FromArgb(40, 255, 211, 138)))
                g.FillPolygon(beam, new[]
                {
                    new PointF(318, 444), new PointF(250, 600),
                    new PointF(440, 600), new PointF(366, 438)
                });

            // Bed foot at the left edge (the ref crops it this way).
            Rect(g, 96, 640, 190, 230, "#203d55", 28);
            Rect(g, 112, 656, 158, 120, "#284a66", 20);
        }
        finally { g.Restore(state); }
    }

    private static void DrawRightGroup(Graphics g, LockSceneLayout layout)
    {
        var state = g.Save();
        g.TranslateTransform((float)layout.RightGroup.X, (float)layout.RightGroup.Y);
        g.ScaleTransform((float)layout.Scale, (float)layout.Scale);
        try
        {
            // Window night sky and frame.
            using (var sky = new LinearGradientBrush(new Rectangle(330, 110, 320, 360),
                       Color.FromArgb(0x14, 0x20, 0x4a), Color.FromArgb(0x28, 0x40, 0x7a), 90f))
                FillRounded(g, sky, 330, 110, 320, 360, 14);
            using (var star = new SolidBrush(Color.FromArgb(255, 246, 200)))
            {
                g.FillEllipse(star, 368, 156, 6, 6);
                g.FillEllipse(star, 606, 146, 5, 5);
                g.FillEllipse(star, 586, 246, 6, 6);
                g.FillEllipse(star, 396, 296, 4, 4);
                g.FillEllipse(star, 616, 386, 5, 5);
                g.FillEllipse(star, 466, 146, 4, 4);
            }
            // Sleeping moon.
            using (var moon = new SolidBrush(Color.FromArgb(246, 220, 126)))
                g.FillEllipse(moon, 430, 242, 116, 116);
            using (var crater = new SolidBrush(Color.FromArgb(0xec, 0xc9, 0x64)))
            {
                g.FillEllipse(crater, 455, 267, 18, 18);
                g.FillEllipse(crater, 506, 320, 20, 20);
            }
            using (var ink = new Pen(Color.FromArgb(0x2b, 0x2f, 0x3a), 4.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(ink, 452, 292, 20, 14, 20, 140);
                g.DrawArc(ink, 492, 292, 20, 14, 20, 140);
                g.DrawArc(ink, 476, 318, 16, 10, 20, 140);
            }
            using (var cheek = new SolidBrush(Color.FromArgb(180, 240, 169, 160)))
            {
                g.FillEllipse(cheek, 444, 314, 16, 10);
                g.FillEllipse(cheek, 516, 314, 16, 10);
            }
            // Window mullions and frame.
            using (var frame = new Pen(Color.FromArgb(0xe8, 0xdc, 0xc6), 12f))
            {
                g.DrawRectangle(frame, 330, 110, 320, 360);
                g.DrawLine(frame, 490, 110, 490, 470);
                g.DrawLine(frame, 330, 290, 650, 290);
            }
            Rect(g, 318, 466, 344, 18, "#d8c9ae", 4);
            // Curtain.
            Path(g, "M300 96 q40 120 6 380 l-28 0 q-6 -200 2 -380z", "#2c5a73");
            // Plant on the sill.
            Rect(g, 590, 430, 40, 36, "#a9543f", 6);
            Path(g, "M610 432 q-24 -40 -6 -66 q10 30 6 66z", "#4f9d78");
            Path(g, "M610 432 q10 -44 34 -54 q-6 34 -34 54z", "#4f9d78");
            // Bed.
            Rect(g, 580, 540, 120, 360, "#4a3324", 10);
            Rect(g, 270, 690, 450, 210, "#d9ccb4", 24);
            using (var pillow = new SolidBrush(Color.FromArgb(0xef, 0xe6, 0xd4)))
                g.FillEllipse(pillow, 520, 630, 200, 76);
            // Plaid blanket.
            using (var blanket = new GraphicsPath())
            {
                blanket.AddBezier(260, 748, 360, 712, 530, 720, 720, 740);
                blanket.AddLine(720, 900, 260, 900);
                blanket.CloseFigure();
                using var baseBrush = new SolidBrush(Color.FromArgb(0x1f, 0x5b, 0x52));
                g.FillPath(baseBrush, blanket);
                var stateClip = g.Save();
                g.SetClip(blanket);
                using (var stripe = new SolidBrush(Color.FromArgb(190, 0x2a, 0x70, 0x64)))
                {
                    for (var x = 240; x < 760; x += 56) g.FillRectangle(stripe, x, 700, 12, 220);
                    for (var y = 700; y < 920; y += 56) g.FillRectangle(stripe, 240, y, 520, 12);
                }
                using (var dark = new Pen(Color.FromArgb(0x12, 0x3b, 0x36), 4))
                {
                    for (var x = 260; x < 760; x += 56) g.DrawLine(dark, x, 700, x, 920);
                    for (var y = 700; y < 920; y += 56) g.DrawLine(dark, 240, y, 760, y);
                }
                g.Restore(stateClip);
            }
            using (var seam = new Pen(Color.FromArgb(0x12, 0x3b, 0x36), 6))
                g.DrawBezier(seam, 260, 748, 360, 712, 530, 720, 720, 740);
        }
        finally { g.Restore(state); }
    }

    private static void DrawCornerShade(Graphics g, LockSceneLayout layout)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(-260, (float)(layout.Height - 300), 820, 420);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(150, 8, 22, 24),
            SurroundColors = [Color.FromArgb(0, 8, 22, 24)]
        };
        g.FillPath(brush, path);
    }

    public static void DrawSleepingZ(Graphics g, LockSceneLayout layout, double time)
    {
        var scale = (float)layout.Scale;
        using var yellow = new SolidBrush(Color.FromArgb(255, 0xd8, 0x4d));
        using var ink = new SolidBrush(Color.FromArgb(26, 26, 26));
        var bx = (float)(layout.RightGroup.X + 540 * layout.Scale);
        var by = (float)(layout.RightGroup.Y + 240 * layout.Scale);
        var sizes = new[] { 18f, 22f, 26f };
        for (var i = 0; i < 3; i++)
        {
            var phase = (time / 3.6 + i / 3.0) % 1.0;
            var x = bx + (float)(26 * phase) * scale;
            var y = by - (float)(30 * phase) * scale;
            var alpha = (int)(255 * Math.Clamp(phase < 0.15 ? phase / 0.15 : phase > 0.75 ? (1 - phase) / 0.25 : 1, 0, 1));
            if (alpha <= 0) continue;
            using var font = Theme.Display(sizes[i] * scale, FontStyle.Bold);
            using var yb = new SolidBrush(Color.FromArgb(alpha, 255, 216, 77));
            using var ib = new SolidBrush(Color.FromArgb(alpha, 26, 26, 26));
            g.DrawString("Z", font, yb, x + 1.5f * scale, y + 1.5f * scale);
            g.DrawString("Z", font, ib, x, y);
        }
    }

    private static void Rect(Graphics g, float x, float y, float w, float h, string color, float radius)
    {
        using var brush = new SolidBrush(ColorTranslator.FromHtml(color));
        if (radius > 0) FillRounded(g, brush, x, y, w, h, radius);
        else g.FillRectangle(brush, x, y, w, h);
    }

    private static void FillRounded(Graphics g, Brush brush, float x, float y, float w, float h, float radius)
    {
        using var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    private static void Path(Graphics g, string path, string color, float width = 0)
    {
        using var brush = new SolidBrush(ColorTranslator.FromHtml(color));
        using var pen = width > 0 ? new Pen(brush, width) { StartCap = LineCap.Round, EndCap = LineCap.Round } : null;
        var points = ParsePath(path);
        if (points.Length < 2) return;
        if (width > 0) g.DrawLines(pen!, points);
        else g.FillPolygon(brush, points);
    }

    private static void Line(Graphics g, string color, float width, float x1, float y1, float x2, float y2)
    {
        using var pen = new Pen(ColorTranslator.FromHtml(color), width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, x1, y1, x2, y2);
    }

    private static PointF[] ParsePath(string path)
    {
        // The few paths used here are simple M/L/q pairs. Parse the line segments
        // and approximate the quadratic curves with a short polyline.
        var tokens = path.Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var points = new List<PointF>();
        float x = 0, y = 0, startX = 0, startY = 0;
        var i = 0;
        while (i < tokens.Length)
        {
            var command = tokens[i++];
            if (command is "M" or "L")
            {
                x = float.Parse(tokens[i++], System.Globalization.CultureInfo.InvariantCulture);
                y = float.Parse(tokens[i++], System.Globalization.CultureInfo.InvariantCulture);
                if (command == "M") { startX = x; startY = y; }
                points.Add(new PointF(x, y));
            }
            else if (command == "q")
            {
                var cx = float.Parse(tokens[i++], System.Globalization.CultureInfo.InvariantCulture);
                var cy = float.Parse(tokens[i++], System.Globalization.CultureInfo.InvariantCulture);
                var ex = float.Parse(tokens[i++], System.Globalization.CultureInfo.InvariantCulture);
                var ey = float.Parse(tokens[i++], System.Globalization.CultureInfo.InvariantCulture);
                for (var t = 0.2f; t <= 1.001f; t += 0.2f)
                {
                    var mt = 1 - t;
                    points.Add(new PointF(mt * mt * x + 2 * mt * t * cx + t * t * ex,
                        mt * mt * y + 2 * mt * t * cy + t * t * ey));
                }
                x = ex; y = ey;
            }
            else if (command == "z")
            {
                points.Add(new PointF(startX, startY));
            }
        }
        return points.ToArray();
    }
}
