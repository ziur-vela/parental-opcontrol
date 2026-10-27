// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Diagnostics;
using System.Management;
using System.Reflection;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

public sealed class HelperContext : ApplicationContext
{
    private readonly string _monitored;
    private readonly LockOverlayManager _overlays;
    private readonly CountdownForm _countdown;
    private readonly KeyboardHook _hook = new();
    private readonly UsageSampler _usage = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly RemoteCaptureHost _remote;
    private readonly Control _marshal = new();
    private RemoteNoticeForm? _notice;
    private PinDialog? _pinDialog;
    private NotificationForm? _notification;
    private MessagePopupForm? _messagePopup;
    private Process? _kidProcess;
    private NotifyIcon? _tray;
    private ToolStripControlHost? _trayHeaderHost;
    private KidTrayHeader? _trayHeader;
    private ContextMenuStrip? _trayMenu;
    private string _trayTheme = "night";
    private ToolStripMenuItem? _trayOpenItem;
    private ToolStripMenuItem? _trayMessageItem;
    private ToolStripMenuItem? _trayLunaItem;
    private ToolStripMenuItem? _trayAboutItem;
    private Icon? _trayIcon;
    private Bitmap? _trayBitmap;
    private string _trayLanguage = Loc.Language;
    private readonly DateTime _helperStartedUtc = DateTime.UtcNow;
    private DateTime _lastNotificationUtc = DateTime.MinValue;
    private bool _lastVisible;
    private bool _hookInstalled;
    private bool _wasLocked;
    private readonly HashSet<int> _warnedThresholds = [];
    private DateTime _warningDay = DateTime.MinValue;

    public HelperContext(string monitored)
    {
        _monitored = monitored;
        _overlays = new LockOverlayManager();
        _overlays.OverrideClicked += ShowPinDialog;
        // The lock overlay opens an owned messaging panel (LockMessagesForm); no
        // separate top-level window may sit above the overlay.
        _overlays.MessageClicked += () => HelperPaths.LogEvent("lock-messages-requested");
        _countdown = new CountdownForm();
        _ = _marshal.Handle;
        _remote = new RemoteCaptureHost(monitored);
        _remote.NoticeChanged += OnRemoteNoticeChanged;
        _remote.Start();
        CreateTrayIcon();
        _timer = new System.Windows.Forms.Timer { Interval = 500 };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        HelperPaths.LogEvent($"helper-started session={System.Diagnostics.Process.GetCurrentProcess().SessionId} user={Environment.UserName}");
        Tick();
    }

