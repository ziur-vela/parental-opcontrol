using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Platform;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Luna;

public partial class LunaWindow : Window
{
    private enum Mode
    {
        Sleep,
        Waking,
        Awake,
        Retract,
        MorphOpen,
        Chat,
        MorphClose
    }

    private const double SlideDistance = 80;
    private const double ScreenMargin = LunaChatSize.ScreenMargin;
    private const double DefaultSleepScale = 1.6;
    private const double MinSleepScale = 1.0;
    private const double MaxSleepScale = 3.0;
    private const double SleepScaleStep = 0.1;
    private const double SleepVisibleDip = 28;
    private const double SleepRegionSize = 48;
    private const double ZTravelPerAxis = 11.3;
    private const int HotkeyBigger = 0x4C41;
    private const int HotkeySmaller = 0x4C42;
    private const int HotkeyReset = 0x4C43;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _tick;
    private readonly DispatcherTimer _memoryTimer;

    private Canvas _moonSlide = null!;
    private Canvas _moonBob = null!;
    private Canvas _moonPop = null!;
    private Canvas _happyEyes = null!;
    private Ellipse _happyEyeLeft = null!;
    private Ellipse _happyEyeRight = null!;
    private Canvas _sleepyFace = null!;
    private Canvas _happyFace = null!;
    private Canvas _yawnFace = null!;
    private Canvas _surpriseFace = null!;
    private Canvas _yawnMouth = null!;
    private Canvas _scene = null!;
    private Canvas _sleepLayer = null!;
    private Border _blob = null!;
    private Border _blobYellow = null!;
    private Grid _chatRoot = null!;
    private LunaAppView _chat = null!;
    private LunaApiClient? _api;
    private LunaGreetingBubble _greeting = null!;
    private Canvas _zLayer = null!;
    private readonly TextBlock[] _zText = new TextBlock[3];
    private readonly TranslateTransform[] _zMove = new TranslateTransform[3];
    private readonly RotateTransform[] _zRotate = new RotateTransform[3];
    private readonly ScaleTransform[] _zScale = new ScaleTransform[3];

    private readonly TranslateTransform _slideTransform = new();
    private readonly TranslateTransform _bobTransform = new();
    private readonly ScaleTransform _popTransform = new();
    private readonly TranslateTransform _eyesTransform = new();
    private readonly ScaleTransform _eyeLeftScale = new();
    private readonly ScaleTransform _eyeRightScale = new();
    private readonly ScaleTransform _yawnScale = new();
    private readonly ScaleTransform _sleepTransform = new();
    private readonly ScaleTransform _greetingScale = new(1, 1);
    private RectangleGeometry? _blobClip;
    private double _blobX = 12;
    private double _blobY = 12;
    private double _blobW = 96;
    private double _blobH = 96;
    private double _chatWidth = LunaChatSize.MinWidth;
    private double _chatHeight = LunaChatSize.MinHeight;
    private double _lastResizeLog = -1;
    private double _sleepScale = DefaultSleepScale;

    private Mode _mode = Mode.Sleep;
    private double _wakeStart;
    private double _retractStart;
    private double _morphStart;
    private double _hoverStart = -1;
    private double _leaveStart = -1;
    private double _autoSleepAt = -1;
    private double _slide;
    private double _bob;
    private double _sleepyOpacity = 1;
    private double _happyOpacity;
    private double _yawnOpacity;
    private double _surpriseOpacity;
    private double _bubbleOpacity;
    private double _lastTick;
    private bool _bubbleTargetOn;
    private double _bubbleAnimStart;
    private double _bubbleScale = 1;
    private double _bubbleSuppressUntil = -1;
    private string _greetingTheme = "night";
    private double _greetingCheckAt = -1;

    private IntPtr _hwnd;
    private Win32.SubclassProc? _hotkeyProc;
    private IntPtr _hotkeyWnd = IntPtr.Zero;
    private bool _hotkeysHooked;
    private int _lastRgnX1 = int.MinValue;
    private int _lastRgnY1 = int.MinValue;
    private int _lastRgnX2 = int.MinValue;
    private int _lastRgnY2 = int.MinValue;
    private int _lastRgnRadius = -1;
    private string _lastRgnShape = string.Empty;
    private bool _lastRgnBubbleVisible;
    private int _lastRgnBubbleX1 = int.MinValue;
    private int _lastRgnBubbleY1 = int.MinValue;
    private int _lastRgnBubbleX2 = int.MinValue;
    private int _lastRgnBubbleY2 = int.MinValue;
    private bool _hitProofSleepLogged;
    private bool _hitProofAwakeLogged;
    private string _lastLoggedRegion = string.Empty;

    private bool _probeActive;
    private double _probeStart;
    private double _probeUntil;
    private int _probeFrames;
    private int _morphFrames;
    private double _morphLogStart;
    private bool _startupLogged;
    private bool _shots;
    private readonly bool[] _morphShotTaken = new bool[5];
    private static readonly double[] MorphShotAt = { 0.15, 0.35, 0.55, 0.75, 0.95 };
    private readonly bool[] _closeShotTaken = new bool[6];
    private static readonly double[] CloseShotAt = { 0.06, 0.20, 0.40, 0.60, 0.80, 0.95 };
    private int _lastMemoryLogSecond;
    private DateTime _processStart;
    private bool _hasProcessStart;

    public LunaWindow()
    {
        InitializeComponent();

        _moonSlide = this.FindControl<Canvas>("MoonSlide")!;
        _moonBob = this.FindControl<Canvas>("MoonBob")!;
        _moonPop = this.FindControl<Canvas>("MoonPop")!;
        _happyEyes = this.FindControl<Canvas>("HappyEyes")!;
        _happyEyeLeft = this.FindControl<Ellipse>("HappyEyeLeft")!;
        _happyEyeRight = this.FindControl<Ellipse>("HappyEyeRight")!;
        _sleepyFace = this.FindControl<Canvas>("SleepyFace")!;
        _happyFace = this.FindControl<Canvas>("HappyFace")!;
        _yawnFace = this.FindControl<Canvas>("YawnFace")!;
        _surpriseFace = this.FindControl<Canvas>("SurpriseFace")!;
        _yawnMouth = this.FindControl<Canvas>("YawnMouth")!;
        _scene = this.FindControl<Canvas>("Scene")!;
        _sleepLayer = this.FindControl<Canvas>("SleepLayer")!;
        _blob = this.FindControl<Border>("Blob")!;
        _blobYellow = this.FindControl<Border>("BlobYellow")!;
        _chatRoot = this.FindControl<Grid>("ChatRoot")!;
        _api = new LunaApiClient();
        _chat = new LunaAppView(_api, null);
        _chatRoot.Children.Add(_chat);
        _chat.ThemeChanged += (_, _) => UpdateGreetingContent();
        _greeting = this.FindControl<LunaGreetingBubble>("GreetingBubble")!;
        _greeting.RenderTransform = _greetingScale;
        _zLayer = this.FindControl<Canvas>("ZLayer")!;

        _zText[0] = this.FindControl<TextBlock>("Z0")!;
        _zText[1] = this.FindControl<TextBlock>("Z1")!;
        _zText[2] = this.FindControl<TextBlock>("Z2")!;
        for (var i = 0; i < 3; i++)
        {
            _zMove[i] = new TranslateTransform();
            _zRotate[i] = new RotateTransform();
            _zScale[i] = new ScaleTransform(1, 1);
            _zText[i].RenderTransform = new TransformGroup
            {
                Children = { _zMove[i], _zRotate[i], _zScale[i] }
            };
            _zText[i].RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        }

        _moonSlide.RenderTransform = _slideTransform;
        _moonBob.RenderTransform = _bobTransform;
        _moonPop.RenderTransform = _popTransform;
        _happyEyes.RenderTransform = _eyesTransform;
        _happyEyeLeft.RenderTransform = _eyeLeftScale;
        _happyEyeRight.RenderTransform = _eyeRightScale;
        _yawnMouth.RenderTransform = _yawnScale;

        _sleepLayer.RenderTransform = _sleepTransform;
        _sleepLayer.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);

        _blobClip = new RectangleGeometry(new Rect(0, 0, 96, 96)) { RadiusX = 48, RadiusY = 48 };
        _blob.Clip = _blobClip;
        SetBlob(12, 12, 96, 96, 48);

        _sleepScale = LoadSleepScale();
        _sleepTransform.ScaleX = _sleepScale;
        _sleepTransform.ScaleY = _sleepScale;

        _chat.CloseRequested += (_, _) => CloseChat();
        _chat.ResizeRequested += ResizeChat;

