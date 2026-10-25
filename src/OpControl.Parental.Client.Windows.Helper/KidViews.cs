// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Globalization;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

internal abstract class KidView : UserControl
{
    protected ChildSummaryDto? Summary;
    public ChildSummaryDto? CachedSummary => Summary;
    private bool _rebuilding;
    private int _lastWidth;

    public static bool Scaling { get; set; }

    public event Action<string>? Navigate;

    protected int ContentWidth => Math.Max(320, (Width > 40 ? Width : 820) - 28);

    protected KidView()
    {
        Dock = DockStyle.Fill;
        BackColor = KidTheme.Bg;
        ForeColor = KidTheme.Text;
        AutoScroll = false;
        Padding = new Padding(0);
        DoubleBuffered = true;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Scaling || _rebuilding || Summary is null || Width <= 0) return;
        if (Math.Abs(Width - _lastWidth) < 24) return;
        _lastWidth = Width;
        Rebuild();
    }

    public virtual void SetSummary(ChildSummaryDto summary)
    {
        Summary = summary;
        _lastWidth = Width;
        BackColor = KidTheme.Bg;
        ForeColor = KidTheme.Text;
        Rebuild();
    }

    public virtual void UpdateLive(RuntimeLockStateDto? state, bool online) { }

    protected abstract void Rebuild();

    protected void Raise(string tab) => Navigate?.Invoke(tab);

    protected Control PageHeader(string eyebrow, string title, Control? trailing = null)
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = trailing is null ? 1 : 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 14)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        if (trailing is not null) header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(KidUi.Label(Loc.T(eyebrow), 9f, true, KidTheme.Label), 0, 0);
        header.Controls.Add(KidUi.Title(title), 0, 1);
        if (trailing is not null)
        {
            trailing.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            header.Controls.Add(trailing, 1, 1);
            header.SetRowSpan(trailing, 2);
            trailing.Anchor = AnchorStyles.Right;
        }
        return header;
    }

    protected static CardPanel Card(string? title = null, bool stretch = true)
    {
        var card = new CardPanel { Dock = stretch ? DockStyle.Fill : DockStyle.Top, Padding = new Padding(16) };
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 0,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        card.Controls.Add(stack);
        card.Tag = stack;
        if (!string.IsNullOrWhiteSpace(title)) AddRow(stack, KidUi.Label(title!, 11f, true, KidTheme.Label));
        return card;
    }

    protected static TableLayoutPanel CardStack(CardPanel card) => (TableLayoutPanel)card.Tag!;

    protected static void AddRow(TableLayoutPanel stack, Control child, int gap = 6)
    {
        var row = stack.RowCount;
        stack.RowCount = row + 1;
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        child.Dock = DockStyle.Top;
        child.Margin = new Padding(0, 0, 0, gap);
        stack.Controls.Add(child, 0, row);
    }

    protected static TableLayoutPanel Grid(int columns, int rows)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = columns,
            RowCount = rows,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        for (var i = 0; i < columns; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        for (var i = 0; i < rows; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        return grid;
    }

    protected static Panel ScrollHost(Control content)
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
        content.Dock = DockStyle.Top;
        scroll.Controls.Add(content);
        return scroll;
    }

    protected static TableLayoutPanel PageStack()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 0,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        return page;
    }

    protected static void AddPageRow(TableLayoutPanel page, Control child, int gap = 12)
    {
        var row = page.RowCount;
        page.RowCount = row + 1;
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        child.Dock = DockStyle.Top;
        child.Margin = new Padding(0, 0, 0, gap);
        page.Controls.Add(child, 0, row);
    }

    protected void RebuildBase(Control content)
    {
        _rebuilding = true;
        SuspendLayout();
        try
        {
            Controls.Clear();
            Controls.Add(content);
        }
        finally
        {
            ResumeLayout(true);
            _rebuilding = false;
        }
    }

    protected static TableLayoutPanel StatRow(string left, string right)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(KidUi.Body(left, size: 10f), 0, 0);
        row.Controls.Add(KidUi.Mono(right, 8.5f, false, KidTheme.Muted), 1, 0);
        return row;
    }

    protected static string LocalTime(DateTime utc) =>
        utc.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.CurrentCulture);

    protected static string DayName(int day) => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)day);

    protected static string WeekdayLabel(int day) => CultureInfo.CurrentCulture.DateTimeFormat.GetDayName((DayOfWeek)day);

    protected static string HoursLabel(int minutes) =>
        minutes >= 60 ? $"{minutes / 60}h{(minutes % 60 > 0 ? $" {minutes % 60}m" : "")}" : $"{minutes}m";
}

