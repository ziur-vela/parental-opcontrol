// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace OpControl.Parental.Helper;

internal static class KidDraw
{
    public static Color Backdrop(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            var color = parent.BackColor;
            if (color.A == 255 && color != Color.Transparent) return color;
            if (parent is KidSurfacePanel) return KidTheme.Bg;
        }
        return KidTheme.Bg;
    }

    public static GraphicsPath Round(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(1, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
        if (d <= 2)
        {
            path.AddRectangle(rect);
            return path;
        }
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRound(Graphics g, Rectangle rect, int radius, Color fill)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || fill.A == 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Round(rect, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);
    }

    public static void StrokeRound(Graphics g, Rectangle rect, int radius, Color stroke, int width = 2)
    {
        if (rect.Width <= 1 || rect.Height <= 1 || stroke.A == 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Round(rect, radius);
        using var pen = new Pen(stroke, width);
        g.DrawPath(pen, path);
    }
}

internal sealed class CardPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor = KidTheme.CardBorder;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor = KidTheme.Card;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius = 20;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderWidth = 2;

    public CardPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        Padding = new Padding(16);
        Margin = new Padding(0);
    }

    public void ApplyTheme()
    {
        BorderColor = KidTheme.CardBorder;
        FillColor = KidTheme.Card;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(KidDraw.Backdrop(this));
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        KidDraw.FillRound(e.Graphics, rect, CornerRadius, FillColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var rect = new Rectangle(1, 1, Math.Max(0, Width - 3), Math.Max(0, Height - 3));
        KidDraw.StrokeRound(e.Graphics, rect, CornerRadius, BorderColor, BorderWidth);
    }
}

internal sealed class BarPanel : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Fraction { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BarColor { get; set; } = KidTheme.BarFill;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TrackColor { get; set; } = KidTheme.BarTrack;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Thickness { get; set; } = 10;

    public BarPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
        Height = 10;
        Margin = new Padding(0, 4, 0, 4);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var h = Math.Min(Thickness, Height);
        var y = Math.Max(0, (Height - h) / 2);
        var track = new Rectangle(0, y, Width, h);
        KidDraw.FillRound(e.Graphics, track, h / 2, TrackColor);
        var fraction = Math.Clamp(Fraction, 0, 1);
        if (fraction <= 0) return;
        var w = Math.Max(h, (int)Math.Round(Width * fraction));
        KidDraw.FillRound(e.Graphics, new Rectangle(0, y, Math.Min(Width, w), h), h / 2, BarColor);
    }
}

internal sealed class RingPanel : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Fraction { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color RingColor { get; set; } = KidTheme.RingFill;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TrackColor { get; set; } = KidTheme.RingTrack;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Thickness { get; set; } = 14;

    public RingPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
        Size = new Size(128, 128);
        Margin = new Padding(0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var t = Math.Min(Thickness, Math.Min(Width, Height) / 3);
        var inset = t / 2 + 1;
        var rect = new Rectangle(inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1);
        using var track = new Pen(TrackColor, t);
        e.Graphics.DrawEllipse(track, rect);
        var fraction = Math.Clamp(Fraction, 0, 1);
        if (fraction <= 0) return;
        using var ring = new Pen(RingColor, t)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        e.Graphics.DrawArc(ring, rect, -90f, (float)(360 * fraction));
    }
}

internal sealed class KidTag : Control
{
    private string _text = "";
    private Color _textColor = KidTheme.Bad;
    private Color _fill = KidTheme.Bad;

    public KidTag(string text, Color textColor, Color fill)
    {
        _text = text;
        _textColor = textColor;
        _fill = fill;
        AutoSize = false;
        Font = Theme.Body(8.5f, FontStyle.Bold);
        Height = 24;
        Width = TextRenderer.MeasureText(text, Font).Width + 18;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
    }

    public static KidTag Danger(string text) => new(text, KidTheme.Bad, Color.FromArgb(40, KidTheme.Bad));
    public static KidTag Warn(string text) => new(text, KidTheme.Warn, Color.FromArgb(40, KidTheme.Warn));
    public static KidTag Value(string text) => new(text, KidTheme.Ok, Color.FromArgb(40, KidTheme.Ok));

