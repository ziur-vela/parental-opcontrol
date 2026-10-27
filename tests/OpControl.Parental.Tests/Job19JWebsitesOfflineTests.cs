// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job19JWebsitesOfflineTests
{
    private static ParentalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("job19j-" + Guid.NewGuid())
            .Options);

    private static TimeZoneInfo UtcMinus7() =>
        TimeZoneInfo.CreateCustomTimeZone("TestUTC-7", TimeSpan.FromHours(-7), "UTC-7", "UTC-7");

    [Fact]
    public void Local_day_bounds_use_the_family_zone_around_local_midnight_and_utc_midnight()
    {
        var tz = UtcMinus7();
        var (start, end) = UsageService.LocalDayBounds(tz, new DateOnly(2026, 10, 26));

        Assert.Equal(new DateTime(2026, 10, 26, 7, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 10, 27, 7, 0, 0, DateTimeKind.Utc), end);

        // 18:48 local on the 26th is 01:48 UTC on the 27th and belongs to the 26th.
        var evening = new DateTime(2026, 10, 27, 1, 48, 0, DateTimeKind.Utc);
        Assert.True(evening >= start && evening < end);

        // 17:00 local on the 26th is 00:00 UTC on the 27th: still the 26th.
        var fivePm = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(fivePm >= start && fivePm < end);

        // 23:59 local on the 26th is 06:59 UTC on the 27th.
        var lastMinute = new DateTime(2026, 10, 27, 6, 59, 0, DateTimeKind.Utc);
        Assert.True(lastMinute >= start && lastMinute < end);

        // 00:00 local on the 27th is 07:00 UTC on the 27th: the next day.
        var nextMidnight = new DateTime(2026, 10, 27, 7, 0, 0, DateTimeKind.Utc);
        Assert.False(nextMidnight >= start && nextMidnight < end);
    }

    [Fact]
    public void Today_at_1857_local_utc_minus_7_is_still_the_26th()
    {
        var tz = UtcMinus7();
        var nowUtc = new DateTime(2026, 10, 27, 1, 57, 0, DateTimeKind.Utc); // 18:57 local on 26th
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        Assert.Equal(new DateTime(2026, 10, 26), local.Date);

        var (start, end) = UsageService.LocalDayBounds(tz, nowUtc);
        Assert.Equal(new DateTime(2026, 10, 26, 7, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 10, 27, 7, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void Older_request_is_ignored_after_a_newer_one_starts()
    {
        var gate = new LatestRequestGate();
        var older = gate.Next();
        var newer = gate.Next();

        Assert.False(gate.IsCurrent(older));
        Assert.True(gate.IsCurrent(newer));
    }

    [Fact]
    public void Subdomains_are_grouped_under_the_registrable_domain_with_their_own_block()
    {
        var rule = new WebsiteRule
        {
            FamilyId = Guid.NewGuid(),
            ChildId = Guid.NewGuid(),
            Domain = "search.brave.com",
            IsBlocked = true,
            Mode = RuleMode.Always
        };
        var now = new DateTime(2026, 10, 27, 2, 0, 0, DateTimeKind.Utc);
        var points = new List<WebsiteVisitPoint>
        {
            new("brave.com", "search.brave.com", now.AddMinutes(-30), false, 0, null),
            new("brave.com", "search.brave.com", now.AddMinutes(-20), false, 0, null),
            new("brave.com", "talk.brave.com", now.AddMinutes(-10), false, 0, null),
            new("brave.com", "brave.com", now.AddMinutes(-5), false, 0, null)
        };

        var rows = WebsiteDayView.Build(points, [rule], new HashSet<string>(), new Dictionary<string, DateTime>(), now);

        var main = Assert.Single(rows);
        Assert.Equal("brave.com", main.Domain);
        Assert.Equal(4, main.Count);
        Assert.False(main.IsBlocked);
        Assert.Equal(2, main.Subdomains.Count);

        var search = main.Subdomains.Single(s => s.Host == "search.brave.com");
        Assert.Equal(2, search.Count);
        Assert.True(search.IsBlocked);
        Assert.True(search.HasOwnRule);
        Assert.Equal(rule.Id, search.RuleId);

        var talk = main.Subdomains.Single(s => s.Host == "talk.brave.com");
        Assert.False(talk.IsBlocked);
        Assert.False(talk.HasOwnRule);
    }

    [Fact]
    public void Subdomain_allow_does_not_change_the_main_or_sibling_rows()
    {
        var rules = new List<WebsiteRule>
        {
            new()
            {
                FamilyId = Guid.NewGuid(),
                ChildId = Guid.NewGuid(),
                Domain = "talk.brave.com",
                IsBlocked = false,
                Mode = RuleMode.Always
            }
        };
        var now = DateTime.UtcNow;
        var rows = WebsiteDayView.Build(
            [
                new("brave.com", "brave.com", now, false, 0, null),
                new("brave.com", "talk.brave.com", now, false, 0, null)
            ],
            rules, new HashSet<string>(), new Dictionary<string, DateTime>(), now.AddDays(-7));

        var main = Assert.Single(rows);
        Assert.True(main.Subdomains.Single().IsAllowed);
        Assert.False(main.Subdomains.Single().IsBlocked);
    }

    [Fact]
    public async Task Exact_host_rule_blocks_only_that_subdomain()
    {
        await using var db = NewDb();
        var family = new Family { Name = "Sites" };
        var child = new Child { FamilyId = family.Id, Name = "Ana" };
        db.Families.Add(family);
        db.Children.Add(child);
        await db.SaveChangesAsync();
        var rules = new RuleService(db, null!);

        var rule = await rules.UpsertWebsiteRuleAsync(family.Id, child.Id, null, "search.brave.com",
            blocked: true, RuleMode.Always, null, exactHost: true);
        var www = await rules.UpsertWebsiteRuleAsync(family.Id, child.Id, null, "www.example.com",
            blocked: true, RuleMode.Always, null, exactHost: true);

        Assert.Equal("search.brave.com", rule.Domain);
        Assert.Equal("www.example.com", www.Domain);
        Assert.Equal("example.com", RuleService.NormalizeDomain("www.example.com"));
        Assert.Equal("www.example.com", RuleService.NormalizeHost("www.example.com"));
    }

    [Fact]
    public async Task Visit_sync_keeps_both_the_full_host_and_the_registrable_domain()
    {
        await using var db = NewDb();
        var family = new Family { Name = "Sites" };
        var child = new Child { FamilyId = family.Id, Name = "Ana" };
        var device = new Device { FamilyId = family.Id, ChildId = child.Id, Name = "X1" };
        db.Families.Add(family);
        db.Children.Add(child);
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        var rules = new RuleService(db, null!);

        await rules.SyncVisitsAsync(device, new VisitBatchRequest(
        [
            new VisitEventDto("brave.com", DateTime.UtcNow.AddMinutes(-3), false, Host: "search.brave.com"),
            new VisitEventDto("brave.com", DateTime.UtcNow.AddMinutes(-2), false, Host: "talk.brave.com")
        ]));

        var rows = await db.VisitRecords.OrderBy(v => v.VisitedUtc).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("brave.com", r.Domain));
        Assert.Equal(new[] { "search.brave.com", "talk.brave.com" }, rows.Select(r => r.Host));
    }

    [Fact]
    public void Offline_alert_is_suppressed_for_a_windows_device_with_the_setting_off()
    {
        var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var threshold = TimeSpan.FromMinutes(120);
        var windowsOff = new Device
        {
            Platform = DevicePlatform.Windows,
            NotifyOffline = false,
            LastSeenUtc = now.AddHours(-5)
        };
        var windowsOn = new Device
        {
            Platform = DevicePlatform.Windows,
            NotifyOffline = true,
            LastSeenUtc = now.AddHours(-5)
        };
        var phone = new Device
        {
            Platform = DevicePlatform.Android,
            NotifyOffline = true,
            LastSeenUtc = now.AddHours(-5)
        };

        Assert.False(UsageLimitService.ShouldNotifyOffline(windowsOff, now, threshold));
        Assert.True(UsageLimitService.ShouldNotifyOffline(windowsOn, now, threshold));
        Assert.True(UsageLimitService.ShouldNotifyOffline(phone, now, threshold));
    }

    [Fact]
    public void Websites_page_uses_one_family_zone_a_stale_guard_and_a_30s_quiet_refresh()
    {
        var page = File.ReadAllText(Path.Combine(FindRoot(), "src", "OpControl.Parental.Server", "Components", "Pages", "Websites.razor"));
        Assert.Contains("UsageService.LocalDayBounds(tz, date)", page, StringComparison.Ordinal);
        Assert.Contains("_refreshGate.IsCurrent(token)", page, StringComparison.Ordinal);
        Assert.Contains("PeriodicTimer(TimeSpan.FromSeconds(30))", page, StringComparison.Ordinal);
        Assert.Contains("RefreshAsync(quiet: true)", page, StringComparison.Ordinal);
        Assert.Contains("Common.Refresh", page, StringComparison.Ordinal);
        Assert.Contains("WebsiteDayView.Build", page, StringComparison.Ordinal);
        Assert.Contains("ocpVisitsScrollSet", page, StringComparison.Ordinal);
    }

    [Fact]
    public void New_install_defaults_offline_alerts_off_for_computers_and_on_for_phones()
    {
        var windows = new Device { Platform = DevicePlatform.Windows, NotifyOffline = false };
        var android = new Device { Platform = DevicePlatform.Android, NotifyOffline = true };
        Assert.False(UsageLimitService.ShouldNotifyOffline(windows, DateTime.UtcNow, TimeSpan.Zero));
        Assert.False(UsageLimitService.ShouldNotifyOffline(android, DateTime.UtcNow, TimeSpan.FromMinutes(120)));

        var root = FindRoot();
        var enrollment = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Services", "EnrollmentService.cs"));
        Assert.Contains("NotifyOffline = request.Platform == DevicePlatform.Android", enrollment, StringComparison.Ordinal);

        // Tamper alerts are a separate path: they never consult NotifyOffline.
        var program = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Program.cs"));
        var tamperStart = program.IndexOf("api/device/tamper", StringComparison.Ordinal);
        Assert.True(tamperStart >= 0);
        Assert.DoesNotContain("NotifyOffline", program.Substring(tamperStart, Math.Min(2500, program.Length - tamperStart)), StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