internal sealed class HomeView : KidView
{
    private Label? _date;
    private Label? _greeting;
    private KidStatusPill? _statusChip;
    private Label? _countdown;
    private Label? _used;
    private Label? _bonus;
    private RingPanel? _ring;
    private Label? _nextLockTitle;
    private Label? _nextLockDetail;
    private Label? _nextLockTime;
    private MoonFace? _nextLockMoon;
    private bool _online;
    private RuntimeLockStateDto? _live;

    public override void SetSummary(ChildSummaryDto summary)
    {
        Summary = summary;
        Rebuild();
        UpdateLive(_live, _online);
    }

    public override void UpdateLive(RuntimeLockStateDto? state, bool online)
    {
        _live = state;
        _online = online;
        if (_date is not null)
            _date.Text = DateTime.Now.ToString("dddd, MMM d", CultureInfo.CurrentCulture);
        if (_greeting is not null && Summary is not null)
        {
            var first = Summary.ChildName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? Summary.ChildName;
            _greeting.Text = Loc.T("Client.Kid.Greeting", first);
        }
        if (_statusChip is not null)
        {
            var locked = state?.IsLocked == true;
            _statusChip.SetText((locked ? Loc.T("Client.Kid.Status.Locked") : Loc.T("Client.Kid.Status.Unlocked"))
                + " · " + (online ? Loc.T("Client.Kid.Status.Online") : Loc.T("Client.Kid.Status.Offline")),
                locked ? KidTheme.Bad : KidTheme.Ok);
        }
        UpdateTimeLeft(state);
    }

    private void UpdateTimeLeft(RuntimeLockStateDto? state)
    {
        if (Summary is null || _countdown is null || _used is null || _ring is null || _bonus is null) return;
        var now = DateTime.UtcNow;
        var remainingSeconds = state?.DailyLimitRemainingSeconds;
        var dailyUsed = Summary.UsedTodaySeconds;
        var dailyLimit = Summary.ScreenTimeEnabled ? Summary.TodayLimitMinutes * 60 : 0;
        if (state?.DailyLimitMinutes is { } liveMinutes && state.DailyLimitRemainingSeconds is { } liveLeft)
        {
            dailyLimit = liveMinutes * 60;
            dailyUsed = Math.Max(0, liveMinutes * 60 - liveLeft);
        }

        TimeSpan? banner = null;
        if (state?.BonusUntilUtc is { } bonus && bonus > now)
            banner = bonus - now;
        else if (state?.NextLockUtc is { } next && next > now)
            banner = next - now;
        else if (remainingSeconds is { } left && left > 0)
            banner = TimeSpan.FromSeconds(left);

        _countdown.Text = dailyLimit > 0
            ? KidUi.Clock(Math.Max(0, dailyLimit - dailyUsed))
            : banner is { } span ? KidUi.Clock((int)span.TotalSeconds)
            : Loc.T("Client.Kid.NoLimit");
        _used.Text = dailyLimit > 0
            ? Loc.T("Client.Kid.UsedOf", KidUi.Duration(dailyUsed), KidUi.Duration(dailyLimit))
            : Loc.T("Client.Kid.UsedNoLimit", KidUi.Duration(dailyUsed));
        _ring.Fraction = dailyLimit > 0 ? Math.Max(0, (double)(dailyLimit - dailyUsed) / dailyLimit) : 0;

        if (state?.BonusUntilUtc is { } bonusUntil && bonusUntil > now)
        {
            _bonus.Text = Loc.T("Client.Kid.BonusActive", state.BonusMinutes ?? 0,
                state.ParentDisplayName ?? Loc.T("Client.Kid.YourParents"),
                bonusUntil.ToLocalTime().ToString("t", CultureInfo.CurrentCulture));
            _bonus.Visible = true;
        }
        else if (state?.Reason == "override" && state.EndsUtc is { } unlockedUntil && unlockedUntil > now)
        {
            var when = unlockedUntil.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
            _bonus.Text = string.IsNullOrWhiteSpace(state.UnlockScheduleName)
                ? Loc.T("Client.Kid.ParentUnlock", when)
                : Loc.T("Client.Kid.ParentUnlockSchedule", when, state.UnlockScheduleName);
            _bonus.Visible = true;
        }
        else
        {
            _bonus.Visible = false;
        }
    }

