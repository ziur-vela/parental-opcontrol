// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Globalization;
using Avalonia;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using LiveMarkdown.Avalonia;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Luna;

public sealed class LunaAppView : UserControl
{
    private enum Screen { Loading, Home, Chat, Chats, Paused, Offline }

    private enum ReplyFollowMode { None, Start, Bottom, BottomTail }

    internal const double BubbleWidthFraction = 0.88;

    private readonly LunaApiClient _api;
    private readonly Grid _host = new();
    private readonly Border _root;
    private LunaPalette _palette;
    private Screen _screen = Screen.Loading;
    private LunaStatusDto? _status;
    private List<LunaChatDto> _chats = [];
    private List<LunaChatDto> _hiddenChats = [];
    private LunaChatDto? _chat;
    private List<LunaChildMessageDto> _messages = [];
    private StackPanel? _messagesPanel;
    private ScrollViewer? _messagesScroll;
    private TextBox? _input;
    private Control? _limitPanel;
    private string? _notice;
    private bool _limitReached;
    private bool _cooldown;
    private bool _requestSent;
    private bool _sending;
    private bool _showHidden;
    private string _filter = "all";
    private bool _initialized;
    private string? _pausedDetail;
    private readonly List<DispatcherTimer> _timers = [];
    private DispatcherTimer? _themeWatch;
    private Grid? _modalLayer;
    private Control? _emptyLabel;
    private bool _followTail = true;
    private bool _userScrolledUp;
    private bool _pendingNewMessage;
    private bool _pinToBottom;
    private bool _scrollEndPending;
    private double _lastObservedOffset;
    private long _lastAutoScrollAt;
    private Control? _replyRow;
    private ReplyFollowMode _replyFollowMode = ReplyFollowMode.None;
    private bool _replyStreaming;
    private double _replyStartOffset;
    private Button? _newMessagePill;
    private ObservableStringBuilder? _revealBuilder;
    private List<string>? _revealTokens;
    private int _revealIndex;
    private int _revealPerTick;
    private DispatcherTimer? _revealTimer;

    public event EventHandler? CloseRequested;
    public event Action<double, double>? ResizeRequested;

    public LunaAppView(LunaApiClient api, string? theme)
    {
        _api = api;
        _palette = LunaThemes.From(theme);
        ApplyThemeVariant(_palette);
        ApplyMarkdownResources(_palette);
        _root = new Border
        {
            CornerRadius = new CornerRadius(22),
            BorderThickness = new Thickness(2),
            BorderBrush = LunaBrushes.B(_palette.Border),
            Background = LunaBrushes.B(_palette.Bg),
            ClipToBounds = true,
            Child = _host
        };
        Content = _root;
        AttachedToVisualTree += (_, _) =>
        {
            if (!_initialized)
            {
                _initialized = true;
                _ = InitAsync();
            }
        };
        DetachedFromVisualTree += (_, _) => StopTimers();
    }

    public void Start()
    {
        if (!_initialized)
        {
            _initialized = true;
            _ = InitAsync();
        }
    }

    private async Task InitAsync()
    {
        ShowLoading();
        try
        {
            await _api.ConnectAsync(dev: Environment.GetCommandLineArgs().Contains("--dev", StringComparer.OrdinalIgnoreCase));
            _status = await _api.GetStatusAsync();
            Loc.SetLanguage(_status.Language);
            _palette = LunaThemes.From(_status.Theme);
            KidThemeStore.WriteLocal(_palette.Key);
            ApplyThemeVariant(_palette);
            ApplyMarkdownResources(_palette);
            _root.Background = LunaBrushes.B(_palette.Bg);
            _root.BorderBrush = LunaBrushes.B(_palette.Border);
            _themeWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _themeWatch.Tick += (_, _) =>
            {
                var local = KidThemeStore.ReadLocal();
                if (local is not null && !string.Equals(local, _palette.Key, StringComparison.Ordinal))
                    _ = SetThemeKeyAsync(local);
            };
            _themeWatch.Start();
            _timers.Add(_themeWatch);
            if (!_status.Enabled)
            {
                ShowPaused();
                return;
            }
            var all = await _api.GetChatsAsync(includeHidden: true);
            _chats = all.Where(c => !c.HiddenByChild).ToList();
            _hiddenChats = all.Where(c => c.HiddenByChild).ToList();
            ShowHome();
        }
        catch (LunaApiException ex) when (ex.Status is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized)
        {
            ShowPaused();
        }
        catch (Exception ex)
        {
            LunaLog.Exception("init", ex);
            ShowOffline();
        }
    }

    private void SetScreen(Screen screen)
    {
        _screen = screen;
        _modalLayer = null;
    }

