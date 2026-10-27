using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Luna;

public sealed record KidWindowOptions(
    string Tab = "home",
    bool MessagesOnly = false,
    bool AboveLock = false,
    string? Theme = null,
    string? Language = null,
    bool Preview = false,
    double Scale = 1);

public sealed class MyOpControlWindow : Window
{
    private enum Screen { Home, Schedule, Rules, Activity, Messages }

    private readonly IKidDataSource _data;
    private readonly KidWindowOptions _options;
    private readonly double _scale;
    private KidSnapshot _snapshot = new(null, null, [], false);
    private LunaPalette _palette;
    private string _themeKey;
    private Screen _screen;
    private bool _activityWeek;
    private bool _sending;
    private bool _loaded;
    private TextBox? _input;
    private TextBlock? _sendStatus;
    private Button? _sendButton;
    private Grid? _content;
    private StackPanel? _messagesPanel;
    private ScrollViewer? _messagesScroll;
    private DispatcherTimer? _refreshTimer;
    private DispatcherTimer? _themeTimer;
    private string? _language;
    private readonly Dictionary<Screen, Button> _navButtons = new();
    private readonly Dictionary<Guid, MessageRow> _messageRows = new();
    private readonly HashSet<Guid> _seenRequested = new();
    private Control? _messagesEmpty;
    private TextBlock? _homeTimeLeft;
    private TextBlock? _homeUsed;
    private ProgressBar? _homeProgress;
    private string _snapshotSignature = string.Empty;
    private string _renderedLanguage = string.Empty;
    private bool _suppressEnterText;
    private int _messageListBuilds;
    private int _messageRowAdds;

    private sealed class MessageRow
    {
        public required Guid Id { get; init; }
        public required StackPanel Root { get; init; }
        public required TextBlock Body { get; init; }
        public required TextBlock Header { get; init; }
        public required StackPanel Meta { get; init; }
        public ChildMessageDto Message { get; set; } = null!;
        public int Index { get; set; }
    }

    public MyOpControlWindow(IKidDataSource data, KidWindowOptions options)
    {
        _data = data;
        _options = options;
        _scale = options.Preview ? Math.Clamp(options.Scale, 1, 2) : 1;
        _themeKey = options.Theme ?? KidThemeStore.ReadLocal() ?? "night";
        _palette = LunaThemes.From(_themeKey);
        _language = options.Language;
        _screen = ParseScreen(options.Tab);
        if (options.MessagesOnly) _screen = Screen.Messages;

        Title = _options.Preview ? " " : Loc.T("Client.Kid.WindowTitle");
        WindowDecorations = WindowDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        CanResize = false;
        ShowInTaskbar = !options.AboveLock;
        Topmost = options.AboveLock;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SizeToScreen();
        BuildRoot();
        Opened += async (_, _) =>
        {
            SizeToScreen();
            await LoadAsync();
#if LUNA_DEV
            if (!string.IsNullOrWhiteSpace(App.KidScreenshotPath))
            {
                await Task.Delay(300);
                CaptureForDev(App.KidScreenshotPath!);
                Close();
                return;
            }
#endif
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _refreshTimer.Tick += async (_, _) => await LoadAsync();
            _refreshTimer.Start();
            _themeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _themeTimer.Tick += (_, _) => SyncTheme();
            _themeTimer.Start();
        };
        Closed += (_, _) =>
        {
            _refreshTimer?.Stop();
            _themeTimer?.Stop();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Close(); }
        };
    }

#if LUNA_DEV
    private void CaptureForDev(string path)
    {
        try
        {
            var pixel = new PixelSize(Math.Max(1, (int)Math.Round(Width)), Math.Max(1, (int)Math.Round(Height)));
            var bitmap = new RenderTargetBitmap(pixel, new Vector(96, 96));
            bitmap.Render(this);
            bitmap.Save(path);
            LunaLog.Write($"kid_screenshot saved={path} size={pixel.Width}x{pixel.Height}");
        }
        catch (Exception ex)
        {
            LunaLog.Write("kid_screenshot_failed " + ex);
        }
    }