    protected override void Rebuild()
    {
        if (Summary is null) return;
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 16)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var awake = new MoonFace(64) { Margin = new Padding(0, 0, 14, 0) };
        header.Controls.Add(awake, 0, 0);
        var titles = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0) };
        _date = KidUi.Label("", 10f, true, KidTheme.Label);
        _greeting = KidUi.Title("");
        _greeting.Font = Theme.Display(24f, FontStyle.Bold);
        titles.Controls.Add(_date, 0, 0);
        titles.Controls.Add(_greeting, 0, 1);
        header.Controls.Add(titles, 1, 0);
        _statusChip = new KidStatusPill("") { Anchor = AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0, 8, 0, 0) };
        header.Controls.Add(_statusChip, 2, 0);
        root.Controls.Add(header, 0, 0);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        for (var i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 246));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var timeCard = BuildTimeCard();
        var lockCard = BuildNextLockCard();
        var usedCard = BuildMostUsedCard();
        var rulesCard = BuildRulesCard();
        var messagesCard = BuildMessagesCard();

        grid.Controls.Add(timeCard, 0, 0);
        grid.SetColumnSpan(timeCard, 2);
        grid.Controls.Add(lockCard, 2, 0);
        grid.Controls.Add(usedCard, 0, 1);
        grid.Controls.Add(rulesCard, 1, 1);
        grid.Controls.Add(messagesCard, 2, 1);
        root.Controls.Add(grid, 0, 1);

        var fullTop = grid.RowStyles[0].Height;
        grid.SizeChanged += (_, _) => FitHomeRows(grid, fullTop);

        RebuildBase(root);
        UpdateLive(_live, _online);
    }

    private void FitHomeRows(TableLayoutPanel grid, float fullTop)
    {
        if (grid.Height <= 0 || grid.RowStyles.Count < 2) return;
        var f = fullTop / 246f;
        var minBottom = 210f * f;
        var minTop = 198f * f;
        var top = Math.Max(minTop, Math.Min(fullTop, grid.Height - minBottom));
        if (Math.Abs(grid.RowStyles[0].Height - top) >= 1f)
            grid.RowStyles[0].Height = top;
        if (_ring is not null)
        {
            var compact = top < 235f * f;
            var ring = (int)Math.Round((compact ? 104f : 128f) * f);
            if (_ring.Width != ring) { _ring.Size = new Size(ring, ring); _ring.Invalidate(); }
        }
    }

    private CardPanel BuildTimeCard()
    {
        var card = Card();
        var stack = CardStack(card);
        AddRow(stack, KidUi.Label(Loc.T("Client.Kid.TimeLeft"), 11f, true, KidTheme.Label), 8);
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _ring = new RingPanel { Size = new Size(128, 128), Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 4, 20, 0) };
        row.Controls.Add(_ring, 0, 0);
        var values = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
        _countdown = new Label
        {
            Text = "0h 00m",
            Font = Theme.Display(29f, FontStyle.Bold),
            ForeColor = KidTheme.Text,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 2)
        };
        _used = KidUi.Body("", true, 10.5f);
        _used.Font = Theme.Body(10.5f, FontStyle.Bold);
        _bonus = KidUi.Body("", false, 9.5f);
        _bonus.Font = Theme.Body(9.5f, FontStyle.Bold);
        _bonus.ForeColor = KidTheme.Warn;
        _bonus.Visible = false;
        values.Controls.Add(_countdown, 0, 0);
        values.Controls.Add(_used, 0, 1);
        values.Controls.Add(KidUi.Body(Loc.T("Client.Kid.ResetsMidnight"), true, 9f), 0, 2);
        values.Controls.Add(_bonus, 0, 3);
        row.Controls.Add(values, 1, 0);
        AddRow(stack, row, 4);
        var footer = KidUi.Link(Loc.T("Client.Kid.ViewSchedule"));
        footer.Click += (_, _) => Raise("schedule");
        AddRow(stack, footer, 0);
        return card;
    }

    private CardPanel BuildNextLockCard()
    {
        var card = Card();
        var stack = CardStack(card);
        AddRow(stack, KidUi.Label(Loc.T("Client.Kid.NextLock"), 11f, true, KidTheme.Label), 8);
        var row = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _nextLockMoon = new MoonFace(42, asleep: true) { Margin = new Padding(0, 2, 12, 0) };
        row.Controls.Add(_nextLockMoon, 0, 0);
        var values = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
        _nextLockTitle = KidUi.Label("", 11f, true, KidTheme.Text);
        _nextLockTime = KidUi.Mono("", 17f, true, KidTheme.Text);
        _nextLockDetail = KidUi.Body("", true, 9f);
        values.Controls.Add(_nextLockTitle, 0, 0);
        values.Controls.Add(_nextLockTime, 0, 1);
        values.Controls.Add(_nextLockDetail, 0, 2);
        row.Controls.Add(values, 1, 0);
        AddRow(stack, row, 8);
        var link = KidUi.Link(Loc.T("Client.Kid.ViewSchedule"));
        link.Click += (_, _) => Raise("schedule");
        AddRow(stack, link, 0);
        FillNextLock();
        return card;
    }

    private void FillNextLock()
    {
        if (Summary is null || _nextLockTitle is null || _nextLockTime is null || _nextLockDetail is null) return;
        var now = DateTime.Now;
        var today = now.DayOfWeek;
        var next = Summary.Schedules
            .Where(s => s.IsEnabled && (s.DaysMask & (1 << (int)today)) != 0 && s.StartMinutes > now.Hour * 60 + now.Minute)
            .OrderBy(s => s.StartMinutes)
            .FirstOrDefault();
        if (next is null)
        {
            _nextLockTitle.Text = Loc.T("Client.Kid.NoLocks");
            _nextLockTime.Text = "";
            _nextLockDetail.Text = "";
            if (_nextLockMoon is not null) _nextLockMoon.Visible = false;
            return;
        }
        var start = now.Date.AddMinutes(next.StartMinutes);
        var end = next.EndMinutes >= next.StartMinutes ? now.Date.AddMinutes(next.EndMinutes) : now.Date.AddDays(1).AddMinutes(next.EndMinutes);
        _nextLockTitle.Text = next.Name;
        _nextLockTime.Text = start.ToString("t", CultureInfo.CurrentCulture);
        _nextLockDetail.Text = Loc.T("Client.Kid.NextLockUntil", end.ToString("t", CultureInfo.CurrentCulture));
        if (_nextLockMoon is not null) _nextLockMoon.Visible = true;
    }

    private CardPanel BuildMostUsedCard()
    {
        var card = Card(Loc.T("Client.Kid.MostUsed"));
        var stack = CardStack(card);
        var apps = Summary!.AppsToday.Where(a => a.Seconds > 0).Take(4).ToList();
        if (apps.Count == 0)
        {
            AddRow(stack, KidUi.Body(Loc.T("Client.Kid.NoUsage"), true), 0);
            return card;
        }
        var max = Math.Max(1, apps.Max(a => a.Seconds));
        foreach (var app in apps)
        {
            var box = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            box.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            box.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var name = new Label
            {
                Text = ShortAppName(app.DisplayName),
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Height = 20,
                Font = Theme.Body(9.5f, FontStyle.Bold),
                ForeColor = KidTheme.Text,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };
            var value = KidUi.Mono(KidUi.Duration(app.Seconds), 8.5f, true, KidTheme.Muted);
            value.Anchor = AnchorStyles.Right;
            value.Margin = new Padding(6, 1, 0, 0);
            box.Controls.Add(name, 0, 0);
            box.Controls.Add(value, 1, 0);
            var bar = new BarPanel { Fraction = (double)app.Seconds / max, Thickness = 8, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 0) };
            box.Controls.Add(bar, 0, 1);
            box.SetColumnSpan(bar, 2);
            AddRow(stack, box, 6);
        }
        return card;
    }

    private static string ShortAppName(string name)
    {
        var value = (name ?? "").Trim();
        var slash = value.LastIndexOf('\\');
        if (slash >= 0) value = value[(slash + 1)..];
        if (!value.Contains(' '))
        {
            var dot = value.IndexOf('.');
            if (dot > 0) value = value[..dot];
        }
        else
        {
            var dot = value.LastIndexOf('.');
            if (dot > 0 && value.Length - dot <= 5) value = value[..dot];
        }
        return string.IsNullOrWhiteSpace(value) ? name ?? "" : value;
    }

    private CardPanel BuildRulesCard()
    {
        var card = Card(Loc.T("Client.Kid.MyRules"));
        var stack = CardStack(card);
        var items = new (string Text, string Label)[]
        {
            (Summary!.Websites.Count(w => !w.Allowed).ToString(CultureInfo.InvariantCulture), Loc.T("Client.Kid.BlockedSites")),
            (Summary.Apps.Count(a => !a.Allowed).ToString(CultureInfo.InvariantCulture), Loc.T("Client.Kid.BlockedApps")),
            (Summary.Apps.Count(a => a.DailyLimitMinutes is > 0).ToString(CultureInfo.InvariantCulture), Loc.T("Client.Kid.AppLimits")),
            ((Summary.Websites.Count(w => w.Allowed) + Summary.Apps.Count(a => a.AlwaysAllowed)).ToString(CultureInfo.InvariantCulture), Loc.T("Client.Kid.AlwaysAllowed"))
        };
        foreach (var item in items)
            AddRow(stack, StatRow(item.Label, item.Text), 4);
        var link = KidUi.Link(Loc.T("Client.Kid.ViewRules"));
        link.Click += (_, _) => Raise("rules");
        AddRow(stack, link, 0);
        return card;
    }

    private CardPanel BuildMessagesCard()
    {
        var card = Card(Loc.T("Client.Kid.Messages"));
        var stack = CardStack(card);
        var latest = Summary!.RecentMessages.LastOrDefault(m => m.Direction == MessageDirection.ParentToChild);
        if (latest is null)
        {
            AddRow(stack, KidUi.Body(Loc.T("Client.Kid.NoMessages"), true), 0);
        }
        else
        {
            var sub = new CardPanel
            {
                FillColor = KidTheme.Sub,
                BorderColor = KidTheme.SubBorder,
                CornerRadius = 14,
                Padding = new Padding(10, 8, 10, 8),
                Dock = DockStyle.Top,
                Height = 62
            };
            var inner = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0) };
            inner.Controls.Add(KidUi.Mono(MessageDisplay.Sender(latest) + " · " + MessageDisplay.Time(latest), 8f, true, KidTheme.Label), 0, 0);
            var body = new Label
            {
                Text = latest.Text,
                Font = Theme.Body(10f, FontStyle.Bold),
                ForeColor = KidTheme.Text,
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            inner.Controls.Add(body, 0, 1);
            sub.Controls.Add(inner);
            AddRow(stack, sub, 6);
            if (Summary.UnreadMessages > 0)
                AddRow(stack, KidUi.Mono(Loc.T("Client.Kid.Unread", Summary.UnreadMessages), 8.5f, true, KidTheme.Warn), 4);
        }
        var button = new KidButton(Loc.T("Client.Kid.MessageParents"), KidButtonKind.Primary)
        {
            Dock = DockStyle.Top,
            Height = 42,
            Margin = new Padding(0, 6, 0, 0)
        };
        button.Click += (_, _) => Raise("messages");
        AddRow(stack, button, 0);
        return card;
    }
}

