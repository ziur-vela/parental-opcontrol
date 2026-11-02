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
    private const float MessageHoldSeconds = 4f;
    private const float MessageFadeSeconds = 0.25f;
    private const float MorphSeconds = 0.45f;
    private const float WidthSeconds = 0.30f;
    private const float TextTravel = 12f;

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

    private float _displayWidth;
    private float _widthFrom;
    private float _widthTo;
    private double _widthStarted;
    private bool _widthAnim;
    private string? _message;
    private float _messageWidth;
    private double _messageStarted;
    private bool _messageActive;
    private bool _messageHideAfter;
    private bool _transitioning;
    private double _transitionStarted;
    private float _messageAlpha;
    private float _normalAlpha = 1f;
    private float _normalOffset;
    private bool _suppressCountdown;

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
        _displayWidth = _layout.Width;
        _reduced = KidMotion.Reduced;
        ApplyBounds();

        _animate = new System.Windows.Forms.Timer { Interval = 33 };
        _animate.Tick += (_, _) => Animate();
        Click += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
    }

    public bool Tucked => _slideTo > 0.5f;

    public bool ShowingMessage => _messageActive || _transitioning;

    /// <summary>Called from the helper tick with the same time the old banner used.</summary>
    public void Update(TimeSpan remaining, string label, LockTimerSettings settings, string theme, string? language = null, string? status = null)
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

        var tooltipStatus = string.IsNullOrWhiteSpace(status) ? $"{label} {time}".Trim() : status;
        _toolTip.SetToolTip(this, Loc.T("Client.Pill.Tooltip", tooltipStatus));
        AccessibleName = $"{label} {time}";

        if (layoutChanged)
        {
            _layout = newLayout;
            if (!_messageActive && !_transitioning)
            {
                _displayWidth = newLayout.Width;
                ApplyBounds();
            }
        }

        if (_reduced && !_messageActive && !_transitioning && _animate.Enabled) _animate.Stop();
        AdvanceMessage();
        if (_phase != LockTimerPhase.Normal) _suppressCountdown = false;

        if (!_messageActive && !_transitioning && _suppressCountdown)
        {
            HideWindow();
            return;
        }

        var messageShowing = _messageActive || _transitioning;
        if (!settings.Enabled && !messageShowing && phase == LockTimerPhase.Normal)
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

        if (messageShowing)
        {
            StartSlide(0f);
        }
        else if (phase != LockTimerPhase.Normal)
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

        if (_visible && (!_reduced || _messageActive || _transitioning) && !_animate.Enabled) _animate.Start();
        Invalidate();
    }

    /// <summary>Shows a short status message, then morphs back into the countdown.</summary>
    public void ShowStatus(string message, bool hideAfter = false)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _message = message;
        _messageHideAfter = hideAfter;
        _messageStarted = Now;
        _messageActive = true;
        _transitioning = false;
        _suppressCountdown = false;
        _messageWidth = PillArt.MessageWidth(_measure, message);
        _toolTip.SetToolTip(this, Loc.T("Client.Pill.Tooltip", message));
        AccessibleName = message;

        if (_reduced)
        {
            _displayWidth = _messageWidth;
            _widthAnim = false;
            _messageAlpha = 1f;
            _normalAlpha = 0f;
            _normalOffset = 0f;
        }
        else
        {
            _messageAlpha = 0f;
            _normalAlpha = 0f;
            _widthFrom = _displayWidth;
            _widthTo = _messageWidth;
            _widthStarted = Now;
            _widthAnim = true;
        }

        if (!_visible)
        {
            _visible = true;
            ShowWithoutActivationSafe();
            _slide = 0f;
            _slideTo = 0f;
            _sliding = false;
        }

        ForceOut();
        _outSince = Now;
        ApplyBounds();
        _animate.Start();
        Invalidate();
    }

    public void HideBanner()
    {
        ClearMessage();
        HideWindow();
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

    private void ClearMessage()
    {
        _messageActive = false;
        _transitioning = false;
        _message = null;
        _messageAlpha = 0f;
        _normalAlpha = 1f;
        _normalOffset = 0f;
        _widthAnim = false;
        _suppressCountdown = false;
        _displayWidth = _layout.Width;
    }

    private void HideWindow()
    {
        _visible = false;
        _sliding = false;
        _slide = 0f;
        _slideTo = 0f;
        if (Visible) Hide();
        _animate.Stop();
    }

    private void ForceOut()
    {
        _slide = 0f;
        _slideTo = 0f;
        _sliding = false;
        ApplyRegion();
    }

    private static double Now => Environment.TickCount64 / 1000.0;

    private void AdvanceMessage()
    {
        if (_transitioning)
        {
            var progress = _reduced ? 1f : (float)Math.Clamp((Now - _transitionStarted) / MorphSeconds, 0, 1);
            if (progress >= 1f) FinishMorph();
            return;
        }

        if (_messageActive && Now - _messageStarted >= MessageHoldSeconds)
            BeginMorph();
    }

    private void BeginMorph()
    {
        _transitioning = true;
        _transitionStarted = Now;
        _widthFrom = _displayWidth;
        _widthTo = _layout.Width;
        _widthStarted = Now;
        _widthAnim = true;
        if (!_reduced) return;

        _messageAlpha = 0f;
        _normalAlpha = 1f;
        _normalOffset = 0f;
        FinishMorph();
    }

    private void FinishMorph()
    {
        _transitioning = false;
        _messageActive = false;
        _message = null;
        _messageAlpha = 0f;
        _normalAlpha = 1f;
        _normalOffset = 0f;
        _displayWidth = _layout.Width;
        _widthAnim = false;
        _suppressCountdown = _messageHideAfter || (!_settings.Enabled && _phase == LockTimerPhase.Normal);
        _messageHideAfter = false;
        _outSince = Now;
        if (_suppressCountdown) HideWindow(); else ApplyBounds();
    }

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

        if (_widthAnim)
        {
            var t = (float)Math.Clamp((Now - _widthStarted) / WidthSeconds, 0, 1);
            var eased = t * t * (3f - 2f * t);
            _displayWidth = _widthFrom + (_widthTo - _widthFrom) * eased;
            if (t >= 1f)
            {
                _displayWidth = _widthTo;
                _widthAnim = false;
            }
            ApplyBounds();
        }

        if (_transitioning)
        {
            var p = (float)Math.Clamp((Now - _transitionStarted) / MorphSeconds, 0, 1);
            _messageAlpha = 1f - p;
            _normalAlpha = p;
            _normalOffset = TextTravel * (1f - p);
            if (p >= 1f) FinishMorph();
        }
        else if (_messageActive)
        {
            var fade = (float)Math.Clamp((Now - _messageStarted) / MessageFadeSeconds, 0, 1);
            _messageAlpha = fade;
            _normalAlpha = 0f;
            _normalOffset = 0f;
            if (Now - _messageStarted >= MessageHoldSeconds) BeginMorph();
        }
        else
        {
            _messageAlpha = 0f;
            _normalAlpha = 1f;
            _normalOffset = 0f;
        }

        if (!_visible)
        {
            _animate.Stop();
            return;
        }

        var t2 = (float)Math.Clamp((Now - _slideStarted) / SlideSeconds, 0, 1);
        if (_sliding)
        {
            var eased = EaseOutBack(t2);
            _slide = _slideFrom + (_slideTo - _slideFrom) * eased;
            if (t2 >= 1f)
            {
                _slide = _slideTo;
                _sliding = false;
            }
            ApplyRegion();
        }

        if (!_sliding && !_messageActive && !_transitioning && _phase == LockTimerPhase.Normal && _settings.Tuck && !_hover)
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
        var width = (int)Math.Ceiling(_displayWidth * _factor);
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
        var offset = PillArt.SlideOffset(_layout with { Width = _displayWidth }, _slide) * _factor;
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
        var layout = _layout with { Width = _displayWidth };
        var phase = (_messageActive || _transitioning) && _phase == LockTimerPhase.Normal
            ? LockTimerPhase.Normal
            : _phase;
        var visual = new PillArt.Visual(
            _theme,
            phase,
            _label,
            _time,
            _slide,
            _reduced,
            Now,
            DateTime.Now,
            _message,
            _messageAlpha,
            _normalAlpha,
            _normalOffset);
        PillArt.Draw(e.Graphics, layout, visual);
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
