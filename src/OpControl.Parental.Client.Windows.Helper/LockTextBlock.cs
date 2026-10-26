// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace OpControl.Parental.Helper;

/// <summary>
/// A measured, word-wrapped line block drawn with GDI+ so the embedded private
/// fonts are actually used. WinForms Labels with UseCompatibleTextRendering=false
/// go through GDI, which cannot see a PrivateFontCollection and silently falls
/// back to Segoe UI (that was the duplicate/misaligned "shadow").
/// </summary>
internal sealed class LockTextBlock
{
    private readonly double[] _lineWidths;

    private LockTextBlock(string[] lines, double[] lineWidths, Font font, double lineHeight, double maxWidth)
    {
        Lines = lines;
        _lineWidths = lineWidths;
        Font = font;
        LineHeight = lineHeight;
        MaxWidth = maxWidth;
    }

    public IReadOnlyList<string> Lines { get; }
    public Font Font { get; }
    public double LineHeight { get; }
    public double MaxWidth { get; }
    public double Width => _lineWidths.Length == 0 ? 0 : _lineWidths.Max();
    public double Height => Lines.Count * LineHeight;

    public static LockTextBlock Create(Graphics g, string text, Font font, double maxWidth, double lineHeight)
    {
        maxWidth = Math.Max(1, maxWidth);
        var lines = Wrap(g, text, font, maxWidth);
        var widths = new double[lines.Length];
        using var format = Typographic();
        for (var i = 0; i < lines.Length; i++)
            widths[i] = g.MeasureString(lines[i], font, PointF.Empty, format).Width;
        return new LockTextBlock(lines, widths, font, lineHeight, maxWidth);
    }

    /// <summary>Single-line width without GDI+'s overhang padding.</summary>
    public static double MeasureWidth(Graphics g, string text, Font font)
    {
        using var format = Typographic();
        return g.MeasureString(text, font, PointF.Empty, format).Width;
    }

    public void Draw(Graphics g, Brush brush, double centerX, double top) =>
        DrawLines(g, brush, centerX, top, centerX * 2);

    /// <summary>
    /// GDI+ clips to the layout rectangle and its MeasureString width is a few
    /// percent tighter than the drawn string, which cut the last glyph. Draw in
    /// a full-screen-wide band centred on the screen instead: no clipping, and
    /// StringAlignment.Center still centres every line exactly.
    /// </summary>
    private void DrawLines(Graphics g, Brush brush, double centerX, double top, double bandWidth)
    {
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
        };
        for (var i = 0; i < Lines.Count; i++)
        {
            var rect = new RectangleF(
                (float)(centerX - bandWidth / 2),
                (float)(top + i * LineHeight),
                (float)bandWidth,
                (float)LineHeight);
            g.DrawString(Lines[i], Font, brush, rect, format);
        }
    }

    /// <summary>Black text on transparency, for the blurred title shadow.</summary>
    public Bitmap RenderShadowMask(int blurRadius, double screenWidth)
    {
        var pad = Math.Max(2, blurRadius * 2 + 2);
        var width = (int)Math.Ceiling(screenWidth) + pad * 2;
        var height = (int)Math.Ceiling(Height) + pad * 2;
        var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            using var ink = new SolidBrush(Color.Black);
            DrawLines(g, ink, bitmap.Width / 2.0, pad, screenWidth);
        }
        BlurAlpha(bitmap, blurRadius);
        return bitmap;
    }

    private static string[] Wrap(Graphics g, string text, Font font, double maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return [string.Empty];
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length == 0 || MeasureWidth(g, candidate, font) <= maxWidth)
            {
                current = candidate;
                continue;
            }
            lines.Add(current);
            current = word;
        }
        lines.Add(current);
        return [.. lines];
    }

    private static StringFormat Typographic()
    {
        var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        return format;
    }

    /// <summary>Three box-blur passes: a soft gaussian-like falloff, alpha only.</summary>
    private static void BlurAlpha(Bitmap bitmap, int radius)
    {
        radius = Math.Clamp(radius, 1, Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 3));
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var bytes = new byte[stride * bitmap.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            var width = bitmap.Width;
            var height = bitmap.Height;
            var alpha = new float[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    alpha[y * width + x] = bytes[y * stride + x * 4 + 3];

            var scratch = new float[alpha.Length];
            for (var pass = 0; pass < 3; pass++)
            {
                BoxHorizontal(alpha, scratch, width, height, radius);
                BoxVertical(scratch, alpha, width, height, radius);
            }

            // Pre-scale the alpha so the composite is ~35% black like the ref text-shadow.
            const float opacity = 0.75f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    var value = (byte)Math.Clamp(alpha[i] * opacity, 0, 255);
                    var offset = y * stride + x * 4;
                    bytes[offset] = 0;
                    bytes[offset + 1] = 0;
                    bytes[offset + 2] = 0;
                    bytes[offset + 3] = value;
                }
            }
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void BoxHorizontal(float[] source, float[] target, int width, int height, int radius)
    {
        var window = radius * 2 + 1;
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            float sum = 0;
            for (var x = -radius; x <= radius; x++)
                sum += source[row + Math.Clamp(x, 0, width - 1)];
            for (var x = 0; x < width; x++)
            {
                target[row + x] = sum / window;
                sum -= source[row + Math.Clamp(x - radius, 0, width - 1)];
                sum += source[row + Math.Clamp(x + radius + 1, 0, width - 1)];
            }
        }
    }

    private static void BoxVertical(float[] source, float[] target, int width, int height, int radius)
    {
        var window = radius * 2 + 1;
        for (var x = 0; x < width; x++)
        {
            float sum = 0;
            for (var y = -radius; y <= radius; y++)
                sum += source[Math.Clamp(y, 0, height - 1) * width + x];
            for (var y = 0; y < height; y++)
            {
                target[y * width + x] = sum / window;
                sum -= source[Math.Clamp(y - radius, 0, height - 1) * width + x];
                sum += source[Math.Clamp(y + radius + 1, 0, height - 1) * width + x];
            }
        }
    }
}
