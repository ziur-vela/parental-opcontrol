// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

public sealed class LockOverlayManager : IDisposable
{
    private readonly List<LockForm> _forms = [];
    private LockMessagesForm? _messages;
    public event Action? OverrideClicked;
    public event Action? MessageClicked;

    public bool Visible => _forms.Any(f => f.Visible);

    /// <summary>Lock form on the primary screen; override dialogs are owned by it so they stay above the overlay.</summary>
    public Form? Owner => _forms.FirstOrDefault(f => f.IsPrimary) ?? _forms.FirstOrDefault();

    private bool DialogOpen => _forms.Any(f => f.OwnedForms.Any(o => o.Visible));

    public void Update(RuntimeLockStateDto state)
    {
        if (_forms.Count == 0)
        {
            Theme.LogFontCheckOnce();
            foreach (var screen in Screen.AllScreens)
            {
                var form = new LockForm(screen.Bounds, screen.Primary);
                form.OverrideClicked += () => OverrideClicked?.Invoke();
                form.MessageClicked += () =>
                {
                    MessageClicked?.Invoke();
                    ShowMessages();
                };
                _forms.Add(form);
            }
        }

        foreach (var form in _forms)
            form.UpdateState(state);

        if (!Visible)
        {
            foreach (var form in _forms)
                form.Show();
            HelperPaths.LogEvent("lock-window-shown");
        }

        // Setting TopMost re-runs SetWindowPos(HWND_TOPMOST) and lifts the overlay above every other
        // topmost window. While a parent dialog is open that would bury it (bug: PIN dialog vanished
        // after ~1 s), so only re-assert when no dialog is showing.
        var dialogOpen = DialogOpen;
        foreach (var form in _forms)
        {
            if (!dialogOpen) form.TopMost = true;
            if (!form.Visible) form.Show();
        }
    }

    /// <summary>Open the messaging panel owned by the primary lock overlay.</summary>
    public void ShowMessages()
    {
        var owner = Owner;
        if (owner is not { IsDisposed: false }) return;
        if (_messages is { IsDisposed: false })
        {
            _messages.Show(owner);
            _messages.BringToFront();
            _messages.Activate();
            return;
        }
        var panel = new LockMessagesForm();
        panel.FormClosed += (_, _) => _messages = null;
        _messages = panel;
        panel.Show(owner);
        panel.BringToFront();
        panel.Activate();
    }

    public void Hide()
    {
        CloseMessages();
        var wasVisible = Visible;
        foreach (var form in _forms)
            form.Hide();
        if (wasVisible)
            HelperPaths.LogEvent("lock-window-hidden");
    }

    private void CloseMessages()
    {
        if (_messages is { IsDisposed: false } panel)
        {
            try { panel.Close(); } catch { }
        }
        _messages = null;
    }

    public void Dispose()
    {
        CloseMessages();
        foreach (var form in _forms)
            form.Dispose();
        _forms.Clear();
    }
}

public sealed class LockForm : Form
{
    private static readonly Image? Logo = LoadLogo();
    private readonly PillButton _message;
    private readonly PillButton _override;
    private readonly System.Windows.Forms.Timer _zTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private LockSceneLayout _layout;
    private LockContentLayout? _content;
    private LockTextBlock? _titleBlock;
    private LockTextBlock? _reasonBlock;
    private LockTextBlock? _hintBlock;
    private Font? _titleFont;
    private Font? _reasonFont;
    private Font? _hintFont;
    private Font? _primaryFont;
    private Font? _secondaryFont;
    private Bitmap? _titleShadow;
    private int _shadowPad;
    private string? _shadowText;
    private float _shadowScale = -1;
    private int _shadowWidth = -1;
    private float _fontScale = -1f;
    private bool _previewMode;
    private int _requestedWidth;
    private int _requestedHeight;
    private string _titleText = string.Empty;
    private string _reasonText = string.Empty;
    private string _hintText = string.Empty;
    private Bitmap? _background;
    private Size _backgroundSize;

    public event Action? OverrideClicked;
    public event Action? MessageClicked;

    public bool IsPrimary { get; }

    /// <summary>Forces a UI scale for the scaling-matrix captures (no Windows setting change).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double? UiScaleOverride { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PreviewMode
    {
        get => _previewMode;
        set
        {
            _previewMode = value;
            TopMost = !value;
            ShowInTaskbar = false;
        }
    }