    private void Tick()
    {
        var state = HelperPaths.ReadState();
        Loc.SetLanguage(state?.Language);
        UpdateRemoteNotice();
        CheckNotification();
        if (state is { IsLocked: true } && !_overlays.Visible)
            _overlays.Update(state);
        CheckMessagePopups();
        CheckUiCommand();
        UpdateTrayLanguage();
        _usage.Sample(state);
        if (state is null)
        {
            _overlays.Hide();
            _countdown.HideBanner();
            _hook.Uninstall();
            HelperPaths.WriteStatus(false, false, null);
            return;
        }

        if (state.IsLocked)
        {
            _countdown.HideBanner();
            if (!_wasLocked)
            {
                if (_kidProcess is { HasExited: false } kid)
                {
                    try { kid.Kill(); } catch { }
                    _kidProcess = null;
                }
                if (state.Reason == "daily-limit")
                    HelperPaths.LogEvent("daily-limit-lock");
            }
            _overlays.Update(state);
            _wasLocked = true;
            if (!_hookInstalled && _hook.Install())
            {
                _hookInstalled = true;
                HelperPaths.LogEvent("keyboard-hook-installed");
            }
            _lastVisible = true;
            HelperPaths.WriteStatus(true, true, state.Reason);
            return;
        }

        if (_wasLocked)
        {
            _kidProcess = null;
        }
        _wasLocked = false;
        _overlays.Hide();
        _hook.Uninstall();
        if (_hookInstalled)
        {
            _hookInstalled = false;
            HelperPaths.LogEvent("keyboard-hook-removed");
        }

        var now = DateTime.UtcNow;
        CheckDailyWarnings(state);
        if (state.BonusUntilUtc is { } bonus && bonus > now)
        {
            var remaining = bonus - now;
            var minutes = state.BonusMinutes ?? 0;
            var text = Loc.T("Client.Countdown.Bonus", minutes, Format(remaining));
            _countdown.UpdateText(text, remaining);
            SetVisible(true, false, state.Reason, text);
        }
        else if (state.Reason == "override" && state.EndsUtc is { } unlockedUntil && unlockedUntil > now)
        {
            var remaining = unlockedUntil - now;
            var when = unlockedUntil.ToLocalTime().ToString("t", System.Globalization.CultureInfo.CurrentCulture);
            var text = string.IsNullOrWhiteSpace(state.UnlockScheduleName)
                ? Loc.T("Client.Countdown.UnlockedUntil", when)
                : Loc.T("Client.Countdown.UnlockedUntilSchedule", when, state.UnlockScheduleName);
            _countdown.UpdateText(text, remaining);
            SetVisible(true, false, state.Reason, text);
        }
        else if (state.NextLockUtc is { } next && next > now)
        {
            var remaining = next - now;
            var text = Loc.T("Client.Countdown.LockIn", Format(remaining));
            _countdown.UpdateText(text, remaining);
            SetVisible(true, false, state.Reason, text);
        }
        else if (state.DailyLimitRemainingSeconds is { } dailyLeft && dailyLeft > 0)
        {
            var remaining = TimeSpan.FromSeconds(dailyLeft);
            var text = Loc.T("Client.Countdown.LeftToday", FormatShort(remaining));
            _countdown.UpdateText(text, remaining);
            SetVisible(true, false, state.Reason, text);
        }
        else
        {
            _countdown.HideBanner();
            SetVisible(false, false, state.Reason);
        }
    }

    private void OnRemoteNoticeChanged()
    {
        try
        {
            if (!_marshal.IsDisposed && _marshal.IsHandleCreated) _marshal.BeginInvoke((Action)UpdateRemoteNotice);
        }
        catch (ObjectDisposedException) { }
    }

    private void UpdateRemoteNotice()
    {
        if (_remote.NoticeActive && !string.IsNullOrWhiteSpace(_remote.NoticeText))
        {
            _notice ??= new RemoteNoticeForm(_remote.NoticeText!);
            _notice.ShowNotice(_remote.NoticeText!);
        }
        else
        {
            _notice?.HideNotice();
        }
    }

    private void CreateTrayIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpControl.Parental.Helper.brand.icon-color-128.png");
            if (stream is not null)
            {
                _trayBitmap = new Bitmap(stream);
                _trayIcon = Icon.FromHandle(_trayBitmap.GetHicon());
            }
            _trayIcon ??= SystemIcons.Shield;

            _trayTheme = ResolveStoredTheme();
            KidTheme.Apply(_trayTheme);
            _trayHeader = new KidTrayHeader();
            _trayHeader.ThemeButton.Click += (_, _) => ToggleTrayTheme();
            _trayHeaderHost = new ToolStripControlHost(_trayHeader)
            {
                AutoSize = false,
                Size = new Size(332, 66),
                Margin = new Padding(2, 2, 2, 4)
            };
            _trayOpenItem = new ToolStripMenuItem(Loc.T("Client.Kid.Open")) { Padding = new Padding(6, 6, 6, 6) };
            _trayOpenItem.Click += (_, _) => ShowMyOpControl("home");
            _trayMessageItem = new ToolStripMenuItem(Loc.T("Client.Messages.Open")) { Padding = new Padding(6, 6, 6, 6) };
            _trayMessageItem.Click += (_, _) => ShowMyOpControl("messages");
            _trayLunaItem = new ToolStripMenuItem(Loc.T("Client.Kid.LunaOpen")) { Padding = new Padding(6, 6, 6, 6) };
            _trayLunaItem.Click += (_, _) => OpenLuna();
            _trayAboutItem = new ToolStripMenuItem(Loc.T("Client.Kid.About.Link")) { Padding = new Padding(6, 6, 6, 6) };
            _trayAboutItem.Click += (_, _) => ShowMyOpControl("about", messagesOnly: false, aboveLock: false);

