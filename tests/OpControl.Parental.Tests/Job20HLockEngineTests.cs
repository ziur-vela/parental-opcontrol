// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia.Headless;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Luna;
using OpControl.Parental.Localization;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job20HLockEngineTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void Rule_1_schedule_first_when_it_is_sooner_than_the_limit()
    {
        var now = At(2026, 10, 19, 16, 33);
        var schedule = ScheduleOn(now, Utc, 17 * 60, 19 * 60);
        var result = Evaluate(now, [schedule], Daily(now, Utc, 120, 92 * 60));

        Assert.False(result.IsLocked);
        Assert.Equal(LockEngineTimeKind.Schedule, result.TimeUntilLockKind);
        Assert.Equal(27 * 60, result.TimeUntilLockSeconds);
        Assert.Equal(28 * 60, result.LimitRemainingSeconds);
    }

    [Fact]
    public void Rule_2_daily_limit_first_when_it_is_sooner_than_the_schedule()
    {
        var now = At(2026, 10, 19, 16, 33);
        var schedule = ScheduleOn(now, Utc, 17 * 60, 19 * 60);
        var result = Evaluate(now, [schedule], Daily(now, Utc, 120, 100 * 60));

        Assert.False(result.IsLocked);
        Assert.Equal(LockEngineTimeKind.DailyLimit, result.TimeUntilLockKind);
        Assert.Equal(20 * 60, result.TimeUntilLockSeconds);
        Assert.True(result.TimeUntilLockIsUsage);
    }

    [Fact]
    public void Rule_3_bonus_is_free_then_the_schedule_locks_and_the_limit_is_kept()
    {
        var now = At(2026, 10, 19, 17, 0);
        var schedule = ScheduleOn(now, Utc, 17 * 60, 19 * 60);
        var daily = Daily(now, Utc, 120, 92 * 60);
        var during = Evaluate(now, [schedule], daily, bonusStart: now, bonusEnd: now.AddMinutes(30));

        Assert.False(during.IsLocked);
        Assert.Equal(LockOverrideKind.Bonus, during.ActiveOverride);
        Assert.Equal(30 * 60, during.TimeUntilLockSeconds);
        Assert.Equal(LockEngineTimeKind.Schedule, during.TimeUntilLockKind);

        var after = Evaluate(now.AddMinutes(30), [schedule], daily, bonusStart: now, bonusEnd: now.AddMinutes(30));
        Assert.True(after.IsLocked);
        Assert.Equal(LockEngineReason.Schedule, after.Reason);
        Assert.Equal(28 * 60, after.LimitRemainingSeconds);
    }

    [Fact]
    public void Rule_4_after_a_schedule_window_the_remaining_limit_can_be_used()
    {
        var schedule = ScheduleOn(At(2026, 10, 19, 17, 0), Utc, 17 * 60, 19 * 60);
        var atEnd = At(2026, 10, 19, 19, 0);
        var daily = Daily(atEnd, Utc, 120, 92 * 60);

        var next = Evaluate(atEnd, [schedule], daily);
        Assert.False(next.IsLocked);
        Assert.Equal(LockEngineTimeKind.DailyLimit, next.TimeUntilLockKind);
        Assert.Equal(28 * 60, next.TimeUntilLockSeconds);

        var outOfLimit = Evaluate(At(2026, 10, 19, 19, 28), [schedule], Daily(At(2026, 10, 19, 19, 28), Utc, 120, 120 * 60));
        Assert.True(outOfLimit.IsLocked);
        Assert.Equal(LockEngineReason.DailyLimit, outOfLimit.Reason);
    }

    [Fact]
    public void Rule_5_bonus_after_the_limit_ran_out_locks_again_at_the_bonus_end()
    {
        var now = At(2026, 10, 19, 14, 0);
        var schedule = ScheduleOn(now, Utc, 17 * 60, 19 * 60);
        var daily = Daily(now, Utc, 120, 120 * 60);

        Assert.True(Evaluate(now, [schedule], daily).IsLocked);

        var during = Evaluate(now, [schedule], daily, bonusStart: now, bonusEnd: now.AddMinutes(30));
        Assert.False(during.IsLocked);
        Assert.Equal(30 * 60, during.TimeUntilLockSeconds);
        Assert.Equal(LockEngineTimeKind.DailyLimit, during.TimeUntilLockKind);

        var after = Evaluate(now.AddMinutes(30), [schedule], daily, bonusStart: now, bonusEnd: now.AddMinutes(30));
        Assert.True(after.IsLocked);
        Assert.Equal(LockEngineReason.DailyLimit, after.Reason);
        Assert.Equal(0, after.LimitRemainingSeconds);
    }

    [Fact]
    public void Rule_6_unlock_before_an_overnight_schedule_skips_it_until_the_override_ends()
    {
        var now = At(2026, 10, 19, 20, 25);
        var schedule = ScheduleOn(now, Utc, 20 * 60 + 30, 7 * 60);
        var until = At(2026, 10, 20, 0, 0);

        var during = Evaluate(now, [schedule], null, unlockStart: now, unlockEnd: until);
        Assert.False(during.IsLocked);
        Assert.Equal(LockOverrideKind.Unlock, during.ActiveOverride);
        Assert.Equal(LockEngineTimeKind.Schedule, during.TimeUntilLockKind);
        Assert.Equal((int)(until - now).TotalSeconds, during.TimeUntilLockSeconds);

        var atScheduleStart = Evaluate(At(2026, 10, 19, 20, 30), [schedule], null, unlockStart: now, unlockEnd: until);
        Assert.False(atScheduleStart.IsLocked);

        var atMidnight = Evaluate(until, [schedule], null, unlockStart: now, unlockEnd: until);
        Assert.True(atMidnight.IsLocked);
        Assert.Equal(LockEngineReason.Schedule, atMidnight.Reason);
    }

    [Fact]
    public void Rule_7_unlock_while_daily_locked_is_free_then_locks_at_the_end()
    {
        var now = At(2026, 10, 19, 14, 0);
        var daily = Daily(now, Utc, 120, 120 * 60);
        var until = now.AddHours(1);

        var during = Evaluate(now, [], daily, unlockStart: now, unlockEnd: until);
        Assert.False(during.IsLocked);
        Assert.Equal(3600, during.TimeUntilLockSeconds);
        Assert.Equal(LockEngineTimeKind.DailyLimit, during.TimeUntilLockKind);

        var after = Evaluate(until, [], daily, unlockStart: now, unlockEnd: until);
        Assert.True(after.IsLocked);
        Assert.Equal(LockEngineReason.DailyLimit, after.Reason);
    }

    [Fact]
    public void Rule_8_a_new_bonus_extends_from_the_current_bonus_end()
    {
        var now = At(2026, 10, 19, 14, 0);
        var currentStart = now.AddMinutes(-10);
        var currentEnd = now.AddMinutes(20);
        var (start, end) = LockEngine.ExtendBonus(currentStart, currentEnd, now, 30);

        Assert.Equal(currentStart, start);
        Assert.Equal(currentEnd.AddMinutes(30), end);
    }

    [Fact]
    public void Rule_9_lock_now_cancels_bonus_and_unlock()
    {
        var now = At(2026, 10, 19, 14, 0);
        var result = Evaluate(now, [], null, bonusStart: now.AddMinutes(-5), bonusEnd: now.AddMinutes(25),
            unlockStart: now.AddMinutes(-5), unlockEnd: now.AddHours(1), manualLock: true);

        Assert.True(result.IsLocked);
        Assert.Equal(LockEngineReason.LockNow, result.Reason);
        Assert.Equal(LockOverrideKind.LockNow, result.ActiveOverride);
        Assert.Equal(0, result.TimeUntilLockSeconds);
        Assert.False(result.HasPill);
    }

    [Fact]
    public void Rule_10_overnight_schedule_and_midnight_reset()
    {
        var start = At(2026, 10, 19, 21, 0);
        var schedule = ScheduleOn(start, Utc, 21 * 60, 7 * 60);
        var beforeMidnight = Daily(start, Utc, 120, 120 * 60);
        Assert.True(Evaluate(At(2026, 10, 19, 23, 0), [schedule], beforeMidnight).IsLocked);

        var afterMidnight = At(2026, 10, 20, 0, 30);
        var freshDay = Daily(afterMidnight, Utc, 120, 0);
        var atMidnight = Evaluate(afterMidnight, [schedule], freshDay);
        Assert.True(atMidnight.IsLocked);
        Assert.Equal(LockEngineReason.Schedule, atMidnight.Reason);

        var morning = Evaluate(At(2026, 10, 20, 7, 0), [schedule], freshDay);
        Assert.False(morning.IsLocked);
        Assert.Equal(120 * 60, morning.TimeUntilLockSeconds);
        Assert.Equal(LockEngineTimeKind.DailyLimit, morning.TimeUntilLockKind);
    }

    [Fact]
    public void Rule_11_idle_time_does_not_change_the_daily_limit_countdown()
    {
        var now = At(2026, 10, 19, 14, 0);
        var daily = Daily(now, Utc, 120, 60 * 60);
        var before = Evaluate(now, [], daily);
        var afterIdle = Evaluate(now.AddMinutes(20), [], daily);

        Assert.Equal(60 * 60, before.TimeUntilLockSeconds);
        Assert.Equal(before.TimeUntilLockSeconds, afterIdle.TimeUntilLockSeconds);
        Assert.Equal(LockEngineTimeKind.DailyLimit, afterIdle.TimeUntilLockKind);
    }

    [Fact]
    public void Rule_12_offline_engine_uses_the_saved_rules_and_local_count()
    {
        var now = At(2026, 10, 19, 14, 0);
        var daily = Daily(now, Utc, 120, 90 * 60);
        var result = Evaluate(now, [], daily);
        Assert.Equal(30 * 60, result.TimeUntilLockSeconds);

        var offlineAfterUse = Evaluate(now, [], Daily(now, Utc, 120, 120 * 60));
        Assert.True(offlineAfterUse.IsLocked);
        Assert.Equal(LockEngineReason.DailyLimit, offlineAfterUse.Reason);
    }

    [Fact]
    public void Rule_13_phoenix_and_a_dst_zone_use_the_device_day()
    {
        var phoenix = Zone("America/Phoenix", "US Mountain Standard Time");
        var pacific = Zone("America/Los_Angeles", "Pacific Standard Time");

        foreach (var zone in new[] { phoenix, pacific })
        {
            var now = AtLocal(zone, 2026, 7, 15, 18, 0);
            var schedule = ScheduleOn(now, zone, 19 * 60, 20 * 60);
            var daily = Daily(now, zone, 120, 60 * 60);
            var result = Evaluate(now, [schedule], daily, zone: zone);
            Assert.Equal(60 * 60, result.TimeUntilLockSeconds);
            Assert.Equal(LockEngineTimeKind.Schedule, result.TimeUntilLockKind);
        }
    }

    [Fact]
    public void Rule_14_no_rules_never_locks_and_has_no_pill()
    {
        var now = At(2026, 10, 19, 14, 0);
        var result = Evaluate(now, [], null);

        Assert.False(result.IsLocked);
        Assert.Equal(LockEngineReason.None, result.Reason);
        Assert.Null(result.TimeUntilLockSeconds);
        Assert.False(result.HasPill);
        Assert.Null(result.NextScheduleLockUtc);
    }

    [Fact]
    public async Task Server_state_shows_the_daily_limit_when_it_comes_first()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;
        var family = new Family { Name = "F" };
        var child = new Child { FamilyId = family.Id, Name = "C" };
        var device = new Device { FamilyId = family.Id, ChildId = child.Id, Name = "D", TimeZoneId = "UTC" };
        db.Families.Add(family);
        db.Children.Add(child);
        db.Devices.Add(device);
        db.LockStates.Add(new LockState { FamilyId = family.Id, ChildId = child.Id });
        db.ScreenTimeLimits.Add(new ScreenTimeLimit
        {
            FamilyId = family.Id, ChildId = child.Id, Enabled = true,
            MondayMinutes = 120, TuesdayMinutes = 120, WednesdayMinutes = 120, ThursdayMinutes = 120,
            FridayMinutes = 120, SaturdayMinutes = 120, SundayMinutes = 120
        });
        db.Schedules.Add(new Schedule
        {
            FamilyId = family.Id, ChildId = child.Id, Name = "Late", IsEnabled = true,
            DaysMask = 1 << (int)now.DayOfWeek,
            StartMinutes = (now.Hour * 60 + now.Minute + 120) % 1440,
            EndMinutes = (now.Hour * 60 + now.Minute + 180) % 1440
        });
        db.UsageIntervals.Add(new UsageInterval
        {
            FamilyId = family.Id, ChildId = child.Id, DeviceId = device.Id,
            AppIdentity = "app:game", AppDisplayName = "Game",
            StartUtc = now.Date, DurationSeconds = 90 * 60, Active = true
        });
        await db.SaveChangesAsync();

        var service = NewLocks(db);
        var state = await service.GetStateAsync(family.Id, child.Id, "UTC");
        Assert.Equal(30 * 60, state.LimitRemainingSeconds);
        Assert.Equal(LockEngineTimeKind.DailyLimit, state.TimeUntilLockKind);
        Assert.False(state.IsLocked);
        Assert.True(state.HasPill);
    }

    [Fact]
    public async Task Usage_sync_stores_the_override_exclusion_flag()
    {
        await using var db = NewDb();
        var family = new Family { Name = "F" };
        var child = new Child { FamilyId = family.Id, Name = "C" };
        var device = new Device { FamilyId = family.Id, ChildId = child.Id, Name = "D", TimeZoneId = "UTC" };
        db.Families.Add(family); db.Children.Add(child); db.Devices.Add(device);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var usage = new UsageService(db, NewAlerts(db));
        await usage.SyncUsageAsync(device, new UsageBatchRequest([
            new UsageIntervalDto(Guid.NewGuid(), "app:a", "A", now.Date, 600, true, true),
            new UsageIntervalDto(Guid.NewGuid(), "app:b", "B", now.Date, 300, true, false)
        ]));
        Assert.Equal(600, await usage.UsedTodaySecondsAsync(family.Id, child.Id, Utc, now));
        Assert.Equal(2, await db.UsageIntervals.CountAsync());
        Assert.Equal(1, await db.UsageIntervals.CountAsync(u => !u.CountsTowardLimit));
    }

    [Fact]
    public async Task Server_bonus_extends_and_lock_now_cancels_both_overrides()
    {
        await using var db = NewDb();
        var family = new Family { Name = "F" };
        var child = new Child { FamilyId = family.Id, Name = "C" };
        db.Families.Add(family); db.Children.Add(child);
        db.LockStates.Add(new LockState { FamilyId = family.Id, ChildId = child.Id });
        await db.SaveChangesAsync();

        var service = NewLocks(db);
        var first = await service.ApplyAsync(family.Id, child.Id, null, LockAction.Bonus, 15);
        var firstEnd = first.BonusUntilUtc;
        var extended = await service.ApplyAsync(family.Id, child.Id, null, LockAction.Bonus, 30);
        Assert.Equal(firstEnd!.Value.AddMinutes(30), extended.BonusUntilUtc);
        Assert.Equal(LockOverrideKind.Bonus, extended.ActiveOverride);

        var locked = await service.ApplyAsync(family.Id, child.Id, null, LockAction.Lock);
        Assert.True(locked.IsLocked);
        Assert.Equal(LockOverrideKind.LockNow, locked.ActiveOverride);
        Assert.Null(locked.BonusUntilUtc);
        Assert.Null(locked.UnlockUntilUtc);
    }

    [Fact]
    public void Dashboard_kid_client_and_pill_surfaces_use_the_engine_output()
    {
        var root = FindRoot();
        var home = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Pages", "Home.razor"));
        Assert.Contains("LimitRemainingSeconds", home, StringComparison.Ordinal);
        Assert.Contains("NextScheduleLockUtc", home, StringComparison.Ordinal);
        Assert.DoesNotContain("LockRules.ActiveSchedule", home, StringComparison.Ordinal);

        var kid = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        Assert.Contains("LimitRemainingSeconds", kid, StringComparison.Ordinal);
        Assert.Contains("TimeUntilLockSeconds", kid, StringComparison.Ordinal);
        Assert.DoesNotContain("DailyLimitRemainingSeconds is", kid, StringComparison.Ordinal);

        var runtime = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows", "LockRuntime.cs"));
        Assert.Contains("LockEngine.Evaluate", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("LockRules.Evaluate", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("LockRules.NextLockUtc", runtime, StringComparison.Ordinal);

        var helper = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "HelperContext.cs"));
        Assert.Contains("state.TimeUntilLockSeconds", helper, StringComparison.Ordinal);
        Assert.Contains("state.HasPill", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("state.NextLockUtc", helper, StringComparison.Ordinal);

        var sync = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows", "DataSyncService.cs"));
        Assert.Contains("CountsTowardLimit", sync, StringComparison.Ordinal);
    }

    [Fact]
    public void Kid_home_shows_the_same_engine_numbers_as_the_dashboard()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new MyOpControlWindow(new KidData(), new KidWindowOptions(Tab: "home"));
            window.Show();
            UiHarness.Pump(250);
            Assert.Equal("20m", window.HomeTimeLeftForTest);
            Assert.Equal("20m", window.HomeNextLockForTest);
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static LockEngineResult Evaluate(
        DateTime now, IReadOnlyList<ScheduleDto> schedules, DailyLimitDto? daily,
        DateTime? bonusStart = null, DateTime? bonusEnd = null,
        DateTime? unlockStart = null, DateTime? unlockEnd = null,
        bool manualLock = false, TimeZoneInfo? zone = null) =>
        LockEngine.Evaluate(new LockEngineInput(
            manualLock, unlockStart, unlockEnd, bonusStart, bonusEnd,
            bonusEnd is { } be && bonusStart is { } bs ? (int)(be - bs).TotalMinutes : null,
            schedules, daily, now, zone ?? Utc));

    private static DailyLimitDto Daily(DateTime nowUtc, TimeZoneInfo tz, int minutes, int usedSeconds)
    {
        var (start, end) = UsageService.LocalDayBounds(tz, nowUtc);
        return new DailyLimitDto(true, minutes, usedSeconds, start, end);
    }

    private static ScheduleDto ScheduleOn(DateTime nowUtc, TimeZoneInfo tz, int start, int end, string name = "Bedtime")
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        return new ScheduleDto(Guid.NewGuid(), name, 1 << (int)local.DayOfWeek, start, end, true);
    }

    private static DateTime At(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static DateTime AtLocal(TimeZoneInfo tz, int year, int month, int day, int hour, int minute) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), tz);

    private static TimeZoneInfo Zone(string iana, string windows) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(iana, out var zone)
            ? zone
            : TimeZoneInfo.FindSystemTimeZoneById(windows);

    private static ParentalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("job20h-" + Guid.NewGuid())
            .Options);

    private static AlertService NewAlerts(ParentalDbContext db)
    {
        var protection = new EphemeralDataProtectionProvider();
        var renderer = new EmailRenderer(
            Microsoft.Extensions.Options.Options.Create(new EmailOptions { LogoUrl = "https://parental.opcontrol.net/brand/email-lockup.png" }),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var sender = new EmailSender(db, protection, renderer,
            Microsoft.Extensions.Options.Options.Create(new EmailOptions()), NullLogger<EmailSender>.Instance);
        return new AlertService(db, new DashboardEventBus(), protection, sender, renderer,
            NullLogger<AlertService>.Instance);
    }

    private static LockService NewLocks(ParentalDbContext db) =>
        new(db, new TestHubContext(), new DashboardEventBus(),
            new RuleService(db, NewAlerts(db)), new UsageService(db, NewAlerts(db)), NewAlerts(db),
            NullLogger<LockService>.Instance);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class KidData : IKidDataSource
    {
        private readonly ChildSummaryDto _summary;
        private readonly RuntimeLockStateDto _state;

        public KidData()
        {
            var child = Guid.NewGuid();
            var now = DateTime.UtcNow;
            _summary = new ChildSummaryDto(child, "Ana", "X1", now, true, 360, 300 * 60, 30000,
                Weekdays: [new SummaryWeekdayDto(1, 360)],
                Schedules: [], Categories: [], Websites: [], Apps: [], AppsToday: [], AppsWeek: [],
                Visits: [], History: [], RecentMessages: [], UnreadMessages: 0, Theme: "night");
            _state = new RuntimeLockStateDto(child, "Ana", "Dad", false, "", null, null, null, null, null, now,
                LimitRemainingSeconds: 1200, LimitMinutes: 360,
                TimeUntilLockSeconds: 1200, TimeUntilLockKind: LockEngineTimeKind.DailyLimit,
                TimeUntilLockIsUsage: true, HasPill: true);
        }

        public Task<KidSnapshot> LoadAsync(CancellationToken ct) =>
            Task.FromResult(new KidSnapshot(_summary, _state, [], true));

        public Task<PinPipeResponse> SendMessageAsync(string text, CancellationToken ct) =>
            Task.FromResult(new PinPipeResponse(true));

        public Task MarkSeenAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public Task MarkAcknowledgedAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public Task SetThemeAsync(string theme, CancellationToken ct) => Task.CompletedTask;

        public Task<AboutInfoDto> GetAboutAsync(CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class TestHubContext : Microsoft.AspNetCore.SignalR.IHubContext<OpControl.Parental.Server.Hubs.ParentalHub>
    {
        public Microsoft.AspNetCore.SignalR.IHubClients Clients { get; } = new TestHubClients();
        public Microsoft.AspNetCore.SignalR.IGroupManager Groups { get; } = new TestGroupManager();
    }

    private sealed class TestHubClients : Microsoft.AspNetCore.SignalR.IHubClients
    {
        private static readonly Microsoft.AspNetCore.SignalR.IClientProxy Proxy = new TestClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy All => Proxy;
        public Microsoft.AspNetCore.SignalR.IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public Microsoft.AspNetCore.SignalR.IClientProxy Client(string connectionId) => Proxy;
        public Microsoft.AspNetCore.SignalR.IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public Microsoft.AspNetCore.SignalR.IClientProxy Group(string groupName) => Proxy;
        public Microsoft.AspNetCore.SignalR.IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public Microsoft.AspNetCore.SignalR.IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public Microsoft.AspNetCore.SignalR.IClientProxy User(string userId) => Proxy;
        public Microsoft.AspNetCore.SignalR.IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class TestClientProxy : Microsoft.AspNetCore.SignalR.IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestGroupManager : Microsoft.AspNetCore.SignalR.IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
