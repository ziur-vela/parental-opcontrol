// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Drawing.Drawing2D;

namespace OpControl.Parental.Helper;

internal static class LockGlyphs
{
    /// <summary>Speech bubble with three dots, as in the approved lock screen.</summary>
    public static void DrawMessageIcon(Graphics g, float x, float y, float size, Color color, float stroke)
    {
        using var pen = new Pen(color, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var w = size;
        var h = size * 0.82f;
        using var bubble = new GraphicsPath();
        var d = size * 0.28f;
        bubble.AddArc(x, y, d, d, 180, 90);
        bubble.AddArc(x + w - d, y, d, d, 270, 90);
        bubble.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        bubble.AddLine(x + w * 0.42f, y + h, x + w * 0.22f, y + h + size * 0.2f);
        bubble.AddLine(x + w * 0.22f, y + h + size * 0.2f, x + w * 0.22f, y + h);
        bubble.AddArc(x, y + h - d, d, d, 90, 90);
        bubble.CloseFigure();
        g.DrawPath(pen, bubble);
        using var dot = new SolidBrush(color);
        var dotR = Math.Max(1.2f, size * 0.055f);
        for (var i = 0; i < 3; i++)
            g.FillEllipse(dot, x + w * (0.28f + i * 0.22f), y + h * 0.42f, dotR * 2, dotR * 2);
    }
}