            _trayMenu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                BackColor = KidTheme.Card,
                ForeColor = KidTheme.Text,
                Padding = new Padding(6),
                Renderer = new KidMenuRenderer()
            };
            _trayMenu.Items.Add(_trayHeaderHost);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_trayOpenItem);
            _trayMenu.Items.Add(_trayMessageItem);
            _trayMenu.Items.Add(_trayLunaItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_trayAboutItem);
            _trayMenu.Opening += (_, _) => UpdateTrayMenu();
            _tray = new NotifyIcon
            {
                Icon = _trayIcon,
                Text = "Parental OpControl",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };
            _tray.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowMyOpControl("home");
            };
            _tray.DoubleClick += (_, _) => ShowMyOpControl("home");
            UpdateTrayMenu();
        }
        catch (Exception ex)
        {
            HelperPaths.LogEvent($"tray-icon-failed {ex.Message}");
        }
    }

    private static string ResolveStoredTheme()
    {
        var local = KidThemeStore.ReadLocal();
        if (local is not null) return local;
        var summary = HelperPaths.ReadSummary();
        if (summary is not null && LunaThemes.IsValid(summary.Theme)) return LunaThemes.Normalize(summary.Theme);
        var state = HelperPaths.ReadState();
        return LunaThemes.Normalize(state?.Theme);
    }

    private void ToggleTrayTheme()
    {
        var next = KidThemeStore.Toggle(_trayTheme);
        KidThemeStore.WriteLocal(next);
        _ = PinPipeClient.SetThemeAsync(next, CancellationToken.None);
        ApplyTrayTheme(next, updateWindow: true);
        UpdateTrayMenu();
    }

    private void ApplyTrayTheme(string theme, bool updateWindow)
    {
        _trayTheme = LunaThemes.Normalize(theme);
        KidTheme.Apply(_trayTheme);
        if (_trayMenu is not null)
        {
            _trayMenu.BackColor = KidTheme.Card;
            _trayMenu.ForeColor = KidTheme.Text;
            _trayMenu.Renderer = new KidMenuRenderer();
        }
        _trayHeader?.ApplyTheme(KidTheme.IsGray);
    }

    private void OpenLuna()
    {
        try
        {
            if (System.Diagnostics.Process.GetProcessesByName("OpControl.Parental.Luna")
                .Any(p => { try { return p.SessionId == System.Diagnostics.Process.GetCurrentProcess().SessionId && !IsKidWindow(p); } catch { return false; } }))
                return;
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Luna", "OpControl.Parental.Luna.exe"));
            if (!File.Exists(path))
            {
                HelperPaths.LogEvent("luna-open-missing");
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            HelperPaths.LogEvent("luna-open-requested");
        }
        catch (Exception ex)
        {
            HelperPaths.LogEvent($"luna-open-failed {ex.Message}");
        }
    }

    private static bool IsKidWindow(Process process)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}");
            foreach (ManagementBaseObject item in searcher.Get())
            {
                var commandLine = item["CommandLine"]?.ToString() ?? "";
                item.Dispose();
                if (LunaProcessPolicy.IsKidCommandLine(commandLine)) return true;
            }
            return false;
        }
        catch { return false; }
    }

    private void UpdateTrayLanguage()
    {
        if (_trayMessageItem is null || _trayLanguage == Loc.Language) return;
        _trayLanguage = Loc.Language;
        UpdateTrayMenu();
    }

    private void UpdateTrayMenu()
    {
        if (_trayOpenItem is null || _trayMessageItem is null || _trayLunaItem is null || _trayAboutItem is null || _trayHeader is null) return;
        var state = HelperPaths.ReadState();
        var local = KidThemeStore.ReadLocal();
        var theme = local ?? LunaThemes.Normalize(state?.Theme ?? _trayTheme);
        if (!string.Equals(theme, _trayTheme, StringComparison.Ordinal))
            ApplyTrayTheme(theme, updateWindow: true);

        var left = CountdownText(state);
        _trayHeader.Caption.Text = Loc.T("Client.Kid.TimeLeft");
        _trayHeader.Time.Text = left ?? Loc.T("Client.Kid.NoLimit");
        _trayHeader.ApplyTheme(KidTheme.IsGray);
        _trayOpenItem.Text = Loc.T("Client.Kid.Open");
        var unread = UnreadCount();
        _trayMessageItem.Text = unread > 0
            ? Loc.T("Client.Kid.TrayMessagesUnread", unread)
            : Loc.T("Client.Messages.Open");
        var lunaEnabled = state?.LunaEnabled == true;
        _trayLunaItem.Text = Loc.T("Client.Kid.LunaOpen");
        _trayLunaItem.Visible = KidTrayPolicy.ShowOpenLuna(lunaEnabled);
        _trayLunaItem.Enabled = lunaEnabled;
        _trayAboutItem.Text = Loc.T("Client.Kid.About.Link");
    }

    private static int UnreadCount()
    {
        var thread = MessageThreadCache.Read() ?? [];
        return thread.Count(m => m.Direction == MessageDirection.ParentToChild && m.SeenUtc is null && !m.Deleted);
    }

    private static string? CountdownText(RuntimeLockStateDto? state)
    {
        var now = DateTime.UtcNow;
        if (state?.BonusUntilUtc is { } bonus && bonus > now) return Format(bonus - now);
        if (state?.NextLockUtc is { } next && next > now) return Format(next - now);
        if (state?.DailyLimitRemainingSeconds is { } left && left > 0) return Format(TimeSpan.FromSeconds(left));
        return null;
    }

    private void CheckMessagePopups()
    {
        if (_messagePopup is { IsDisposed: false }) return;
        try
        {
            if (!Directory.Exists(HelperPaths.MessagePopupDirectory)) return;
            var files = Directory.GetFiles(HelperPaths.MessagePopupDirectory, "*.json");
            if (files.Length == 0) return;

            var pending = new List<(ChildMessageDto Message, string Path)>();
            foreach (var file in files)
            {
                ChildMessageDto? message = null;
                try
                {
                    message = System.Text.Json.JsonSerializer.Deserialize<ChildMessageDto>(
                        File.ReadAllText(file), HelperPaths.JsonOptions);
                }
                catch { }
                if (message is null || DateTime.UtcNow - message.CreatedUtc > TimeSpan.FromHours(24))
                {
                    try { File.Delete(file); } catch { }
                    continue;
                }
                pending.Add((message, file));
            }

            foreach (var (message, path) in pending.OrderBy(p => p.Message.CreatedUtc))
            {
                try { File.Delete(path); } catch { }
                ShowMessagePopup(message);
                break;
            }
        }
        catch (Exception ex)
        {
            HelperPaths.LogEvent($"message-popup-failed {ex.Message}");
        }
    }

    private void ShowMessagePopup(ChildMessageDto message)
    {
        try
        {
            var popup = new MessagePopupForm(message);
            popup.Seen += m => _ = ReportSeenAsync(m.Id);
            popup.Acknowledged += m => _ = ReportAcknowledgedAsync(m.Id);
            popup.ReplyClicked += OpenChildMessagesFromPopup;
            popup.FormClosed += (_, _) => _messagePopup = null;
            _messagePopup = popup;

            var owner = _overlays.Visible ? _overlays.Owner : null;
            if (owner is { IsDisposed: false }) popup.Show(owner);
            else popup.Show();
            popup.BringToFront();
            popup.Activate();
            HelperPaths.LogEvent($"message-popup-shown id={message.Id}");
        }
        catch (Exception ex)
        {
            HelperPaths.LogEvent($"message-popup-show-failed {ex.Message}");
        }
    }

    private void OpenChildMessagesFromPopup()
    {
        _messagePopup = null;
        ShowMyOpControl("messages");
    }

    private void CheckUiCommand()
    {
        try
        {
            if (!File.Exists(HelperPaths.UiCommandFile)) return;
            var json = File.ReadAllText(HelperPaths.UiCommandFile);
            try { File.Delete(HelperPaths.UiCommandFile); } catch { }
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var root = document.RootElement;
            var tab = (root.TryGetProperty("tab", out var tabValue) ? tabValue.GetString() : null) switch
            {
                "schedule" => "schedule",
                "rules" => "rules",
                "activity" => "activity",
                "messages" => "messages",
                _ => "home"
            };
            var theme = root.TryGetProperty("theme", out var themeValue) ? themeValue.GetString() : null;
            var language = root.TryGetProperty("language", out var langValue) ? langValue.GetString() : null;
            var messagesOnly = root.TryGetProperty("messagesOnly", out var onlyValue) && onlyValue.ValueKind == System.Text.Json.JsonValueKind.True;
            ShowMyOpControl(tab, theme, language, messagesOnly);
        }
        catch (Exception ex)
        {
            HelperPaths.LogEvent($"ui-command-failed {ex.Message}");
        }
    }

    private void ShowMyOpControl(string tab, string? theme = null, string? language = null,
        bool? messagesOnly = null, bool? aboveLock = null)
    {
        try
        {
            var isAboveLock = aboveLock ?? _overlays.Visible;
            var onlyMessages = messagesOnly ?? isAboveLock;
            var lunaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Luna",
                "OpControl.Parental.Luna.exe"));
            if (!File.Exists(lunaPath))
            {
                HelperPaths.LogEvent($"my-opcontrol-missing-exe {lunaPath}");
                return;
            }

            if (_kidProcess is { HasExited: false } running)
            {
                try { running.Kill(); } catch { }
            }

            var start = new ProcessStartInfo(lunaPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(lunaPath)
            };
            start.ArgumentList.Add("--kid-window");
            start.ArgumentList.Add("--tab");
            start.ArgumentList.Add(string.IsNullOrWhiteSpace(tab) ? "home" : tab);
            if (onlyMessages) start.ArgumentList.Add("--messages-only");
            if (isAboveLock) start.ArgumentList.Add("--above-lock");
            if (!string.IsNullOrWhiteSpace(theme))
            {
                start.ArgumentList.Add("--theme");
                start.ArgumentList.Add(theme);
            }
            if (!string.IsNullOrWhiteSpace(language))
            {
                start.ArgumentList.Add("--language");
                start.ArgumentList.Add(language);
            }
            _kidProcess = Process.Start(start);
            ApplyTrayTheme(ResolveStoredTheme(), updateWindow: false);
            HelperPaths.LogEvent($"my-opcontrol-opened tab={tab} above_lock={isAboveLock} only_messages={onlyMessages}");
        }
        catch (Exception ex)
        {
            HelperPaths.LogEvent($"my-opcontrol-failed {ex.Message}");
        }
    }

    private static async Task ReportSeenAsync(Guid messageId)
    {
        try
        {
            var response = await PinPipeClient.MarkSeenAsync(messageId, CancellationToken.None);
            if (!response.Ok) HelperPaths.LogEvent($"message-seen-failed {messageId} {response.Error}");
        }
        catch { }
    }

    private static async Task ReportAcknowledgedAsync(Guid messageId)
    {
        try
        {
            var response = await PinPipeClient.MarkAcknowledgedAsync(messageId, CancellationToken.None);
            if (!response.Ok) HelperPaths.LogEvent($"message-ack-failed {messageId} {response.Error}");
        }
        catch { }
    }

    private void CheckDailyWarnings(RuntimeLockStateDto state)
    {
        var today = DateTime.Today;
        if (today != _warningDay)
        {
            _warningDay = today;
            _warnedThresholds.Clear();
        }

        if (state.DailyLimitRemainingSeconds is not { } left || left <= 0 || state.BonusUntilUtc is { } bonus && bonus > DateTime.UtcNow)
            return;

        foreach (var minutes in new[] { 15, 5, 1 })
        {
            if (left > minutes * 60 || left <= (minutes - 1) * 60) continue;
            if (!_warnedThresholds.Add(minutes)) continue;
            var message = Loc.T(minutes == 1 ? "Client.Notice.MinutesLeftOne" : "Client.Notice.MinutesLeftMany", minutes);
            ShowNotification(Loc.T("Client.Notice.ScreenTime"), message);
            if (state.SpokenWarnings) Speak(message);
            HelperPaths.LogEvent($"daily-warning-{minutes}");
            break;
        }
    }

    private void ShowNotification(string title, string message)
    {
        try
        {
            _notification?.Close();
            _notification = new NotificationForm(title, message);
            _notification.Show();
        }
        catch { }
    }

    private static void Speak(string text)
    {
        try
        {
            var type = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (type is null) return;
            dynamic? voice = Activator.CreateInstance(type);
            voice?.Speak(text, 0);
        }
        catch { }
    }

    private void CheckNotification()
    {
        var notification = NotificationForm.Read();
        if (notification is null) return;
        var (title, message, utc) = notification.Value;
        if (utc <= _lastNotificationUtc) return;
        _lastNotificationUtc = utc;
        if (utc < _helperStartedUtc) return;
        try
        {
            _notification?.Close();
            _notification = new NotificationForm(title, message);
            _notification.Show();
            HelperPaths.LogEvent($"notification-shown {title}: {message}");
        }
        catch { }
    }

    private void SetVisible(bool countdown, bool locked, string? reason, string? banner = null)
    {
        if (_lastVisible != countdown)
        {
            HelperPaths.LogEvent(countdown ? "countdown-shown" : "countdown-hidden");
            _lastVisible = countdown;
        }
        HelperPaths.WriteStatus(locked, countdown, reason, banner);
    }

    private static string Format(TimeSpan span) =>
        $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";

    private static string FormatShort(TimeSpan span) =>
        span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : $"{Math.Max(1, span.Minutes)}m";

    private void ShowPinDialog()
    {
        if (_pinDialog is { IsDisposed: false })
        {
            _pinDialog.BringToFront();
            _pinDialog.Activate();
            return;
        }

        var dialog = new PinDialog();
        dialog.Verified += ticket =>
        {
            dialog.Close();
            ShowOverrideMenu(ticket);
        };
        dialog.FormClosed += (_, _) => _pinDialog = null;
        _pinDialog = dialog;
        ShowOwned(dialog);
        dialog.BringToFront();
        dialog.Activate();
    }

    private void ShowOverrideMenu(string ticket)
    {
        OverrideMenu? menu = null;
        menu = new OverrideMenu(
            (option, minutes) =>
            {
                menu!.Close();
                _ = ApplyOverrideAsync(ticket, option, minutes);
            },
            () => menu?.Close());
        ShowOwned(menu);
        menu.BringToFront();
        menu.Activate();
    }

    private void ShowOwned(Form dialog)
    {
        var owner = _overlays.Visible ? _overlays.Owner : null;
        if (owner is { IsDisposed: false }) dialog.Show(owner);
        else dialog.Show();
    }

    private async Task ApplyOverrideAsync(string ticket, string option, int? minutes)
    {
        var response = await PinPipeClient.ApplyOverrideAsync(ticket, option, minutes, CancellationToken.None);
        if (!response.Ok)
        {
            MessageBox.Show(response.Error ?? Loc.T("Client.OverrideFailedBody"), Loc.T("Client.OverrideFailedTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1,
                MessageBoxOptions.DefaultDesktopOnly);
        }
        else
        {
            HelperPaths.LogEvent($"override-applied option={option} minutes={minutes}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _remote.Dispose();
            _notice?.Dispose();
            _marshal.Dispose();
            _hook.Dispose();
            _overlays.Dispose();
            _countdown.Dispose();
            _pinDialog?.Dispose();
            _notification?.Dispose();
            _messagePopup?.Dispose();
            if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
            _trayIcon?.Dispose();
            _trayBitmap?.Dispose();
            _usage.FlushAll();
        }
        base.Dispose(disposing);
    }
}