internal sealed class ScheduleView : KidView
{
    protected override void Rebuild()
    {
        if (Summary is null) return;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        var header = PageHeader("Client.Kid.Schedule.Eyebrow", Loc.T("Client.Kid.Schedule.Title"));
        header.Margin = new Padding(0, 0, 0, 14);
        root.Controls.Add(header, 0, 0);
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        var week = BuildWeekCard();
        week.Margin = new Padding(0, 0, 7, 0);
        var right = BuildLocksAndLimits();
        right.Margin = new Padding(7, 0, 0, 0);
        grid.Controls.Add(week, 0, 0);
        grid.Controls.Add(right, 1, 0);
        root.Controls.Add(grid, 0, 1);
        RebuildBase(root);
    }

    private CardPanel BuildWeekCard()
    {
        var card = Card(Loc.T("Client.Kid.Schedule.Daily"));
        var stack = CardStack(card);
        for (var day = 0; day < 7; day++)
        {
            var minutes = Summary!.Weekdays.FirstOrDefault(w => w.Day == day)?.Minutes ?? 0;
            var isToday = (int)DateTime.Now.DayOfWeek == day;
            var row = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = new Padding(0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            var label = isToday ? DayName(day) + " · " + Loc.T("Client.Kid.Today") : DayName(day);
            row.Controls.Add(KidUi.Label(label, 9.5f, true, isToday ? KidTheme.Text : KidTheme.Muted), 0, 0);
            var bar = new BarPanel
            {
                Fraction = Summary.ScreenTimeEnabled ? Math.Min(1, minutes / 480d) : 0,
                BarColor = isToday ? KidTheme.RingFill : KidTheme.BarFill,
                TrackColor = KidTheme.BarTrack,
                Thickness = 8,
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 8, 8, 8)
            };
            row.Controls.Add(bar, 1, 0);
            row.Controls.Add(KidUi.Mono(Summary.ScreenTimeEnabled ? HoursLabel(minutes) : Loc.T("Client.Kid.Schedule.Off"), 8.5f, isToday, isToday ? KidTheme.Text : KidTheme.Muted), 2, 0);
            AddRow(stack, row, 4);
        }
        AddRow(stack, KidUi.Body(Loc.T("Client.Kid.Schedule.RunsOut"), true, 9f), 0);
        return card;
    }