        try
        {
            _processStart = Process.GetCurrentProcess().StartTime;
            _hasProcessStart = true;
        }
        catch
        {
            _hasProcessStart = false;
        }

#if LUNA_DEV
        if (App.LunaOpenChatPreview)
        {
            Opened += async (_, _) =>
            {
                await Task.Delay(900);
                OpenChat();
            };
        }
        if (!string.IsNullOrWhiteSpace(App.LunaScreenshotPath))
        {
            Opened += async (_, _) =>
            {
                await Task.Delay(2800);
                CapturePreview(App.LunaScreenshotPath);
            };
        }
#endif
        Opened += OnOpened;
        Closed += (_, _) =>
        {
            UnregisterHotkeys();
            _api?.Dispose();
        };
        PointerPressed += OnPointerPressed;
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            var point = e.GetCurrentPoint(this);
            LunaLog.Write($"pointer_pressed mode={_mode} left={point.Properties.IsLeftButtonPressed} " +
                           $"right={point.Properties.IsRightButtonPressed} pos={point.Position} handled={e.Handled}");
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyDown += OnKeyDown;

        _lastTick = _clock.Elapsed.TotalSeconds;
        _tick = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);
        _tick.Start();

        _memoryTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, OnMemoryTick);
        _memoryTimer.Start();

        LunaLog.Write($"process_start offset_ms={(long)_clock.Elapsed.TotalMilliseconds} pid={Environment.ProcessId}");
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        try
        {
        PlaceAtCorner();
        _hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        if (OperatingSystem.IsWindows() && _hwnd != IntPtr.Zero)
        {
            var style = Win32.GetWindowLongPtr(_hwnd, Win32.GwlExStyle).ToInt64();
            Win32.SetWindowLongPtr(_hwnd, Win32.GwlExStyle, new IntPtr(style | Win32.WsExToolWindow));
            Win32.SetWindowPos(_hwnd, Win32.HwndTopmost, 0, 0, 0, 0,
                Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoActivate);
            LunaLog.Write($"hwnd=0x{_hwnd.ToInt64():X} exstyle=0x{style | Win32.WsExToolWindow:X}");
        }

        var screen = Screens.Primary;
        InitializeChatSize(screen);
        UpdateGreetingContent();
        LunaLog.Write(
            $"startup screen={screen?.Bounds.Width}x{screen?.Bounds.Height}@{screen?.Bounds.X},{screen?.Bounds.Y} " +
            $"work={screen?.WorkingArea.Width}x{screen?.WorkingArea.Height}@{screen?.WorkingArea.X},{screen?.WorkingArea.Y} " +
            $"scaling={RenderScaling:0.##} window={Width}x{Height} pos={Position.X},{Position.Y}");

        StartProbe(2.0);
        Dispatcher.UIThread.Post(() => TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFirstFrame),
            DispatcherPriority.Loaded);
        ResetToSleep(updateRegion: true);
        RegisterHotkeys();
        _chat.Start();
        _ = WatchDeviceLockAsync();
        if (Environment.GetCommandLineArgs().Contains("--open", StringComparer.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(2200);
                Dispatcher.UIThread.Post(OpenChat);
                var themeArg = ArgValue("--theme");
                if (!string.IsNullOrWhiteSpace(themeArg))
                {
                    await Dispatcher.UIThread.InvokeAsync(() => _chat.SetThemeKeyAsync(themeArg));
                }
                var screen = ArgValue("--screen");
                if (string.Equals(screen, "chats", StringComparison.OrdinalIgnoreCase))
                {
                    await Dispatcher.UIThread.InvokeAsync(() => _chat.ShowMyChats());
                }
                var chatTitle = ArgValue("--chat-title");
                if (!string.IsNullOrWhiteSpace(chatTitle))
                {
                    await Task.Delay(1000);
                    await Dispatcher.UIThread.InvokeAsync(() => _chat.OpenChatByTitleAsync(chatTitle));
                }
                var subject = ArgValue("--chat");
                if (!string.IsNullOrWhiteSpace(subject))
                {
                    await Task.Delay(1600);
                    await Dispatcher.UIThread.InvokeAsync(() => _chat.OpenSubjectAsync(subject));
#if LUNA_DEV
                    if (Environment.GetCommandLineArgs().Contains("--thinking", StringComparer.OrdinalIgnoreCase))
                    {
                        await Task.Delay(300);
                        Dispatcher.UIThread.Post(() => _chat.ShowThinkingDemo("What's a volcano?"));
                    }
#endif
                    var send = ArgValue("--send");
                    if (!string.IsNullOrWhiteSpace(send))
                    {
                        await Task.Delay(400);
                        Dispatcher.UIThread.Post(() => _chat.Send(send));
                    }
                }
            });
        }

        _shots = Environment.GetCommandLineArgs().Contains("--shots");
        if (Environment.GetCommandLineArgs().Contains("--autotest"))
        {
            LunaLog.Write($"autotest_begin shots={_shots}");
            _ = RunAutotest();
        }

        if (Environment.GetCommandLineArgs().Contains("--sleepdiag"))
        {
            _ = RunSleepDiag();
        }
        }
        catch (Exception ex)
        {
            LunaLog.Write("OPENED_EXCEPTION " + ex);
            throw;
        }
    }

    private async Task RunAutotest()
    {
        try
        {
            await Task.Delay(800);
            Capture("autotest-asleep.png");
            await Task.Delay(700);
            StartWake(_clock.Elapsed.TotalSeconds);
            await Task.Delay(1400);
            Capture("autotest-awake.png");
            await Task.Delay(900);
            OpenChat();
            await Task.Delay(1400);
            _chat.Send("Help with math");
            await Task.Delay(3200);
            Capture("autotest-chat.png");
            Dispatcher.UIThread.Post(LogChatLayout, DispatcherPriority.Loaded);
            ResizeChat(210, 300);
            await Task.Delay(600);
            Capture("autotest-resized.png");
            Dispatcher.UIThread.Post(LogChatLayout, DispatcherPriority.Loaded);
            await Task.Delay(500);
            CloseChat();
            await Task.Delay(1800);
            Capture("autotest-closed.png");
            await Task.Delay(600);
            LunaLog.Write("autotest_end");
            Exit();
        }
        catch (Exception ex)
        {
            LunaLog.Write("autotest_exception " + ex);
            Exit();
        }
    }

    private async Task RunSleepDiag()
    {
        try
        {
            _shots = true;
            var hideZ = Environment.GetCommandLineArgs().Contains("--hide-z");
            var frames = int.TryParse(ArgValue("--frames"), out var f) && f > 0 ? f : 1;
            var interval = int.TryParse(ArgValue("--interval"), out var iv) && iv >= 0 ? iv : 500;
            var prefix = ArgValue("--prefix") ?? "sleepdiag";
            if (hideZ)
            {
                _zLayer.IsVisible = false;
            }

            LogSleepDiagState(hideZ);
            await Task.Delay(900);
            for (var i = 0; i < frames; i++)
            {
                Capture($"{prefix}-{i:000}.png");
                if (i < frames - 1)
                {
                    await Task.Delay(interval);
                }
            }

            LogSleepDiagHitTest(prefix);
            LunaLog.Write($"sleepdiag_end prefix={prefix} frames={frames} hide_z={hideZ}");
            Exit();
        }
        catch (Exception ex)
        {
            LunaLog.Write("sleepdiag_exception " + ex);
            Exit();
        }
    }

#if LUNA_DEV
    private void CapturePreview(string path)
    {
        try
        {
            var content = _scene;
            var scale = 1.5;
            var width = Math.Max(1, (int)Math.Round(content.Bounds.Width * scale));
            var height = Math.Max(1, (int)Math.Round(content.Bounds.Height * scale));
            var pixel = new PixelSize(width, height);
            var dpi = new Vector(96 * scale, 96 * scale);
            var contentBitmap = new RenderTargetBitmap(pixel, dpi);
            contentBitmap.Render(content);
            var output = new RenderTargetBitmap(pixel, dpi);
            using (var context = output.CreateDrawingContext())
            {
                context.FillRectangle(Brushes.Magenta, new Rect(0, 0, width, height));
                context.DrawImage(contentBitmap, new Rect(0, 0, width, height));
            }
            output.Save(path);
            LunaLog.Write($"luna_screenshot saved={path} size={width}x{height}");
        }
        catch (Exception ex)
        {
            LunaLog.Write("luna_screenshot_failed " + ex);
        }
    }
