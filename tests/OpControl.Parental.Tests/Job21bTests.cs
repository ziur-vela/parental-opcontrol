// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Globalization;
using OpControl.Parental.Server.Services;
using OpControl.Parental.Server.Services.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job21bTests
{
    [Fact]
    public void Day_grouping_uses_the_family_time_zone_at_midnight()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var items = new[]
        {
            new { Id = 1, Utc = new DateTime(2026, 11, 13, 6, 30, 0, DateTimeKind.Utc) },
            new { Id = 2, Utc = new DateTime(2026, 11, 13, 7, 30, 0, DateTimeKind.Utc) }
        };
        var today = new DateOnly(2026, 11, 13);
        var groups = MessageDayGrouping.Group(items, item => item.Utc, zone, today,
            (date, now) => MessageDayGrouping.Label(date, now, "Today", "Yesterday", CultureInfo.GetCultureInfo("en-US")));

        Assert.Equal(2, groups.Count);
        Assert.Equal("Yesterday", groups[0].Label);
        Assert.Single(groups[0].Items);
        Assert.Equal("Today", groups[1].Label);
        Assert.Single(groups[1].Items);
        Assert.Equal("msg-day-2026-11-12", groups[0].Id);
    }

    [Fact]
    public void Older_day_label_uses_the_dashboard_language()
    {
        var date = new DateOnly(2026, 11, 10);
        Assert.Equal("Tue 10 Nov", MessageDayGrouping.Label(date, new DateOnly(2026, 11, 13),
            "Today", "Yesterday", CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("mar 10 nov", MessageDayGrouping.Label(date, new DateOnly(2026, 11, 13),
            "Hoy", "Ayer", CultureInfo.GetCultureInfo("es-MX")));
    }

    [Fact]
    public void Search_intent_hint_is_conservative_and_bilingual()
    {
        Assert.True(LunaSearchIntent.LikelyNeedsSearch("What important space news happened this week?"));
        Assert.True(LunaSearchIntent.LikelyNeedsSearch("¿Cuáles son las noticias de hoy?"));
        Assert.False(LunaSearchIntent.LikelyNeedsSearch("What is 7 times 8?"));
        Assert.False(LunaSearchIntent.LikelyNeedsSearch(""));
    }

    [Fact]
    public void Activity_store_is_child_scoped_and_clears()
    {
        var store = new LunaActivityStore();
        var child = Guid.NewGuid();
        var chat = Guid.NewGuid();
        Assert.Equal("idle", store.Get(child, chat));
        store.Set(child, chat, LunaActivityStore.Searching);
        Assert.Equal("searching", store.Get(child, chat));
        Assert.Equal("idle", store.Get(Guid.NewGuid(), chat));
        store.Set(child, chat, LunaActivityStore.Thinking);
        Assert.Equal("thinking", store.Get(child, chat));
        store.Clear(chat);
        Assert.Equal("idle", store.Get(child, chat));
    }

    [Fact]
    public void Parent_thread_has_auto_scroll_and_a_jump_button()
    {
        var root = FindRoot();
        var js = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "wwwroot", "js", "app.js"));
        var page = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Pages", "Messages.razor"));
        Assert.Contains("afterRender", js, StringComparison.Ordinal);
        Assert.Contains("scrollToBottom", js, StringComparison.Ordinal);
        Assert.Contains("msg-new-message", page, StringComparison.Ordinal);
        Assert.Contains("data-token", page, StringComparison.Ordinal);
        Assert.Contains("MessageDayGrouping.Group", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Popup_uses_the_child_theme_palette()
    {
        var root = FindRoot();
        var theme = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "MessagePopupTheme.cs"));
        Assert.Contains("KidTheme.Apply(theme)", theme, StringComparison.Ordinal);
        Assert.Contains("KidTheme.Primary", theme, StringComparison.Ordinal);
        Assert.Contains("gray", theme, StringComparison.Ordinal);
        Assert.Contains("night", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void Kid_messages_have_day_separators()
    {
        var root = FindRoot();
        var window = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        Assert.Contains("FriendlyMessageDay", window, StringComparison.Ordinal);
        Assert.Contains("Client.Messages.Today", window, StringComparison.Ordinal);
        Assert.Contains("Client.Messages.Yesterday", window, StringComparison.Ordinal);
    }


    [Fact]
    public void Searching_view_respects_reduced_motion_and_swaps_with_thinking()
    {
        using var session = Avalonia.Headless.HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            OpControl.Parental.Luna.LunaMotion.ForceReducedMotionForTest = true;
            var palette = OpControl.Parental.Luna.LunaThemes.From("night");
            var view = new OpControl.Parental.Luna.LunaSearchingView(palette);
            Assert.False(view.AnimatingForTest);
            var waiting = new OpControl.Parental.Luna.LunaWaitingView(palette);
            Assert.False(waiting.SearchingShown);
            waiting.ShowSearching();
            Assert.True(waiting.SearchingShown);
            waiting.ShowThinking();
            Assert.False(waiting.SearchingShown);
            view.Stop();
            waiting.Stop();
            OpControl.Parental.Luna.LunaMotion.ForceReducedMotionForTest = null;
        }, CancellationToken.None);
    }

    [Fact]
    public void Job_21_source_label_is_still_the_same_in_both_languages()
    {
        var root = FindRoot();
        var en = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "en.json"));
        var es = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "es-MX.json"));
        Assert.Contains("Where I found this", en, StringComparison.Ordinal);
        Assert.Contains("Dónde lo encontré", es, StringComparison.Ordinal);
        Assert.Contains("Investigating on the Web…", en, StringComparison.Ordinal);
        Assert.Contains("Investigando en la web…", es, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
