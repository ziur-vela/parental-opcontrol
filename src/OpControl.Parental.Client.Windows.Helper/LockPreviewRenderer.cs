// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

#if DEBUG
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

/// <summary>
/// Draws the lock screen at an arbitrary size for 16:9 / 3:2 screenshots on a
/// small build-host screen. The real overlay uses the same geometry and renderer.
/// </summary>
internal static class LockPreviewRenderer
{
    private static readonly Image? Logo = LoadLogo();

    public static void Render(string path, int width, int height, RuntimeLockStateDto state)
    {
        var layout = LockSceneLayout.For(width, height);
        using var bitmap = LockSceneRenderer.Render(width, height);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        LockSceneRenderer.DrawStars(g, layout, 1.1);
        LockSceneRenderer.DrawSleepingZ(g, layout, 0.9);

        var (title, reason, hint) = LockForm.Texts(state);
        var scale = (float)layout.Scale;
        var ui = (float)layout.UiScale;
        using (var titleFont = Theme.DisplayPx(56 * ui, FontStyle.Bold))
        using (var reasonFont = Theme.DisplayPx(32 * ui, FontStyle.Regular))
        using (var hintFont = Theme.BodyPx(22 * ui, FontStyle.Bold))
        using (var white = new SolidBrush(Color.White))
        using (var mint = new SolidBrush(Color.FromArgb(0x7e, 0xe8, 0xc4)))
        using (var pale = new SolidBrush(Color.FromArgb(0xc8, 0xea, 0xdf)))
        using (var titleFormat = CenterFormat())
        using (var reasonFormat = CenterFormat())
        using (var hintFormat = CenterFormat())
        {
            var x = (float)layout.TextColumn.X;
            var w = (float)layout.TextColumn.Width;
            var y = (float)layout.TextColumn.Y;
            var titleSize = g.MeasureString(title, titleFont, (int)w);
            var reasonSize = g.MeasureString(reason, reasonFont, (int)w);
            var hintSize = g.MeasureString(hint, hintFont, (int)w);
            using (var shadow = new SolidBrush(Color.FromArgb(95, 0, 0, 0)))
                g.DrawString(title, titleFont, shadow,
                    new RectangleF(x, y + 3 * ui, w, titleSize.Height + 2), titleFormat);
            g.DrawString(title, titleFont, white, new RectangleF(x, y, w, titleSize.Height + 2), titleFormat);
            y += titleSize.Height + 18 * ui;
            g.DrawString(reason, reasonFont, mint, new RectangleF(x, y, w, reasonSize.Height + 2), reasonFormat);
            y += reasonSize.Height + 10 * ui;
            g.DrawString(hint, hintFont, pale, new RectangleF(x, y, w, hintSize.Height + 2), hintFormat);
        }

        DrawButton(g, layout.PrimaryButton, Loc.T("Client.Messages.Open"), primary: true, icon: true, ui);
        DrawButton(g, layout.SecondaryButton, Loc.T("Client.Lock.Override"), primary: false, icon: false, ui);
        if (Logo is not null)
        {
            var box = layout.Logo;
            g.DrawImage(Logo, new Rectangle((int)Math.Round(box.X), (int)Math.Round(box.Y),
                Math.Max(1, (int)Math.Round(box.Width)), Math.Max(1, (int)Math.Round(box.Height))));
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void DrawButton(Graphics g, LockBox box, string text, bool primary, bool icon, float ui)
    {
        var rect = new Rectangle((int)Math.Round(box.X), (int)Math.Round(box.Y),
            (int)Math.Round(box.Width), (int)Math.Round(box.Height));
        var lift = Math.Max(3, (int)(6 * ui));
        using var path = Rounded(new Rectangle(rect.X, rect.Y, rect.Width, rect.Height - lift), (rect.Height - lift) / 2);
        if (primary)
        {
            using var shadow = new SolidBrush(Color.FromArgb(0x0f, 0x7a, 0x37));
            using var shadowPath = Rounded(new Rectangle(rect.X, rect.Y + lift, rect.Width, rect.Height - lift), (rect.Height - lift) / 2);
            g.FillPath(shadow, shadowPath);
            using var fill = new SolidBrush(Color.FromArgb(0x16, 0xa3, 0x4a));
            g.FillPath(fill, path);
        }
        else
        {
            using var fill = new SolidBrush(Color.FromArgb(90, 11, 29, 32));
            g.FillPath(fill, path);
            using var border = new Pen(Color.FromArgb(0x4f, 0xd1, 0xa5), Math.Max(1.5f, 2 * ui));
            g.DrawPath(border, path);
        }

        using var font = primary ? Theme.DisplayPx(26 * ui, FontStyle.Bold) : Theme.BodyPx(18 * ui, FontStyle.Bold);
        var color = primary ? Color.White : Color.FromArgb(0xd8, 0xf5, 0xea);
        using var brush = new SolidBrush(color);
        var iconSize = icon ? (int)(32 * ui) : 0;
        var iconSpace = icon ? iconSize + (int)(14 * ui) : 0;
        var textSize = g.MeasureString(text, font);
        if (icon)
        {
            var ix = rect.X + (rect.Width - (int)textSize.Width - iconSpace) / 2;
            var iy = rect.Y + (rect.Height - lift - iconSize) / 2;
            DrawMessageIcon(g, ix, iy, iconSize, color, Math.Max(1.8f, 2.2f * ui));
        }
        using var format = CenterFormat();
        var textRect = new RectangleF(rect.X + iconSpace, rect.Y, rect.Width - iconSpace, rect.Height - lift);
        g.DrawString(text, font, brush, textRect, format);
    }

    internal static void DrawMessageIcon(Graphics g, float x, float y, float size, Color color, float stroke) =>
        LockGlyphs.DrawMessageIcon(g, x, y, size, color, stroke);

    private static StringFormat CenterFormat() => new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
        FormatFlags = StringFormatFlags.LineLimit
    };

    private static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(1, radius * 2);
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Image? LoadLogo()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpControl.Parental.Helper.brand.lockup-ondark-300h.png");
            if (stream is null) return null;
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch { return null; }
    }
}
#endif
