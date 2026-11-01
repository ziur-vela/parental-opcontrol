// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Diagnostics;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

/// <summary>
/// Luna-style lock countdown pill above the taskbar clock. It never activates,
/// sits on top, and only slides/tucks; clicking opens My OpControl.
/// </summary>
public sealed class CountdownForm : Form
{
    private const float SlideSeconds = 0.45f;

    private readonly float _factor;
    private readonly System.Windows.Forms.Timer _animate;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ToolTip _toolTip = new();
    private readonly Graphics _measure;
    private readonly Bitmap _measureBitmap;

    private PillArt.Layout _layout;
    private string _theme = "night";
    private LockTimerPhase _phase = LockTimerPhase.Normal;
    private string _label = "Lock in";
    private string _time = "0:00";
    private LockTimerSettings _settings = LockTimerSettings.Default;
    private bool _visible;
    private bool _hover;
    private bool _reduced;
    private double _outSince;
    private float _slide;
    private float _slideFrom;
    private float _slideTo;
    private double _slideStarted;
    private bool _sliding;

    public event EventHandler? Clicked;

    public CountdownForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);

        _factor = Math.Max(1f, Dpi.FactorFor(Screen.PrimaryScreen ?? Screen.AllScreens[0]));
        _measureBitmap = new Bitmap(1, 1);
        _measure = Graphics.FromImage(_measureBitmap);
        _layout = PillArt.Measure(_measure, _label, _time);
        _reduced = KidMotion.Reduced;
        ApplyBounds();

        _animate = new System.Windows.Forms.Timer { Interval = 33 };
        _animate.Tick += (_, _) => Animate();
        Click += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
    }

    public bool Tucked => _slideTo > 0.5f;

    /// <summary>Called from the helper tick with the same time the old banner used.</summary>
    public void Update(TimeSpan remaining, string label, LockTimerSettings settings, string theme, string? language = null)
    {
        settings = settings.Normalize();
        _settings = settings;
        _reduced = KidMotion.Reduced;
        var time = LockTimerFormat.Format(remaining, settings, language);
        var phase = LockTimerFormat.Phase(remaining);
        var newLayout = PillArt.Measure(_measure, label, time);
        var layoutChanged = Math.Abs(newLayout.Width - _layout.Width) > 0.5f ||
                            !string.Equals(label, _label, StringComparison.Ordinal) ||
                            !string.Equals(time, _time, StringComparison.Ordinal);

        _label = label;
        _time = time;
        _phase = phase;
        _theme = theme;
        if (layoutChanged)
        {
            _layout = newLayout;
            ApplyBounds();
        }

        _toolTip.SetToolTip(this, Loc.T("Client.Pill.Open"));
        AccessibleName = $"{label} {time}";
        if (_reduced && _animate.Enabled) _animate.Stop();

        if (!settings.Enabled)
        {
            HideBanner();
            return;
        }

        if (!_visible)
        {
            _visible = true;
            ShowWithoutActivationSafe();
            _outSince = Now;
            _slide = 0f;
            _slideFrom = 0f;
            _slideTo = 0f;
            _sliding = false;
            ApplyRegion();
        }

        // Under five minutes and in the last minute the pill is always out.
        if (phase != LockTimerPhase.Normal)
        {
            StartSlide(0f);
        }
        else if (!settings.Tuck)
        {
            ForceOut();
        }
        else if (!_hover)
        {
            var visibleFor = Now - _outSince;
            if (_slideTo < 0.5f && _slide <= 0.001f && visibleFor >= settings.TuckAfterSeconds)
                StartSlide(1f);
        }

        if (_visible && !_reduced && !_animate.Enabled) _animate.Start();
        Invalidate();
    }

    public void HideBanner()
    {
        if (!_visible && !Visible) return;
        _visible = false;
        _sliding = false;
        _slide = 0f;
        _slideTo = 0f;
        if (Visible) Hide();
    }

    /// <summary>Debug/preview helper: hold the pill fully tucked or out.</summary>
    public void ForceTuck(bool tucked)
    {
        ForceOut();
        if (tucked)
        {
            _slide = 1f;
            _slideTo = 1f;
            ApplyRegion();
            Invalidate();
        }
    }

    private void ForceOut()
    {
        _slide = 0f;
        _slideTo = 0f;
        _sliding = false;
        ApplyRegion();
    }

    private static double Now => Environment.TickCount64 / 1000.0;

    private void StartSlide(float target)
    {
        if (Math.Abs(_slideTo - target) < 0.01f && !_sliding) return;
        _slideFrom = _slide;
        _slideTo = target;
        _slideStarted = Now;
        _sliding = true;
        if (_reduced)
        {
            _slide = target;
            _sliding = false;
            ApplyRegion();
            Invalidate();
            return;
        }
        _animate.Start();
    }

    private void Animate()
    {
        if (!_visible)
        {
            _animate.Stop();
            return;
        }

        var t = (float)Math.Clamp((Now - _slideStarted) / SlideSeconds, 0, 1);
        if (_sliding)
        {
            var eased = EaseOutBack(t);
            _slide = _slideFrom + (_slideTo - _slideFrom) * eased;
            if (t >= 1f)
            {
                _slide = _slideTo;
                _sliding = false;
            }
            ApplyRegion();
        }

        if (!_sliding && _phase == LockTimerPhase.Normal && _settings.Tuck && !_hover)
        {
            var visibleFor = Now - _outSince;
            if (_slideTo < 0.5f && _slide <= 0.001f && visibleFor >= _settings.TuckAfterSeconds)
                StartSlide(1f);
        }

        Invalidate();
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        var u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private void ApplyBounds()
    {
        var area = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        var width = (int)Math.Ceiling(_layout.Width * _factor);
        var height = (int)Math.Ceiling((PillArt.Height + PillArt.Shadow) * _factor);
        Size = new Size(width, height);
        Location = new Point(
            area.Right - width + (int)Math.Round(28 * _factor),
            area.Bottom - height - (int)Math.Round(8 * _factor));
        ApplyRegion();
    }

    private void ApplyRegion()
    {
        if (!IsHandleCreated) return;
        var offset = PillArt.SlideOffset(_layout, _slide) * _factor;
        var radius = PillArt.Height * _factor / 2f;
        var bottom = (PillArt.Height + PillArt.Shadow) * _factor;
        var shadowTop = PillArt.Shadow * _factor;
        using var pill = PillArt.Path(offset, 0, Width + 2, bottom - shadowTop, radius);
        using var shadow = PillArt.Path(offset, shadowTop, Width + 2, bottom, radius);
        using var path = new System.Drawing.Drawing2D.GraphicsPath(System.Drawing.Drawing2D.FillMode.Winding);
        path.AddPath(pill, false);
        path.AddPath(shadow, false);
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    private void ShowWithoutActivationSafe()
    {
        if (!Visible) Show();
        if (!_animate.Enabled && !_reduced) _animate.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.ScaleTransform(_factor, _factor);
        var visual = new PillArt.Visual(
            _theme,
            _phase,
            _label,
            _time,
            _slide,
            _reduced,
            Now,
            DateTime.Now);
        PillArt.Draw(e.Graphics, _layout, visual);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // The window region is the pill; do not fill the rectangle.
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        _outSince = Now;
        if (_visible && _slideTo > 0.01f) StartSlide(0f);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _outSince = Now;
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyBounds();
        Invalidate();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animate.Dispose();
            _toolTip.Dispose();
            _measure.Dispose();
            _measureBitmap.Dispose();
            Region?.Dispose();
        }
        base.Dispose(disposing);
    }
}
