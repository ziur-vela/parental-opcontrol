// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Reflection;
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
    private readonly Label _title;
    private readonly Label _reason;
    private readonly Label _hint;
    private readonly PillButton _message;
    private readonly PillButton _override;
    private readonly System.Windows.Forms.Timer _zTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private LockSceneLayout _layout;
    private Bitmap? _background;
    private Size _backgroundSize;
    private float _fontScale = -1f;
    private bool _previewMode;

    public event Action? OverrideClicked;
    public event Action? MessageClicked;

    public bool IsPrimary { get; }
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

        _title = LabelFor(Theme.DisplayPx(56, FontStyle.Bold), Color.White);
        _reason = LabelFor(Theme.DisplayPx(32, FontStyle.Regular), Color.FromArgb(0x7e, 0xe8, 0xc4));
        _hint = LabelFor(Theme.BodyPx(22, FontStyle.Bold), Color.FromArgb(0xc8, 0xea, 0xdf));
        _title.AutoSize = true;
        _reason.AutoSize = true;
        _hint.AutoSize = true;
        _message = new PillButton
        {
            Primary = true,
            MessageIcon = true,
            Text = Loc.T("Client.Messages.Open"),
            Font = Theme.DisplayPx(26, FontStyle.Bold)
        };
        _message.Click += (_, _) => MessageClicked?.Invoke();
        _override = new PillButton
        {
            Primary = false,
            Text = Loc.T("Client.Lock.Override"),
            Font = Theme.BodyPx(18, FontStyle.Bold)
        };
        _override.Click += (_, _) => OverrideClicked?.Invoke();

        Controls.Add(_title);
        Controls.Add(_reason);
        Controls.Add(_hint);
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
        _title.Font?.Dispose();
        _reason.Font?.Dispose();
        _hint.Font?.Dispose();
        _message.Font?.Dispose();
        _override.Font?.Dispose();
        _title.Font = Theme.DisplayPx(56 * ui, FontStyle.Bold);
        _reason.Font = Theme.DisplayPx(32 * ui, FontStyle.Regular);
        _hint.Font = Theme.BodyPx(22 * ui, FontStyle.Bold);
        _message.Font = Theme.DisplayPx(26 * ui, FontStyle.Bold);
        _override.Font = Theme.BodyPx(18 * ui, FontStyle.Bold);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Relayout();
    }

    private static Label LabelFor(Font font, Color color) => new()
    {
        AutoSize = false,
        BackColor = Color.Transparent,
        ForeColor = color,
        Font = font,
        TextAlign = ContentAlignment.MiddleCenter,
        UseCompatibleTextRendering = false
    };

    public void UpdateState(RuntimeLockStateDto state)
    {
        var (title, reason, hint) = Texts(state);
        _title.Text = title;
        _reason.Text = reason;
        _hint.Text = hint;
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
        _layout = LockSceneLayout.For(ClientSize.Width, ClientSize.Height, DeviceDpi / 96.0);
        ApplyTextFonts();
        var scale = (float)_layout.Scale;
        var ui = (float)_layout.UiScale;

        var textLeft = (int)_layout.TextColumn.X;
        var textWidth = (int)_layout.TextColumn.Width;
        _title.MaximumSize = new Size(textWidth, 0);
        _reason.MaximumSize = new Size(textWidth, 0);
        _hint.MaximumSize = new Size(textWidth, 0);
        _title.Location = new Point(textLeft, (int)_layout.TextColumn.Y);
        _reason.Location = new Point(textLeft, _title.Bottom + (int)(18 * ui));
        _hint.Location = new Point(textLeft, _reason.Bottom + (int)(10 * ui));
        _title.Width = textWidth;
        _reason.Width = textWidth;
        _hint.Width = textWidth;

        _message.Bounds = new Rectangle(
            (int)_layout.PrimaryButton.X,
            (int)_layout.PrimaryButton.Y,
            (int)_layout.PrimaryButton.Width,
            (int)_layout.PrimaryButton.Height);
        _override.Bounds = new Rectangle(
            (int)_layout.SecondaryButton.X,
            (int)_layout.SecondaryButton.Y,
            (int)_layout.SecondaryButton.Width,
            (int)_layout.SecondaryButton.Height);
        _message.Invalidate();
        _override.Invalidate();

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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        LockSceneRenderer.DrawStars(e.Graphics, _layout, _clock.Elapsed.TotalSeconds);
        LockSceneRenderer.DrawSleepingZ(e.Graphics, _layout, _clock.Elapsed.TotalSeconds);
        DrawTitleShadow(e.Graphics);
        if (Logo is not null)
        {
            var box = _layout.Logo;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(Logo, new Rectangle((int)Math.Round(box.X), (int)Math.Round(box.Y),
                Math.Max(1, (int)Math.Round(box.Width)), Math.Max(1, (int)Math.Round(box.Height))));
        }
    }

    protected override bool ShowWithoutActivation => !PreviewMode;

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
        }
        base.Dispose(disposing);
    }

    private void DrawTitleShadow(Graphics g)
    {
        if (string.IsNullOrEmpty(_title.Text)) return;
        var offset = (int)Math.Round(3 * _layout.UiScale);
        var rect = new RectangleF(_title.Left, _title.Top, _title.Width, Math.Max(1, _title.Height + offset));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near,
            FormatFlags = StringFormatFlags.LineLimit
        };
        using var shadow = new SolidBrush(Color.FromArgb(95, 0, 0, 0));
        g.DrawString(_title.Text, _title.Font, shadow,
            new RectangleF(rect.X, rect.Y + offset, rect.Width, rect.Height), format);
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
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Rounded(rect, Height / 2);
            if (Primary)
            {
                using var shadow = new SolidBrush(Color.FromArgb(0x0f, 0x7a, 0x37));
                var lift = Math.Max(3, Height / 12);
                using var shadowPath = Rounded(new Rectangle(0, lift, Width - 1, Height - 1 - lift),
                    (Height - lift) / 2);
                e.Graphics.FillPath(shadow, shadowPath);
                using var fill = new SolidBrush(Color.FromArgb(0x16, 0xa3, 0x4a));
                using var face = Rounded(new Rectangle(0, 0, Width - 1, Height - 1 - lift),
                    (Height - lift) / 2);
                e.Graphics.FillPath(fill, face);
            }
            else
            {
                using var fill = new SolidBrush(Color.FromArgb(90, 11, 29, 32));
                e.Graphics.FillPath(fill, path);
                using var border = new Pen(Color.FromArgb(0x4f, 0xd1, 0xa5), 2);
                e.Graphics.DrawPath(border, path);
            }

            var iconSize = Math.Max(18, Height / 3);
            var iconSpace = MessageIcon ? iconSize + 14 : 0;
            if (MessageIcon)
            {
                var ink = Primary ? Color.White : Color.FromArgb(0xd8, 0xf5, 0xea);
                var textWidth = TextRenderer.MeasureText(Text, Font).Width;
                var ix = (Width - textWidth - iconSpace) / 2;
                var lift = Math.Max(3, Height / 12);
                var iy = (Height - lift - iconSize) / 2f;
                LockGlyphs.DrawMessageIcon(e.Graphics, ix, iy, iconSize, ink, Math.Max(1.8f, Height / 32f));
            }
            var textRect = new Rectangle(MessageIcon ? iconSpace : 0, 0, Width - (MessageIcon ? iconSpace : 0), Height - Math.Max(3, Height / 12));
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect,
                Primary ? Color.White : Color.FromArgb(0xd8, 0xf5, 0xea),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