    private CardPanel BuildLocksAndLimits()
    {
        var card = Card();
        var stack = CardStack(card);
        AddRow(stack, KidUi.Label(Loc.T("Client.Kid.Schedule.LockedTimes"), 11f, true, KidTheme.Label), 8);
        if (Summary!.Schedules.Count == 0)
        {
            AddRow(stack, KidUi.Body(Loc.T("Client.Kid.Schedule.NoLocks"), true), 8);
        }
        foreach (var schedule in Summary.Schedules)
        {
            var row = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var moon = new MoonFace(34, asleep: true) { Margin = new Padding(0, 0, 10, 0) };
            row.Controls.Add(moon, 0, 0);
            var names = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            names.Controls.Add(KidUi.Label(schedule.Name, 10f, true, KidTheme.Text), 0, 0);
            names.Controls.Add(KidUi.Body(Days(schedule.DaysMask), true, 8.5f), 0, 1);
            row.Controls.Add(names, 1, 0);
            row.Controls.Add(KidUi.Body($"{ClockLabel(schedule.StartMinutes)} {Loc.T("Client.Kid.Schedule.To")} {ClockLabel(schedule.EndMinutes)}", size: 9f), 2, 0);
            AddRow(stack, row, 8);
        }

        AddRow(stack, KidUi.Label(Loc.T("Client.Kid.Schedule.AppLimits"), 11f, true, KidTheme.Label), 6);
        var apps = Summary.Apps.Where(a => a.DailyLimitMinutes is > 0).ToList();
        if (apps.Count == 0)
            AddRow(stack, KidUi.Body(Loc.T("Client.Kid.Schedule.NoAppLimits"), true), 0);
        foreach (var app in apps)
        {
            var row = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Controls.Add(KidUi.Body(app.DisplayName, size: 9.5f), 0, 0);
            row.Controls.Add(KidUi.Mono(Loc.T("Client.Kid.Schedule.Used", KidUi.Duration(app.UsedTodaySeconds)), 8f), 1, 0);
            row.Controls.Add(KidUi.Mono(Loc.T("Client.Kid.Schedule.Limit", app.DailyLimitMinutes ?? 0), 8f, true, KidTheme.Muted), 2, 0);
            AddRow(stack, row, 4);
        }
        return card;
    }

