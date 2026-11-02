// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job20GTimerTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly LockTimerSettings DaysHidden = LockTimerSettings.Default with { DaysFormat = true, ShowSeconds = false };
    private static readonly LockTimerSettings DaysShown = LockTimerSettings.Default with { DaysFormat = true, ShowSeconds = true };
    private static readonly LockTimerSettings HoursHidden = LockTimerSettings.Default with { DaysFormat = false, ShowSeconds = false };
    private static readonly LockTimerSettings HoursShown = LockTimerSettings.Default with { DaysFormat = false, ShowSeconds = true };

    [Theory]
    [InlineData(86400, "1d 0h 0m", "1 d 0 h 0 min")]
    [InlineData(23 * 3600 + 59 * 60, "23h 59m", "23 h 59 min")]
    [InlineData(3600, "1h 0m", "1 h 0 min")]
    [InlineData(59 * 60, "59m", "59 min")]
    [InlineData(60, "1m", "1 min")]
    [InlineData(59, "0:59s", "0:59s")]
    [InlineData(0, "0:00s", "0:00s")]
    public void Format_boundaries_with_seconds_hidden_in_both_languages(int seconds, string english, string spanish)
    {
        var span = TimeSpan.FromSeconds(seconds);
        Assert.Equal(english, LockTimerFormat.Format(span, DaysHidden, "en"));
        Assert.Equal(spanish, LockTimerFormat.Format(span, DaysHidden, "es-MX"));
    }

    [Theory]
    [InlineData(86400, "1d 0h 0m 0s", "1 d 0 h 0 min 0 s")]
    [InlineData(23 * 3600 + 59 * 60, "23h 59m 00s", "23 h 59 min 00 s")]
    [InlineData(3600, "1h 0m 00s", "1 h 0 min 00 s")]
    [InlineData(59 * 60, "59m 00s", "59 min 00 s")]
    [InlineData(60, "1m 00s", "1 min 00 s")]
    [InlineData(59, "0:59s", "0:59s")]
    [InlineData(0, "0:00s", "0:00s")]
    public void Format_boundaries_with_seconds_shown_in_both_languages(int seconds, string english, string spanish)
    {
        var span = TimeSpan.FromSeconds(seconds);
        Assert.Equal(english, LockTimerFormat.Format(span, DaysShown, "en"));
        Assert.Equal(spanish, LockTimerFormat.Format(span, DaysShown, "es-MX"));
    }

    [Theory]
    [InlineData(86400, "24h 0m", "24 h 0 min")]
    [InlineData(32 * 3600 + 45 * 60, "32h 45m", "32 h 45 min")]
    [InlineData(23 * 3600 + 59 * 60, "23h 59m", "23 h 59 min")]
    [InlineData(3600, "1h 0m", "1 h 0 min")]
    [InlineData(59 * 60, "59m", "59 min")]
    [InlineData(60, "1m", "1 min")]
    [InlineData(59, "0:59s", "0:59s")]
    public void Hours_setting_uses_hours_instead_of_days(int seconds, string english, string spanish)
    {
        var span = TimeSpan.FromSeconds(seconds);
        Assert.Equal(english, LockTimerFormat.Format(span, HoursHidden, "en"));
        Assert.Equal(spanish, LockTimerFormat.Format(span, HoursHidden, "es-MX"));
    }

    [Fact]
    public void Hours_setting_with_seconds_shown_keeps_the_same_boundaries()
    {
        Assert.Equal("32h 45m 52s", LockTimerFormat.Format(TimeSpan.FromSeconds(32 * 3600 + 45 * 60 + 52), HoursShown, "en"));
        Assert.Equal("24h 0m 00s", LockTimerFormat.Format(TimeSpan.FromSeconds(86400), HoursShown, "en"));
        Assert.Equal("59m 59s", LockTimerFormat.Format(TimeSpan.FromSeconds(59 * 60 + 59), HoursShown, "en"));
        Assert.Equal("1m 00s", LockTimerFormat.Format(TimeSpan.FromSeconds(60), HoursShown, "en"));
        Assert.Equal("0:42s", LockTimerFormat.Format(TimeSpan.FromSeconds(42), HoursShown, "es-MX"));
    }

    [Fact]
    public void Unlock_five_minutes_before_an_upcoming_schedule_skips_it_until_the_daily_window_ends()
    {
        var now = new DateTime(2026, 10, 19, 20, 25, 0, DateTimeKind.Utc);
        var dayEnd = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
        var daily = new DailyLimitDto(true, 300, 0, now.Date, dayEnd);
        var schedules = new[] { Schedule(DayMask(now), 20 * 60 + 30, 7 * 60, "Bedtime") };

        var unlockUntil = LockRules.NextLockCycleUtc(schedules, Utc, now, daily);
        Assert.Equal(dayEnd, unlockUntil);

        Assert.False(LockRules.Evaluate(false, unlockUntil, null, schedules, Utc,
            new DateTime(2026, 10, 19, 20, 30, 0, DateTimeKind.Utc), daily).IsLocked);
        Assert.False(LockRules.Evaluate(false, unlockUntil, null, schedules, Utc,
            new DateTime(2026, 10, 19, 23, 59, 0, DateTimeKind.Utc), daily).IsLocked);

        var relocked = LockRules.Evaluate(false, unlockUntil, null, schedules, Utc, dayEnd, daily);
        Assert.True(relocked.IsLocked);
        Assert.Equal("schedule", relocked.Reason);

        var nextDay = LockRules.Evaluate(false, unlockUntil, null, schedules, Utc, dayEnd.AddHours(8), daily);
        Assert.False(nextDay.IsLocked);
    }

    [Fact]
    public void Unlock_while_locked_ends_at_the_active_window_end()
    {
        var now = new DateTime(2026, 10, 19, 23, 0, 0, DateTimeKind.Utc);
        var schedules = new[] { Schedule(DayMask(now), 22 * 60, 6 * 60, "Bedtime") };
        var end = LockRules.NextLockCycleUtc(schedules, Utc, now);
        Assert.Equal(new DateTime(2026, 10, 20, 6, 0, 0, DateTimeKind.Utc), end);
        Assert.False(LockRules.Evaluate(false, end, null, schedules, Utc, now).IsLocked);
    }

    [Fact]
    public void Unlock_with_nothing_scheduled_ends_at_midnight()
    {
        var now = new DateTime(2026, 10, 19, 21, 0, 0, DateTimeKind.Utc);
        var end = LockRules.NextLockCycleUtc([], Utc, now);
        Assert.Equal(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), end);
        Assert.Equal("override",
            LockRules.Evaluate(false, end, null, [], Utc, now).Reason);
    }

    [Fact]
    public void Upcoming_schedule_window_end_is_used_when_no_daily_limit_is_set()
    {
        var now = new DateTime(2026, 10, 19, 20, 25, 0, DateTimeKind.Utc);
        var schedules = new[] { Schedule(DayMask(now), 20 * 60 + 30, 22 * 60, "Bedtime") };
        var end = LockRules.NextLockCycleUtc(schedules, Utc, now);
        Assert.Equal(new DateTime(2026, 10, 19, 22, 0, 0, DateTimeKind.Utc), end);
        Assert.False(LockRules.Evaluate(false, end, null, schedules, Utc,
            new DateTime(2026, 10, 19, 20, 30, 0, DateTimeKind.Utc)).IsLocked);
    }

    [Fact]
    public void Status_message_detects_unlock_bonus_and_lock_time_changes()
    {
        var now = new DateTime(2026, 10, 19, 20, 0, 0, DateTimeKind.Utc);
        var locked = State(now, true, "schedule");
        var unlocked = State(now, false, "override", timeUntil: 4 * 3600, until: now.AddHours(4),
            overrideKind: LockOverrideKind.Unlock);

        var change = LockTimerStatus.Detect(locked, unlocked, now);
        Assert.Equal(LockTimerChangeKind.Unlocked, change.Kind);
        Assert.Equal(now.AddHours(4), change.AtUtc);

        var beforeBonus = State(now, false, "unlocked", timeUntil: 3600, nextLock: now.AddHours(1));
        var afterBonus = beforeBonus with
        {
            Reason = "bonus",
            BonusUntilUtc = now.AddMinutes(30),
            BonusMinutes = 30,
            ActiveOverride = LockOverrideKind.Bonus,
            ActiveOverrideEndUtc = now.AddMinutes(30),
            TimeUntilLockSeconds = 30 * 60,
            TimeUntilLockKind = LockEngineTimeKind.Schedule,
            NextLockAtUtc = now.AddMinutes(30)
        };
        var bonus = LockTimerStatus.Detect(beforeBonus, afterBonus, now);
        Assert.Equal(LockTimerChangeKind.BonusAdded, bonus.Kind);
        Assert.Equal(30, bonus.Minutes);
        Assert.False(bonus.MakesLockEarlier);

        var later = State(now, false, "unlocked", timeUntil: 3 * 3600, nextLock: now.AddHours(3));
        var earlier = later with { TimeUntilLockSeconds = 3600, NextLockAtUtc = now.AddHours(1) };
        var changed = LockTimerStatus.Detect(later, earlier, now);
        Assert.Equal(LockTimerChangeKind.LockTimeChanged, changed.Kind);
        Assert.True(changed.MakesLockEarlier);

        var noLock = State(now, false, "unlocked");
        var removed = LockTimerStatus.Detect(later, noLock, now);
        Assert.Equal(LockTimerChangeKind.NoLockScheduled, removed.Kind);
        Assert.True(removed.HideAfter);
    }

    [Fact]
    public void Status_message_is_silent_for_a_baseline_natural_unlock_and_warning_changes()
    {
        var now = new DateTime(2026, 10, 19, 20, 0, 0, DateTimeKind.Utc);
        var state = State(now, false, "unlocked", timeUntil: 3600, nextLock: now.AddHours(1));
        Assert.Equal(LockTimerChangeKind.None, LockTimerStatus.Detect(null, state, now).Kind);
        Assert.Equal(LockTimerChangeKind.None,
            LockTimerStatus.Detect(State(now, true, "schedule"), state, now).Kind);
        Assert.Equal(LockTimerChangeKind.None, LockTimerStatus.Detect(state, state, now).Kind);
    }

    [Fact]
    public void Pill_has_message_morph_hidden_timer_and_tooltip_wiring()
    {
        var root = FindRoot();
        var pill = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "CountdownForm.cs"));
        Assert.Contains("ShowStatus", pill, StringComparison.Ordinal);
        Assert.Contains("ShowingMessage", pill, StringComparison.Ordinal);
        Assert.Contains("MessageHoldSeconds", pill, StringComparison.Ordinal);
        Assert.Contains("MessageFadeSeconds", pill, StringComparison.Ordinal);
        Assert.Contains("MorphSeconds", pill, StringComparison.Ordinal);
        Assert.Contains("Client.Pill.Tooltip", pill, StringComparison.Ordinal);
        Assert.Contains("phase != LockTimerPhase.Normal", pill, StringComparison.Ordinal);

        var helper = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "HelperContext.cs"));
        Assert.Contains("LockTimerStatus.Detect", helper, StringComparison.Ordinal);
        Assert.Contains("_countdown.ShowStatus", helper, StringComparison.Ordinal);
        Assert.Contains("_countdown.ShowingMessage", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowNotification(", helper, StringComparison.Ordinal);
        Assert.Contains("if (minutes == 15)", helper, StringComparison.Ordinal);

        var preview = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "TimerPreview.cs"));
        Assert.Contains("message", preview, StringComparison.Ordinal);

        var art = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "PillArt.cs"));
        Assert.Contains("FaceClip", art, StringComparison.Ordinal);
        Assert.Contains("CombineMode.Intersect", art, StringComparison.Ordinal);
    }

    [Fact]
    public void New_pill_strings_and_timer_labels_exist_in_both_languages()
    {
        var root = FindRoot();
        var en = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "en.json"));
        var es = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "es-MX.json"));
        foreach (var key in new[]
                 {
                     "Client.Pill.Tooltip", "Client.Pill.Msg.Unlocked", "Client.Pill.Msg.Bonus",
                     "Client.Pill.Msg.LockAt", "Client.Pill.Msg.NoLock"
                 })
        {
            Assert.Contains($"\"{key}\"", en, StringComparison.Ordinal);
            Assert.Contains($"\"{key}\"", es, StringComparison.Ordinal);
        }

        Assert.Contains("\"Client.Kid.Timer.HoursExample\": \"32h 45m\"", en, StringComparison.Ordinal);
        Assert.Contains("\"Client.Kid.Timer.HoursExample\": \"32 h 45 min\"", es, StringComparison.Ordinal);
        Assert.Contains("pop up 5 minutes before", en, StringComparison.Ordinal);
        Assert.Contains("5 minutos antes", es, StringComparison.Ordinal);
    }

    private static int DayMask(DateTime utc) => 1 << (int)utc.DayOfWeek;

    private static ScheduleDto Schedule(int days, int start, int end, string name = "Bedtime") =>
        new(Guid.NewGuid(), name, days, start, end, true);

    private static RuntimeLockStateDto State(
        DateTime now, bool locked, string reason, DateTime? ends = null, DateTime? nextLock = null,
        int? timeUntil = null, DateTime? until = null, LockOverrideKind overrideKind = LockOverrideKind.None) =>
        new(
            Guid.NewGuid(), "Emma", "Ziur", locked, reason, null, ends, nextLock,
            null, null, now,
            TimeUntilLockSeconds: timeUntil,
            NextLockAtUtc: nextLock,
            ActiveOverride: overrideKind,
            ActiveOverrideEndUtc: until,
            HasPill: timeUntil is > 0,
            TimeUntilLockKind: timeUntil is null ? LockEngineTimeKind.None : LockEngineTimeKind.Schedule);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