    public LockForm(Screen screen) : this(screen.Bounds, screen.Primary) { }

    public LockForm(Rectangle bounds, bool primary)
    {
        IsPrimary = primary;
        _requestedWidth = Math.Max(1, bounds.Width);
        _requestedHeight = Math.Max(1, bounds.Height);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = Color.FromArgb(0x17, 0x39, 0x3d);
        ForeColor = Color.White;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        _layout = LockSceneLayout.For(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));

        _message = new PillButton
        {
            Primary = true,
            MessageIcon = true,
            Text = Loc.T("Client.Messages.Open")
        };
        _message.Click += (_, _) => MessageClicked?.Invoke();
        _override = new PillButton
        {
            Primary = false,
            Text = Loc.T("Client.Lock.Override")
        };
        _override.Click += (_, _) => OverrideClicked?.Invoke();

        Controls.Add(_message);
        Controls.Add(_override);

        _zTimer = new System.Windows.Forms.Timer { Interval = 90 };
        _zTimer.Tick += (_, _) => { if (Visible) Invalidate(); };
        _zTimer.Start();

        Resize += (_, _) => Relayout();
        FormClosing += (_, e) =>
        {
            if (!PreviewMode && e.CloseReason is CloseReason.UserClosing or CloseReason.TaskManagerClosing)
                e.Cancel = true;
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                if (PreviewMode) Close();
            }
        };
        Relayout();
    }

    private void ApplyTextFonts()
    {
        var ui = (float)_layout.UiScale;
        if (Math.Abs(ui - _fontScale) < 0.01f) return;
        _fontScale = ui;
        _titleBlock = null;
        _reasonBlock = null;
        _hintBlock = null;
        _titleFont?.Dispose();
        _reasonFont?.Dispose();
        _hintFont?.Dispose();
        _primaryFont?.Dispose();
        _secondaryFont?.Dispose();
        _titleFont = Theme.LockTitle(56 * ui);
        _reasonFont = Theme.LockReason(32 * ui);
        _hintFont = Theme.LockHint(22 * ui);
        _primaryFont = Theme.LockTitle(26 * ui);
        _secondaryFont = Theme.LockSecondary(18 * ui);
        _message.Font = _primaryFont;
        _override.Font = _secondaryFont;
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Relayout();
    }

    public void UpdateState(RuntimeLockStateDto state)
    {
        var (title, reason, hint) = Texts(state);
        _titleText = title;
        _reasonText = reason;
        _hintText = hint;
        _message.Text = Loc.T("Client.Messages.Open");
        _override.Text = Loc.T("Client.Lock.Override");
        Relayout();
        Invalidate();
    }

    public static (string Title, string Reason, string Hint) Texts(RuntimeLockStateDto state)
    {
        var title = Loc.T("Client.Lock.Title");
        var reason = state.Reason switch
        {
            "manual" => Loc.T("Client.Lock.ReasonManual", state.ParentDisplayName ?? Loc.T("Client.Lock.AParent")),
            "schedule" => Loc.T("Client.Lock.ReasonSchedule", string.IsNullOrWhiteSpace(state.ScheduleName) ? Loc.T("Client.Lock.ASchedule") : state.ScheduleName),
            "daily-limit" => Loc.T("Client.Lock.ReasonDailyLimit"),
            "paused" => Loc.T("Client.Lock.Paused"),
            _ => Loc.T("Client.Lock.ReasonLocked")
        };
        var hint = state.Reason switch
        {
            "daily-limit" => Loc.T("Client.Lock.ResetsMidnight"),
            "paused" => Loc.T("Client.Lock.AskParent"),
            _ => state.EndsUtc is { } ends
                ? Loc.T("Client.Lock.Unlocks", ends.ToLocalTime().ToString("t"))
                : Loc.T("Client.Lock.AskParent")
        };
        return (title, reason, hint);
    }

    private void Relayout()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        _layout = LockSceneLayout.For(ClientSize.Width, ClientSize.Height, UiScaleOverride ?? DeviceDpi / 96.0);
        ApplyTextFonts();
        var ui = (float)_layout.UiScale;
        var maxTextWidth = _layout.TextColumn.Width;

        using (var measureBitmap = new Bitmap(1, 1))
        using (var g = Graphics.FromImage(measureBitmap))
        {
            _titleBlock = LockTextBlock.Create(g, _titleText, _titleFont!,
                maxTextWidth, LockSceneLayout.TitleLineHeight * ui);
            _reasonBlock = LockTextBlock.Create(g, _reasonText, _reasonFont!,
                maxTextWidth, LockSceneLayout.ReasonLineHeight * ui);
            _hintBlock = LockTextBlock.Create(g, _hintText, _hintFont!,
                maxTextWidth, LockSceneLayout.HintLineHeight * ui);
            var primaryText = LockTextBlock.MeasureWidth(g, _message.Text, _primaryFont!);
            var secondaryText = LockTextBlock.MeasureWidth(g, _override.Text, _secondaryFont!);
            _content = _layout.ArrangeContent(
                _titleBlock.Width, _titleBlock.Height,
                _reasonBlock.Width, _reasonBlock.Height,
                _hintBlock.Width, _hintBlock.Height,
                primaryText, secondaryText);
        }

        _message.Bounds = ToRectangle(_content!.PrimaryButton);
        _override.Bounds = ToRectangle(_content.SecondaryButton);
        _message.Invalidate();
        _override.Invalidate();

        BuildTitleShadow();

        if (_background is null || _backgroundSize != ClientSize)
        {
            _background?.Dispose();
            _background = LockSceneRenderer.Render(ClientSize.Width, ClientSize.Height);
            _backgroundSize = ClientSize;
            BackgroundImage = _background;
            BackgroundImageLayout = ImageLayout.None;
        }
        Invalidate();
    }

    private void BuildTitleShadow()
    {
        if (_titleShadow is not null
            && _shadowText == _titleText
            && Math.Abs(_shadowScale - _fontScale) < 0.001f
            && _shadowWidth == (int)_layout.Width)
            return;
        _titleShadow?.Dispose();
        _titleShadow = null;
        _shadowText = _titleText;
        _shadowScale = _fontScale;
        _shadowWidth = (int)_layout.Width;
        if (_titleBlock is null || string.IsNullOrEmpty(_titleText)) return;
        var radius = Math.Max(2, (int)Math.Round(6 * _layout.UiScale));
        _shadowPad = Math.Max(2, radius * 2 + 2);
        try
        {
            _titleShadow = _titleBlock.RenderShadowMask(radius, _layout.Width);
        }
        catch
        {
            _titleShadow = null;
        }
    }

    /// <summary>Runtime check used by the capture matrix: centres within 1 px, no overlaps.</summary>
    public IReadOnlyList<string> ValidateLayout()
    {
        var issues = new List<string>();
        if (_content is null) return ["no content"];
        var center = _layout.Width / 2;
        var boxes = _content.InOrder().ToArray();
        foreach (var (name, box) in boxes)
        {
            if (Math.Abs(box.CenterX - center) > 1)
                issues.Add($"{name} centre {box.CenterX:0.##} != {center:0.##}");
            if (box.X < 0 || box.Right > _layout.Width + 0.001)
                issues.Add($"{name} horizontally out of bounds");
        }
        for (var i = 0; i < boxes.Length - 1; i++)
        {
            if (boxes[i].Box.Bottom > boxes[i + 1].Box.Y + 0.501)
                issues.Add($"{boxes[i].Name} overlaps {boxes[i + 1].Name}");
        }
        return issues;
    }

    private static Rectangle ToRectangle(LockBox box) => new(
        (int)Math.Round(box.X), (int)Math.Round(box.Y),
        Math.Max(1, (int)Math.Round(box.Width)), Math.Max(1, (int)Math.Round(box.Height)));

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        LockSceneRenderer.DrawStars(g, _layout, _clock.Elapsed.TotalSeconds);
        LockSceneRenderer.DrawSleepingZ(g, _layout, _clock.Elapsed.TotalSeconds);

        if (_content is null) return;
        var centerX = _layout.Width / 2;

        if (_titleShadow is not null)
        {
            var x = (float)(centerX - _titleShadow.Width / 2.0);
            var y = (float)(_content.Title.Y - _shadowPad + 3 * _layout.UiScale);
            g.DrawImage(_titleShadow, x, y);
        }

        using (var white = new SolidBrush(Color.White))
            _titleBlock?.Draw(g, white, centerX, _content.Title.Y);
        using (var mint = new SolidBrush(Color.FromArgb(0x7e, 0xe8, 0xc4)))
            _reasonBlock?.Draw(g, mint, centerX, _content.Reason.Y);
        using (var pale = new SolidBrush(Color.FromArgb(0xc8, 0xea, 0xdf)))
            _hintBlock?.Draw(g, pale, centerX, _content.Hint.Y);

        if (Logo is not null)
        {
            var box = _layout.Logo;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(Logo, new Rectangle((int)Math.Round(box.X), (int)Math.Round(box.Y),
                Math.Max(1, (int)Math.Round(box.Width)), Math.Max(1, (int)Math.Round(box.Height))));
        }
    }

    protected override bool ShowWithoutActivation => !PreviewMode;

    private const int WmGetMinMaxInfo = 0x0024;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!PreviewMode) return;
        // Windows clamps a window to the desktop's max track size. Captures on
        // the XPS run in a 1024x768 session but must render 1600x900 and
        // 1620x1080, so allow the requested size for the preview only.
        SetWindowPos(Handle, IntPtr.Zero, Left, Top, _requestedWidth, _requestedHeight,
            SwpNoZOrder | SwpNoActivate);
    }

    protected override void WndProc(ref Message m)
    {
        if (PreviewMode && m.Msg == WmGetMinMaxInfo)
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
            info.MaxTrackSize.X = Math.Max(info.MaxTrackSize.X, _requestedWidth + 64);
            info.MaxTrackSize.Y = Math.Max(info.MaxTrackSize.Y, _requestedHeight + 64);
            Marshal.StructureToPtr(info, m.LParam, false);
            return;
        }
        base.WndProc(ref m);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (!PreviewMode)
            {
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            }
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _zTimer.Dispose();
            _background?.Dispose();
            _titleShadow?.Dispose();
            _titleFont?.Dispose();
            _reasonFont?.Dispose();
            _hintFont?.Dispose();
            _primaryFont?.Dispose();
            _secondaryFont?.Dispose();
        }
        base.Dispose(disposing);
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
        catch
        {
            return null;
        }
    }

    private sealed class PillButton : Button
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Primary { get; init; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool MessageIcon { get; init; }

        public PillButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 0 || Height <= 0) return;
            using var path = Rounded(new Rectangle(0, 0, Width, Height), Height / 2);
            Region = new Region(path);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Rounded(rect, Height / 2);
            if (Primary)
            {
                using var shadow = new SolidBrush(Color.FromArgb(0x0f, 0x7a, 0x37));
                var lift = Math.Max(3, Height / 12);
                using var shadowPath = Rounded(new Rectangle(0, lift, Width - 1, Height - 1 - lift),
                    (Height - lift) / 2);
                g.FillPath(shadow, shadowPath);
                using var fill = new SolidBrush(Color.FromArgb(0x16, 0xa3, 0x4a));
                using var face = Rounded(new Rectangle(0, 0, Width - 1, Height - 1 - lift),
                    (Height - lift) / 2);
                g.FillPath(fill, face);
            }
            else
            {
                using var fill = new SolidBrush(Color.FromArgb(90, 11, 29, 32));
                g.FillPath(fill, path);
                using var border = new Pen(Color.FromArgb(0x4f, 0xd1, 0xa5), 2);
                g.DrawPath(border, path);
            }

            var liftPx = Math.Max(3, Height / 12);
            var ink = Primary ? Color.White : Color.FromArgb(0xd8, 0xf5, 0xea);
            var iconSize = MessageIcon ? Math.Max(16, Height * 32 / 72) : 0;
            var iconGap = MessageIcon ? Math.Max(8, Height * 14 / 72) : 0;
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            var textWidth = LockTextBlock.MeasureWidth(g, Text, Font);
            var contentWidth = iconSize + iconGap + textWidth;
            var startX = (float)((Width - contentWidth) / 2);
            if (MessageIcon)
            {
                var iy = (Height - liftPx - iconSize) / 2f;
                LockGlyphs.DrawMessageIcon(g, (float)startX, (float)iy, (float)iconSize, ink, Math.Max(1.8f, Height / 32f));
                startX += iconSize + iconGap;
            }
            var textRect = new RectangleF((float)startX, 0, (float)(Width - startX), Height - liftPx);
            using var brush = new SolidBrush(ink);
            g.DrawString(Text, Font, brush, textRect, format);
        }

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
    }
}