#endif

    private static string? ArgValue(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private void LogSleepDiagState(bool hideZ)
    {
        var k = _sleepScale;
        LunaLog.Write(
            $"sleepdiag_begin scale={k:0.0} hide_z={hideZ} region_dip={SleepRegionSize * k:0.#} " +
            $"z_start_dip=4,4 z_travel_per_axis_dip={ZTravelPerAxis * k:0.##} " +
            $"z_travel_total_dip={ZTravelPerAxis * k * Math.Sqrt(2):0.##} " +
            $"z_end_dip={(4 + ZTravelPerAxis) * k:0.##},{(4 + ZTravelPerAxis) * k:0.##}");
    }

    private void LogSleepDiagHitTest(string tag)
    {
        if (!OperatingSystem.IsWindows() || _hwnd == IntPtr.Zero)
        {
            return;
        }

        var s = RenderScaling;
        var pos = Position;
        var k = _sleepScale;
        var endDip = 4 + ZTravelPerAxis;
        var probes = new (string Name, double DipX, double DipY, bool ExpectMoon)[]
        {
            ("moon_10k", 10, 10, true),
            ("around_40k", 40, 40, false),
            ("zend_15k", endDip, endDip, false),
            ("far_300", 300, 300, false)
        };

        foreach (var probe in probes)
        {
            var sx = pos.X + (int)(probe.DipX * k * s);
            var sy = pos.Y + (int)(probe.DipY * k * s);
            var point = new Win32.Point { X = sx, Y = sy };
            var lp = ((long)(sy & 0xFFFF) << 16) | (uint)(sx & 0xFFFF);
            var ht = Win32.SendMessage(_hwnd, Win32.WmNcHitTest, IntPtr.Zero, new IntPtr(lp)).ToInt64();
            var wfp = Win32.WindowFromPoint(point);
            LunaLog.Write(
                $"sleepdiag_hittest tag={tag} scale={k:0.0} probe={probe.Name} " +
                $"screen={sx},{sy} expected_moon={probe.ExpectMoon} wm_nchittest=0x{ht:X} " +
                $"transparent={ht == Win32.HtTransparent} windowfrompoint_ours={IsOurs(wfp)} " +
                $"windowfrompoint=0x{wfp.ToInt64():X}");
        }
    }

    private void Capture(string name)
    {
        if (!_shots)
        {
            return;
        }

        try
        {
            var scale = RenderScaling;
            var size = new PixelSize(
                Math.Max(1, (int)Math.Ceiling(Bounds.Width * scale)),
                Math.Max(1, (int)Math.Ceiling(Bounds.Height * scale)));
            using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
            bitmap.Render(this);
            var dir = OperatingSystem.IsWindows()
                ? @"C:\Temp\LunaDev\shots"
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LunaDev", "shots");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, name);
            using (var stream = System.IO.File.Create(path)) bitmap.Save(stream);
            LunaLog.Write("capture path=" + path);
        }
        catch (Exception ex)
        {
            LunaLog.Write("capture_failed " + ex);
        }
    }

    private void OnFirstFrame(TimeSpan _)
    {
        if (_startupLogged)
        {
            return;
        }

        _startupLogged = true;
        if (_hasProcessStart)
        {
            var elapsed = (DateTime.Now - _processStart).TotalMilliseconds;
            LunaLog.Write($"startup_ms={elapsed:0} first_frame=1");
        }
        else
        {
            LunaLog.Write($"startup_ms={_clock.Elapsed.TotalMilliseconds:0} first_frame=1 (process start unavailable)");
        }
    }

    private void PlaceAtCorner()
    {
        var work = Screens.Primary?.WorkingArea;
        if (work is { } w)
        {
            Position = w.Position;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        try
        {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Max(0.001, Math.Min(0.1, now - _lastTick));
        _lastTick = now;

        if (now - _greetingCheckAt >= 1.0)
        {
            _greetingCheckAt = now;
            var local = KidThemeStore.ReadLocal();
            if (local is not null && !string.Equals(local, _greetingTheme, StringComparison.Ordinal))
            {
                UpdateGreetingContent();
            }
        }

        var overMoon = IsCursorOverMoon();
        var overBubble = IsCursorOverBubble();

        switch (_mode)
        {
            case Mode.Sleep:
                _slide = 0;
                _hoverStart = overMoon ? (_hoverStart < 0 ? now : _hoverStart) : -1;
                if (_hoverStart >= 0 && now - _hoverStart >= 0.4)
                {
                    StartWake(now);
                }
                break;

            case Mode.Waking:
                _slide = EaseOutBack(Math.Clamp((now - _wakeStart) / 0.5, 0, 1));
                if (now - _wakeStart >= 0.5)
                {
                    _mode = Mode.Awake;
                    _slide = 1;
                    _leaveStart = overMoon ? -1 : now;
                    UpdateRegion();
                    LogHitProof(awake: true);
                }
                break;

            case Mode.Awake:
                _slide = 1;
                if (overMoon || overBubble)
                {
                    _leaveStart = -1;
                }
                else if (_leaveStart < 0)
                {
                    _leaveStart = now;
                }
                else if (now - _leaveStart >= 3.0)
                {
                    StartRetract(now);
                }

                if (_autoSleepAt > 0 && now >= _autoSleepAt)
                {
                    _autoSleepAt = -1;
                    StartRetract(now);
                }
                break;

            case Mode.Retract:
                _slide = 1 - EaseInCubic(Math.Clamp((now - _retractStart) / 0.5, 0, 1));
                if (now - _retractStart >= 0.5)
                {
                    ResetToSleep(updateRegion: true);
                }
                break;

            case Mode.MorphOpen:
                UpdateMorphOpen(now);
                break;

            case Mode.Chat:
                break;

            case Mode.MorphClose:
                UpdateMorphClose(now);
                break;
        }

        UpdateVisuals(now, dt, overMoon, overBubble);
        UpdateRegion();
        }
        catch (Exception ex)
        {
            LunaLog.Write("TICK_EXCEPTION " + ex);
            throw;
        }
    }

    private void StartWake(double now)
    {
        _mode = Mode.Waking;
        _wakeStart = now;
        _hoverStart = -1;
        _leaveStart = -1;
        _autoSleepAt = -1;
        EnsureTopmost();
        StartProbe(2.6);
        LunaLog.Write("wake_start hover_ms=400");
    }

    private void StartRetract(double now)
    {
        _mode = Mode.Retract;
        _retractStart = now;
        _leaveStart = -1;
        _autoSleepAt = -1;
        LunaLog.Write("sleep_start leave_ms=3000");
    }

    private void ResetToSleep(bool updateRegion)
    {
        _mode = Mode.Sleep;
        _slide = 0;
        _hoverStart = -1;
        _leaveStart = -1;
        _autoSleepAt = -1;
        _sleepyOpacity = 1;
        _happyOpacity = 0;
        _yawnOpacity = 0;
        _surpriseOpacity = 0;
        _bubbleOpacity = 0;
        _bubbleTargetOn = false;
        _bubbleScale = 1;
        _bob = 0;
        _blob.IsVisible = false;
        SetBlobChrome(true);
        _chatRoot.IsVisible = false;
        _greeting.Opacity = 0;
        if (updateRegion)
        {
            UpdateRegion();
            LogHitProof(awake: false);
        }
    }

    private void InitializeChatSize(Screen? screen)
    {
        var scale = RenderScaling > 0 ? RenderScaling : 1.0;
        var workWidth = screen is { } s ? s.WorkingArea.Width / scale : 1920;
        var workHeight = screen is { } s2 ? s2.WorkingArea.Height / scale : 1080;
        var saved = LoadChatSize();
        var size = saved is { } value
            ? LunaChatSize.Clamp(value.Width, value.Height, workWidth, workHeight)
            : LunaChatSize.Default(workWidth, workHeight);
        _chatWidth = size.Width;
        _chatHeight = size.Height;
        ApplyWindowSize(_chatWidth, _chatHeight);
        LunaLog.Write($"chat_size source={(saved is null ? "default" : "saved")} " +
                      $"{_chatWidth:0.#}x{_chatHeight:0.#} work={workWidth:0.#}x{workHeight:0.#} scaling={scale:0.##}");
    }

    private void ClampChatSize()
    {
        var max = MaxChatSize();
        _chatWidth = Math.Clamp(_chatWidth, Math.Min(LunaChatSize.MinWidth, max.Width), max.Width);
        _chatHeight = Math.Clamp(_chatHeight, Math.Min(LunaChatSize.MinHeight, max.Height), max.Height);
    }

    private (double Width, double Height) MaxChatSize()
    {
        var screen = Screens.Primary;
        var scale = RenderScaling > 0 ? RenderScaling : 1.0;
        var workWidth = screen is { } s ? s.WorkingArea.Width / scale : 1920;
        var workHeight = screen is { } s2 ? s2.WorkingArea.Height / scale : 1080;
        var max = LunaChatSize.Max(workWidth, workHeight);
        return (max.Width, max.Height);
    }

    private void ApplyWindowSize(double w, double h)
    {
        if (Math.Abs(Width - w) > 0.1)
        {
            Width = w;
        }

        if (Math.Abs(Height - h) > 0.1)
        {
            Height = h;
        }

        _scene.Width = w;
        _scene.Height = h;
    }

    private void ResizeChat(double dx, double dy)
    {
        if (_mode != Mode.Chat)
        {
            return;
        }

        var (maxW, maxH) = MaxChatSize();
        var w = Math.Clamp(_chatWidth + dx, Math.Min(LunaChatSize.MinWidth, maxW), maxW);
        var h = Math.Clamp(_chatHeight + dy, Math.Min(LunaChatSize.MinHeight, maxH), maxH);
        if (Math.Abs(w - _chatWidth) < 0.1 && Math.Abs(h - _chatHeight) < 0.1)
        {
            return;
        }

        _chatWidth = w;
        _chatHeight = h;
        ApplyWindowSize(w, h);
        SetBlob(0, 0, w, h, 0);
        UpdateRegion();
        SaveChatSize();

        var now = _clock.Elapsed.TotalSeconds;
        if (now - _lastResizeLog > 0.25)
        {
            _lastResizeLog = now;
            LunaLog.Write($"chat_resize {w:0.#}x{h:0.#} max={maxW:0.#}x{maxH:0.#}");
        }
    }

    private void LogChatLayout()
    {
        var cs = ClientSize;
        LunaLog.Write(
            $"chat_layout window_dip={Width:0.#}x{Height:0.#} client_dip={cs.Width:0.#}x{cs.Height:0.#} " +
            $"scaling={RenderScaling:0.##} blob={_blob.Width:0.#}x{_blob.Height:0.#} " +
            $"blob_visible={_blob.IsVisible} chat_visible={_chatRoot.IsVisible} {_chat.LayoutInfo()}");
    }

    private void OpenChat()
    {
        LunaLog.Write($"open_chat_requested mode={_mode}");
        if (_mode is Mode.MorphOpen or Mode.Chat or Mode.MorphClose)
        {
            return;
        }

        _mode = Mode.MorphOpen;
        _morphStart = _clock.Elapsed.TotalSeconds;
        _morphLogStart = _morphStart;
        _morphFrames = 0;
        _hoverStart = -1;
        _leaveStart = -1;
        _autoSleepAt = -1;
        ClampChatSize();
        ApplyWindowSize(_chatWidth, _chatHeight);
        _blob.IsVisible = true;
        SetBlobChrome(true);
        _chatRoot.Opacity = 0;
        _chatRoot.IsVisible = false;
        _blobYellow.Opacity = 1;
        SetBlob(12, 12, 96, 96, 48);
        EnsureTopmost();
        StartProbe(2.5);
        LunaLog.Write($"morph_open_begin from_mode slide={_slide:0.###}");
    }

    internal bool ChatRootVisibleForTest => _chatRoot.IsVisible && _blob.IsVisible;
    internal string ModeForTest => _mode.ToString();
    internal double BubbleOpacityForTest => _bubbleOpacity;
    internal double BubbleScaleForTest => _bubbleScale;
    internal LunaGreetingBubble GreetingForTest => _greeting;

    internal void OpenChatForTest() => OpenChat();

    internal void WakeForTest() => StartWake(_clock.Elapsed.TotalSeconds);

    internal void SetGreetingThemeForTest(string theme)
    {
        UpdateGreetingContent(theme);
    }

    /// <summary>Headless tests drive the animation frame directly (no native timer).</summary>
    internal void TickForTest() => OnTick(null, EventArgs.Empty);

    private void CloseChat()
    {
        if (_mode != Mode.Chat)
        {
            return;
        }

        _mode = Mode.MorphClose;
        _morphStart = _clock.Elapsed.TotalSeconds;
        _morphLogStart = _morphStart;
        _morphFrames = 0;
        _blob.IsVisible = true;
        SetBlobChrome(true);
        StartProbe(1.5);
        LunaLog.Write("morph_close_begin");
    }

    private void UpdateMorphOpen(double now)
    {
        var t = Math.Clamp((now - _morphStart) / 0.9, 0, 1);
        var (x, y, w, h, r) = MorphRect(t, opening: true, _chatWidth, _chatHeight);
        SetBlob(x, y, w, h, r);

        _blobYellow.Opacity = YellowAlphaAt(t);
        _chatRoot.Opacity = t < 0.65 ? 0 : Math.Clamp((t - 0.65) / 0.35, 0, 1);
        _chatRoot.IsVisible = t >= 0.65;

        if (_shots)
        {
            for (var i = 0; i < MorphShotAt.Length; i++)
            {
                if (!_morphShotTaken[i] && t >= MorphShotAt[i])
                {
                    _morphShotTaken[i] = true;
                    Capture($"morph-{(int)(MorphShotAt[i] * 100):000}.png");
                }
            }
        }

        if (t >= 1)
        {
            _mode = Mode.Chat;
            SetBlob(0, 0, _chatWidth, _chatHeight, 0);
            _blobYellow.Opacity = 0;
            // ChatRoot lives inside Blob, so Blob must stay visible. Only its dark
            // chrome is removed; the chat's own rounded border is the background.
            _blob.IsVisible = true;
            SetBlobChrome(false);
            _chatRoot.Opacity = 1;
            _chatRoot.IsVisible = true;
            UpdateRegion();
            Activate();
            _chat.FocusInput();
            Dispatcher.UIThread.Post(LogChatLayout, DispatcherPriority.Loaded);
            LogMorph("morph_open");
        }
    }

    private void UpdateMorphClose(double now)
    {
        var t = Math.Clamp((now - _morphStart) / 0.9, 0, 1);
        var u = 1 - t;
        var (x, y, w, h, r) = MorphRect(u, opening: true, _chatWidth, _chatHeight);
        SetBlob(x, y, w, h, r);

        var fade = 0.12 / 0.9;
        _chatRoot.Opacity = t < fade ? 1 - t / fade : 0;
        _chatRoot.IsVisible = t < fade;
        _blobYellow.Opacity = YellowAlphaAt(u);

        _scene.InvalidateVisual();

        if (_shots)
        {
            for (var i = 0; i < CloseShotAt.Length; i++)
            {
                if (!_closeShotTaken[i] && t >= CloseShotAt[i])
                {
                    _closeShotTaken[i] = true;
                    Capture($"close-{(int)(t * 100):000}.png");
                }
            }
        }

        if (t >= 1)
        {
            _blob.IsVisible = false;
            SetBlobChrome(true);
            _greeting.Opacity = 0;
            _bubbleOpacity = 0;
            _bubbleTargetOn = false;
            _bubbleScale = 1;
            _bubbleSuppressUntil = now + 0.9;
            _mode = Mode.Awake;
            _slide = 1;
            _wakeStart = now - 2.5;
            _leaveStart = now;
            _autoSleepAt = now + 0.6;
            UpdateRegion();
            LogMorph("morph_close");
        }
    }

    private static double YellowAlphaAt(double t) =>
        t <= 0.2 ? 1 : t < 0.6 ? 1 - (t - 0.2) / 0.4 : 0;


    private void LogMorph(string name)
    {
        var seconds = Math.Max(0.001, _clock.Elapsed.TotalSeconds - _morphLogStart);
        var fps = _morphFrames / seconds;
        LunaLog.Write($"{name}_end frames={_morphFrames} seconds={seconds:0.###} fps={fps:0.#}");
        _morphLogStart = 0;
    }

    private void SetBlobChrome(bool visible)
    {
        if (visible)
        {
            _blob.Background = new SolidColorBrush(Color.Parse("#0e1d19"));
            _blob.BorderBrush = new SolidColorBrush(Color.Parse("#2a4a42"));
            _blob.BorderThickness = new Thickness(1);
        }
        else
        {
            _blob.Background = Brushes.Transparent;
            _blob.BorderThickness = new Thickness(0);
        }
    }

    private void SetBlob(double x, double y, double w, double h, double r)
    {
        _blobX = x;
        _blobY = y;
        _blobW = w;
        _blobH = h;
        Canvas.SetLeft(_blob, x);
        Canvas.SetTop(_blob, y);
        _blob.Width = w;
        _blob.Height = h;
        _blob.CornerRadius = new CornerRadius(r);
        if (_blobClip is null)
        {
            _blobClip = new RectangleGeometry(new Rect(0, 0, w, h)) { RadiusX = r, RadiusY = r };
            _blob.Clip = _blobClip;
        }
        else
        {
            _blobClip.Rect = new Rect(0, 0, w, h);
            _blobClip.RadiusX = r;
            _blobClip.RadiusY = r;
        }
    }

    private void UpdateVisuals(double now, double dt, bool overMoon, bool overBubble)
    {
        var elapsed = _wakeStart > 0 ? now - _wakeStart : 0;

        double slide = _slide;

        if (_mode is Mode.Awake)
        {
            _bob = -3 * (1 - Math.Cos(now * 2 * Math.PI / 2.8)) / 2;
        }
        else
        {
            _bob = 0;
        }

        _slideTransform.X = slide * SlideDistance;
        _slideTransform.Y = slide * SlideDistance + _bob;
        _bobTransform.Y = 0;

        var yawnTarget = 0.0;
        var surpriseTarget = 0.0;
        var happyTarget = 0.0;
        var sleepyTarget = 1.0;

        if (_mode is Mode.Waking or Mode.Awake)
        {
            sleepyTarget = 0;
            yawnTarget = elapsed >= 0.4 && elapsed < 1.2 ? 1 : 0;
            surpriseTarget = elapsed >= 1.2 && elapsed < 1.9 ? 1 : 0;
            happyTarget = elapsed >= 1.9 ? 1 : 0;
        }
        else if (_mode is Mode.MorphOpen or Mode.Chat or Mode.MorphClose)
        {
            sleepyTarget = 0;
            happyTarget = 0;
        }
        else if (_mode is Mode.Retract)
        {
            sleepyTarget = Math.Clamp((now - _retractStart) / 0.5, 0, 1);
        }

        _sleepyOpacity = Approach(_sleepyOpacity, sleepyTarget, dt, 0.15);
        _yawnOpacity = Approach(_yawnOpacity, yawnTarget, dt, 0.15);
        _surpriseOpacity = Approach(_surpriseOpacity, surpriseTarget, dt, 0.15);
        _happyOpacity = Approach(_happyOpacity, happyTarget, dt, 0.15);

        _sleepyFace.Opacity = _sleepyOpacity;
        _yawnFace.Opacity = _yawnOpacity;
        _surpriseFace.Opacity = _surpriseOpacity;
        _happyFace.Opacity = _happyOpacity;

        var yawnP = Math.Clamp((elapsed - 0.4) / 1.2, 0, 1);
        var (ysx, ysy) = yawnP switch
        {
            < 0.4 => (Lerp(0.6, 1, yawnP / 0.4), Lerp(0.15, 1, yawnP / 0.4)),
            < 0.75 => (1.0, 1.0),
            _ => (Lerp(1, 0.6, (yawnP - 0.75) / 0.25), Lerp(1, 0.2, (yawnP - 0.75) / 0.25))
        };
        _yawnScale.ScaleX = ysx;
        _yawnScale.ScaleY = ysy;

        var pop = 1.0;
        if ((_mode is Mode.Waking or Mode.Awake) && elapsed >= 0.4)
        {
            var surpriseP = (elapsed - 1.2) / 0.35;
            if (surpriseP is >= 0 and < 1)
            {
                pop = surpriseP < 0.4
                    ? Lerp(1, 1.1, surpriseP / 0.4)
                    : Lerp(1.1, 1, (surpriseP - 0.4) / 0.6);
            }
        }
        _popTransform.ScaleX = pop;
        _popTransform.ScaleY = pop;

        if ((_mode is Mode.Waking or Mode.Awake) && elapsed >= 1.9)
        {
            var lookP = ((elapsed - 1.9 - 0.6) % 5.0 + 5.0) % 5.0 / 5.0;
            _eyesTransform.X = LookOffset(lookP);
        }
        else
        {
            _eyesTransform.X = 0;
        }

        var blinkPeriod = elapsed % 4.2;
        var eyeScale = 1.0;
        if (elapsed >= 1.9)
        {
            if (blinkPeriod is >= 3.864 and < 3.99)
            {
                eyeScale = Lerp(1, 0.1, (blinkPeriod - 3.864) / 0.126);
            }
            else if (blinkPeriod is >= 3.99 and < 4.2)
            {
                eyeScale = Lerp(0.1, 1, (blinkPeriod - 3.99) / 0.21);
            }
        }
        _eyeLeftScale.ScaleY = eyeScale;
        _eyeRightScale.ScaleY = eyeScale;

        var moonHidden = _mode is Mode.MorphOpen or Mode.Chat or Mode.MorphClose;
        _moonSlide.Opacity = moonHidden
            ? (_mode == Mode.MorphOpen ? Math.Max(0, 1 - Math.Max(0, (now - _morphStart) / 0.08)) : 0)
            : 1;

        var sleepScaleNow = Lerp(_sleepScale, 1, Math.Clamp(slide, 0, 1));
        _sleepTransform.ScaleX = sleepScaleNow;
        _sleepTransform.ScaleY = sleepScaleNow;

        var zTarget = _mode is Mode.Sleep or Mode.Retract
            ? (_mode is Mode.Retract ? Math.Clamp((now - _retractStart) / 0.5, 0, 1) : 1)
            : 0;
        _zLayer.Opacity = zTarget;
        UpdateZ(now);

        var bubbleTargetOn = _mode is Mode.Awake && elapsed >= 1.9 && _slide >= 0.99 &&
                             now >= _bubbleSuppressUntil;
        if (bubbleTargetOn != _bubbleTargetOn)
        {
            _bubbleTargetOn = bubbleTargetOn;
            _bubbleAnimStart = now;
            if (bubbleTargetOn)
            {
                UpdateGreetingContent();
            }
        }

        if (LunaMotion.ReducedMotion)
        {
            _bubbleOpacity = Approach(_bubbleOpacity, bubbleTargetOn ? 1 : 0, dt, bubbleTargetOn ? 0.15 : 0.08);
            _bubbleScale = 1;
        }
        else if (bubbleTargetOn)
        {
            var p = Math.Clamp((now - _bubbleAnimStart) / 0.3, 0, 1);
            _bubbleOpacity = Math.Clamp(p * 3, 0, 1);
            _bubbleScale = PopScale(p);
        }
        else
        {
            _bubbleOpacity = Approach(_bubbleOpacity, 0, dt, 0.07);
            _bubbleScale = Lerp(0.3, 1, _bubbleOpacity);
            if (_bubbleOpacity < 0.01)
            {
                _bubbleOpacity = 0;
                _bubbleScale = 1;
            }
        }

        _greeting.Opacity = _bubbleOpacity;
        _greetingScale.ScaleX = _bubbleScale;
        _greetingScale.ScaleY = _bubbleScale;
        _greeting.IsHitTestVisible = false;
    }

    private static double PopScale(double p) => p switch
    {
        < 0.5 => Lerp(0.3, 1.08, EaseOutCubic(p / 0.5)),
        < 0.75 => Lerp(1.08, 0.97, (p - 0.5) / 0.25),
        _ => Lerp(0.97, 1, (p - 0.75) / 0.25)
    };

    private void UpdateZ(double now)
    {
        for (var i = 0; i < 3; i++)
        {
            var t = ((now / 3.0) + i / 3.0) % 1.0;
            var opacity = t < 0.15 ? t / 0.15 : t < 0.7 ? Lerp(1, 0.85, (t - 0.15) / 0.55) : Lerp(0.85, 0, (t - 0.7) / 0.3);
            _zText[i].Opacity = opacity;
            _zMove[i].X = ZTravelPerAxis * t;
            _zMove[i].Y = ZTravelPerAxis * t;
            _zRotate[i].Angle = -10 + 22 * t;
            var s = 0.7 + 0.4 * t;
            _zScale[i].ScaleX = s;
            _zScale[i].ScaleY = s;
        }
    }

    private bool IsPointOverSleepMoon(double screenX, double screenY)
    {
        var s = RenderScaling;
        var x = (screenX - Position.X) / s;
        var y = (screenY - Position.Y) / s;
        var k = _sleepScale;
        var dx = x + 20 * k;
        var dy = y + 20 * k;
        return dx * dx + dy * dy <= 48 * k * 48 * k;
    }

    private bool IsCursorOverMoon()
    {
        if (!OperatingSystem.IsWindows() || _hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (!Win32.GetCursorPos(out var p))
        {
            return false;
        }

        var s = RenderScaling;
        var pos = Position;
        var x = (p.X - pos.X) / s;
        var y = (p.Y - pos.Y) / s;

        if (_mode == Mode.Sleep)
        {
            var k = _sleepScale;
            return x >= -4 * k && x <= 44 * k && y >= -4 * k && y <= 44 * k;
        }

        var cx = -20 + _slide * SlideDistance;
        var cy = -20 + _slide * SlideDistance + _bob;
        var dx = x - cx;
        var dy = y - cy;
        return dx * dx + dy * dy <= 58 * 58;
    }

    private void UpdateGreetingContent(string? themeOverride = null)
    {
        var local = KidThemeStore.ReadLocal();
        _greetingTheme = themeOverride ?? local ?? _greetingTheme;
        var first = Loc.T("Luna.Greeting.Line1");
        if (string.Equals(first, "Luna.Greeting.Line1", StringComparison.Ordinal))
        {
            first = Loc.Language == Loc.Spanish ? "¡Hola, soy Luna!" : "Hi, I'm Luna!";
        }

        var second = Loc.T("Luna.Greeting.Line2");
        if (string.Equals(second, "Luna.Greeting.Line2", StringComparison.Ordinal))
        {
            second = Loc.Language == Loc.Spanish
                ? "¿Tienes una pregunta? ¡Te escucho!"
                : "Got a question? I'm all ears!";
        }

        _greeting.Configure(_greetingTheme, first, second);
        PlaceGreeting();
        UpdateRegion();
    }

    private void PlaceGreeting()
    {
        var screen = Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var scale = RenderScaling > 0 ? RenderScaling : 1.0;
        var viewWidth = screen.WorkingArea.Width / scale;
        var viewHeight = screen.WorkingArea.Height / scale;
        var placement = LunaGreetingLayout.Compute(
            60, 56,
            _greeting.TotalWidth, _greeting.TotalHeight, _greeting.BodyHeight,
            viewWidth, viewHeight);
        _greeting.SetTailSide(placement.TailSide);
        Canvas.SetLeft(_greeting, placement.Left);
        Canvas.SetTop(_greeting, placement.Top);
    }

    private Rect GreetingBounds()
    {
        var left = Canvas.GetLeft(_greeting);
        var top = Canvas.GetTop(_greeting);
        if (double.IsNaN(left))
        {
            left = 0;
        }

        if (double.IsNaN(top))
        {
            top = 0;
        }

        return new Rect(left, top, _greeting.TotalWidth, _greeting.TotalHeight);
    }

    private bool IsCursorOverBubble()
    {
        if (_bubbleOpacity < 0.2 || !OperatingSystem.IsWindows() || _hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (!Win32.GetCursorPos(out var p))
        {
            return false;
        }

        var s = RenderScaling;
        var pos = Position;
        var x = (p.X - pos.X) / s;
        var y = (p.Y - pos.Y) / s;
        var bubble = GreetingBounds().Inflate(3);
        return bubble.Contains(new Point(x, y));
    }

    private void UpdateRegion()
    {
        if (!OperatingSystem.IsWindows() || _hwnd == IntPtr.Zero)
        {
            return;
        }

        var s = RenderScaling;
        string shape;
        int x1, y1, x2, y2;
        var radius = 0.0;

        switch (_mode)
        {
            case Mode.Sleep:
                shape = "sliver";
                x1 = 0; y1 = 0;
                x2 = (int)Math.Ceiling(SleepRegionSize * _sleepScale * s);
                y2 = (int)Math.Ceiling(SleepRegionSize * _sleepScale * s);
                break;

            case Mode.Waking:
                shape = "waking";
                x1 = 0; y1 = 0;
                x2 = (int)Math.Ceiling(200 * s); y2 = (int)Math.Ceiling(200 * s);
                break;

            case Mode.MorphOpen:
                shape = "blob";
                radius = _blob.CornerRadius.TopLeft;
                x1 = (int)Math.Floor(_blobX * s);
                y1 = (int)Math.Floor(_blobY * s);
                x2 = (int)Math.Ceiling((_blobX + _blobW) * s);
                y2 = (int)Math.Ceiling((_blobY + _blobH) * s);
                break;

            case Mode.MorphClose:
                shape = "closing";
                radius = _blob.CornerRadius.TopLeft;
                x1 = 0; y1 = 0;
                x2 = (int)Math.Ceiling(_chatWidth * s);
                y2 = (int)Math.Ceiling(_chatHeight * s);
                break;

            case Mode.Chat:
                shape = "full";
                radius = 22;
                x1 = 0; y1 = 0;
                x2 = (int)Math.Ceiling(_chatWidth * s); y2 = (int)Math.Ceiling(_chatHeight * s);
                break;

            case Mode.Retract:
                shape = "retract";
                var retractScale = Lerp(1, _sleepScale, Math.Clamp(_slide, 0, 1));
                var retractReach = Math.Max(118, (28 + 80 * _slide) * retractScale) + 4;
                x1 = 0; y1 = 0;
                x2 = (int)Math.Ceiling(retractReach * s);
                y2 = (int)Math.Ceiling((retractReach + 8) * s);
                break;

            default:
                shape = "moon";
                var cx = (-20 + _slide * SlideDistance) * s;
                var cy = (-20 + _slide * SlideDistance + _bob) * s;
                var moonRadius = 58 * s;
                x1 = (int)Math.Floor(cx - moonRadius);
                y1 = (int)Math.Floor(cy - moonRadius);
                x2 = (int)Math.Ceiling(cx + moonRadius);
                y2 = (int)Math.Ceiling(cy + moonRadius);
                break;
        }

        var radiusPx = (int)Math.Ceiling(radius * 2 * s);
        var bubbleVisible = shape == "moon" && _bubbleOpacity > 0.1;
        var bx1 = 0;
        var by1 = 0;
        var bx2 = 0;
        var by2 = 0;
        if (bubbleVisible)
        {
            var bubble = GreetingBounds().Inflate(2);
            bx1 = (int)Math.Floor(bubble.Left * s);
            by1 = (int)Math.Floor(bubble.Top * s);
            bx2 = (int)Math.Ceiling(bubble.Right * s);
            by2 = (int)Math.Ceiling(bubble.Bottom * s);
        }

        if (shape == _lastRgnShape && x1 == _lastRgnX1 && y1 == _lastRgnY1 && x2 == _lastRgnX2 &&
            y2 == _lastRgnY2 && radiusPx == _lastRgnRadius && bubbleVisible == _lastRgnBubbleVisible &&
            bx1 == _lastRgnBubbleX1 && by1 == _lastRgnBubbleY1 && bx2 == _lastRgnBubbleX2 &&
            by2 == _lastRgnBubbleY2)
        {
            return;
        }

        _lastRgnShape = shape;
        _lastRgnX1 = x1;
        _lastRgnY1 = y1;
        _lastRgnX2 = x2;
        _lastRgnY2 = y2;
        _lastRgnRadius = radiusPx;
        _lastRgnBubbleVisible = bubbleVisible;
        _lastRgnBubbleX1 = bx1;
        _lastRgnBubbleY1 = by1;
        _lastRgnBubbleX2 = bx2;
        _lastRgnBubbleY2 = by2;

        IntPtr region;
        if (shape == "moon")
        {
            region = Win32.CreateEllipticRgn(x1, y1, x2, y2);
            if (bubbleVisible)
            {
                var bubble = Win32.CreateRectRgn(bx1, by1, bx2, by2);
                if (bubble != IntPtr.Zero)
                {
                    Win32.CombineRgn(region, region, bubble, Win32.RgnOr);
                    Win32.DeleteObject(bubble);
                }
            }
        }
        else if (radiusPx > 0)
        {
            region = Win32.CreateRoundRectRgn(x1, y1, x2, y2, radiusPx, radiusPx);
        }
        else
        {
            region = Win32.CreateRectRgn(x1, y1, x2, y2);
        }

        if (region != IntPtr.Zero)
        {
            var setResult = Win32.SetWindowRgn(_hwnd, region, true);
            if (setResult == 0)
            {
                var err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                LunaLog.Write($"setwindowrgn_failed shape={shape} rect={x1},{y1},{x2},{y2} err={err}");
                Win32.DeleteObject(region);
            }
            else if (_lastLoggedRegion != shape)
            {
                _lastLoggedRegion = shape;
                LunaLog.Write($"setwindowrgn_ok shape={shape} rect={x1},{y1},{x2},{y2}");
            }
        }
    }

    private void LogHitProof(bool awake)
    {
        if (!OperatingSystem.IsWindows() || _hwnd == IntPtr.Zero)
        {
            return;
        }

        if (awake ? _hitProofAwakeLogged : _hitProofSleepLogged)
        {
            return;
        }

        var s = RenderScaling;
        var pos = Position;
        var inside = awake ? new Win32.Point { X = pos.X + (int)(60 * s), Y = pos.Y + (int)(60 * s) }
                           : new Win32.Point { X = pos.X + (int)(16 * s), Y = pos.Y + (int)(16 * s) };
        var outside = new Win32.Point { X = pos.X + (int)(300 * s), Y = pos.Y + (int)(300 * s) };

        var hitInside = Win32.WindowFromPoint(inside);
        var hitOutside = Win32.WindowFromPoint(outside);
        var oursInside = IsOurs(hitInside);
        var oursOutside = IsOurs(hitOutside);

        if (awake)
        {
            _hitProofAwakeLogged = true;
        }
        else
        {
            _hitProofSleepLogged = true;
        }

        LunaLog.Write(
            $"hittest state={(awake ? "awake" : "sleep")} inside_at_{inside.X - pos.X},{inside.Y - pos.Y}_ours={oursInside} " +
            $"hit_in=0x{hitInside.ToInt64():X} class_in={ClassName(hitInside)} " +
            $"outside_at_{outside.X - pos.X},{outside.Y - pos.Y}_ours={oursOutside} " +
            $"hit_out=0x{hitOutside.ToInt64():X} class_out={ClassName(hitOutside)} own_hwnd=0x{_hwnd.ToInt64():X} " +
            $"fg=0x{Win32.GetForegroundWindow().ToInt64():X}/{ClassName(Win32.GetForegroundWindow())} " +
            $"in_ex=0x{Win32.GetWindowLongPtr(hitInside, Win32.GwlExStyle).ToInt64():X} own_rect={OwnRect()} " +
            $"applied_rgn={_lastRgnShape}:{_lastRgnX1},{_lastRgnY1},{_lastRgnX2},{_lastRgnY2} " +
            $"own_visible={Win32.IsWindowVisible(_hwnd)} own_enabled={Win32.IsWindowEnabled(_hwnd)} " +
            $"own_iconic={Win32.IsIconic(_hwnd)} own_style=0x{Win32.GetWindowStyle(_hwnd):X} " +
            $"os_rgn={OsRegion()} zorder={ZOrder()} {HitProbe(awake)}");
    }

    private void EnsureTopmost()
    {
        if (OperatingSystem.IsWindows() && _hwnd != IntPtr.Zero)
        {
            Win32.SetWindowPos(_hwnd, Win32.HwndTopmost, 0, 0, 0, 0,
                Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoActivate);
        }
    }

    private static string ClassName(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return "(null)";
        }

        var builder = new System.Text.StringBuilder(256);
        var length = Win32.GetClassName(hWnd, builder, builder.Capacity);
        return length > 0 ? builder.ToString() : "(unknown)";
    }

    private string HitProbe(bool awake)
    {
        var s = RenderScaling;
        var x = awake ? (int)(60 * s) : (int)(16 * s);
        var y = awake ? (int)(60 * s) : (int)(16 * s);
        var pos = Position;
        var screenPoint = new Win32.Point { X = pos.X + x, Y = pos.Y + y };
        var lParam = (IntPtr)((screenPoint.Y << 16) | (screenPoint.X & 0xFFFF));
        var ht = Win32.SendMessage(_hwnd, 0x0084, IntPtr.Zero, lParam);
        Win32.GetClientRect(_hwnd, out var client);
        var origin = new Win32.Point { X = 0, Y = 0 };
        Win32.ClientToScreen(_hwnd, ref origin);
        var cloaked = 0;
        Win32.DwmGetWindowAttribute(_hwnd, 14, out cloaked, sizeof(int));
        return $"client={client.Right}x{client.Bottom} client_origin={origin.X},{origin.Y} " +
               $"nchittest({screenPoint.X},{screenPoint.Y})=0x{ht.ToInt64():X} cloaked={cloaked}";
    }

    private string OsRegion()
    {
        var rgn = Win32.CreateRectRgn(0, 0, 0, 0);
        try
        {
            var kind = Win32.GetWindowRgn(_hwnd, rgn);
            if (kind == 0)
            {
                return "none";
            }

            return Win32.GetRgnBox(rgn, out var box) != 0
                ? $"{box.Left},{box.Top},{box.Right},{box.Bottom}"
                : "empty";
        }
        finally
        {
            Win32.DeleteObject(rgn);
        }
    }

    private string ZOrder()
    {
        var parts = new System.Collections.Generic.List<string>();
        var w = Win32.GetWindow(_hwnd, Win32.GwHwndPrev);
        for (var i = 0; i < 6 && w != IntPtr.Zero; i++)
        {
            parts.Add($"{ClassName(w)}:0x{Win32.GetWindowLongPtr(w, Win32.GwlExStyle).ToInt64():X}");
            w = Win32.GetWindow(w, Win32.GwHwndPrev);
        }

        return string.Join(" > ", parts);
    }

    private string OwnRect()
    {
        return Win32.GetWindowRect(_hwnd, out var r) ? $"{r.Left},{r.Top},{r.Right},{r.Bottom}" : "?";
    }

    private bool IsOurs(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        return hWnd == _hwnd || Win32.GetAncestor(hWnd, Win32.GaRoot) == _hwnd;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_mode is Mode.Chat or Mode.MorphOpen or Mode.MorphClose)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        OpenChat();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _mode == Mode.Chat)
        {
            e.Handled = true;
            CloseChat();
        }
        else if (e.Key == Key.Q && e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                 e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            Exit();
        }
    }

    private void MenuOpen_Click(object? sender, RoutedEventArgs e) => OpenChat();

    private void MenuExit_Click(object? sender, RoutedEventArgs e) => Exit();

    private async Task WatchDeviceLockAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(1000);
                var state = HelperState.Read();
                if (state?.IsLocked == true)
                {
                    LunaLog.Write("device_locked closing luna");
                    Exit();
                    return;
                }
            }
            catch (Exception ex)
            {
                LunaLog.Exception("lock-watch", ex);
            }
        }
    }

    private void Exit()
    {
        LunaLog.Write("exit_requested");
        UnregisterHotkeys();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    // Installed under Program Files the exe folder is read-only, so the tuner lives in
    // the child's local app data. A legacy file next to the exe (the spike/dev package)
    // is still read once so the approved 1.6/1.9 sizes carry over.
    private static string LegacySleepScalePath => System.IO.Path.Combine(AppContext.BaseDirectory, "sleep-scale.txt");
    private static string SleepScalePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpControlParental", "luna-sleep-scale.txt");

    private static string ChatSizePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpControlParental", "luna-chat-size.txt");

    internal static (double Width, double Height)? ParseChatSize(string text)
    {
        text = text.Trim();
        var parts = text.Split('x', ',');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var height) &&
            width > 0 && height > 0)
            return (width, height);
        return null;
    }

    internal static string FormatChatSize(double width, double height) =>
        $"{width.ToString("0.#", CultureInfo.InvariantCulture)}x{height.ToString("0.#", CultureInfo.InvariantCulture)}";

    private static (double Width, double Height)? LoadChatSize()
    {
        try
        {
            if (!System.IO.File.Exists(ChatSizePath)) return null;
            var text = System.IO.File.ReadAllText(ChatSizePath).Trim();
            var size = ParseChatSize(text);
            if (size is not null) return size;
            LunaLog.Write($"chat_size_invalid text={text}");
        }
        catch (Exception ex)
        {
            LunaLog.Write($"chat_size_load_failed {ex.Message}");
        }
        return null;
    }

    private void SaveChatSize()
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ChatSizePath)!);
            System.IO.File.WriteAllText(ChatSizePath, FormatChatSize(_chatWidth, _chatHeight));
        }
        catch (Exception ex)
        {
            LunaLog.Write($"chat_size_save_failed {ex.Message}");
        }
    }

    private double LoadSleepScale()
    {
        var value = DefaultSleepScale;
        try
        {
            var path = System.IO.File.Exists(LegacySleepScalePath) ? LegacySleepScalePath : SleepScalePath;
            if (System.IO.File.Exists(path))
            {
                var text = System.IO.File.ReadAllText(path).Trim();
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    value = Math.Clamp(Math.Round(parsed, 1), MinSleepScale, MaxSleepScale);
                }
                else
                {
                    LunaLog.Write($"sleep_scale_invalid text={text}");
                }
            }
        }
        catch (Exception ex)
        {
            LunaLog.Write($"sleep_scale_load_failed {ex.Message}");
        }

        var visible = SleepVisibleDip * value;
        LunaLog.Write($"sleep_scale={value:0.0} visible_dip={visible:0.#}x{visible:0.#} source=startup");
        return value;
    }

    private void SaveSleepScale()
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SleepScalePath)!);
            System.IO.File.WriteAllText(SleepScalePath, _sleepScale.ToString("0.0", CultureInfo.InvariantCulture));
            LunaLog.Write($"sleep_scale_saved path={SleepScalePath}");
        }
        catch (Exception ex)
        {
            LunaLog.Write($"sleep_scale_save_failed {ex.Message}");
        }
    }

    private void AdjustSleepScale(int hotkey)
    {
        if (_mode is not (Mode.Sleep or Mode.Retract))
        {
            LunaLog.Write($"sleep_scale_ignored mode={_mode} hotkey=0x{hotkey:X}");
            return;
        }

        var target = hotkey switch
        {
            HotkeyBigger => Math.Min(MaxSleepScale, _sleepScale + SleepScaleStep),
            HotkeySmaller => Math.Max(MinSleepScale, _sleepScale - SleepScaleStep),
            _ => DefaultSleepScale
        };
        target = Math.Round(target, 1);
        if (Math.Abs(target - _sleepScale) < 0.001 && hotkey != HotkeyReset)
        {
            LunaLog.Write($"sleep_scale={_sleepScale:0.0} at_limit");
            return;
        }

        _sleepScale = target;
        var visible = SleepVisibleDip * _sleepScale;
        LunaLog.Write($"sleep_scale={_sleepScale:0.0} visible_dip={visible:0.#}x{visible:0.#} source=hotkey");
        SaveSleepScale();
        UpdateRegion();
    }

    private void RegisterHotkeys()
    {
        if (!OperatingSystem.IsWindows() || _hwnd == IntPtr.Zero || _hotkeysHooked)
        {
            return;
        }

        _hotkeyWnd = _hwnd;
        _hotkeyProc = HotkeyWndProc;
        Win32.SetWindowSubclass(_hotkeyWnd, _hotkeyProc, (UIntPtr)HotkeyBigger, IntPtr.Zero);
        _hotkeysHooked = true;

        var mods = Win32.ModControl | Win32.ModAlt | Win32.ModShift | Win32.ModNoRepeat;
        LogHotkey("shift_pageup", Win32.RegisterHotKey(_hotkeyWnd, HotkeyBigger, mods, 0x21), 0x21);
        LogHotkey("shift_pagedown", Win32.RegisterHotKey(_hotkeyWnd, HotkeySmaller, mods, 0x22), 0x22);
        LogHotkey("shift_home", Win32.RegisterHotKey(_hotkeyWnd, HotkeyReset, mods, 0x24), 0x24);
    }

    private void LogHotkey(string name, bool ok, uint vk)
    {
        var err = ok ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        LunaLog.Write($"hotkey name=ctrl_alt_{name} vk=0x{vk:X} registered={ok} err={err}");
    }

    private void UnregisterHotkeys()
    {
        if (!_hotkeysHooked || _hotkeyWnd == IntPtr.Zero)
        {
            return;
        }

        var up = Win32.UnregisterHotKey(_hotkeyWnd, HotkeyBigger);
        var down = Win32.UnregisterHotKey(_hotkeyWnd, HotkeySmaller);
        var reset = Win32.UnregisterHotKey(_hotkeyWnd, HotkeyReset);
        if (_hotkeyProc is not null)
        {
            Win32.RemoveWindowSubclass(_hotkeyWnd, _hotkeyProc, (UIntPtr)HotkeyBigger);
        }

        _hotkeysHooked = false;
        LunaLog.Write($"hotkey_unregister up={up} down={down} reset={reset}");
    }

    private IntPtr HotkeyWndProc(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (msg == Win32.WmHotkey)
        {
            var id = wParam.ToInt32();
            if (id is HotkeyBigger or HotkeySmaller or HotkeyReset)
            {
                Dispatcher.UIThread.Post(() => AdjustSleepScale(id));
                return IntPtr.Zero;
            }
        }

        if (msg == Win32.WmNcHitTest && _mode == Mode.Sleep)
        {
            var lp = lParam.ToInt64();
            var sx = (short)(lp & 0xFFFF);
            var sy = (short)((lp >> 16) & 0xFFFF);
            if (!IsPointOverSleepMoon(sx, sy))
            {
                return new IntPtr(Win32.HtTransparent);
            }
        }

        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private void OnMemoryTick(object? sender, EventArgs e)
    {
        var second = (int)_clock.Elapsed.TotalSeconds;
        if (second == _lastMemoryLogSecond)
        {
            return;
        }

        if (second > 60 && second % 30 != 0)
        {
            return;
        }

        _lastMemoryLogSecond = second;
        using var process = Process.GetCurrentProcess();
        LunaLog.Write(
            $"memory t={second}s working_set_mb={process.WorkingSet64 / 1024.0 / 1024.0:0.0} " +
            $"private_mb={process.PrivateMemorySize64 / 1024.0 / 1024.0:0.0} gc_mb={GC.GetTotalMemory(false) / 1024.0 / 1024.0:0.0} " +
            $"threads={process.Threads.Count} state={_mode}");
    }

    private void StartProbe(double seconds)
    {
        var now = _clock.Elapsed.TotalSeconds;
        _probeStart = now;
        _probeFrames = 0;
        _probeUntil = now + seconds;

        if (_probeActive)
        {
            return;
        }

        _probeActive = true;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan _)
    {
        if (!_probeActive)
        {
            return;
        }

        _probeFrames++;
        if (_mode is Mode.MorphOpen or Mode.MorphClose)
        {
            _morphFrames++;
        }

        var now = _clock.Elapsed.TotalSeconds;
        if (now >= _probeUntil)
        {
            _probeActive = false;
            var seconds = Math.Max(0.001, now - _probeStart);
            LunaLog.Write($"fps state={_mode} frames={_probeFrames} seconds={seconds:0.##} fps={_probeFrames / seconds:0.#}");
            return;
        }

        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    private static double Approach(double value, double target, double dt, double seconds) =>
        value + (target - value) * Math.Min(1, dt / seconds);

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);

    private static double EaseInCubic(double t) => t * t * t;

    private static double EaseOutBack(double t)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        var p = t - 1;
        return 1 + c3 * p * p * p + c1 * p * p;
    }

    private static double LookOffset(double t) => t switch
    {
        < 0.12 => 0,
        < 0.22 => Lerp(0, -6, (t - 0.12) / 0.10),
        < 0.40 => -6,
        < 0.50 => Lerp(-6, 6, (t - 0.40) / 0.10),
        < 0.68 => 6,
        < 0.78 => Lerp(6, 0, (t - 0.68) / 0.10),
        _ => 0
    };

    internal static (double X, double Y, double W, double H, double R) MorphRectForTest(
        double u, bool opening, double targetW, double targetH) =>
        MorphRect(u, opening, targetW, targetH);

    internal LunaChatDimensions ChatSizeForTest => new(_chatWidth, _chatHeight);

    private static (double X, double Y, double W, double H, double R) MorphRect(
        double u, bool opening, double targetW, double targetH)
    {
        if (!opening)
        {
            return (12, 12, 96, 96, 48);
        }

        var w55 = targetW * (400.0 / 420.0);
        var h55 = targetH * (320.0 / 600.0);
        var w78 = targetW * (372.0 / 420.0);
        var h78 = targetH * (370.0 / 600.0);

        var stops = new (double T, double X, double Y, double W, double H, double R)[]
        {
            (0.00, 12, 12, 96, 96, 48),
            (0.30, 12, 12, 130, 230, 62),
            (0.55, 12, 12, w55, h55, 40),
            (0.78, 12, 12, w78, h78, 18),
            (1.00, 0, 0, targetW, targetH, 22)
        };

        for (var i = 1; i < stops.Length; i++)
        {
            if (u <= stops[i].T)
            {
                var a = stops[i - 1];
                var b = stops[i];
                var p = EaseOutCubic((u - a.T) / (b.T - a.T));
                return (
                    Lerp(a.X, b.X, p),
                    Lerp(a.Y, b.Y, p),
                    Lerp(a.W, b.W, p),
                    Lerp(a.H, b.H, p),
                    Lerp(a.R, b.R, p));
            }
        }

        return (0, 0, targetW, targetH, 0);
    }
}