    private static string Clock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    private static string ClockLabel(int minutes) =>
        DateTime.Today.AddMinutes(minutes).ToString("t", CultureInfo.CurrentCulture);

    private static string Days(int mask)
    {
        var selected = Enumerable.Range(0, 7).Where(d => (mask & (1 << d)) != 0).ToList();
        if (selected.Count == 7) return Loc.T("Client.Kid.Schedule.EveryDay");
        if (selected.SequenceEqual(new[] { 1, 2, 3, 4, 5 })) return Loc.T("Client.Kid.Schedule.Weekdays");
        if (selected.SequenceEqual(new[] { 0, 6 })) return Loc.T("Client.Kid.Schedule.Weekends");
        return string.Join(", ", selected.Select(DayName));
    }
}

internal sealed class RulesView : KidView
{
    private bool _apps;

    public void ShowApps()
    {
        _apps = true;
        if (Summary is not null) Rebuild();
    }

    protected override void Rebuild()
    {
        if (Summary is null) return;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var segmented = new KidSegmented(Loc.T("Client.Kid.Rules.Websites"), Loc.T("Client.Kid.Rules.Apps"))
        {
            SelectedIndex = _apps ? 1 : 0,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        segmented.Changed += index => { if ((index == 1) != _apps) { _apps = index == 1; Rebuild(); } };
        var header = PageHeader("Client.Kid.Rules.Eyebrow", Loc.T("Client.Kid.Rules.Title"), segmented);
        header.Margin = new Padding(0, 0, 0, 14);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(BuildGrid(), 0, 1);

        var footer = KidUi.Body(Loc.T("Client.Kid.Rules.AskParents"), true, 9.5f);
        footer.Cursor = Cursors.Hand;
        footer.Margin = new Padding(0, 12, 0, 0);
        footer.Click += (_, _) => Raise("messages");
        root.Controls.Add(footer, 0, 2);
        RebuildBase(root);
    }

    private Control BuildGrid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        if (!_apps)
        {
            var categories = Card(Loc.T("Client.Kid.Rules.Categories"));
            categories.AutoScroll = true;
            var catStack = CardStack(categories);
            if (Summary!.Categories.Count == 0) AddRow(catStack, KidUi.Body(Loc.T("Client.Kid.Rules.None"), true), 4);
            foreach (var category in Summary.Categories)
                AddRow(catStack, KidUi.Body(LocalizeCategory(category), size: 10f), 4);
            AddRow(catStack, KidUi.Label(Loc.T("Client.Kid.Rules.AlwaysAllowedSites"), 10f, true, KidTheme.Label), 4);
            var allowedSites = Summary.Websites.Where(w => w.Allowed).ToList();
            if (allowedSites.Count == 0) AddRow(catStack, KidUi.Body(Loc.T("Client.Kid.Rules.None"), true), 0);
            foreach (var site in allowedSites)
            {
                var row = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                row.Controls.Add(new Panel { Width = 10, Height = 10, BackColor = KidTheme.DotOk, Margin = new Padding(0, 6, 8, 0) }, 0, 0);
                row.Controls.Add(KidUi.Body(site.Domain, size: 9.5f), 1, 0);
                AddRow(catStack, row, 4);
            }
            categories.Margin = new Padding(0, 0, 7, 0);

            var blocked = Card(Loc.T("Client.Kid.Rules.BlockedSites"));
            blocked.AutoScroll = true;
            var blockedStack = CardStack(blocked);
            var blockedSites = Summary.Websites.Where(w => !w.Allowed).ToList();
            if (blockedSites.Count == 0) AddRow(blockedStack, KidUi.Body(Loc.T("Client.Kid.Rules.None"), true), 0);
            foreach (var site in blockedSites)
            {
                var row = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.Controls.Add(KidUi.Body(site.Domain, size: 9.5f), 0, 0);
                row.Controls.Add(site.Always
                    ? KidTag.Danger(Loc.T("Client.Kid.Rules.Always"))
                    : KidTag.Warn(site.ScheduleName ?? Loc.T("Client.Kid.Rules.Scheduled")), 1, 0);
                AddRow(blockedStack, row, 5);
            }
            blocked.Margin = new Padding(7, 0, 0, 0);
            grid.Controls.Add(categories, 0, 0);
            grid.Controls.Add(blocked, 1, 0);
        }
        else
        {
            var blocked = Card(Loc.T("Client.Kid.Rules.BlockedApps"));
            blocked.AutoScroll = true;
            var blockedStack = CardStack(blocked);
            var blockedApps = Summary!.Apps.Where(a => !a.Allowed).ToList();
            if (blockedApps.Count == 0) AddRow(blockedStack, KidUi.Body(Loc.T("Client.Kid.Rules.None"), true), 0);
            foreach (var app in blockedApps)
            {
                var row = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.Controls.Add(KidUi.Body(app.DisplayName, size: 9.5f), 0, 0);
                row.Controls.Add(app.ScheduleName is null
                    ? KidTag.Danger(Loc.T("Client.Kid.Rules.Always"))
                    : KidTag.Warn(app.ScheduleName), 1, 0);
                AddRow(blockedStack, row, 5);
            }
            blocked.Margin = new Padding(0, 0, 7, 0);

            var allowed = Card(Loc.T("Client.Kid.Rules.AlwaysAllowedApps"));
            allowed.AutoScroll = true;
            var allowedStack = CardStack(allowed);
            AddRow(allowedStack, KidUi.Body(Loc.T("Client.Kid.Rules.AlwaysAllowedNote"), true, 9f), 6);
            var allowedApps = Summary.Apps.Where(a => a.AlwaysAllowed).ToList();
            if (allowedApps.Count == 0) AddRow(allowedStack, KidUi.Body(Loc.T("Client.Kid.Rules.None"), true), 0);
            foreach (var app in allowedApps)
            {
                var row = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                row.Controls.Add(new Panel { Width = 10, Height = 10, BackColor = KidTheme.DotOk, Margin = new Padding(0, 6, 8, 0) }, 0, 0);
                row.Controls.Add(KidUi.Body(app.DisplayName, size: 9.5f), 1, 0);
                AddRow(allowedStack, row, 4);
            }
            allowed.Margin = new Padding(7, 0, 0, 0);
            grid.Controls.Add(blocked, 0, 0);
            grid.Controls.Add(allowed, 1, 0);
        }
        return grid;
    }

