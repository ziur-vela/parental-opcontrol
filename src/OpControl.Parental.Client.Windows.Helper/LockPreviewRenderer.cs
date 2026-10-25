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
        LockSceneRenderer.DrawSleepingZ(g, layout, 0.9);

        var (title, reason, hint) = LockForm.Texts(state);
        var scale = (float)layout.Scale;
        using (var titleFont = Theme.DisplayPx(56, FontStyle.Bold))
        using (var reasonFont = Theme.DisplayPx(32, FontStyle.Regular))
        using (var hintFont = Theme.BodyPx(22, FontStyle.Bold))
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
            g.DrawString(title, titleFont, white, new RectangleF(x, y, w, titleSize.Height + 2), titleFormat);
            y += titleSize.Height + 14 * scale;
            g.DrawString(reason, reasonFont, mint, new RectangleF(x, y, w, reasonSize.Height + 2), reasonFormat);
            y += reasonSize.Height + 10 * scale;
            g.DrawString(hint, hintFont, pale, new RectangleF(x, y, w, hintSize.Height + 2), hintFormat);
        }

        DrawButton(g, layout.PrimaryButton, Loc.T("Client.Messages.Open"), primary: true, icon: true, scale);
        DrawButton(g, layout.SecondaryButton, Loc.T("Client.Lock.Override"), primary: false, icon: false, scale);
        if (Logo is not null)
        {
            var box = layout.Logo;
            g.DrawImage(Logo, new Rectangle((int)box.X, (int)box.Y, (int)box.Width, (int)box.Height));
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void DrawButton(Graphics g, LockBox box, string text, bool primary, bool icon, float scale)
    {
        var rect = new Rectangle((int)box.X, (int)box.Y, (int)box.Width, (int)box.Height);
        using var path = Rounded(rect, rect.Height / 2);
        if (primary)
        {
            using var shadow = new SolidBrush(Color.FromArgb(0x0f, 0x7a, 0x37));
            using var shadowPath = Rounded(new Rectangle(rect.X, rect.Y + (int)(5 * scale), rect.Width, rect.Height), rect.Height / 2);
            g.FillPath(shadow, shadowPath);
            using var fill = new SolidBrush(Color.FromArgb(0x16, 0xa3, 0x4a));
            g.FillPath(fill, path);
        }
        else
        {
            using var fill = new SolidBrush(Color.FromArgb(90, 11, 29, 32));
            g.FillPath(fill, path);
            using var border = new Pen(Color.FromArgb(0x4f, 0xd1, 0xa5), Math.Max(1.5f, 2 * scale));
            g.DrawPath(border, path);
        }

        using var font = primary ? Theme.DisplayPx(26, FontStyle.Bold) : Theme.BodyPx(18, FontStyle.Bold);
        var color = primary ? Color.White : Color.FromArgb(0xd8, 0xf5, 0xea);
        using var brush = new SolidBrush(color);
        var iconSize = icon ? (int)(32 * scale) : 0;
        var iconSpace = icon ? iconSize + (int)(14 * scale) : 0;
        if (icon)
        {
            using var pen = new Pen(color, Math.Max(1.8f, 2.2f * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            var textSize = g.MeasureString(text, font);
            var ix = rect.X + (rect.Width - (int)textSize.Width - iconSpace) / 2;
            var iy = rect.Y + (rect.Height - iconSize) / 2;
            var bw = iconSize;
            var bh = iconSize * 0.8f;
            using var bubble = new GraphicsPath();
            bubble.AddArc(ix, iy, bw, bh, 180, 180);
            bubble.AddLine(ix + bw, iy + bh / 2, ix + bw, iy + bh);
            bubble.AddLine(ix + bw, iy + bh, ix + bw * 0.55f, iy + iconSize);
            bubble.AddLine(ix + bw * 0.55f, iy + iconSize, ix + bw * 0.25f, iy + bh);
            bubble.AddLine(ix + bw * 0.25f, iy + bh, ix, iy + bh);
            bubble.AddArc(ix, iy, bw, bh, 90, 90);
            g.DrawPath(pen, bubble);
        }
        using var format = CenterFormat();
        var textRect = new RectangleF(rect.X + iconSpace, rect.Y, rect.Width - iconSpace, rect.Height);
        g.DrawString(text, font, brush, textRect, format);
    }

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