    protected override void OnPaint(PaintEventArgs e)
    {
        KidDraw.FillRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), Height / 2, _fill);
        TextRenderer.DrawText(e.Graphics, _text, Font, ClientRectangle,
            _textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

internal enum KidButtonKind { Primary, Ghost, Chip, Segment }

internal sealed class KidButton : Button
{
    private bool _hover;
    private bool _selected;
    private KidButtonKind _kind = KidButtonKind.Ghost;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public KidButtonKind Kind
    {
        get => _kind;
        set { _kind = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    public KidButton(string text, KidButtonKind kind = KidButtonKind.Ghost)
    {
        Text = text;
        _kind = kind;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = KidTheme.Text;
        Font = Theme.Body(10f, FontStyle.Bold);
        Height = kind == KidButtonKind.Chip ? 40 : 44;
        Cursor = Cursors.Hand;
        TabStop = true;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    public void ApplyTheme()
    {
        ForeColor = KidTheme.Text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill, ink, border;
        var radius = _kind == KidButtonKind.Chip || _kind == KidButtonKind.Segment ? Height / 2 : 16;
        switch (_kind)
        {
            case KidButtonKind.Primary:
                fill = KidTheme.Primary; ink = KidTheme.PrimaryInk; border = KidTheme.Primary;
                break;
            case KidButtonKind.Chip:
                fill = _hover ? (KidTheme.IsGray ? Color.FromArgb(245, 246, 249) : Color.FromArgb(58, 47, 122)) : KidTheme.Chip;
                ink = KidTheme.ChipText; border = KidTheme.ChipBorder;
                break;
            case KidButtonKind.Segment:
                fill = _selected ? KidTheme.SegOn : Color.Transparent;
                ink = _selected ? KidTheme.SegOnInk : KidTheme.ChipText;
                border = Color.Transparent;
                break;
            default:
                fill = _hover ? (KidTheme.IsGray ? Color.FromArgb(245, 246, 249) : Color.FromArgb(58, 47, 122)) : Color.Transparent;
                ink = KidTheme.Text; border = KidTheme.ChipBorder;
                break;
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (_kind == KidButtonKind.Primary)
        {
            KidDraw.FillRound(e.Graphics, new Rectangle(0, 4, Width - 1, Height - 5), radius, KidTheme.PrimaryShadow);
            rect = new Rectangle(0, 0, Width - 1, Height - 5);
        }
        KidDraw.FillRound(e.Graphics, rect, radius, fill);
        if (border.A > 0) KidDraw.StrokeRound(e.Graphics, rect, radius, border, 2);
        TextRenderer.DrawText(e.Graphics, Text, Font, rect, ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

internal enum KidIconKind { Home, Clock, Shield, Bars, Chat, Moon, Sun, Close, Minimize, School, Send, Check }

internal sealed class KidIcon : Control
{
    private KidIconKind _kind;
    private Color _color = KidTheme.Text;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public KidIconKind Kind { get => _kind; set { _kind = value; Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color IconColor { get => _color; set { _color = value; Invalidate(); } }

    public KidIcon(KidIconKind kind, Color? color = null, int size = 20)
    {
        _kind = kind;
        _color = color ?? KidTheme.Text;
        Size = new Size(size, size);
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var s = Math.Min(Width, Height);
        var scale = s / 24f;
        g.TranslateTransform((Width - s) / 2f, (Height - s) / 2f);
        g.ScaleTransform(scale, scale);
        using var pen = new Pen(_color, 2.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (_kind)
        {
            case KidIconKind.Home:
                g.DrawLines(pen, new[] { new PointF(4, 11), new PointF(12, 4), new PointF(20, 11), new PointF(20, 20), new PointF(4, 20), new PointF(4, 11) });
                g.DrawLines(pen, new[] { new PointF(10, 20), new PointF(10, 14), new PointF(14, 14), new PointF(14, 20) });
                break;
            case KidIconKind.Clock:
                g.DrawEllipse(pen, 3, 3, 18, 18);
                g.DrawLine(pen, 12, 7, 12, 12);
                g.DrawLine(pen, 12, 12, 15, 14);
                break;
            case KidIconKind.Shield:
                g.DrawLines(pen, new[] { new PointF(12, 3), new PointF(20, 6), new PointF(20, 12), new PointF(18, 16), new PointF(12, 21), new PointF(6, 16), new PointF(4, 12), new PointF(4, 6), new PointF(12, 3) });
                break;
            case KidIconKind.Bars:
                g.DrawLine(pen, 4, 20, 4, 10);
                g.DrawLine(pen, 10, 20, 10, 4);
                g.DrawLine(pen, 16, 20, 16, 13);
                g.DrawLine(pen, 2, 20, 22, 20);
                break;
            case KidIconKind.Chat:
                g.DrawLines(pen, new[] { new PointF(4, 5), new PointF(20, 5), new PointF(20, 16), new PointF(9, 16), new PointF(4, 20), new PointF(4, 5) });
                break;
            case KidIconKind.Moon:
                g.DrawArc(pen, 5, 2, 16, 20, 70, 230);
                g.DrawArc(pen, 9, 5, 12, 16, 250, 90);
                break;
            case KidIconKind.Sun:
                g.DrawEllipse(pen, 8, 8, 8, 8);
                for (var i = 0; i < 8; i++)
                {
                    var a = i * Math.PI / 4;
                    g.DrawLine(pen, 12 + (float)Math.Cos(a) * 7, 12 + (float)Math.Sin(a) * 7,
                        12 + (float)Math.Cos(a) * 10, 12 + (float)Math.Sin(a) * 10);
                }
                break;
            case KidIconKind.Close:
                g.DrawLine(pen, 6, 6, 18, 18);
                g.DrawLine(pen, 18, 6, 6, 18);
                break;
            case KidIconKind.Minimize:
                g.DrawLine(pen, 6, 12, 18, 12);
                break;
            case KidIconKind.School:
                g.DrawLines(pen, new[] { new PointF(2, 9), new PointF(12, 4), new PointF(22, 9), new PointF(12, 14), new PointF(2, 9) });
                g.DrawLines(pen, new[] { new PointF(6, 11), new PointF(6, 16), new PointF(18, 16), new PointF(18, 11) });
                break;
            case KidIconKind.Send:
                g.DrawLines(pen, new[] { new PointF(3, 12), new PointF(21, 4), new PointF(14, 21), new PointF(11, 13), new PointF(3, 12) });
                break;
            case KidIconKind.Check:
                g.DrawLines(pen, new[] { new PointF(5, 12), new PointF(10, 17), new PointF(19, 7) });
                break;
        }
    }
}

internal sealed class MoonFace : Control
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly DateTime _start = DateTime.UtcNow;
    private bool _asleep;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Asleep { get => _asleep; set { _asleep = value; Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowZzz { get; set; } = true;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Animated { get; set; } = true;

    public MoonFace(int size = 64, bool asleep = false)
    {
        Size = new Size(size, size);
        _asleep = asleep;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        _timer = new System.Windows.Forms.Timer { Interval = 50 };
        _timer.Tick += (_, _) => { if (Animated) Invalidate(); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Animated = !KidMotion.Reduced;
        if (Animated) _timer.Start();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _timer.Stop();
        base.OnHandleDestroyed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var size = Math.Min(Width, Height);
        var seconds = (float)(DateTime.UtcNow - _start).TotalSeconds;
        var floatY = Animated ? (float)Math.Sin(seconds * Math.PI * 2 / 4.0) * (size * 0.045f) : 0f;
        var blink = Animated && !_asleep && seconds % 4.2f > 3.86f && seconds % 4.2f < 4.08f;
        var scale = size / 172f;
        g.TranslateTransform((Width - size) / 2f, (Height - size) / 2f + floatY);
        g.ScaleTransform(scale, scale);

        using var moon = new SolidBrush(ColorTranslator.FromHtml("#f6dc7e"));
        using var moonEdge = new Pen(ColorTranslator.FromHtml("#e2bd52"), 4);
        using var crater = new SolidBrush(ColorTranslator.FromHtml("#ecc964"));
        using var crater2 = new SolidBrush(ColorTranslator.FromHtml("#e8c35c"));
        using var ink = new SolidBrush(ColorTranslator.FromHtml("#2b2f3a"));
        using var blush = new SolidBrush(Color.FromArgb(204, 240, 169, 160));
        using var line = new Pen(ColorTranslator.FromHtml("#2b2f3a"), 8) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.FillEllipse(moon, 3, 3, 166, 166);
        g.DrawEllipse(moonEdge, 3, 3, 166, 166);
        g.FillEllipse(crater, 41, 37, 22, 22);
        g.FillEllipse(crater2, 109, 115, 26, 26);

        if (_asleep)
        {
            g.TranslateTransform(86, 86);
            g.RotateTransform(-12);
            g.TranslateTransform(-86, -86);
            g.DrawArc(line, 52, 78, 24, 14, 15, 150);
            g.DrawArc(line, 96, 78, 24, 14, 15, 150);
            using var mouth = new Pen(ColorTranslator.FromHtml("#2b2f3a"), 7) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(mouth, 74, 110, 24, 10, 15, 150);
        }
        else
        {
            var eyeH = blink ? 2 : 22;
            var eyeY = blink ? 88 : 78 - (22 - eyeH) / 2;
            g.FillEllipse(ink, 56, eyeY, 16, eyeH);
            g.FillEllipse(ink, 100, eyeY, 16, eyeH);
            g.FillEllipse(blush, 38, 94, 20, 12);
            g.FillEllipse(blush, 114, 94, 20, 12);
            g.DrawArc(line, 60, 100, 52, 34, 20, 140);
        }

        if (ShowZzz)
        {
            using var zFont = Theme.DisplayPx(Math.Max(9f, size * 0.16f), FontStyle.Bold);
            for (var i = 0; i < 3; i++)
            {
                var phase = ((seconds / 3f) + i / 3f) % 1f;
                var alpha = Animated ? (int)(255 * Math.Min(1, Math.Min(phase * 5, (1 - phase) * 3))) : 190;
                var dx = Animated ? phase * size * 0.18f : i * 2f;
                var dy = Animated ? -phase * size * 0.2f : -i * 3f;
                using var zBrush = new SolidBrush(Color.FromArgb(Math.Clamp(alpha, 40, 255), KidTheme.Label));
                g.DrawString("Z", zFont, zBrush, 118 + dx, 18 + dy);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class KidNavItem : Control
{
    private bool _active;
    private bool _hover;
    private int _unread;
    private readonly KidIcon _icon;

    public string Label { get; }
    public KidIconKind IconKind { get; }

    public KidNavItem(string label, KidIconKind icon)
    {
        Label = label;
        IconKind = icon;
        Height = 44;
        Cursor = Cursors.Hand;
        Font = Theme.Display(11f, FontStyle.Bold);
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        _icon = new KidIcon(icon, KidTheme.Muted, 20) { Location = new Point(14, 12), Enabled = false };
        Controls.Add(_icon);
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
        Click += (_, _) => { };
    }

    public void SetActive(bool active)
    {
        _active = active;
        _icon.IconColor = active ? KidTheme.NavActiveText : KidTheme.Muted;
        Invalidate();
    }

    public void SetUnread(int count)
    {
        _unread = count;
        Invalidate();
    }

    public void ApplyTheme()
    {
        _icon.IconColor = _active ? KidTheme.NavActiveText : KidTheme.Muted;
        Font = Theme.Display(11f, FontStyle.Bold);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (_active)
        {
            KidDraw.FillRound(e.Graphics, rect, 14, KidTheme.NavActive);
            KidDraw.StrokeRound(e.Graphics, rect, 14, KidTheme.NavActiveBorder, 2);
        }
        else if (_hover)
        {
            KidDraw.FillRound(e.Graphics, rect, 14, KidTheme.IsGray ? Color.White : Color.FromArgb(30, 255, 255, 255));
        }
        TextRenderer.DrawText(e.Graphics, Label, Font, new Rectangle(42, 0, Width - 54, Height),
            _active ? KidTheme.NavActiveText : KidTheme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (_unread > 0)
        {
            var text = _unread > 99 ? "99+" : _unread.ToString();
            var w = Math.Max(20, TextRenderer.MeasureText(text, Theme.Body(8f, FontStyle.Bold)).Width + 8);
            var badge = new Rectangle(Width - w - 10, (Height - 18) / 2, w, 18);
            KidDraw.FillRound(e.Graphics, badge, 9, KidTheme.Warn);
            TextRenderer.DrawText(e.Graphics, text, Theme.Body(8f, FontStyle.Bold), badge, KidTheme.PrimaryInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

internal sealed class KidSegmented : Panel
{
    private readonly KidButton[] _buttons;
    private int _selected;

    public event Action<int>? Changed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex { get => _selected; set { _selected = value; UpdateSelected(); } }

    public KidSegmented(params string[] labels)
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Height = 42;
        Padding = new Padding(3);
        _buttons = new KidButton[labels.Length];
        var x = 3;
        for (var i = 0; i < labels.Length; i++)
        {
            var b = new KidButton(labels[i], KidButtonKind.Segment)
            {
                Height = 36,
                Width = TextRenderer.MeasureText(labels[i], Theme.Body(10f, FontStyle.Bold)).Width + 30,
                Location = new Point(x, 3),
                Font = Theme.Display(10f, FontStyle.Bold)
            };
            var index = i;
            b.Click += (_, _) => { SelectedIndex = index; Changed?.Invoke(index); };
            _buttons[i] = b;
            Controls.Add(b);
            x += b.Width + 2;
        }
        Width = x + 1;
        UpdateSelected();
    }

    private void UpdateSelected()
    {
        for (var i = 0; i < _buttons.Length; i++) _buttons[i].Selected = i == _selected;
        Invalidate();
    }

    public void ApplyTheme()
    {
        foreach (var b in _buttons) b.ApplyTheme();
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(KidDraw.Backdrop(this));
        KidDraw.FillRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), Height / 2, KidTheme.SegBg);
        KidDraw.StrokeRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), Height / 2, KidTheme.SegBorder, 2);
    }
}

internal static class KidUi
{
    public static Label Eyebrow(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        Font = Theme.Display(8.5f, FontStyle.Bold),
        ForeColor = KidTheme.Label,
        AutoSize = true,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 4)
    };

    public static Label Title(string text) => new()
    {
        Text = text,
        Font = Theme.Display(23f, FontStyle.Bold),
        ForeColor = KidTheme.Text,
        AutoSize = true,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 4)
    };

    public static Label Body(string text, bool muted = false, float size = 10.5f) => new()
    {
        Text = text,
        Font = Theme.Body(size),
        ForeColor = muted ? KidTheme.Muted : KidTheme.Text,
        AutoSize = true,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 4)
    };

    public static Label Mono(string text, float size = 9.5f, bool bold = false, Color? color = null) => new()
    {
        Text = text,
        Font = Theme.Display(size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = color ?? KidTheme.Muted,
        AutoSize = true,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 3)
    };

    public static Label Label(string text, float size = 10f, bool bold = true, Color? color = null) => new()
    {
        Text = text,
        Font = Theme.Display(size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = color ?? KidTheme.Label,
        AutoSize = true,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 3)
    };

    public static Label Link(string text) => new()
    {
        Text = text,
        Font = Theme.Display(9.5f, FontStyle.Bold),
        ForeColor = KidTheme.Link,
        AutoSize = true,
        BackColor = Color.Transparent,
        Cursor = Cursors.Hand,
        Margin = new Padding(0, 6, 0, 0)
    };

    public static Label Divider(string text) => new()
    {
        Text = text,
        Font = Theme.Body(8.5f),
        ForeColor = KidTheme.Faint,
        AutoSize = true,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 4, 0, 2)
    };

    public static string Duration(int seconds)
    {
        if (seconds <= 0) return "0m";
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h {span.Minutes:00}m"
            : $"{Math.Max(1, span.Minutes)}m";
    }

    public static string Clock(int seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes:00}m" : $"{span.Minutes}m";
    }

    public static FlowLayoutPanel Row(int gap = 8)
    {
        return new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
    }

    public static FlowLayoutPanel Stack(int gap = 4)
    {
        return new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
    }
}

internal sealed class KidSurfacePanel : Panel
{
    private readonly Random _stars = new(42);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Decorated { get; set; } = true;

    public KidSurfacePanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor.A == 255 ? BackColor : KidTheme.Bg);
        if (!Decorated || Width <= 0 || Height <= 0) return;
        if (KidTheme.IsGray)
        {
            using var crater = new SolidBrush(ColorTranslator.FromHtml("#e2e5eb"));
            using var edge = new Pen(ColorTranslator.FromHtml("#d7dbe3"), 3);
            var a = new Rectangle(Width - 90, -20, 70, 70);
            e.Graphics.FillEllipse(crater, a);
            e.Graphics.DrawEllipse(edge, a);
            var b = new Rectangle(-30, Height - 70, 52, 52);
            e.Graphics.FillEllipse(crater, b);
            e.Graphics.DrawEllipse(edge, b);
            return;
        }
        using var white = new SolidBrush(Color.FromArgb(210, 255, 255, 255));
        using var lilac = new SolidBrush(Color.FromArgb(190, 217, 210, 255));
        using var warm = new SolidBrush(Color.FromArgb(220, 255, 246, 200));
        for (var i = 0; i < 46; i++)
        {
            var x = _stars.Next(0, Math.Max(1, Width));
            var y = _stars.Next(0, Math.Max(1, Height));
            var r = i % 11 == 0 ? 2 : 1;
            e.Graphics.FillEllipse(i % 7 == 0 ? warm : i % 3 == 0 ? lilac : white, x, y, r, r);
        }
    }
}

internal sealed class KidIconButton : Button
{
    private bool _hover;
    private readonly KidIcon _icon;

    public KidIconButton(KidIconKind kind)
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Size = new Size(40, 40);
        Cursor = Cursors.Hand;
        TabStop = true;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        _icon = new KidIcon(kind, KidTheme.Label, 20) { Location = new Point(10, 10), Enabled = false };
        Controls.Add(_icon);
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    public void SetIcon(KidIconKind kind) => _icon.Kind = kind;

    protected override void OnPaint(PaintEventArgs e)
    {
        KidDraw.FillRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 12,
            _hover ? (KidTheme.IsGray ? Color.FromArgb(245, 246, 249) : Color.FromArgb(58, 47, 122)) : KidTheme.SegBg);
        KidDraw.StrokeRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 12, KidTheme.CardBorder, 2);
    }
}

internal sealed class KidStatusPill : Control
{
    private string _text = "";
    private Color _dot = KidTheme.Ok;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DotColor { get => _dot; set { _dot = value; Invalidate(); } }

    public KidStatusPill(string text)
    {
        _text = text;
        Font = Theme.Body(9.5f, FontStyle.Bold);
        Height = 32;
        Width = TextRenderer.MeasureText(text, Font).Width + 42;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
    }

    public void SetText(string text, Color dot)
    {
        _text = text;
        _dot = dot;
        Width = TextRenderer.MeasureText(text, Font).Width + 42;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(KidDraw.Backdrop(this));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        KidDraw.FillRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 14, KidTheme.Sub);
        KidDraw.StrokeRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 14, KidTheme.SubBorder, 2);
        using var dot = new SolidBrush(_dot);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.FillEllipse(dot, 14, (Height - 10) / 2, 10, 10);
        TextRenderer.DrawText(e.Graphics, _text, Font, new Rectangle(30, 0, Width - 34, Height),
            KidTheme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}


internal sealed class KidTrayHeader : Panel
{
    public Label Caption { get; } = new();
    public Label Time { get; } = new();
    public KidIconButton ThemeButton { get; }

    public KidTrayHeader()
    {
        BackColor = Color.Transparent;
        Height = 62;
        Width = 320;
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0), Padding = new Padding(4) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(new MoonFace(40) { Margin = new Padding(4, 6, 10, 0) }, 0, 0);
        Caption.Text = "";
        Caption.Font = Theme.Body(8.5f, FontStyle.Bold);
        Caption.ForeColor = KidTheme.Muted;
        Caption.AutoSize = false;
        Caption.Dock = DockStyle.Top;
        Caption.Height = 20;
        Caption.BackColor = Color.Transparent;
        Time.Text = "";
        Time.Font = Theme.Display(14f, FontStyle.Bold);
        Time.ForeColor = KidTheme.Text;
        Time.AutoSize = false;
        Time.Dock = DockStyle.Top;
        Time.Height = 26;
        Time.BackColor = Color.Transparent;
        var stack = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        stack.Controls.Add(Time);
        stack.Controls.Add(Caption);
        row.Controls.Add(stack, 1, 0);
        ThemeButton = new KidIconButton(KidIconKind.Moon) { Dock = DockStyle.None, Anchor = AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(6, 4, 2, 0) };
        row.Controls.Add(ThemeButton, 2, 0);
        Controls.Add(row);
    }

    public void ApplyTheme(bool gray)
    {
        Caption.ForeColor = KidTheme.Muted;
        Time.ForeColor = KidTheme.Text;
        ThemeButton.SetIcon(gray ? KidIconKind.Sun : KidIconKind.Moon);
        ThemeButton.Invalidate();
        Invalidate(true);
    }
}