    private static string LocalizeCategory(SummaryCategoryDto category)
    {
        var key = "Client.Kid.Category." + category.Key;
        var text = Loc.T(key);
        return text == key ? category.Name : text;
    }
}

internal sealed class ActivityView : KidView
{
    private bool _week;

    public void ShowWeek()
    {
        _week = true;
        if (Summary is not null) Rebuild();
    }

    protected override void Rebuild()
    {
        if (Summary is null) return;
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        var segmented = new KidSegmented(Loc.T("Client.Kid.Activity.Today"), Loc.T("Client.Kid.Activity.Week"))
        {
            SelectedIndex = _week ? 1 : 0,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        segmented.Changed += index => { if ((index == 1) != _week) { _week = index == 1; Rebuild(); } };
        page.Controls.Add(PageHeader("Client.Kid.Activity.Eyebrow", Loc.T("Client.Kid.Activity.Title"), segmented), 0, 0);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 4, 0, 0) };
        for (var i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
        grid.Controls.Add(BuildAppsCard(), 0, 0);
        grid.Controls.Add(BuildSitesCard(), 1, 0);
        grid.Controls.Add(BuildHistoryCard(), 2, 0);
        page.Controls.Add(grid, 0, 2);
        RebuildBase(page);
    }

    private CardPanel BuildAppsCard()
    {
        var summary = Summary!;
        var total = _week ? summary.UsedWeekSeconds : summary.UsedTodaySeconds;
        var card = Card(Loc.T("Client.Kid.Activity.Apps") + " · " + (_week ? Loc.T("Client.Kid.Activity.Week") : Loc.T("Client.Kid.Activity.Today")).ToLowerInvariant());
        card.Padding = new Padding(16);
        var stack = CardStack(card);
        AddRow(stack, KidUi.Mono(KidUi.Duration(total), 20f, true, KidTheme.Text), 8);
        var apps = (_week ? summary.AppsWeek : summary.AppsToday).Take(10).ToList();
        if (apps.Count == 0)
        {
            AddRow(stack, KidUi.Body(Loc.T("Client.Kid.Activity.None"), true), 0);
            return card;
        }
        var max = Math.Max(1, apps.Max(a => a.Seconds));
        foreach (var app in apps)
        {
            var box = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            box.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            box.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            box.Controls.Add(KidUi.Body(ShortAppName(app.DisplayName), size: 9f), 0, 0);
            var value = KidUi.Mono(KidUi.Duration(app.Seconds), 8f, true, KidTheme.Muted);
            value.Anchor = AnchorStyles.Right;
            box.Controls.Add(value, 1, 0);
            var bar = new BarPanel { Fraction = (double)app.Seconds / max, Thickness = 8, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 0) };
            box.Controls.Add(bar, 0, 1);
            box.SetColumnSpan(bar, 2);
            AddRow(stack, box, 6);
        }
        return card;
    }

    private static string ShortAppName(string name)
    {
        var value = (name ?? "").Trim();
        var slash = value.LastIndexOf('\\');
        if (slash >= 0) value = value[(slash + 1)..];
        return string.IsNullOrWhiteSpace(value) ? name ?? "" : value;
    }

    private CardPanel BuildSitesCard()
    {
        var card = Card(Loc.T("Client.Kid.Activity.Websites"));
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent, Margin = new Padding(0) };
        var stack = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, RowCount = 0, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = new Padding(0) };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        host.Controls.Add(stack);
        var visits = Summary!.Visits.AsEnumerable();
        if (!_week)
        {
            var start = DateTime.Today;
            visits = visits.Where(v => v.VisitedUtc.ToLocalTime() >= start);
        }
        var list = visits.Take(80).ToList();
        if (list.Count == 0) AddRow(stack, KidUi.Body(Loc.T("Client.Kid.Activity.None"), true), 0);
        foreach (var visit in list)
        {
            var row = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Controls.Add(KidUi.Mono(visit.VisitedUtc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture), 8f, false, KidTheme.Faint), 0, 0);
            var domain = KidUi.Body(visit.Domain, size: 9f);
            domain.MaximumSize = new Size(160, 0);
            domain.AutoEllipsis = true;
            row.Controls.Add(domain, 1, 0);
            if (visit.Blocked) row.Controls.Add(KidTag.Danger(Loc.T("Client.Kid.Activity.Blocked")), 2, 0);
            AddRow(stack, row, 4);
        }
        card.Controls.Add(host);
        host.BringToFront();
        return card;
    }

    private CardPanel BuildHistoryCard()
    {
        var card = Card(Loc.T("Client.Kid.Activity.History"));
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent, Margin = new Padding(0) };
        var stack = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, RowCount = 0, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = new Padding(0) };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        host.Controls.Add(stack);
        var cutoff = _week ? DateTime.Today.AddDays(-6) : DateTime.Today;
        var rows = Summary!.History.Where(h => h.CreatedUtc.ToLocalTime() >= cutoff).Take(40).ToList();
        if (rows.Count == 0) AddRow(stack, KidUi.Body(Loc.T("Client.Kid.Activity.None"), true), 0);
        foreach (var item in rows)
        {
            var row = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            var dotColor = item.Kind switch { "locked" => KidTheme.DotBad, "unlocked" => KidTheme.DotOk, _ => KidTheme.DotWarn };
            row.Controls.Add(new Panel { Width = 10, Height = 10, BackColor = dotColor, Margin = new Padding(0, 6, 8, 0) }, 0, 0);
            var text = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            text.Controls.Add(KidUi.Body(HistoryText(item), size: 9f), 0, 0);
            text.Controls.Add(KidUi.Mono(item.CreatedUtc.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.CurrentCulture), 8f, false, KidTheme.Faint), 0, 1);
            row.Controls.Add(text, 1, 0);
            AddRow(stack, row, 6);
        }
        card.Controls.Add(host);
        host.BringToFront();
        return card;
    }

    private static string HistoryText(SummaryHistoryDto item) => item.Kind switch
    {
        "locked" => Loc.T("Client.Kid.Activity.Locked"),
        "unlocked" => Loc.T("Client.Kid.Activity.Unlocked"),
        _ => item.Minutes is { } minutes ? Loc.T("Client.Kid.Activity.BonusMinutes", minutes) : Loc.T("Client.Kid.Activity.Bonus")
    };
}