#endif

    private void SizeToScreen()
    {
        var screen = Screens.Primary;
        var scale = screen?.Scaling ?? 1;
        var workWidth = (screen?.WorkingArea.Width ?? 1366) / scale;
        var workHeight = (screen?.WorkingArea.Height ?? 768) / scale;
        if (_options.MessagesOnly || _screen == Screen.Messages && _options.AboveLock)
        {
            Width = Math.Clamp(Math.Min(560, workWidth - 24), 420, 560);
            Height = Math.Clamp(Math.Min(640, workHeight - 32), 420, 640);
        }
        else
        {
            Width = Math.Clamp(Math.Min(1040, workWidth - 24), 760, 1040);
            Height = Math.Clamp(Math.Min(700, workHeight - 32), 480, 700);
        }
        if (_scale != 1)
        {
            Width *= _scale;
            Height *= _scale;
        }
    }

    private Color HighlightColor => _palette.Key == "gray" ? _palette.Success : _palette.Accent;

    private void BuildRoot()
    {
        var compact = _options.MessagesOnly;
        var root = new Border
        {
            CornerRadius = new CornerRadius(22),
            BorderThickness = new Thickness(2),
            BorderBrush = LunaBrushes.B(_palette.Border),
            Background = LunaBrushes.B(_palette.Bg),
            ClipToBounds = true
        };
        if (_scale != 1)
        {
            root.Width = Width / _scale;
            root.Height = Height / _scale;
        }
        _navButtons.Clear();
        var outer = new Grid { RowDefinitions = new RowDefinitions("52,*") };
        outer.Children.Add(BuildTitleBar());
        var layout = new Grid
        {
            ColumnDefinitions = compact ? new ColumnDefinitions("*") : new ColumnDefinitions("196,*")
        };
        if (!compact)
        {
            var sidebar = BuildSidebar();
            layout.Children.Add(sidebar);
        }
        var main = BuildMain();
        Grid.SetColumn(main, compact ? 0 : 1);
        layout.Children.Add(main);
        Grid.SetRow(layout, 1);
        outer.Children.Add(layout);
        root.Child = outer;
        UpdateNavSelection();
        if (_scale != 1)
        {
            Content = new LayoutTransformControl
            {
                Child = root,
                LayoutTransform = new ScaleTransform(_scale, _scale)
            };
        }
        else
        {
            Content = root;
        }
    }

    private Control BuildTitleBar()
    {
        var bar = new Border
        {
            Name = "TitleBar",
            Background = LunaBrushes.B(_palette.Surface),
            BorderBrush = LunaBrushes.B(_palette.Surface2),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 0, 10, 0)
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto") };
        var moon = MoonFace(28);
        moon.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(moon);

        var brand = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 0, 8, 0) };
        brand.Children.Add(Text(Loc.T("Client.Kid.WindowTitle"), 18, _palette.Text, FontWeight.Bold, LunaThemes.Display, wrap: false));
        brand.Children.Add(Text(Loc.T("Client.Kid.ViewOnly"), 10.5, _palette.Muted, FontWeight.SemiBold));
        brand.Children[1].Name = "ViewOnlySubtitle";
        Grid.SetColumn(brand, 1);
        grid.Children.Add(brand);

        var theme = SmallButton("◐", Loc.T("Client.Kid.Theme.Pick"));
        theme.Name = "ThemeButton";
        theme.Click += async (_, _) => await ToggleThemeAsync();
        Grid.SetColumn(theme, 2);
        grid.Children.Add(theme);
        var minimize = SmallButton("–", Loc.T("Client.Kid.Minimize"));
        minimize.Name = "MinimizeButton";
        minimize.Click += (_, _) => WindowState = WindowState.Minimized;
        Grid.SetColumn(minimize, 3);
        grid.Children.Add(minimize);
        var close = SmallButton("×", Loc.T("Client.Kid.Close"));
        close.Name = "CloseButton";
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 4);
        grid.Children.Add(close);

        bar.Child = grid;
        bar.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            var onButton = (e.Source as Visual)?.GetVisualAncestors().OfType<Button>().Any() == true;
            if (!onButton) BeginMoveDrag(e);
        };
        return bar;
    }

    private Control BuildSidebar()
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        var nav = new StackPanel { Spacing = 4, Margin = new Thickness(12, 16, 12, 0) };
        nav.Children.Add(NavButton(Screen.Home, "⌂", Loc.T("Client.Kid.Nav.Home")));
        nav.Children.Add(NavButton(Screen.Schedule, "◷", Loc.T("Client.Kid.Nav.Schedule")));
        nav.Children.Add(NavButton(Screen.Rules, "◎", Loc.T("Client.Kid.Nav.Rules")));
        nav.Children.Add(NavButton(Screen.Activity, "▤", Loc.T("Client.Kid.Nav.Activity")));
        nav.Children.Add(NavButton(Screen.Messages, "✉", Loc.T("Client.Kid.Nav.Messages")));
        grid.Children.Add(nav);

        var bottom = new StackPanel { Spacing = 8, Margin = new Thickness(12, 0, 12, 16) };
        var message = new Button
        {
            Name = "MessageParentsButton",
            Content = Text(Loc.T("Client.Kid.MessageParents"), 12, _palette.AccentInk, FontWeight.Bold),
            Background = LunaBrushes.B(_palette.Accent),
            Foreground = LunaBrushes.B(_palette.AccentInk),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 9),
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        message.Click += (_, _) => ShowScreen(Screen.Messages);
        bottom.Children.Add(message);
        Grid.SetRow(bottom, 1);
        grid.Children.Add(bottom);

        return new Border
        {
            Background = LunaBrushes.B(_palette.Surface),
            BorderBrush = LunaBrushes.B(_palette.Surface2),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = grid
        };
    }

    private Button NavButton(Screen screen, string glyph, string label)
    {
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*") };
        content.Children.Add(Text(glyph, 16, _palette.Muted, FontWeight.Bold));
        var text = Text(label, 13, _palette.Text, FontWeight.SemiBold);
        Grid.SetColumn(text, 1);
        content.Children.Add(text);
        var button = new Button
        {
            Content = content,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 9),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        button.Name = "Nav" + screen;
        button.Click += (_, _) => ShowScreen(screen);
        _navButtons[screen] = button;
        return button;
    }

    private void UpdateNavSelection()
    {
        foreach (var (screen, button) in _navButtons)
        {
            var active = screen == _screen;
            button.Background = active ? LunaBrushes.B(_palette.Surface2) : Brushes.Transparent;
            button.BorderBrush = LunaBrushes.B(active ? _palette.Accent : Colors.Transparent);
            button.BorderThickness = new Thickness(active ? 2 : 0);
        }
    }

    private Control BuildMain()
    {
        var grid = new Grid();
        _content = new Grid { Name = "ContentRoot" };
        grid.Children.Add(_content);
        return grid;
    }

    private Button SmallButton(string text, string tip)
    {
        var button = new Button
        {
            Content = Text(text, 16, _palette.Muted, FontWeight.Bold),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 2),
            Margin = new Thickness(2, 0)
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    private async Task LoadAsync()
    {
        try
        {
            var snapshot = await _data.LoadAsync(CancellationToken.None);
            if (snapshot.Error is not null && !_loaded)
            {
                _snapshot = snapshot;
                RenderCurrent();
                return;
            }
            _snapshot = snapshot;
            if (string.IsNullOrWhiteSpace(_language) && !string.IsNullOrWhiteSpace(snapshot.Language))
                _language = snapshot.Language;
            if (!string.IsNullOrWhiteSpace(_language)) Loc.SetLanguage(_language);
            var local = KidThemeStore.ReadLocal();
            if (local is not null && !string.Equals(local, _themeKey, StringComparison.OrdinalIgnoreCase))
            {
                _themeKey = local;
                _palette = LunaThemes.From(_themeKey);
            }
            _loaded = true;
            var languageChanged = !string.Equals(Loc.Language, _renderedLanguage, StringComparison.OrdinalIgnoreCase);
            if (_screen == Screen.Messages && _messagesPanel is not null && !languageChanged)
            {
                UpdateMessagesInPlace();
                return;
            }

            var signature = SnapshotSignature(snapshot);
            if (languageChanged || !string.Equals(signature, _snapshotSignature, StringComparison.Ordinal))
            {
                _snapshotSignature = signature;
                RenderCurrent();
            }
            else
            {
                UpdateVolatileHomeValues();
            }
        }
        catch (Exception ex)
        {
            LunaLog.Exception("kid-load", ex);
        }
    }

    /// <summary>
    /// Everything that changes a screen's structure. The live countdown and the
    /// runtime UpdatedUtc tick every poll, so they are updated in place instead of
    /// forcing a rebuild.
    /// </summary>
    private static string SnapshotSignature(KidSnapshot snapshot)
    {
        var summary = snapshot.Summary;
        var state = snapshot.State;
        return JsonSerializer.Serialize(new
        {
            snapshot.Online,
            snapshot.Error,
            Language = snapshot.Language,
            Theme = snapshot.ThemeKey,
            Summary = summary is null ? null : new
            {
                summary.ChildId,
                summary.ChildName,
                summary.DeviceName,
                summary.GeneratedUtc,
                summary.ScreenTimeEnabled,
                summary.TodayLimitMinutes,
                summary.UsedTodaySeconds,
                summary.UsedWeekSeconds,
                summary.Weekdays,
                summary.Schedules,
                summary.Categories,
                summary.Websites,
                summary.Apps,
                summary.AppsToday,
                summary.AppsWeek,
                summary.Visits,
                summary.History,
                summary.UnreadMessages,
                summary.Theme,
                Recent = summary.RecentMessages.Select(message => new
                {
                    message.Id,
                    message.Text,
                    message.SenderName,
                    message.CreatedUtc,
                    Status = (int)message.Status
                })
            },
            State = state is null ? null : new
            {
                state.ChildId,
                state.ChildName,
                state.ParentDisplayName,
                state.IsLocked,
                state.Reason,
                state.ScheduleName,
                state.EndsUtc,
                state.NextLockUtc,
                state.BonusUntilUtc,
                state.BonusMinutes,
                state.DailyLimitMinutes,
                state.SpokenWarnings,
                state.Language,
                state.UnlockScheduleName,
                state.LunaEnabled,
                state.Theme
            }
        });
    }

    private void UpdateVolatileHomeValues()
    {
        if (_screen != Screen.Home || _homeTimeLeft is null) return;
        var summary = _snapshot.Summary;
        var state = _snapshot.State;
        if (summary is null || !summary.ScreenTimeEnabled) return;
        var left = state?.DailyLimitRemainingSeconds is { } seconds
            ? Math.Max(0, seconds)
            : Math.Max(0, summary.TodayLimitMinutes * 60 - summary.UsedTodaySeconds);
        var text = FormatDuration(left);
        if (!string.Equals(_homeTimeLeft.Text, text, StringComparison.Ordinal))
            _homeTimeLeft.Text = text;
    }

    private void SyncTheme()
    {
        var local = KidThemeStore.ReadLocal();
        if (local is null || string.Equals(local, _themeKey, StringComparison.OrdinalIgnoreCase)) return;
        _themeKey = local;
        _palette = LunaThemes.From(_themeKey);
        BuildRoot();
        RenderCurrent();
    }

    private async Task ToggleThemeAsync()
    {
        _themeKey = string.Equals(_themeKey, "gray", StringComparison.OrdinalIgnoreCase) ? "night" : "gray";
        KidThemeStore.WriteLocal(_themeKey);
        _palette = LunaThemes.From(_themeKey);
        try { await _data.SetThemeAsync(_themeKey, CancellationToken.None); } catch { }
        BuildRoot();
        RenderCurrent();
    }

    private void ShowScreen(Screen screen)
    {
        _screen = screen;
        UpdateNavSelection();
        RenderCurrent();
    }

    private void RenderCurrent()
    {
        if (_content is null) return;
        _content.Children.Clear();
        _messagesPanel = null;
        _messagesScroll = null;
        _messagesEmpty = null;
        _messageRows.Clear();
        _homeTimeLeft = null;
        _homeUsed = null;
        _homeProgress = null;
        _content.Children.Add(_screen switch
        {
            Screen.Schedule => BuildSchedule(),
            Screen.Rules => BuildRules(),
            Screen.Activity => BuildActivity(),
            Screen.Messages => BuildMessages(),
            _ => BuildHome()
        });
        _renderedLanguage = Loc.Language;
    }

    private Control BuildHome()
    {
        var summary = _snapshot.Summary;
        var state = _snapshot.State;
        if (summary is null)
            return Scroll(NoData());

        var stack = new StackPanel { Spacing = 14, Margin = new Thickness(22, 6, 22, 22) };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(Text(Loc.T("Client.Kid.Greeting", summary.ChildName), 26, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        status.Children.Add(Pill(state?.IsLocked == true ? Loc.T("Client.Kid.Status.Locked") : Loc.T("Client.Kid.Status.Unlocked"),
            state?.IsLocked == true ? _palette.Danger : _palette.Success, _palette.Key == "gray" ? Colors.White : _palette.AccentInk));
        status.Children.Add(Pill(_snapshot.Online ? Loc.T("Client.Kid.Status.Online") : Loc.T("Client.Kid.Status.Offline"),
            _palette.Surface2, _palette.Muted));
        Grid.SetColumn(status, 1);
        head.Children.Add(status);
        stack.Children.Add(head);

        var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var time = TimeLeftCard(summary, state);
        time.Margin = new Thickness(0, 0, 7, 0);
        cards.Children.Add(time);
        var next = NextLockCard(state, summary);
        next.Margin = new Thickness(7, 0, 0, 0);
        Grid.SetColumn(next, 1);
        cards.Children.Add(next);
        stack.Children.Add(cards);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(ActionButton(Loc.T("Client.Kid.ViewSchedule"), () => ShowScreen(Screen.Schedule), primary: false));
        actions.Children.Add(ActionButton(Loc.T("Client.Kid.ViewRules"), () => ShowScreen(Screen.Rules), primary: false));
        stack.Children.Add(actions);

        stack.Children.Add(MostUsedCard(summary));
        stack.Children.Add(RulesSummaryCard(summary));
        stack.Children.Add(MessagesSummaryCard(summary));
        if (summary.GeneratedUtc > DateTime.MinValue)
            stack.Children.Add(Text(Loc.T("Client.Kid.Updated", summary.GeneratedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)),
                10.5, _palette.Muted));
        return Scroll(stack);
    }

    private Control TimeLeftCard(ChildSummaryDto summary, RuntimeLockStateDto? state)
    {
        var limit = summary.TodayLimitMinutes;
        var left = state?.DailyLimitRemainingSeconds is { } seconds
            ? Math.Max(0, seconds)
            : Math.Max(0, limit * 60 - summary.UsedTodaySeconds);
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(Text(Loc.T("Client.Kid.TimeLeft"), 11, _palette.Muted, FontWeight.Bold));
        _homeTimeLeft = Text(summary.ScreenTimeEnabled ? FormatDuration(left) : Loc.T("Client.Kid.NoLimit"),
            34, summary.ScreenTimeEnabled ? HighlightColor : _palette.Muted, FontWeight.Bold, LunaThemes.Display);
        stack.Children.Add(_homeTimeLeft);
        if (summary.ScreenTimeEnabled)
        {
            var used = Math.Max(0, Math.Min(limit * 60, summary.UsedTodaySeconds));
            _homeProgress = new ProgressBar
            {
                Minimum = 0,
                Maximum = Math.Max(1, limit * 60),
                Value = used,
                Height = 10,
                Foreground = LunaBrushes.B(HighlightColor),
                Background = LunaBrushes.B(_palette.Sunken)
            };
            stack.Children.Add(_homeProgress);
            _homeUsed = Text(Loc.T("Client.Kid.UsedOf", FormatDuration(summary.UsedTodaySeconds), FormatDuration(limit * 60)),
                11.5, _palette.Muted);
            stack.Children.Add(_homeUsed);
        }
        else
        {
            stack.Children.Add(Text(Loc.T("Client.Kid.UsedNoLimit", FormatDuration(summary.UsedTodaySeconds)),
                11.5, _palette.Muted));
        }
        stack.Children.Add(Text(Loc.T("Client.Kid.ResetsMidnight"), 11, _palette.Muted));
        return Card(stack);
    }

    private Control NextLockCard(RuntimeLockStateDto? state, ChildSummaryDto summary)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(Text(Loc.T("Client.Kid.NextLock"), 11, _palette.Muted, FontWeight.Bold));
        if (state?.BonusUntilUtc is { } bonus && bonus > DateTime.UtcNow)
        {
            stack.Children.Add(Text(Loc.T("Client.Kid.BonusActive", state.BonusMinutes ?? 0,
                state.ParentDisplayName ?? Loc.T("Client.Kid.YourParents"),
                bonus.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)), 14, _palette.Text, FontWeight.Bold));
        }
        else if (state?.Reason == "override" && state.EndsUtc is { } ends && ends > DateTime.UtcNow)
        {
            stack.Children.Add(Text(Loc.T("Client.Kid.ParentUnlock", ends.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)),
                14, _palette.Success, FontWeight.Bold));
        }
        else if (state?.NextLockUtc is { } next && next > DateTime.UtcNow)
        {
            stack.Children.Add(Text(next.ToLocalTime().ToString("t", CultureInfo.CurrentCulture),
                26, _palette.Text, FontWeight.Bold, LunaThemes.Display));
            stack.Children.Add(Text(Loc.T("Client.Kid.NextLockUntil", next.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)),
                11, _palette.Muted));
        }
        else
        {
            stack.Children.Add(Text(Loc.T("Client.Kid.NoLocks"), 14, _palette.Muted, FontWeight.SemiBold));
        }
        var weekdays = summary.Weekdays;
        if (weekdays.Count > 0)
        {
            var chips = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var day in weekdays)
            {
                var chip = Pill($"{DayName(day.Day)} {day.Minutes}m", _palette.Surface2, _palette.Muted);
                chip.Margin = new Thickness(0, 0, 4, 4);
                chips.Children.Add(chip);
            }
            stack.Children.Add(chips);
        }
        return Card(stack);
    }

    private Control MostUsedCard(ChildSummaryDto summary)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Text(Loc.T("Client.Kid.MostUsed"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        var apps = summary.AppsToday.Take(4).ToList();
        if (apps.Count == 0)
            stack.Children.Add(Text(Loc.T("Client.Kid.NoUsage"), 12, _palette.Muted));
        foreach (var app in apps)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(Text(app.DisplayName, 12.5, _palette.Text, FontWeight.SemiBold));
            var duration = Text(FormatDuration(app.Seconds), 12, _palette.Muted);
            Grid.SetColumn(duration, 1);
            row.Children.Add(duration);
            stack.Children.Add(row);
        }
        return Card(stack);
    }

    private Control RulesSummaryCard(ChildSummaryDto summary)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Text(Loc.T("Client.Kid.MyRules"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        var blockedSites = summary.Websites.Count(w => !w.Allowed);
        var blockedApps = summary.Apps.Count(a => !a.Allowed);
        var limits = summary.Apps.Count(a => a.DailyLimitMinutes is not null);
        var allowed = summary.Websites.Count(w => w.Allowed) + summary.Apps.Count(a => a.AlwaysAllowed);
        AddStat(grid, 0, 0, blockedSites, Loc.T("Client.Kid.BlockedSites"));
        AddStat(grid, 1, 0, blockedApps, Loc.T("Client.Kid.BlockedApps"));
        AddStat(grid, 0, 1, limits, Loc.T("Client.Kid.AppLimits"));
        AddStat(grid, 1, 1, allowed, Loc.T("Client.Kid.AlwaysAllowed"));
        stack.Children.Add(grid);
        return Card(stack);
    }

    private void AddStat(Grid grid, int column, int row, int value, string label)
    {
        var stack = new StackPanel { Spacing = 0, Margin = new Thickness(0, 2, 0, 2) };
        stack.Children.Add(Text(value.ToString(CultureInfo.CurrentCulture), 20, HighlightColor, FontWeight.Bold, LunaThemes.Display));
        stack.Children.Add(Text(label, 11, _palette.Muted));
        Grid.SetColumn(stack, column);
        Grid.SetRow(stack, row);
        grid.Children.Add(stack);
    }

    private Control MessagesSummaryCard(ChildSummaryDto summary)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(Text(Loc.T("Client.Kid.Messages"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        if (summary.UnreadMessages > 0)
            stack.Children.Add(Text(Loc.T("Client.Kid.Unread", summary.UnreadMessages), 12, _palette.Success, FontWeight.Bold));
        var latest = summary.RecentMessages.FirstOrDefault();
        if (latest is null)
            stack.Children.Add(Text(Loc.T("Client.Kid.NoMessages"), 12, _palette.Muted));
        else
            stack.Children.Add(Text(latest.Text, 12.5, _palette.Text, FontWeight.SemiBold));
        var button = ActionButton(Loc.T("Client.Kid.MessageParents"), () => ShowScreen(Screen.Messages), primary: true);
        stack.Children.Add(button);
        return Card(stack);
    }

    private Control BuildSchedule()
    {
        var summary = _snapshot.Summary;
        if (summary is null) return Scroll(NoData());
        var stack = new StackPanel { Spacing = 14, Margin = new Thickness(22, 6, 22, 22) };
        stack.Children.Add(ScreenTitle(Loc.T("Client.Kid.Schedule.Eyebrow"), Loc.T("Client.Kid.Schedule.Title")));

        var daily = new StackPanel { Spacing = 6 };
        daily.Children.Add(Text(Loc.T("Client.Kid.Schedule.Daily"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        if (summary.ScreenTimeEnabled)
        {
            daily.Children.Add(Text(Loc.T("Client.Kid.Schedule.Limit", summary.TodayLimitMinutes), 12, _palette.Muted));
            daily.Children.Add(Text(Loc.T("Client.Kid.UsedOf", FormatDuration(summary.UsedTodaySeconds), FormatDuration(summary.TodayLimitMinutes * 60)),
                14, _palette.Accent, FontWeight.Bold));
            daily.Children.Add(new ProgressBar
            {
                Minimum = 0,
                Maximum = Math.Max(1, summary.TodayLimitMinutes * 60),
                Value = Math.Min(summary.TodayLimitMinutes * 60, summary.UsedTodaySeconds),
                Height = 10,
                Foreground = LunaBrushes.B(HighlightColor),
                Background = LunaBrushes.B(_palette.Sunken)
            });
            daily.Children.Add(Text(Loc.T("Client.Kid.Schedule.RunsOut"), 11.5, _palette.Muted));
            var days = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var day in summary.Weekdays)
            {
                var chip = Pill($"{DayName(day.Day)} {day.Minutes}m", _palette.Surface2, _palette.Text);
                chip.Margin = new Thickness(0, 0, 4, 4);
                days.Children.Add(chip);
            }
            daily.Children.Add(days);
        }
        else
        {
            daily.Children.Add(Text(Loc.T("Client.Kid.Schedule.Off"), 14, _palette.Muted, FontWeight.Bold));
        }
        stack.Children.Add(Card(daily));

        var locks = new StackPanel { Spacing = 8 };
        locks.Children.Add(Text(Loc.T("Client.Kid.Schedule.LockedTimes"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        if (summary.Schedules.Count == 0)
            locks.Children.Add(Text(Loc.T("Client.Kid.Schedule.NoLocks"), 12, _palette.Muted));
        foreach (var schedule in summary.Schedules.Where(s => s.IsEnabled))
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(Text(schedule.Name, 12.5, _palette.Text, FontWeight.SemiBold));
            var when = Text($"{FormatMinutes(schedule.StartMinutes)} – {FormatMinutes(schedule.EndMinutes)}", 12, _palette.Muted);
            Grid.SetColumn(when, 1);
            row.Children.Add(when);
            var days = Text(DaysLabel(schedule.DaysMask), 10.5, _palette.Muted);
            var wrap = new StackPanel { Spacing = 1 };
            wrap.Children.Add(row);
            wrap.Children.Add(days);
            locks.Children.Add(wrap);
        }
        stack.Children.Add(Card(locks));

        var limits = new StackPanel { Spacing = 8 };
        limits.Children.Add(Text(Loc.T("Client.Kid.Schedule.AppLimits"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        var appLimits = summary.Apps.Where(a => a.DailyLimitMinutes is not null).ToList();
        if (appLimits.Count == 0)
            limits.Children.Add(Text(Loc.T("Client.Kid.Schedule.NoAppLimits"), 12, _palette.Muted));
        foreach (var app in appLimits)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(Text(app.DisplayName, 12.5, _palette.Text, FontWeight.SemiBold));
            var detail = Text($"{FormatDuration(app.UsedTodaySeconds)} / {app.DailyLimitMinutes} min", 11.5, _palette.Muted);
            Grid.SetColumn(detail, 1);
            row.Children.Add(detail);
            limits.Children.Add(row);
        }
        stack.Children.Add(Card(limits));
        return Scroll(stack);
    }

    private Control BuildRules()
    {
        var summary = _snapshot.Summary;
        if (summary is null) return Scroll(NoData());
        var stack = new StackPanel { Spacing = 14, Margin = new Thickness(22, 6, 22, 22) };
        stack.Children.Add(ScreenTitle(Loc.T("Client.Kid.Rules.Eyebrow"), Loc.T("Client.Kid.Rules.Title")));

        var categories = new StackPanel { Spacing = 8 };
        categories.Children.Add(Text(Loc.T("Client.Kid.Rules.Categories"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        var chips = new WrapPanel { Orientation = Orientation.Horizontal };
        if (summary.Categories.Count == 0) categories.Children.Add(Text(Loc.T("Client.Kid.Rules.None"), 12, _palette.Muted));
        foreach (var category in summary.Categories)
        {
            var chip = Pill(LocalizedCategory(category), _palette.Surface2, _palette.Text);
            chip.Margin = new Thickness(0, 0, 5, 5);
            chips.Children.Add(chip);
        }
        categories.Children.Add(chips);
        stack.Children.Add(Card(categories));

        var websites = new StackPanel { Spacing = 6 };
        websites.Children.Add(Text(Loc.T("Client.Kid.Rules.Websites"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        AddRuleGroup(websites, Loc.T("Client.Kid.Rules.AlwaysAllowedSites"), summary.Websites.Where(w => w.Allowed).Select(w => w.Domain));
        AddRuleGroup(websites, Loc.T("Client.Kid.Rules.BlockedSites"), summary.Websites.Where(w => !w.Allowed).Select(w => $"{w.Domain} · {(w.Always ? Loc.T("Client.Kid.Rules.Always") : Loc.T("Client.Kid.Rules.Scheduled"))}"));
        stack.Children.Add(Card(websites));

        var apps = new StackPanel { Spacing = 6 };
        apps.Children.Add(Text(Loc.T("Client.Kid.Rules.Apps"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        AddRuleGroup(apps, Loc.T("Client.Kid.Rules.AlwaysAllowedApps"), summary.Apps.Where(a => a.AlwaysAllowed).Select(a => a.DisplayName));
        AddRuleGroup(apps, Loc.T("Client.Kid.Rules.BlockedApps"), summary.Apps.Where(a => !a.Allowed).Select(a => a.DisplayName));
        stack.Children.Add(Card(apps));

        stack.Children.Add(Text(Loc.T("Client.Kid.Rules.AlwaysAllowedNote"), 11.5, _palette.Muted));
        stack.Children.Add(Text(Loc.T("Client.Kid.Rules.AskParents"), 12, _palette.Accent, FontWeight.SemiBold));
        return Scroll(stack);
    }

    private void AddRuleGroup(StackPanel parent, string title, IEnumerable<string> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        parent.Children.Add(Text(title, 11.5, _palette.Muted, FontWeight.Bold));
        foreach (var item in list)
            parent.Children.Add(Text(item, 12.5, _palette.Text));
    }

    private Control BuildActivity()
    {
        var summary = _snapshot.Summary;
        if (summary is null) return Scroll(NoData());
        var stack = new StackPanel { Spacing = 14, Margin = new Thickness(22, 6, 22, 22) };
        stack.Children.Add(ScreenTitle(Loc.T("Client.Kid.Activity.Eyebrow"), Loc.T("Client.Kid.Activity.Title")));
        var toggle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        toggle.Children.Add(ActionButton(Loc.T("Client.Kid.Activity.Today"), () => { _activityWeek = false; RenderCurrent(); }, primary: !_activityWeek));
        toggle.Children.Add(ActionButton(Loc.T("Client.Kid.Activity.Week"), () => { _activityWeek = true; RenderCurrent(); }, primary: _activityWeek));
        stack.Children.Add(toggle);

        var usage = new StackPanel { Spacing = 6 };
        usage.Children.Add(Text(Loc.T("Client.Kid.Activity.Apps"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        var apps = _activityWeek ? summary.AppsWeek : summary.AppsToday;
        var total = apps.Sum(app => app.Seconds);
        usage.Children.Add(Text(Loc.T("Client.Kid.Activity.Total", FormatDuration(total)), 11.5, _palette.Muted));
        if (apps.Count == 0) usage.Children.Add(Text(Loc.T("Client.Kid.Activity.None"), 12, _palette.Muted));
        foreach (var app in apps.Take(8))
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(Text(app.DisplayName, 12.5, _palette.Text, FontWeight.SemiBold));
            var duration = Text(FormatDuration(app.Seconds), 12, _palette.Muted);
            Grid.SetColumn(duration, 1);
            row.Children.Add(duration);
            usage.Children.Add(row);
        }
        stack.Children.Add(Card(usage));

        var visits = new StackPanel { Spacing = 6 };
        visits.Children.Add(Text(Loc.T("Client.Kid.Activity.Websites"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        if (summary.Visits.Count == 0) visits.Children.Add(Text(Loc.T("Client.Kid.Activity.None"), 12, _palette.Muted));
        foreach (var visit in summary.Visits.Take(8))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(Text(visit.Domain, 12.5, _palette.Text));
            if (visit.Blocked) row.Children.Add(Text(Loc.T("Client.Kid.Activity.Blocked"), 9.5, _palette.Danger, FontWeight.Bold));
            visits.Children.Add(row);
        }
        stack.Children.Add(Card(visits));

        var history = new StackPanel { Spacing = 6 };
        history.Children.Add(Text(Loc.T("Client.Kid.Activity.History"), 14, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        if (summary.History.Count == 0) history.Children.Add(Text(Loc.T("Client.Kid.Activity.None"), 12, _palette.Muted));
        foreach (var item in summary.History.Take(8))
        {
            var label = item.Kind switch
            {
                "bonus" => Loc.T("Client.Kid.Activity.BonusMinutes", item.Minutes ?? 0),
                "unlocked" => Loc.T("Client.Kid.Activity.Unlocked"),
                "locked" => Loc.T("Client.Kid.Activity.Locked"),
                _ => item.Kind
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(Text(label, 12.5, _palette.Text));
            var when = Text(item.CreatedUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture), 11, _palette.Muted);
            Grid.SetColumn(when, 1);
            row.Children.Add(when);
            history.Children.Add(row);
        }
        stack.Children.Add(Card(history));
        return Scroll(stack);
    }

    private Control BuildMessages()
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var head = new StackPanel { Spacing = 2, Margin = new Thickness(22, 6, 22, 8) };
        head.Children.Add(Text(Loc.T("Client.Messages.Title"), 22, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        head.Children.Add(Text(Loc.T("Client.Messages.Subtitle"), 11.5, _palette.Muted));
        grid.Children.Add(head);

        _messagesPanel = new StackPanel { Spacing = 8, Margin = new Thickness(22, 0, 22, 8) };
        var scroll = new ScrollViewer
        {
            Content = _messagesPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 1);
        grid.Children.Add(scroll);
        _messagesScroll = scroll;

        _messageListBuilds++;
        RebuildMessageList(MessageThreadDiff.Normalize(_snapshot.Messages));

        var composer = new StackPanel { Spacing = 6, Margin = new Thickness(22, 4, 22, 18) };
        var quick = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var text in new[]
                 {
                     Loc.T("Client.Messages.Quick.MoreTime"),
                     Loc.T("Client.Messages.Quick.Homework"),
                     Loc.T("Client.Messages.Quick.ComeHere")
                 })
        {
            var button = new Button
            {
                Content = Text(text, 11.5, _palette.Text, FontWeight.SemiBold, wrap: false),
                Background = Brushes.Transparent,
                BorderBrush = LunaBrushes.B(_palette.Surface2),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(99),
                Padding = new Thickness(14, 8),
                MinHeight = 36,
                Margin = new Thickness(0, 0, 8, 8),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            button.Click += (_, _) => { if (_input is not null) { _input.Text = text; _input.Focus(); } };
            quick.Children.Add(button);
        }
        composer.Children.Add(quick);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        _input = new TextBox
        {
            PlaceholderText = Loc.T("Client.Messages.Placeholder"),
            MaxLength = 500,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
            MaxHeight = 110,
            Background = LunaBrushes.B(_palette.InputBg),
            Foreground = LunaBrushes.B(_palette.Text),
            BorderBrush = LunaBrushes.B(_palette.Surface2),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 8)
        };
        // Tunnel so the TextBox's own Enter handling (newline) cannot swallow the
        // send. Shift+Enter still gets a newline; IME composition never sends.
        _input.AddHandler(KeyDownEvent, OnComposerKeyDown, RoutingStrategies.Tunnel);
        _input.AddHandler(TextInputEvent, OnComposerTextInput, RoutingStrategies.Tunnel);
        row.Children.Add(_input);
        _sendButton = new Button
        {
            Content = Text(Loc.T("Client.Messages.Send"), 12, _palette.AccentInk, FontWeight.Bold),
            Background = LunaBrushes.B(_palette.Accent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _sendButton.Click += async (_, _) => await SendAsync();
        Grid.SetColumn(_sendButton, 1);
        row.Children.Add(_sendButton);
        composer.Children.Add(row);
        _sendStatus = Text("", 10.5, _palette.Muted);
        composer.Children.Add(_sendStatus);
        Grid.SetRow(composer, 2);
        grid.Children.Add(composer);

        Dispatcher.UIThread.Post(() => scroll.ScrollToEnd(), DispatcherPriority.Background);
        return grid;
    }

    private void OnComposerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.ImeProcessed or Key.None) return;
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        _suppressEnterText = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            InsertComposerNewline();
        else
            _ = SendAsync();
    }

    private void OnComposerTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        if (_suppressEnterText && (e.Text == "\r" || e.Text == "\n" || e.Text == "\r\n"))
        {
            e.Handled = true;
            _suppressEnterText = false;
            return;
        }
        _suppressEnterText = false;
    }

    private void InsertComposerNewline()
    {
        if (_input is null) return;
        var text = _input.Text ?? string.Empty;
        var start = Math.Clamp(_input.SelectionStart, 0, text.Length);
        var end = Math.Clamp(_input.SelectionEnd, 0, text.Length);
        if (start > end) (start, end) = (end, start);
        if (text.Length - (end - start) >= _input.MaxLength) return;
        _input.Text = text[..start] + "\n" + text[end..];
        _input.CaretIndex = start + 1;
        _input.SelectionStart = _input.CaretIndex;
        _input.SelectionEnd = _input.CaretIndex;
    }

    private void RebuildMessageList(IReadOnlyList<ChildMessageDto> messages)
    {
        if (_messagesPanel is null) return;
        _messagesPanel.Children.Clear();
        _messageRows.Clear();
        _messagesEmpty = null;
        if (messages.Count == 0)
        {
            _messagesEmpty = Text(Loc.T("Client.Messages.Empty"), 12, _palette.Muted);
            _messagesPanel.Children.Add(_messagesEmpty);
        }
        for (var index = 0; index < messages.Count; index++)
        {
            var row = CreateMessageRow(messages[index], index);
            _messageRows[messages[index].Id] = row;
            _messagesPanel.Children.Add(row.Root);
        }
        MarkSeen(messages);
    }

    private void UpdateMessagesInPlace()
    {
        if (_messagesPanel is null) return;
        var previous = _messageRows.Values.OrderBy(row => row.Index).Select(row => row.Message).ToList();
        var delta = MessageThreadDiff.Compare(previous, _snapshot.Messages);
        if (!delta.Changed)
        {
            MarkSeen(delta.Final);
            return;
        }

        var scroll = _messagesScroll;
        var wasAtBottom = IsScrolledToBottom(scroll);
        var offset = scroll?.Offset.Y ?? 0;
        if (delta.OrderChanged || delta.Removed.Count > 0)
        {
            RebuildMessageList(delta.Final);
        }
        else
        {
            foreach (var message in delta.Updated)
            {
                if (_messageRows.TryGetValue(message.Id, out var row))
                    UpdateMessageRow(row, message);
            }
            foreach (var message in delta.Added)
                AddMessageRow(message);
        }
        MarkSeen(delta.Final);

        if (scroll is null) return;
        if (wasAtBottom)
        {
            ScrollMessagesToEnd();
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_messagesScroll != scroll) return;
                var max = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                scroll.Offset = new Vector(scroll.Offset.X, Math.Min(offset, max));
            }, DispatcherPriority.Background);
        }
    }

    private static bool IsScrolledToBottom(ScrollViewer? scroll) =>
        scroll is null || scroll.Extent.Height <= scroll.Viewport.Height + 1 ||
        scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y <= 24;

    private void ScrollMessagesToEnd() =>
        Dispatcher.UIThread.Post(() => _messagesScroll?.ScrollToEnd(), DispatcherPriority.Background);

    private void AddMessageRow(ChildMessageDto message)
    {
        if (_messagesPanel is null || _messageRows.ContainsKey(message.Id)) return;
        if (_messagesEmpty is not null)
        {
            _messagesPanel.Children.Remove(_messagesEmpty);
            _messagesEmpty = null;
        }
        var index = 0;
        foreach (var row in _messageRows.Values.OrderBy(row => row.Index))
        {
            if (row.Message.CreatedUtc <= message.CreatedUtc) index = row.Index + 1;
        }
        var added = CreateMessageRow(message, index);
        _messagesPanel.Children.Insert(Math.Min(index, _messagesPanel.Children.Count), added.Root);
        foreach (var row in _messageRows.Values.Where(row => row.Index >= index))
            row.Index++;
        _messageRows[message.Id] = added;
        _messageRowAdds++;
    }

    private MessageRow CreateMessageRow(ChildMessageDto message, int index)
    {
        var own = message.Direction == MessageDirection.ChildToParent;
        var stack = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 420
        };
        var header = Text(own ? Loc.T("Client.Messages.SentByYou") : Loc.T("Client.Messages.YourParents"),
            9.5, _palette.Muted, FontWeight.Bold, wrap: false);
        stack.Children.Add(header);
        var bubbleText = Text(message.Text, 13, _palette.Text);
        var bubble = new Border
        {
            Background = LunaBrushes.B(own ? _palette.ChildBubble : _palette.Surface2),
            BorderBrush = LunaBrushes.B(own ? _palette.ChildBubbleBorder : _palette.Surface2),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14, 14, own ? 4 : 14, 14),
            Padding = new Thickness(12, 8),
            Child = bubbleText
        };
        stack.Children.Add(bubble);
        var meta = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };
        stack.Children.Add(meta);
        var row = new MessageRow
        {
            Id = message.Id,
            Root = stack,
            Body = bubbleText,
            Header = header,
            Meta = meta,
            Message = message,
            Index = index
        };
        FillMessageMeta(row);
        return row;
    }

    private void UpdateMessageRow(MessageRow row, ChildMessageDto message)
    {
        row.Message = message;
        row.Body.Text = message.Text;
        row.Header.Text = message.Direction == MessageDirection.ChildToParent
            ? Loc.T("Client.Messages.SentByYou")
            : Loc.T("Client.Messages.YourParents");
        FillMessageMeta(row);
    }

    private void FillMessageMeta(MessageRow row)
    {
        var message = row.Message;
        var own = message.Direction == MessageDirection.ChildToParent;
        row.Meta.Children.Clear();
        var when = message.CreatedUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
        row.Meta.Children.Add(Text(when, 9.5, _palette.Muted));
        if (own)
        {
            row.Meta.Children.Add(Text(MessageStatusIcon(message), 10, _palette.Success));
        }
        else if (message.RequireAck && message.AcknowledgedUtc is null)
        {
            var gotIt = new Button
            {
                Content = Text(Loc.T("Client.Messages.GotIt"), 9.5, _palette.AccentInk, FontWeight.Bold),
                Background = LunaBrushes.B(_palette.Accent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 3)
            };
            gotIt.Click += async (_, _) => await AcknowledgeAsync(message.Id);
            row.Meta.Children.Add(gotIt);
        }
    }

    private void MarkSeen(IReadOnlyList<ChildMessageDto> messages)
    {
        foreach (var message in messages.Where(message =>
                     message.Direction == MessageDirection.ParentToChild && message.SeenUtc is null))
        {
            if (_seenRequested.Add(message.Id))
                _ = _data.MarkSeenAsync(message.Id, CancellationToken.None);
        }
    }

    private async Task AcknowledgeAsync(Guid messageId)
    {
        await _data.MarkAcknowledgedAsync(messageId, CancellationToken.None);
        await LoadAsync();
    }

    private async Task SendAsync()
    {
        if (_sending || _input is null || _sendButton is null) return;
        var text = _input.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            if (_sendStatus is not null) _sendStatus.Text = Loc.T("Client.Messages.EmptyInput");
            return;
        }
        if (text.Length > 500)
        {
            if (_sendStatus is not null) _sendStatus.Text = Loc.T("Client.Messages.TooLong");
            return;
        }
        _sending = true;
        _sendButton.IsEnabled = false;
        if (_sendStatus is not null) _sendStatus.Text = Loc.T("Client.Messages.Sending");
        try
        {
            var result = await _data.SendMessageAsync(text, CancellationToken.None);
            if (!result.Ok)
            {
                if (_sendStatus is not null) _sendStatus.Text = result.Error ?? Loc.T("Client.Messages.SendFailed");
                return;
            }
            _input.Text = string.Empty;
            if (_sendStatus is not null)
                _sendStatus.Text = result.Queued ? Loc.T("Client.Messages.Queued") : Loc.T("Client.Messages.Sent");
            await LoadAsync();
            ScrollMessagesToEnd();
            _input.Focus();
        }
        finally
        {
            _sending = false;
            if (_sendButton is not null) _sendButton.IsEnabled = true;
        }
    }

    private string MessageStatusIcon(ChildMessageDto message)
    {
        var status = MessageStatusRules.ForDisplay(message);
        return status switch
        {
            MessageUiStatus.Pending => "◷",
            MessageUiStatus.Failed => "!",
            MessageUiStatus.Delivered => "✓✓",
            MessageUiStatus.Read => "✓✓",
            _ => "✓"
        };
    }

    private Control ScreenTitle(string eyebrow, string title)
    {
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 2) };
        stack.Children.Add(Text(eyebrow.ToUpperInvariant(), 10, _palette.Muted, FontWeight.Bold, wrap: false));
        stack.Children.Add(Text(title, 24, _palette.Text, FontWeight.Bold, LunaThemes.Display));
        return stack;
    }

    private Border Card(Control child) => new()
    {
        Background = LunaBrushes.B(_palette.Surface),
        BorderBrush = LunaBrushes.B(_palette.Surface2),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(20),
        Padding = new Thickness(16),
        Child = child
    };

    private Border Pill(string text, Color background, Color foreground) => new()
    {
        Background = LunaBrushes.B(background),
        CornerRadius = new CornerRadius(20),
        Padding = new Thickness(9, 4),
        Child = Text(text, 10.5, foreground, FontWeight.Bold, wrap: false)
    };

    private Button ActionButton(string text, Action action, bool primary)
    {
        var button = new Button
        {
            Content = Text(text, 11.5, primary ? _palette.AccentInk : _palette.Text, FontWeight.Bold),
            Background = LunaBrushes.B(primary ? _palette.Accent : _palette.Surface2),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Control NoData()
    {
        var stack = new StackPanel { Spacing = 6, Margin = new Thickness(22), MaxWidth = 520 };
        stack.Children.Add(Text(Loc.T("Client.Kid.NoData"), 13, _palette.Muted));
        if (_snapshot.Error is not null)
            stack.Children.Add(Text(_snapshot.Error, 11, _palette.Danger));
        return stack;
    }

    private Control Scroll(Control child) => new ScrollViewer
    {
        Content = child,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    private TextBlock Text(string text, double size, Color color, FontWeight weight = FontWeight.Normal,
        FontFamily? family = null, bool wrap = true) => new()
    {
        Text = text,
        FontFamily = family ?? LunaThemes.Body,
        FontSize = size,
        FontWeight = weight,
        Foreground = LunaBrushes.B(color),
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap
    };

    private Control MoonFace(double size)
    {
        var moon = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = new SolidColorBrush(Color.Parse("#f6dc7e"))
        };
        var grid = new Grid();
        var eyeLeft = new Border
        {
            Width = size * 0.11, Height = size * 0.14,
            CornerRadius = new CornerRadius(size),
            Background = new SolidColorBrush(Color.Parse("#2b2f3a")),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(size * 0.32, size * 0.4, 0, 0)
        };
        var eyeRight = new Border
        {
            Width = size * 0.11, Height = size * 0.14,
            CornerRadius = new CornerRadius(size),
            Background = new SolidColorBrush(Color.Parse("#2b2f3a")),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(size * 0.56, size * 0.4, 0, 0)
        };
        var mouth = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse($"M{size * 0.34},{size * 0.62} Q{size * 0.5},{size * 0.74} {size * 0.66},{size * 0.62}"),
            Stroke = new SolidColorBrush(Color.Parse("#2b2f3a")),
            StrokeThickness = size * 0.05,
            StrokeLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0)
        };
        grid.Children.Add(eyeLeft);
        grid.Children.Add(eyeRight);
        grid.Children.Add(mouth);
        moon.Child = grid;
        return moon;
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds <= 0) return "0m";
        var time = TimeSpan.FromSeconds(seconds);
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        return $"{Math.Max(1, time.Minutes)}m";
    }

    private static string FormatMinutes(int minutes) =>
        minutes >= 1440 ? "12:00 AM" : DateTime.Today.AddMinutes(minutes).ToString("t", CultureInfo.CurrentCulture);

    private static string DayName(int day) => day switch
    {
        0 => "Sun", 1 => "Mon", 2 => "Tue", 3 => "Wed", 4 => "Thu", 5 => "Fri", 6 => "Sat", _ => ""
    };

    private static string DaysLabel(int mask)
    {
        var days = new List<string>();
        for (var i = 0; i < 7; i++)
            if ((mask & (1 << i)) != 0) days.Add(DayName(i));
        return days.Count == 7 ? "Every day" : string.Join(" · ", days);
    }

    private string LocalizedCategory(SummaryCategoryDto category)
    {
        var key = "Client.Kid.Category." + category.Key;
        var localized = Loc.T(key);
        return string.Equals(localized, key, StringComparison.Ordinal) ? category.Name : localized;
    }

    internal string CurrentScreenName => _screen.ToString();
    internal Task RefreshForTestAsync() => LoadAsync();
    internal TextBox? ComposerForTest => _input;
    internal int MessageRowCountForTest => _messageRows.Count;
    internal int MessageListBuildCountForTest => _messageListBuilds;
    internal int MessageRowAddCountForTest => _messageRowAdds;
    internal string MessageBodyForTest(Guid id) =>
        _messageRows.TryGetValue(id, out var row) ? row.Body.Text ?? string.Empty : string.Empty;
    internal string MessageStatusForTest(Guid id) =>
        _messageRows.TryGetValue(id, out var row)
            ? string.Join(" ", row.Meta.Children.OfType<TextBlock>().Select(text => text.Text ?? string.Empty))
            : string.Empty;
    internal Task SendForTestAsync() => SendAsync();
    internal void ShowScreenForTest(string screen) => ShowScreen(ParseScreen(screen));

    private static Screen ParseScreen(string? tab) => (tab ?? "home").ToLowerInvariant() switch
    {
        "schedule" => Screen.Schedule,
        "rules" => Screen.Rules,
        "activity" => Screen.Activity,
        "messages" => Screen.Messages,
        _ => Screen.Home
    };
}

internal static class ControlPositionExtensions
{
    public static T WithPosition<T>(this T control, double left, double top) where T : Control
    {
        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);
        return control;
    }
}