    private void ShowLoading()
    {
        SetScreen(Screen.Loading);
        _host.Children.Clear();
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 14
        };
        stack.Children.Add(CreateMoonFace(72, sleepy: false));
        stack.Children.Add(Text(Loc.T("Luna.Common.Loading"), 16, _palette.Muted, FontWeight.SemiBold));
        _host.Children.Add(new Grid { Children = { stack } });
    }

    private void ShowPaused()
    {
        SetScreen(Screen.Paused);
        var grid = StateScreen(
            CreateMoonFace(92, sleepy: true),
            Loc.T("Luna.Chat.Paused"),
            _pausedDetail ?? Loc.T("Luna.Chat.PausedDetail"),
            showRetry: false);
        _host.Children.Clear();
        _host.Children.Add(grid);
    }

    private void ShowOffline()
    {
        SetScreen(Screen.Offline);
        var grid = StateScreen(
            CreateMoonFace(92, sleepy: true),
            Loc.T("Luna.Chat.Offline"),
            "",
            showRetry: true);
        _host.Children.Clear();
        _host.Children.Add(grid);
    }

    private Grid StateScreen(Control art, string title, string detail, bool showRetry)
    {
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 14,
            MaxWidth = 420,
            Margin = new Thickness(24)
        };
        stack.Children.Add(art);
        stack.Children.Add(Text(title, 24, _palette.Text, FontWeight.SemiBold, center: true, display: true));
        if (!string.IsNullOrWhiteSpace(detail))
            stack.Children.Add(Text(detail, 14, _palette.Muted, FontWeight.SemiBold, center: true, wrap: true));
        if (showRetry)
        {
            var retry = PillButton(Loc.T("Luna.Chat.Retry"), _palette.Accent, _palette.AccentInk, 48);
            retry.Click += (_, _) => _ = InitAsync();
            stack.Children.Add(retry);
        }
        var close = PillButton(Loc.T("Luna.Common.Close"), _palette.Surface, _palette.Text, 48);
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        stack.Children.Add(close);
        return new Grid { Children = { stack } };
    }

    private void ShowHome()
    {
        SetScreen(Screen.Home);
        _host.Children.Clear();

        var layer = new Grid();
        layer.Children.Add(ThemeBackdrop());
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            Margin = new Thickness(18, 16, 16, 8)
        };
        var face = CreateMoonFace(48, sleepy: false);
        Grid.SetColumn(face, 0);
        header.Children.Add(face);
        var greeting = new StackPanel { Spacing = 0, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        greeting.Children.Add(Text(Loc.T("Luna.Home.Hi", _status?.ChildFirstName ?? "Ana"), 24, _palette.Text, FontWeight.SemiBold, display: true));
        greeting.Children.Add(Text(Loc.T("Luna.Home.Question"), 14, _palette.Muted, FontWeight.Bold));
        Grid.SetColumn(greeting, 1);
        header.Children.Add(greeting);
        var themeButton = IconButton(ThemeIcon(), Loc.T("Luna.Theme.Pick"));
        themeButton.Click += (_, _) => _ = ToggleThemeAsync();
        Grid.SetColumn(themeButton, 2);
        header.Children.Add(themeButton);
        var close = IconButton(CloseIcon(), Loc.T("Luna.Common.Close"));
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        close.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(close, 3);
        header.Children.Add(close);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var tiles = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            Margin = new Thickness(18, 4, 18, 0)
        };
        var subjects = new[] { "math", "science", "history", "dictionary", "languages", "curious" };
        for (var i = 0; i < subjects.Length; i++)
        {
            var tile = SubjectTile(subjects[i]);
            Grid.SetRow(tile, i / 3);
            Grid.SetColumn(tile, i % 3);
            tile.Margin = TileMargin(i);
            tiles.Children.Add(tile);
        }
        Grid.SetRow(tiles, 1);
        root.Children.Add(tiles);

        var chats = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(10, 14, 10, 0) };
        var chatHead = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(8, 0, 8, 6) };
        chatHead.Children.Add(Text(Loc.T("Luna.Home.MyChats"), 18, _palette.Text, FontWeight.SemiBold, display: true));
        var seeAll = LinkButton(Loc.T("Luna.Home.SeeAll"), _palette.Key == "gray" ? Color.Parse("#7a5f00") : _palette.Accent);
        seeAll.Click += (_, _) => ShowChats();
        Grid.SetColumn(seeAll, 1);
        chatHead.Children.Add(seeAll);
        Grid.SetRow(chatHead, 0);
        chats.Children.Add(chatHead);

        var list = new StackPanel { Spacing = 2 };
        if (_chats.Count == 0)
        {
            list.Children.Add(Text(Loc.T("Luna.Home.NoChats"), 14, _palette.Muted, FontWeight.SemiBold, wrap: true));
        }
        else
        {
            foreach (var chat in _chats.Take(3))
                list.Children.Add(ChatRow(chat));
        }
        var scroll = new ScrollViewer
        {
            Content = list,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 1);
        chats.Children.Add(scroll);
        Grid.SetRow(chats, 2);
        root.Children.Add(chats);

        var disclaimer = Text(Loc.T("Luna.Disclaimer"), 12, _palette.Soft, FontWeight.Bold, center: true, wrap: true);
        disclaimer.Margin = new Thickness(18, 8, 18, 16);
        Grid.SetRow(disclaimer, 3);
        root.Children.Add(disclaimer);

        layer.Children.Add(root);
        layer.Children.Add(CreateResizeGrip());
        _host.Children.Add(layer);
    }

    private static Thickness TileMargin(int i)
    {
        var left = i % 3 == 0 ? 0 : 6;
        var right = i % 3 == 2 ? 0 : 6;
        var top = i < 3 ? 0 : 8;
        return new Thickness(left, top, right, 0);
    }

    private Button SubjectTile(string subject)
    {
        var style = _palette.Subject(subject);
        var stack = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(SubjectIcon(subject, style.Ink));
        stack.Children.Add(Text(Loc.T("Luna.Subject." + SubjectKey(subject)), 16, style.Ink, FontWeight.SemiBold, display: true));
        var tile = new Button
        {
            Content = stack,
            Height = 86,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = LunaBrushes.B(style.Tile),
            Foreground = LunaBrushes.B(style.Ink),
            BorderBrush = LunaBrushes.B(style.Shadow),
            BorderThickness = new Thickness(0, 0, 0, 4),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(2),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        tile.Click += (_, _) => _ = StartChatAsync(subject);
        return tile;
    }

    private static string SubjectKey(string subject) => subject switch
    {
        "math" => "Math",
        "science" => "Science",
        "history" => "History",
        "dictionary" => "Dictionary",
        "languages" => "Languages",
        _ => "Curious"
    };

    private async Task StartChatAsync(string subject)
    {
        try
        {
            var chat = await _api.CreateChatAsync(subject);
            _chats = await _api.GetChatsAsync();
            await OpenChatAsync(chat);
        }
        catch (Exception ex)
        {
            LunaLog.Exception("create-chat", ex);
            ShowOffline();
        }
    }

    private async Task OpenChatAsync(LunaChatDto chat)
    {
        _chat = chat;
        _notice = _status?.Notice;
        _limitReached = _status?.DailyLimitReached == true;
        _cooldown = _status?.CooldownUntilUtc is { } until && until > DateTime.UtcNow;
        _requestSent = false;
        try
        {
            _messages = await _api.GetMessagesAsync(chat.Id);
        }
        catch (Exception ex)
        {
            LunaLog.Exception("messages", ex);
            _messages = [];
        }
        _followTail = true;
        _userScrolledUp = false;
        _pendingNewMessage = false;
        _replyRow = null;
        _replyFollowMode = ReplyFollowMode.None;
        ShowChat();
    }

    private void ShowChat()
    {
        SetScreen(Screen.Chat);
        if (_chat is null) { ShowHome(); return; }
        var previousOffset = _messagesScroll?.Offset.Y ?? 0;
        var hadMessages = _messagesScroll is not null;
        _lastAutoScrollAt = Environment.TickCount64;
        _lastObservedOffset = 0;
        if (_revealTimer is not null)
        {
            _revealTimer.Stop();
            _timers.Remove(_revealTimer);
            _revealTimer = null;
        }
        _revealBuilder = null;
        _revealTokens = null;
        _replyStreaming = false;
        _replyRow = null;
        _replyFollowMode = ReplyFollowMode.None;
        _host.Children.Clear();
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto") };
        root.Children.Add(ChatHeader());

        _messagesPanel = new StackPanel { Spacing = 10, Margin = new Thickness(12, 8, 12, 8) };
        foreach (var message in _messages)
        {
            if (message.Role == LunaChatRoles.Child)
                _messagesPanel.Children.Add(ChildBubble(message.Text, Flagged(message.Flag)));
            else
                _messagesPanel.Children.Add(LunaBubble(message.Text));
        }
        _emptyLabel = null;
        if (_messages.Count == 0)
        {
            _emptyLabel = Text(Loc.T("Luna.Chat.NoMessages"), 13, _palette.Muted, FontWeight.SemiBold, wrap: true);
            _messagesPanel.Children.Add(_emptyLabel);
        }

        _messagesScroll = new ScrollViewer
        {
            Content = _messagesPanel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        _messagesScroll.ScrollChanged += OnMessagesScrollChanged;
        _messagesScroll.LayoutUpdated += OnMessagesLayoutUpdated;
        Grid.SetRow(_messagesScroll, 1);
        root.Children.Add(_messagesScroll);

        _newMessagePill = NewMessagePill();
        _newMessagePill.IsVisible = _pendingNewMessage;
        Grid.SetRow(_newMessagePill, 1);
        root.Children.Add(_newMessagePill);

        var middle = new StackPanel { Spacing = 6, Margin = new Thickness(12, 0, 12, 6) };
        if (_status is { MessagesLeftToday: > 0 and <= 5 } && !_limitReached)
            middle.Children.Add(LeftPill(_status.MessagesLeftToday == 1
                ? Loc.T("Luna.Chat.MessagesLeftOne")
                : Loc.T("Luna.Chat.MessagesLeft", _status.MessagesLeftToday)));
        if (!_limitReached && !string.IsNullOrWhiteSpace(_notice))
            middle.Children.Add(NoticeBanner(_notice));
        else if (_cooldown)
            middle.Children.Add(NoticeBanner(Loc.T("Luna.Chat.Cooldown")));
        Grid.SetRow(middle, 2);
        root.Children.Add(middle);

        _limitPanel = _limitReached ? LimitPanel() : null;
        if (_limitPanel is not null)
        {
            Grid.SetRow(_limitPanel, 3);
            root.Children.Add(_limitPanel);
        }

        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(12, 0, 12, 14) };
        if (!_limitReached)
        {
            var inputRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            _input = new TextBox
            {
                PlaceholderText = Loc.T("Luna.Chat.Placeholder"),
                MaxLength = 1000,
                Height = 46,
                FontFamily = LunaThemes.Body,
                FontSize = 14,
                Background = LunaBrushes.B(_palette.InputBg),
                Foreground = LunaBrushes.B(_palette.Text),
                BorderBrush = LunaBrushes.B(_palette.Surface2),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                IsEnabled = !_cooldown
            };
            _input.KeyDown += OnInputKeyDown;
            Grid.SetColumn(_input, 0);
            inputRow.Children.Add(_input);
            var ask = PillButton(Loc.T("Luna.Chat.Ask"), _palette.Accent, _palette.AccentInk, 46, 96);
            ask.IsEnabled = !_cooldown;
            ask.Click += (_, _) => _ = SendFromInputAsync();
            Grid.SetColumn(ask, 1);
            inputRow.Children.Add(ask);
            footer.Children.Add(inputRow);
        }
        else
        {
            footer.Children.Add(Text(Loc.T("Luna.Chat.RestUntilTomorrow"), 13, _palette.Muted, FontWeight.SemiBold, center: true, wrap: true));
        }
        footer.Children.Add(Text(Loc.T("Luna.Disclaimer"), 11, _palette.Soft, FontWeight.Bold, center: true, wrap: true));
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        var grip = CreateResizeGrip();
        Grid.SetRow(grip, 0);
        Grid.SetRowSpan(grip, 5);
        root.Children.Add(grip);
        _host.Children.Add(root);
        if (hadMessages && _userScrolledUp)
            RestoreScroll(previousOffset);
        else
            ScrollToEndSoon();
    }

    private Control ChatHeader()
    {
        var style = _palette.Subject(_chat!.Subject);
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto,Auto"),
            Margin = new Thickness(12, 12, 12, 6)
        };
        var back = IconButton(BackIcon(), Loc.T("Luna.Common.Back"));
        back.Click += (_, _) => ShowHome();
        Grid.SetColumn(back, 0);
        header.Children.Add(back);
        var icon = SubjectIcon(_chat.Subject, style.Dot);
        icon.Margin = new Thickness(8, 0, 8, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(icon, 1);
        header.Children.Add(icon);
        var titles = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(Text(Loc.T("Luna.Subject.WithLuna", Loc.T("Luna.Subject." + SubjectKey(_chat.Subject))), 12, _palette.Muted, FontWeight.Bold));
        titles.Children.Add(Text(string.IsNullOrWhiteSpace(_chat.Title) ? Loc.T("Luna.Chat.NewChat") : _chat.Title, 18, _palette.Text, FontWeight.SemiBold, display: true));
        Grid.SetColumn(titles, 2);
        header.Children.Add(titles);
        var theme = IconButton(ThemeIcon(), Loc.T("Luna.Theme.Pick"));
        theme.Click += (_, _) => _ = ToggleThemeAsync();
        Grid.SetColumn(theme, 3);
        header.Children.Add(theme);
        var rename = IconButton(MoreIcon(), Loc.T("Luna.Chat.Options"));
        rename.Click += (_, _) => ShowChatMenu(rename);
        rename.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(rename, 4);
        header.Children.Add(rename);
        var close = IconButton(CloseIcon(), Loc.T("Luna.Common.Close"));
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        close.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(close, 5);
        header.Children.Add(close);
        return header;
    }

    private void ShowChatMenu(Control anchor)
    {
        if (_chat is null) return;
        var flyout = new MenuFlyout();
        var rename = new MenuItem { Header = Loc.T("Luna.Common.Rename") };
        rename.Click += (_, _) => ShowRenameDialog(_chat);
        var hide = new MenuItem { Header = Loc.T("Luna.Common.Hide") };
        hide.Click += (_, _) => ShowHideDialog(_chat);
        flyout.Items.Add(rename);
        flyout.Items.Add(hide);
        flyout.ShowAt(anchor);
    }

    private void ShowChats()
    {
        SetScreen(Screen.Chats);
        _host.Children.Clear();
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Margin = new Thickness(12, 14, 12, 6) };
        var back = IconButton(BackIcon(), Loc.T("Luna.Common.Back"));
        back.Click += (_, _) => ShowHome();
        header.Children.Add(back);
        var title = Text(Loc.T("Luna.Home.MyChats"), 24, _palette.Text, FontWeight.SemiBold, display: true);
        title.Margin = new Thickness(10, 0, 0, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        var theme = IconButton(ThemeIcon(), Loc.T("Luna.Theme.Pick"));
        theme.Click += (_, _) => _ = ToggleThemeAsync();
        Grid.SetColumn(theme, 2);
        header.Children.Add(theme);
        var close = IconButton(CloseIcon(), Loc.T("Luna.Common.Close"));
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        close.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(close, 3);
        header.Children.Add(close);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var chips = new WrapPanel { Margin = new Thickness(12, 4, 12, 8) };
        foreach (var key in new[] { "all", "math", "science", "history", "dictionary", "languages", "curious" })
        {
            var label = key == "all" ? Loc.T("Luna.Common.All") : Loc.T("Luna.Subject." + SubjectKey(key));
            var chip = new Button
            {
                Content = label,
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 6, 6),
                CornerRadius = new CornerRadius(14),
                FontFamily = LunaThemes.Body,
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                Background = key == _filter ? LunaBrushes.B(_palette.Accent) : LunaBrushes.B(_palette.Surface),
                Foreground = key == _filter ? LunaBrushes.B(_palette.AccentInk) : LunaBrushes.B(_palette.Text),
                BorderBrush = LunaBrushes.B(_palette.Surface2),
                BorderThickness = new Thickness(1)
            };
            var selected = key;
            chip.Click += (_, _) => { _filter = selected; ShowChats(); };
            chips.Children.Add(chip);
        }
        Grid.SetRow(chips, 1);
        root.Children.Add(chips);

        var list = new StackPanel { Spacing = 4, Margin = new Thickness(10, 0, 10, 0) };
        var filtered = _filter == "all" ? _chats : _chats.Where(c => c.Subject == _filter).ToList();
        if (filtered.Count == 0)
        {
            list.Children.Add(Text(Loc.T("Luna.Home.NoChats"), 14, _palette.Muted, FontWeight.SemiBold, wrap: true));
        }
        else
        {
            AddGroup(list, Loc.T("Luna.Chats.Today"), filtered.Where(c => c.LastMessageUtc.ToLocalTime().Date == DateTime.Today));
            AddGroup(list, Loc.T("Luna.Chats.ThisWeek"), filtered.Where(c =>
            {
                var d = c.LastMessageUtc.ToLocalTime().Date;
                return d < DateTime.Today && d >= DateTime.Today.AddDays(-6);
            }));
            AddGroup(list, Loc.T("Luna.Chats.Earlier"), filtered.Where(c => c.LastMessageUtc.ToLocalTime().Date < DateTime.Today.AddDays(-6)));
        }
        if (_showHidden && _hiddenChats.Count > 0)
        {
            list.Children.Add(SectionTitle(Loc.T("Luna.Chats.HiddenCount", _hiddenChats.Count)));
            foreach (var chat in _hiddenChats)
                list.Children.Add(ChatRow(chat, hidden: true));
        }
        var scroll = new ScrollViewer
        {
            Content = list,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);

        var hiddenButton = LinkButton(
            _showHidden ? Loc.T("Luna.Common.Hide") : Loc.T("Luna.Chats.HiddenCount", _hiddenChats.Count),
            _palette.Muted);
        hiddenButton.HorizontalAlignment = HorizontalAlignment.Left;
        hiddenButton.Margin = new Thickness(18, 8, 18, 16);
        hiddenButton.Click += async (_, _) =>
        {
            _showHidden = !_showHidden;
            if (_showHidden)
            {
                try { _hiddenChats = (await _api.GetChatsAsync(includeHidden: true)).Where(c => c.HiddenByChild).ToList(); }
                catch { _hiddenChats = []; }
            }
            ShowChats();
        };
        Grid.SetRow(hiddenButton, 3);
        root.Children.Add(hiddenButton);

        var grip = CreateResizeGrip();
        Grid.SetRow(grip, 0);
        Grid.SetRowSpan(grip, 4);
        root.Children.Add(grip);
        _host.Children.Add(new Grid { Children = { ThemeBackdrop(), root } });
    }

    private void AddGroup(StackPanel list, string title, IEnumerable<LunaChatDto> chats)
    {
        var rows = chats.ToList();
        if (rows.Count == 0) return;
        list.Children.Add(SectionTitle(title));
        foreach (var chat in rows) list.Children.Add(ChatRow(chat));
    }

    private TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontFamily = LunaThemes.Display,
        FontSize = 15,
        FontWeight = FontWeight.SemiBold,
        Foreground = LunaBrushes.B(_palette.Muted),
        Margin = new Thickness(8, 10, 8, 2)
    };

    private Control ChatRow(LunaChatDto chat, bool hidden = false) => ChatRowStatic(chat, this, hidden);

    private Control ChatRowStatic(LunaChatDto chat, LunaAppView? owner = null, bool hidden = false)
    {
        var palette = _palette;
        var style = palette.Subject(chat.Subject);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(8, 2, 8, 2), MinHeight = 48 };
        var dot = new Ellipse { Width = 14, Height = 14, Fill = LunaBrushes.B(style.Dot), VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(dot);
        var stack = new StackPanel { Spacing = 0, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(Text(string.IsNullOrWhiteSpace(chat.Title) ? Loc.T("Luna.Chat.NewChat") : chat.Title, 16, palette.Text, FontWeight.Bold));
        stack.Children.Add(Text($"{Loc.T("Luna.Subject." + SubjectKey(chat.Subject))} · {FriendlyDate(chat.LastMessageUtc)}", 13, palette.Muted, FontWeight.SemiBold));
        Grid.SetColumn(stack, 1);
        row.Children.Add(stack);
        var more = new Button
        {
            Content = MoreIcon(),
            Width = 44,
            Height = 44,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Foreground = LunaBrushes.B(palette.Muted),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        if (owner is not null)
        {
            var flyout = new MenuFlyout();
            var rename = new MenuItem { Header = Loc.T("Luna.Common.Rename") };
            rename.Click += (_, _) => owner.ShowRenameDialog(chat);
            var hide = new MenuItem { Header = hidden ? Loc.T("Luna.Common.Unhide") : Loc.T("Luna.Common.Hide") };
            hide.Click += (_, _) => _ = owner.ToggleHiddenAsync(chat, !hidden);
            flyout.Items.Add(rename);
            flyout.Items.Add(hide);
            more.Flyout = flyout;
            if (!hidden)
            {
                var open = new Button
                {
                    Content = row,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                open.Click += (_, _) => _ = owner.OpenChatAsync(chat);
                var wrapper = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                wrapper.Children.Add(open);
                Grid.SetColumn(more, 1);
                wrapper.Children.Add(more);
                return wrapper;
            }
        }
        Grid.SetColumn(more, 2);
        row.Children.Add(more);
        return row;
    }

    private static string FriendlyDate(DateTime utc)
    {
        var local = utc.ToLocalTime();
        if (local.Date == DateTime.Today)
            return Loc.T("Luna.Chats.Today");
        if (local.Date >= DateTime.Today.AddDays(-6))
            return local.ToString("dddd", CultureInfo.CurrentCulture);
        return local.ToString("MMM d", CultureInfo.CurrentCulture);
    }

    private Control LunaBubble(string text)
    {
        var grid = new Grid { ColumnDefinitions = BubbleColumns(withFace: true) };
        var face = CreateMoonFace(28, sleepy: false);
        face.VerticalAlignment = VerticalAlignment.Top;
        face.Margin = new Thickness(0, 2, 8, 0);
        grid.Children.Add(face);
        var content = new LunaRichContent(text, _palette, _chat?.Subject ?? "curious");
        var bubble = BubbleFrame(content, _palette, double.PositiveInfinity);
        Grid.SetColumn(bubble, 1);
        grid.Children.Add(bubble);
        return grid;
    }

    internal static ColumnDefinitions BubbleColumns(bool withFace)
    {
        var wide = BubbleWidthFraction.ToString("0.##", CultureInfo.InvariantCulture);
        var narrow = (1 - BubbleWidthFraction).ToString("0.##", CultureInfo.InvariantCulture);
        return new ColumnDefinitions(withFace ? $"Auto,{wide}*,{narrow}*" : $"{narrow}*,{wide}*");
    }

    internal static Border BubbleFrame(Control content, LunaPalette palette, double maxWidth = 380) => new()
    {
        Child = content,
        Background = LunaBrushes.B(palette.LunaBubble),
        BorderBrush = LunaBrushes.B(palette.LunaBubbleBorder),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14),
        Padding = new Thickness(12, 9, 12, 9),
        MaxWidth = maxWidth,
        HorizontalAlignment = HorizontalAlignment.Left
    };

    private Control ChildBubble(string text, bool flagged)
    {
        var style = _palette.Subject(_chat?.Subject ?? "curious");
        var gray = _palette.Key == "gray";
        var bubbleBg = flagged ? (gray ? Color.Parse("#f4ece1") : Color.Parse("#3d3478")) : style.Tile;
        var bubbleInk = flagged ? (gray ? Color.Parse("#5a4a33") : Color.Parse("#d9d3ff")) : style.Ink;
        var border = flagged ? (gray ? Color.Parse("#c9a26b") : Color.Parse("#6b5fc4")) : style.Shadow;
        var content = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = LunaThemes.Body,
            FontSize = 14.5,
            LineHeight = 20,
            Foreground = LunaBrushes.B(bubbleInk)
        };
        var bubble = new Border
        {
            Child = content,
            Background = LunaBrushes.B(bubbleBg),
            BorderBrush = LunaBrushes.B(flagged ? Colors.Transparent : border),
            BorderThickness = new Thickness(flagged ? 0 : 1),
            CornerRadius = new CornerRadius(14, 14, flagged ? 4 : 14, 14),
            Padding = new Thickness(12, 9, 12, 9),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        if (!flagged)
        {
            var wrapper = new Grid { ColumnDefinitions = BubbleColumns(withFace: false) };
            Grid.SetColumn(bubble, 1);
            wrapper.Children.Add(bubble);
            return wrapper;
        }

        var dashed = new Rectangle
        {
            Stroke = LunaBrushes.B(border),
            StrokeThickness = 2,
            StrokeDashArray = new AvaloniaList<double> { 3, 3 },
            RadiusX = 14,
            RadiusY = 14,
            IsHitTestVisible = false
        };
        var bubbleGrid = new Grid { Children = { bubble, dashed }, HorizontalAlignment = HorizontalAlignment.Right };
        var noteRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 3, 2, 0)
        };
        noteRow.Children.Add(new Path
        {
            Data = Geometry.Parse("M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20M12 8v5M12 16h.01"),
            Stroke = LunaBrushes.B(_palette.Muted),
            StrokeThickness = 2.2,
            StrokeLineCap = PenLineCap.Round,
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform
        });
        noteRow.Children.Add(Text(Loc.T("Luna.Chat.BlockedNote"), 11.5, _palette.Muted, FontWeight.Bold));
        var stack = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        stack.Children.Add(bubbleGrid);
        stack.Children.Add(noteRow);
        var flaggedWrapper = new Grid { ColumnDefinitions = BubbleColumns(withFace: false) };
        Grid.SetColumn(stack, 1);
        flaggedWrapper.Children.Add(stack);
        return flaggedWrapper;
    }

    private static bool Flagged(string? flag) =>
        string.Equals(flag, "blocked", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(flag, "urgent", StringComparison.OrdinalIgnoreCase);

    private (Control Bubble, ThinkingView Thinking)? AddPendingChild(string text)
    {
        if (_messagesPanel is null || _chat is null) return null;
        if (_emptyLabel is not null)
        {
            _messagesPanel.Children.Remove(_emptyLabel);
            _emptyLabel = null;
        }
        var local = new LunaChildMessageDto(Guid.NewGuid(), _chat.Id, LunaChatRoles.Child, text, DateTime.UtcNow, "none");
        _messages.Add(local);
        var bubble = ChildBubble(text, flagged: false);
        _messagesPanel.Children.Add(bubble);
        var thinking = new ThinkingView(_palette);
        _messagesPanel.Children.Add(thinking);
        _followTail = true;
        _userScrolledUp = false;
        _pendingNewMessage = false;
        _replyRow = null;
        _replyFollowMode = ReplyFollowMode.None;
        HideNewMessagePill();
        ScrollToEndSoon();
        return (bubble, thinking);
    }

    private async Task SendFromInputAsync()
    {
        if (_sending || _chat is null || _input is null) return;
        var text = _input.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 1000) text = text[..1000];
        _input.Text = string.Empty;
        _sending = true;
        _notice = null;
        var pending = AddPendingChild(text);
        if (pending is null) { _sending = false; return; }
        var (bubble, thinking) = pending.Value;
        var local = _messages[^1];
        try
        {
            var response = await _api.SendMessageAsync(_chat.Id, text);
            thinking.Stop();
            _messagesPanel?.Children.Remove(thinking);
            if (response.ChildMessage is { } child)
            {
                local = child;
                _messages[^1] = child;
                if (Flagged(child.Flag))
                {
                    _messagesPanel?.Children.Remove(bubble);
                    _messagesPanel?.Children.Add(ChildBubble(child.Text, flagged: true));
                }
            }
            _limitReached = response.LimitReached;
            _cooldown = response.Cooldown;
            _notice = response.Notice;
            if (response.LunaMessage is null && !response.LimitReached && !response.Cooldown
                && !string.IsNullOrWhiteSpace(response.Notice))
            {
                _pausedDetail = response.Notice;
                thinking.Stop();
                _messagesPanel?.Children.Remove(thinking);
                ShowPaused();
                return;
            }
            if (_status is not null)
                _status = _status with
                {
                    MessagesLeftToday = response.MessagesLeftToday,
                    DailyLimitReached = response.LimitReached,
                    CooldownUntilUtc = response.Cooldown ? DateTime.UtcNow.AddMinutes(15) : _status.CooldownUntilUtc
                };
            if (response.LunaMessage is { } luna)
            {
                _messages.Add(luna);
                AddStreamingReply(luna.Text);
            }
            _requestSent = false;
            await RefreshChatsQuietAsync();
        }
        catch (Exception ex)
        {
            LunaLog.Exception("send", ex);
            thinking.Stop();
            _messagesPanel?.Children.Remove(thinking);
            _messagesPanel?.Children.Add(NoticeBanner(Loc.T("Luna.Chat.SendError")));
        }
        finally
        {
            _sending = false;
            if (_limitReached)
            {
                ShowChat();
            }
            else
            {
                var refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                refresh.Tick += async (_, _) =>
                {
                    refresh.Stop();
                    _timers.Remove(refresh);
                    await RefreshChatsQuietAsync();
                    if (_screen == Screen.Chat && !_sending) ShowChat();
                };
                _timers.Add(refresh);
                refresh.Start();
            }
        }
    }

    private int _messagesLeft => _status?.MessagesLeftToday ?? 0;

    private void AddStreamingReply(string markdown)
    {
        if (_messagesPanel is null) return;
        _replyStreaming = true;

        var row = new Grid { ColumnDefinitions = BubbleColumns(withFace: true) };
        var face = CreateMoonFace(28, sleepy: false);
        face.VerticalAlignment = VerticalAlignment.Top;
        face.Margin = new Thickness(0, 2, 8, 0);
        row.Children.Add(face);

        if (LunaRichContent.HasPictures(markdown))
        {
            var rich = BubbleFrame(new LunaRichContent(markdown, _palette, _chat?.Subject ?? "curious"),
                _palette, double.PositiveInfinity);
            Grid.SetColumn(rich, 1);
            row.Children.Add(rich);
            _messagesPanel.Children.Add(row);
            _replyStreaming = false;
            BeginReplyFollow(row);
            return;
        }

        var builder = new ObservableStringBuilder();
        var renderer = new MarkdownRenderer { MarkdownBuilder = builder };
        var bubble = BubbleFrame(renderer, _palette, double.PositiveInfinity);
        Grid.SetColumn(bubble, 1);
        row.Children.Add(bubble);
        _messagesPanel.Children.Add(row);
        BeginReplyFollow(row);

        if (_revealTimer is not null)
        {
            _revealTimer.Stop();
            _timers.Remove(_revealTimer);
            _revealTimer = null;
        }
        _revealBuilder = builder;
        _revealTokens = Tokenize(LunaMarkdown.Prepare(markdown));
        _revealPerTick = Math.Max(1, (int)Math.Ceiling(_revealTokens.Count / 50.0));
        _revealIndex = 0;
        _revealTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(28) };
        _revealTimer.Tick += (_, _) => AdvanceStreaming();
        _timers.Add(_revealTimer);
        _revealTimer.Start();
    }

    private void AdvanceStreaming()
    {
        if (_revealBuilder is null || _revealTokens is null) return;
        var end = Math.Min(_revealTokens.Count, _revealIndex + _revealPerTick);
        for (; _revealIndex < end; _revealIndex++)
            _revealBuilder.Append(_revealTokens[_revealIndex]);
        KeepReplyInView();
        if (_revealIndex < _revealTokens.Count) return;

        _replyStreaming = false;
        _revealTimer?.Stop();
        if (_revealTimer is not null) _timers.Remove(_revealTimer);
        _revealTimer = null;
        _revealBuilder = null;
        _revealTokens = null;
    }

    internal void AdvanceStreamingForTest(int ticks = 1)
    {
        for (var i = 0; i < ticks; i++) AdvanceStreaming();
    }

    private static List<string> Tokenize(string text)
    {
        var parts = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                if (i > start) parts.Add(text[start..i]);
                var ws = i;
                while (i + 1 < text.Length && char.IsWhiteSpace(text[i + 1])) i++;
                parts.Add(text[ws..(i + 1)]);
                start = i + 1;
            }
        }
        if (start < text.Length) parts.Add(text[start..]);
        return parts;
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            _ = SendFromInputAsync();
        }
    }

    private Control LimitPanel()
    {
        var stack = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(12, 6, 12, 8) };
        stack.Children.Add(CreateMoonFace(66, sleepy: true));
        stack.Children.Add(Text(Loc.T("Luna.Chat.LimitTitle"), 20, _palette.Text, FontWeight.SemiBold, center: true, display: true));
        var message = string.IsNullOrWhiteSpace(_notice)
            ? Loc.T("Luna.Chat.RestUntilTomorrow")
            : _notice!;
        stack.Children.Add(Text(message, 13, _palette.Muted, FontWeight.SemiBold, center: true, wrap: true));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        var ask = PillButton(_requestSent ? Loc.T("Luna.Chat.RequestSent") : Loc.T("Luna.Chat.AskParents"), _palette.Accent, _palette.AccentInk, 44);
        ask.IsEnabled = !_requestSent;
        ask.Click += (_, _) => _ = RequestMoreAsync();
        var ok = PillButton(Loc.T("Luna.Chat.Ok"), _palette.Surface, _palette.Text, 44);
        ok.Click += (_, _) => ShowHome();
        row.Children.Add(ask);
        row.Children.Add(ok);
        stack.Children.Add(row);
        return stack;
    }

    private async Task RequestMoreAsync()
    {
        if (_requestSent) return;
        try
        {
            await _api.RequestMoreAsync();
            _requestSent = true;
            ShowChat();
        }
        catch (Exception ex)
        {
            LunaLog.Exception("request-more", ex);
            _notice = Loc.T("Luna.Chat.SendError");
            ShowChat();
        }
    }

    private Control NoticeBanner(string text)
    {
        return new Border
        {
            Child = Text(text, 12, _palette.Text, FontWeight.SemiBold, wrap: true),
            Background = LunaBrushes.B(_palette.Surface),
            BorderBrush = LunaBrushes.B(_palette.Danger),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6, 10, 6)
        };
    }

    private Control LeftPill(string text)
    {
        return new Border
        {
            Child = Text(text, 12, _palette.AccentInk, FontWeight.Bold),
            Background = LunaBrushes.B(_palette.Accent),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    private async Task ToggleThemeAsync()
    {
        var next = LunaThemes.Toggle(_status?.Theme ?? _palette.Key);
        _palette = next;
        KidThemeStore.WriteLocal(next.Key);
        ApplyThemeVariant(_palette);
        ApplyMarkdownResources(_palette);
        if (_status is not null) _status = _status with { Theme = next.Key };
        try { await _api.SetThemeAsync(next.Key); }
        catch (Exception ex) { LunaLog.Exception("theme", ex); }
        _root.Background = LunaBrushes.B(_palette.Bg);
        _root.BorderBrush = LunaBrushes.B(_palette.Border);
        RenderCurrent();
    }

    private static void ApplyThemeVariant(LunaPalette palette)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = palette.Key == "gray" ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    private static void ApplyMarkdownResources(LunaPalette palette)
    {
        if (Application.Current is not { } app) return;
        var resources = app.Resources;
        resources["ForegroundColor"] = palette.Text;
        resources["BorderColor"] = palette.Surface2;
        resources["CardBackgroundColor"] = palette.Surface;
        resources["SecondaryCardBackgroundColor"] = palette.Surface2;
        resources["CodeInlineColor"] = palette.Text;
        resources["QuoteBorderColor"] = palette.Accent;
        resources["FontSizeM"] = 14.5;
        resources["FontSizeL"] = 16.0;
        resources["FontSizeXl"] = 18.0;
        resources["FontSize2Xl"] = 20.0;
        resources["FontSize3Xl"] = 22.0;
    }

    private void RenderCurrent()
    {
        switch (_screen)
        {
            case Screen.Chat: ShowChat(); break;
            case Screen.Chats: ShowChats(); break;
            case Screen.Paused: ShowPaused(); break;
            case Screen.Offline: ShowOffline(); break;
            default: ShowHome(); break;
        }
    }

    private async Task RefreshChatsQuietAsync()
    {
        try
        {
            _chats = await _api.GetChatsAsync();
            if (_chat is not null)
                _chat = _chats.FirstOrDefault(c => c.Id == _chat.Id) ?? _chat;
        }
        catch (Exception ex) { LunaLog.Exception("refresh-chats", ex); }
    }

    private void ShowRenameDialog(LunaChatDto chat)
    {
        var overlay = ModalOverlay();
        var stack = new StackPanel { Spacing = 12, Width = 340 };
        stack.Children.Add(Text(Loc.T("Luna.Chat.RenameTitle"), 20, _palette.Text, FontWeight.SemiBold, display: true));
        var box = new TextBox
        {
            Text = chat.Title,
            PlaceholderText = Loc.T("Luna.Chat.TitlePlaceholder"),
            MaxLength = 80,
            Height = 44,
            FontFamily = LunaThemes.Body,
            FontSize = 14,
            Background = LunaBrushes.B(_palette.InputBg),
            Foreground = LunaBrushes.B(_palette.Text),
            BorderBrush = LunaBrushes.B(_palette.Surface2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 0, 10, 0)
        };
        stack.Children.Add(box);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = PillButton(Loc.T("Luna.Common.Cancel"), _palette.Surface, _palette.Text, 44);
        cancel.Click += (_, _) => CloseModal();
        var save = PillButton(Loc.T("Luna.Common.Save"), _palette.Accent, _palette.AccentInk, 44);
        save.Click += async (_, _) =>
        {
            var title = box.Text?.Trim() ?? "";
            if (title.Length == 0) { CloseModal(); return; }
            try
            {
                await _api.UpdateChatAsync(chat.Id, new LunaUpdateChatRequest(title, null));
                await RefreshChatsQuietAsync();
                CloseModal();
                RenderCurrent();
            }
            catch (Exception ex) { LunaLog.Exception("rename", ex); }
        };
        row.Children.Add(cancel);
        row.Children.Add(save);
        stack.Children.Add(row);
        overlay.Children.Add(new Border
        {
            Child = stack,
            Background = LunaBrushes.B(_palette.Surface),
            BorderBrush = LunaBrushes.B(_palette.Surface2),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(18),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        _modalLayer = overlay;
        _host.Children.Add(overlay);
        box.Focus();
    }

    private void ShowHideDialog(LunaChatDto chat)
    {
        var overlay = ModalOverlay();
        var stack = new StackPanel { Spacing = 14, Width = 320 };
        stack.Children.Add(Text(Loc.T("Luna.Chat.HideConfirm"), 18, _palette.Text, FontWeight.SemiBold, wrap: true, display: true));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = PillButton(Loc.T("Luna.Common.Cancel"), _palette.Surface, _palette.Text, 42);
        cancel.Click += (_, _) => CloseModal();
        var hide = PillButton(Loc.T("Luna.Common.Hide"), _palette.Accent, _palette.AccentInk, 42);
        hide.Click += async (_, _) =>
        {
            CloseModal();
            await ToggleHiddenAsync(chat, true);
        };
        row.Children.Add(cancel);
        row.Children.Add(hide);
        stack.Children.Add(row);
        overlay.Children.Add(new Border
        {
            Child = stack,
            Background = LunaBrushes.B(_palette.Surface),
            BorderBrush = LunaBrushes.B(_palette.Surface2),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(18),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        _modalLayer = overlay;
        _host.Children.Add(overlay);
    }

    private Grid ModalOverlay()
    {
        var overlay = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
            ZIndex = 50
        };
        overlay.PointerPressed += (_, e) =>
        {
            if (e.Source == overlay) CloseModal();
        };
        return overlay;
    }

    private void CloseModal()
    {
        if (_modalLayer is null) return;
        _host.Children.Remove(_modalLayer);
        _modalLayer = null;
    }

    private async Task ToggleHiddenAsync(LunaChatDto chat, bool hidden)
    {
        try
        {
            await _api.UpdateChatAsync(chat.Id, new LunaUpdateChatRequest(null, hidden));
            await RefreshChatsQuietAsync();
            if (hidden && _chat?.Id == chat.Id) { _chat = null; ShowHome(); return; }
            if (_showHidden)
                _hiddenChats = (await _api.GetChatsAsync(includeHidden: true)).Where(c => c.HiddenByChild).ToList();
            RenderCurrent();
        }
        catch (Exception ex) { LunaLog.Exception("hide", ex); }
    }

#if LUNA_DEV
    public void ShowThinkingDemo(string question)
    {
        if (_messagesPanel is null) return;
        if (_emptyLabel is not null)
        {
            _messagesPanel.Children.Remove(_emptyLabel);
            _emptyLabel = null;
        }
        _messagesPanel.Children.Add(ChildBubble(string.IsNullOrWhiteSpace(question) ? "What's a volcano?" : question, flagged: false));
        _messagesPanel.Children.Add(new ThinkingView(_palette));
        ScrollToEndSoon();
    }
#endif

    public void FocusInput() => _input?.Focus();

    public void OpenSubject(string subject) => _ = StartChatAsync(subject);

    public void ShowMyChats() => ShowChats();

    public async Task SetThemeKeyAsync(string theme)
    {
        _palette = LunaThemes.From(theme);
        KidThemeStore.WriteLocal(_palette.Key);
        ApplyThemeVariant(_palette);
        if (_status is not null) _status = _status with { Theme = _palette.Key };
        try { await _api.SetThemeAsync(_palette.Key); } catch (Exception ex) { LunaLog.Exception("theme", ex); }
        _root.Background = LunaBrushes.B(_palette.Bg);
        _root.BorderBrush = LunaBrushes.B(_palette.Border);
        RenderCurrent();
    }

    public Task OpenChatByTitleAsync(string title)
    {
        var chat = _chats.FirstOrDefault(c => string.Equals(c.Title, title, StringComparison.OrdinalIgnoreCase))
            ?? _chats.FirstOrDefault(c => c.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        return chat is null ? Task.CompletedTask : OpenChatAsync(chat);
    }

    public Task OpenSubjectAsync(string subject) => StartChatAsync(subject);

    public Task OpenChatIdAsync(Guid id)
    {
        var chat = _chats.FirstOrDefault(c => c.Id == id);
        return chat is null ? Task.CompletedTask : OpenChatAsync(chat);
    }

    public string LayoutInfo() =>
        $"layout luna screen={_screen} window={Bounds.Width:0.#}x{Bounds.Height:0.#} " +
        $"input={_input?.Bounds.Width:0.#}x{_input?.Bounds.Height:0.#}";

    public void Send(string text)
    {
        if (_input is null) return;
        _input.Text = text;
        _ = SendFromInputAsync();
    }

    private Control CreateResizeGrip()
    {
        var grip = new Border
        {
            Width = 18,
            Height = 18,
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = new Cursor(StandardCursorType.BottomRightCorner),
            ZIndex = 20
        };
        var path = new Path
        {
            Data = Geometry.Parse("M6 18 L18 6 M10 18 L18 10 M14 18 L18 14"),
            Stroke = LunaBrushes.B(_palette.Muted),
            StrokeThickness = 1.2,
            StrokeLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };
        grip.Child = path;
        var dragging = false;
        var last = default(Point);
        grip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
            dragging = true;
            last = e.GetPosition(this);
            e.Pointer.Capture(grip);
        };
        grip.PointerMoved += (_, e) =>
        {
            if (!dragging) return;
            var point = e.GetPosition(this);
            var dx = point.X - last.X;
            var dy = point.Y - last.Y;
            last = point;
            if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1) ResizeRequested?.Invoke(dx, dy);
        };
        grip.PointerReleased += (_, _) => { dragging = false; e_Reset(); };
        return grip;
    }

    private void e_Reset() { }

    private Button NewMessagePill()
    {
        var pill = new Button
        {
            Name = "NewMessagePill",
            Content = new TextBlock
            {
                Text = Loc.T("Luna.Chat.NewMessage"),
                FontFamily = LunaThemes.Body,
                FontSize = 12.5,
                FontWeight = FontWeight.Bold,
                Foreground = LunaBrushes.B(_palette.AccentInk)
            },
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            Background = LunaBrushes.B(_palette.Accent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(16),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 10),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        pill.Click += (_, _) => FollowLatest();
        return pill;
    }

    private void ShowNewMessagePill()
    {
        if (_newMessagePill is not null) _newMessagePill.IsVisible = true;
    }

    private void HideNewMessagePill()
    {
        _pendingNewMessage = false;
        if (_newMessagePill is not null) _newMessagePill.IsVisible = false;
    }

    private void FollowLatest()
    {
        _followTail = true;
        _userScrolledUp = false;
        _pendingNewMessage = false;
        _pinToBottom = true;
        _replyFollowMode = ReplyFollowMode.BottomTail;
        HideNewMessagePill();
        ScrollToEndSoon();
    }

    private bool IsAtBottom()
    {
        if (_messagesScroll is null) return true;
        var maximum = Math.Max(0, _messagesScroll.Extent.Height - _messagesScroll.Viewport.Height);
        return _messagesScroll.Offset.Y >= maximum - 24;
    }

    private void OnMessagesScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_messagesScroll is null) return;
        var offset = _messagesScroll.Offset.Y;
        var previousOffset = _lastObservedOffset;
        _lastObservedOffset = offset;
        if (Environment.TickCount64 - _lastAutoScrollAt < 150) return;

        if (IsAtBottom())
        {
            _followTail = true;
            _userScrolledUp = false;
            _pinToBottom = true;
            if (_replyFollowMode == ReplyFollowMode.Start)
                _replyFollowMode = ReplyFollowMode.BottomTail;
            HideNewMessagePill();
            return;
        }

        if (_replyFollowMode == ReplyFollowMode.Start &&
            Math.Abs(offset - _replyStartOffset) <= 4)
            return;

        // While we are following the tail, a growing reply changes the extent
        // but not the offset. Only a real upward move (a smaller offset) means
        // the child took over the scroll.
        if (_pinToBottom)
        {
            if (offset < previousOffset - 2)
            {
                _pinToBottom = false;
                _followTail = false;
                _userScrolledUp = true;
                if (_replyStreaming)
                {
                    _pendingNewMessage = true;
                    ShowNewMessagePill();
                }
            }
            return;
        }

        _followTail = false;
        _userScrolledUp = true;
        _pinToBottom = false;
        if (_replyFollowMode != ReplyFollowMode.None)
            _replyFollowMode = ReplyFollowMode.None;
        if (_replyStreaming)
        {
            _pendingNewMessage = true;
            ShowNewMessagePill();
        }
    }

    private void OnMessagesLayoutUpdated(object? sender, EventArgs e)
    {
        if (_messagesScroll is null || _messagesPanel is null) return;
        if (_replyRow is not null && !_userScrolledUp &&
            _replyFollowMode == ReplyFollowMode.Bottom &&
            _replyRow.Bounds.Height > _messagesScroll.Viewport.Height - 24)
        {
            var top = _replyRow.TranslatePoint(new Point(0, 0), _messagesPanel)?.Y ?? 0;
            _replyFollowMode = ReplyFollowMode.Start;
            _pinToBottom = false;
            _scrollEndPending = false;
            _replyStartOffset = Math.Max(0, top - 8);
            _lastAutoScrollAt = Environment.TickCount64;
            _messagesScroll.Offset = new Vector(_messagesScroll.Offset.X, _replyStartOffset);
            return;
        }
        if (_scrollEndPending)
        {
            _scrollEndPending = false;
            _lastAutoScrollAt = Environment.TickCount64;
            _messagesScroll.ScrollToEnd();
            return;
        }
        if (_pinToBottom && !_userScrolledUp)
        {
            _lastAutoScrollAt = Environment.TickCount64;
            _messagesScroll.ScrollToEnd();
        }
    }

    private void ScrollTo(double offset)
    {
        if (_messagesScroll is null) return;
        var maximum = Math.Max(0, _messagesScroll.Extent.Height - _messagesScroll.Viewport.Height);
        _lastAutoScrollAt = Environment.TickCount64;
        _messagesScroll.Offset = new Vector(_messagesScroll.Offset.X, Math.Clamp(offset, 0, maximum));
    }

    private void ScrollToEndSoon()
    {
        _followTail = true;
        _userScrolledUp = false;
        _pinToBottom = true;
        _pendingNewMessage = false;
        _scrollEndPending = true;
        _lastAutoScrollAt = Environment.TickCount64;
        _messagesScroll?.InvalidateMeasure();
        AttemptScrollToEnd(3);
    }

    private void AttemptScrollToEnd(int remaining)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_messagesScroll is null) return;
            _lastAutoScrollAt = Environment.TickCount64;
            _messagesScroll.ScrollToEnd();
            if (remaining > 0 && !IsAtBottom())
                AttemptScrollToEnd(remaining - 1);
        }, DispatcherPriority.Background);
    }

    private void RestoreScroll(double offset)
    {
        _scrollEndPending = false;
        _pinToBottom = false;
        _lastAutoScrollAt = Environment.TickCount64;
        _messagesScroll?.InvalidateMeasure();
        AttemptRestoreScroll(offset, 2);
    }

    private void AttemptRestoreScroll(double offset, int remaining)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_messagesScroll is null) return;
            ScrollTo(offset);
            if (remaining > 0) AttemptRestoreScroll(offset, remaining - 1);
        }, DispatcherPriority.Background);
    }

    private void BeginReplyFollow(Control row)
    {
        _replyRow = row;
        if (_userScrolledUp)
        {
            _replyFollowMode = ReplyFollowMode.None;
            _pinToBottom = false;
            _pendingNewMessage = true;
            ShowNewMessagePill();
            return;
        }
        _replyFollowMode = ReplyFollowMode.Bottom;
        Dispatcher.UIThread.Post(() => FocusReply(row), DispatcherPriority.Loaded);
    }

    private void FocusReply(Control row)
    {
        if (_messagesScroll is null || _messagesPanel is null ||
            !ReferenceEquals(_replyRow, row) || _userScrolledUp) return;
        var viewport = _messagesScroll.Viewport.Height;
        if (viewport <= 0) return;
        var top = row.TranslatePoint(new Point(0, 0), _messagesPanel)?.Y ?? 0;
        if (row.Bounds.Height > viewport - 24)
        {
            _replyFollowMode = ReplyFollowMode.Start;
            _pinToBottom = false;
            _replyStartOffset = Math.Max(0, top - 8);
            _lastAutoScrollAt = Environment.TickCount64;
            _messagesScroll.Offset = new Vector(_messagesScroll.Offset.X, _replyStartOffset);
        }
        else
        {
            _replyFollowMode = ReplyFollowMode.Bottom;
            ScrollToEndSoon();
        }
    }

    private void KeepReplyInView()
    {
        if (_messagesScroll is null || _messagesPanel is null || _replyRow is null) return;
        if (_userScrolledUp)
        {
            if (_pendingNewMessage) ShowNewMessagePill();
            return;
        }
        var viewport = _messagesScroll.Viewport.Height;
        if (viewport <= 0) return;
        switch (_replyFollowMode)
        {
            case ReplyFollowMode.None:
            case ReplyFollowMode.Start:
                return;
            case ReplyFollowMode.BottomTail:
                ScrollToEndSoon();
                return;
            case ReplyFollowMode.Bottom:
                if (_replyRow.Bounds.Height > viewport - 24)
                {
                    var top = _replyRow.TranslatePoint(new Point(0, 0), _messagesPanel)?.Y ?? 0;
                    _replyFollowMode = ReplyFollowMode.Start;
                    _pinToBottom = false;
                    _replyStartOffset = Math.Max(0, top - 8);
                    _lastAutoScrollAt = Environment.TickCount64;
                    _messagesScroll.Offset = new Vector(_messagesScroll.Offset.X, _replyStartOffset);
                }
                else
                {
                    ScrollToEndSoon();
                }
                return;
        }
    }

    internal ScrollViewer? MessagesScrollForTest => _messagesScroll;
    internal bool FollowTailForTest => _followTail;
    internal bool UserScrolledUpForTest => _userScrolledUp;
    internal bool NewMessagePillVisibleForTest => _newMessagePill?.IsVisible == true;
    internal Control? LastMessageRowForTest => _messagesPanel is { Children.Count: > 0 }
        ? _messagesPanel.Children[^1] as Control : null;

    internal void MarkInitializedForTest() => _initialized = true;

    internal void OpenChatForTest(LunaChatDto chat, List<LunaChildMessageDto> messages)
    {
        _chat = chat;
        _messages = messages;
        _followTail = true;
        _userScrolledUp = false;
        _pendingNewMessage = false;
        _replyRow = null;
        _replyFollowMode = ReplyFollowMode.None;
        ShowChat();
    }

    internal (Control Bubble, ThinkingView Thinking)? AddPendingChildForTest(string text) => AddPendingChild(text);

    internal void DeliverReplyForTest(string text) => AddStreamingReply(text);

    internal void ScrollToStartForTest()
    {
        if (_messagesScroll is null) return;
        _lastAutoScrollAt = 0;
        _messagesScroll.Offset = new Vector(_messagesScroll.Offset.X, 0);
    }

    private void StopTimers()
    {
        foreach (var timer in _timers.ToList()) timer.Stop();
        _timers.Clear();
    }

    private Control ThemeBackdrop()
    {
        var canvas = new Canvas { IsHitTestVisible = false };
        if (_palette.Key == "night")
        {
            var rnd = new Random(17);
            for (var i = 0; i < 26; i++)
            {
                var star = new Ellipse
                {
                    Width = i % 5 == 0 ? 2.4 : 1.5,
                    Height = i % 5 == 0 ? 2.4 : 1.5,
                    Fill = LunaBrushes.B(i % 4 == 0 ? Color.Parse("#fff6c8") : Color.Parse("#d9d2ff")),
                    Opacity = i % 3 == 0 ? 0.75 : 0.4
                };
                Canvas.SetLeft(star, rnd.NextDouble() * 520);
                Canvas.SetTop(star, rnd.NextDouble() * 600);
                canvas.Children.Add(star);
            }
        }
        else
        {
            foreach (var (x, y, r) in new[] { (480.0, 42.0, 30.0), (410.0, 92.0, 11.0), (32.0, 560.0, 40.0), (116.0, 592.0, 14.0) })
            {
                canvas.Children.Add(new Ellipse
                {
                    Width = r * 2,
                    Height = r * 2,
                    Fill = LunaBrushes.B(Color.Parse("#e2e5eb")),
                    Stroke = LunaBrushes.B(Color.Parse("#d7dbe3")),
                    StrokeThickness = 1,
                    Opacity = 0.8
                });
                var ellipse = (Ellipse)canvas.Children[^1];
                Canvas.SetLeft(ellipse, x - r);
                Canvas.SetTop(ellipse, y - r);
            }
        }
        return canvas;
    }

    private Button IconButton(Control icon, string label)
    {
        var button = new Button
        {
            Content = icon,
            Width = 44,
            Height = 44,
            Background = LunaBrushes.B(_palette.Surface),
            BorderBrush = LunaBrushes.B(_palette.Surface2),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(button, label);
        return button;
    }

    private Button PillButton(string label, Color background, Color foreground, double height, double minWidth = 0)
    {
        return new Button
        {
            Content = Text(label, 15, foreground, FontWeight.SemiBold, display: true),
            MinWidth = minWidth,
            Height = height,
            Padding = new Thickness(18, 0, 18, 0),
            Background = LunaBrushes.B(background),
            Foreground = LunaBrushes.B(foreground),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(height / 2),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
    }

    private Button LinkButton(string label, Color color)
    {
        return new Button
        {
            Content = Text(label, 14, color, FontWeight.Bold),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2),
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
    }

    private TextBlock Text(string text, double size, Color color, FontWeight weight,
        bool center = false, bool wrap = false, bool display = false)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = display ? LunaThemes.Display : LunaThemes.Body,
            FontSize = size,
            FontWeight = weight,
            Foreground = LunaBrushes.B(color),
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
            HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private Control CreateMoonFace(double size, bool sleepy)
    {
        var s = size / 172.0;
        var canvas = new Canvas { Width = size, Height = size };
        void Place(Shape shape, double x, double y, double w, double h)
        {
            shape.Width = w * s;
            shape.Height = h * s;
            Canvas.SetLeft(shape, x * s);
            Canvas.SetTop(shape, y * s);
            canvas.Children.Add(shape);
        }

        var face = new Ellipse
        {
            Fill = LunaBrushes.B(Color.Parse("#f6dc7e")),
            Stroke = _palette.Key == "gray" ? LunaBrushes.B(Color.Parse("#e2bd52")) : null,
            StrokeThickness = _palette.Key == "gray" ? 4 : 0
        };
        Place(face, 2, 2, 168, 168);
        Place(new Ellipse { Fill = LunaBrushes.B(Color.Parse("#ecc964")) }, 41, 37, 22, 22);
        Place(new Ellipse { Fill = LunaBrushes.B(Color.Parse("#ecc964")) }, 121, 51, 14, 14);
        Place(new Ellipse { Fill = LunaBrushes.B(Color.Parse("#e8c35c")) }, 109, 115, 26, 26);
        Place(new Ellipse { Fill = LunaBrushes.B(Color.Parse("#ecc964")) }, 34, 112, 12, 12);
        if (sleepy)
        {
            var left = new Path { Data = Geometry.Parse("M52 82 q12 10 24 0"), Stroke = LunaBrushes.B(Color.Parse("#2b2f3a")), StrokeThickness = 7, StrokeLineCap = PenLineCap.Round };
            var right = new Path { Data = Geometry.Parse("M96 82 q12 10 24 0"), Stroke = LunaBrushes.B(Color.Parse("#2b2f3a")), StrokeThickness = 7, StrokeLineCap = PenLineCap.Round };
            left.RenderTransform = new ScaleTransform(s, s);
            right.RenderTransform = new ScaleTransform(s, s);
            canvas.Children.Add(left);
            canvas.Children.Add(right);
        }
        else
        {
            Place(new Ellipse { Fill = LunaBrushes.B(Color.Parse("#2b2f3a")) }, 56, 67, 16, 22);
            Place(new Ellipse { Fill = LunaBrushes.B(Color.Parse("#2b2f3a")) }, 100, 67, 16, 22);
            var smile = new Path { Data = Geometry.Parse("M60 106 Q86 134 112 106"), Stroke = LunaBrushes.B(Color.Parse("#2b2f3a")), StrokeThickness = 8, StrokeLineCap = PenLineCap.Round, RenderTransform = new ScaleTransform(s, s) };
            canvas.Children.Add(smile);
            Place(new Ellipse { Fill = LunaBrushes.B(Color.Parse("#f0a9a0")), Opacity = 0.8 }, 38, 94, 20, 12);
            Place(new Ellipse { Fill = LunaBrushes.B(Color.Parse("#f0a9a0")), Opacity = 0.8 }, 114, 94, 20, 12);
        }
        return canvas;
    }

    private Control SubjectIcon(string subject, Color color)
    {
        var data = subject switch
        {
            "math" => "M12 5v6M9 8h6M6 16h4M16 14l4 4M20 14l-4 4",
            "science" => "M9 3h6M10 3v6l-5 9a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-9V3M7.5 15h9",
            "history" => "M3 21h18M5 21V10M19 21V10M9 21V10M15 21V10M2 10l10-6 10 6z",
            "dictionary" => "M4 6h10M4 10h7M4 14h5M16 11a4 4 0 1 0 0 8 4 4 0 0 0 0-8M19 18l2.5 2.5",
            "languages" => "M4 5h16v10H9l-5 4zM8 9h8M8 12h5",
            _ => "M9 9a3 3 0 1 1 4 2.8c-.7.3-1 .9-1 1.6V14M12 18h.01M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z"
        };
        return new Path
        {
            Data = Geometry.Parse(data),
            Stroke = LunaBrushes.B(color),
            StrokeThickness = 2.2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Width = 28,
            Height = 28,
            Stretch = Stretch.Uniform
        };
    }

    private Control ThemeIcon()
    {
        if (_palette.Key == "gray")
        {
            return new Path
            {
                Data = Geometry.Parse("M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z"),
                Stroke = LunaBrushes.B(_palette.Muted),
                StrokeThickness = 2,
                Width = 22,
                Height = 22,
                Stretch = Stretch.Uniform
            };
        }
        return new Path
        {
            Data = Geometry.Parse("M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9z"),
            Stroke = LunaBrushes.B(_palette.Accent),
            StrokeThickness = 2,
            Width = 22,
            Height = 22,
            Stretch = Stretch.Uniform
        };
    }

    private Control CloseIcon() => new Path
    {
        Data = Geometry.Parse("M6 6l12 12M18 6L6 18"),
        Stroke = LunaBrushes.B(_palette.Text),
        StrokeThickness = 2.5,
        StrokeLineCap = PenLineCap.Round,
        Width = 20,
        Height = 20,
        Stretch = Stretch.Uniform
    };

    private Control BackIcon() => new Path
    {
        Data = Geometry.Parse("M15 18l-6-6 6-6"),
        Stroke = LunaBrushes.B(_palette.Text),
        StrokeThickness = 2.5,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        Width = 22,
        Height = 22,
        Stretch = Stretch.Uniform
    };

    private Control MoreIcon()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 3; i++)
            panel.Children.Add(new Ellipse { Width = 3.2, Height = 3.2, Fill = LunaBrushes.B(_palette.Muted) });
        return panel;
    }
}
