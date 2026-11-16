// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public sealed class Job25Tests
{
    private sealed class Harness : IAsyncDisposable
    {
        public required ParentalDbContext Db { get; init; }
        public required Family Family { get; init; }
        public required Child Child { get; init; }
        public required Device Device { get; init; }
        public required RuleService Rules { get; init; }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private static async Task<Harness> NewHarnessAsync(string timeZoneId = "UTC")
    {
        var db = new ParentalDbContext(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("job25-" + Guid.NewGuid()).Options);
        var family = new Family { Name = "Ruiz" };
        var child = new Child { FamilyId = family.Id, Name = "Emma" };
        var device = new Device
        {
            FamilyId = family.Id,
            ChildId = child.Id,
            Name = "ZIUR-X1-LAPTOP",
            TimeZoneId = timeZoneId
        };
        db.Families.Add(family);
        db.Children.Add(child);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var protection = new EphemeralDataProtectionProvider();
        var emailOptions = Options.Create(new EmailOptions());
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var renderer = new EmailRenderer(emailOptions, configuration);
        var email = new EmailSender(db, protection, renderer, emailOptions, NullLogger<EmailSender>.Instance);
        var alerts = new AlertService(db, new DashboardEventBus(), protection, email, renderer,
            NullLogger<AlertService>.Instance);
        var rules = new RuleService(db, alerts, NullLogger<RuleService>.Instance);
        return new Harness { Db = db, Family = family, Child = child, Device = device, Rules = rules };
    }

    private static VisitBatchRequest Blocked(string host, string domain, int lookups = 5, DateTime? at = null) =>
        new([new VisitEventDto(domain, at ?? DateTime.UtcNow, true, "rule:always", lookups, host)]);

    private static async Task<int> AlertCountAsync(Harness h, Guid? childId = null) =>
        await h.Db.Alerts.CountAsync(a => a.FamilyId == h.Family.Id
            && a.ChildId == (childId ?? h.Child.Id) && a.Type == "repeated-blocked-site");

    [Theory]
    [InlineData("config.edge.skype.com", true)]
    [InlineData("sub.config.edge.skype.com", true)]
    [InlineData("anything.edge.skype.com", true)]
    [InlineData("v10.events.data.microsoft.com", true)]
    [InlineData("events.data.microsoft.com", true)]
    [InlineData("foo.msedge.net", true)]
    [InlineData("ctldl.windowsupdate.com", true)]
    [InlineData("www.youtube.com", false)]
    [InlineData("skype.com", false)]
    [InlineData("notskype.com", false)]
    [InlineData("microsoft.com", false)]
    public void Background_hosts_match_exact_and_wildcard(string host, bool expected) =>
        Assert.Equal(expected, BackgroundHosts.IsBackground(host));

    [Fact]
    public async Task Background_hits_are_logged_but_do_not_alert_or_count()
    {
        await using var h = await NewHarnessAsync();
        await h.Rules.SyncVisitsAsync(h.Device, Blocked("config.edge.skype.com", "skype.com"));

        Assert.Equal(0, await AlertCountAsync(h));
        var row = await h.Db.VisitRecords.SingleAsync(v => v.ChildId == h.Child.Id);
        Assert.True(row.Background);
        Assert.True(row.Blocked);
        Assert.Equal(5, row.BlockedLookups);
    }

    [Fact]
    public async Task Foreground_hits_still_alert()
    {
        await using var h = await NewHarnessAsync();
        await h.Rules.SyncVisitsAsync(h.Device, Blocked("www.roblox.com", "roblox.com"));

        Assert.Equal(1, await AlertCountAsync(h));
        var alert = await h.Db.Alerts.SingleAsync(a => a.Type == "repeated-blocked-site");
        Assert.Contains("roblox.com", alert.ArgsJson);
    }

    [Fact]
    public async Task Repeated_block_alerts_fire_only_once_per_site_per_local_day()
    {
        await using var h = await NewHarnessAsync();
        await h.Rules.SyncVisitsAsync(h.Device, Blocked("www.roblox.com", "roblox.com"));
        await h.Rules.SyncVisitsAsync(h.Device, Blocked("www.roblox.com", "roblox.com"));

        Assert.Equal(1, await AlertCountAsync(h));

        // Move the existing alert to well before today's local start and past the
        // cooldown: the next day may alert again.
        var alert = await h.Db.Alerts.SingleAsync(a => a.Type == "repeated-blocked-site");
        alert.CreatedUtc = DateTime.UtcNow.AddHours(-30);
        h.Db.VisitRecords.RemoveRange(await h.Db.VisitRecords.ToListAsync());
        await h.Db.SaveChangesAsync();

        await h.Rules.SyncVisitsAsync(h.Device, Blocked("www.roblox.com", "roblox.com"));
        Assert.Equal(2, await AlertCountAsync(h));
    }

    [Fact]
    public async Task Device_time_zone_defines_the_local_day()
    {
        await using var h = await NewHarnessAsync("America/Phoenix");
        await h.Rules.SyncVisitsAsync(h.Device, Blocked("www.roblox.com", "roblox.com"));

        var alert = await h.Db.Alerts.SingleAsync(a => a.Type == "repeated-blocked-site");
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(alert.CreatedUtc, DateTimeKind.Utc),
            TimeZoneResolver.Resolve("America/Phoenix"));

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneResolver.Resolve("America/Phoenix"));
        Assert.Equal(localNow.Date, local.Date);

        // A second burst a few minutes later on the same local day stays silent.
        await h.Rules.SyncVisitsAsync(h.Device, Blocked("www.roblox.com", "roblox.com"));
        Assert.Equal(1, await AlertCountAsync(h));
    }

    [Fact]
    public async Task Mute_is_per_child_and_domain_and_can_be_removed()
    {
        await using var h = await NewHarnessAsync();
        var other = new Child { FamilyId = h.Family.Id, Name = "Alex" };
        var otherDevice = new Device
        {
            FamilyId = h.Family.Id,
            ChildId = other.Id,
            Name = "EMMA-YOGALAPTOP",
            TimeZoneId = "UTC"
        };
        h.Db.Children.Add(other);
        h.Db.Devices.Add(otherDevice);
        await h.Db.SaveChangesAsync();

        await h.Rules.MuteSiteAlertsAsync(h.Family.Id, h.Child.Id, null, "roblox.com");
        Assert.True(await h.Rules.IsSiteAlertMutedAsync(h.Family.Id, h.Child.Id, "roblox.com"));
        Assert.False(await h.Rules.IsSiteAlertMutedAsync(h.Family.Id, other.Id, "roblox.com"));

        await h.Rules.SyncVisitsAsync(h.Device, Blocked("www.roblox.com", "roblox.com"));
        Assert.Equal(0, await AlertCountAsync(h));

        await h.Rules.SyncVisitsAsync(otherDevice, Blocked("www.roblox.com", "roblox.com"));
        Assert.Equal(1, await AlertCountAsync(h, other.Id));

        await h.Rules.UnmuteSiteAlertsAsync(h.Family.Id, h.Child.Id, "roblox.com");
        await h.Rules.SyncVisitsAsync(h.Device, Blocked("www.roblox.com", "roblox.com"));
        Assert.Equal(1, await AlertCountAsync(h));
    }

    [Fact]
    public void Microsoft_background_policy_sets_zero_when_on_and_removes_when_off()
    {
        var store = new FakePolicyStore();
        MicrosoftBackgroundPolicyRules.Apply(true, store);
        Assert.Equal(MicrosoftBackgroundPolicyRules.EdgePolicyKey, store.KeyPath);
        Assert.Equal(MicrosoftBackgroundPolicyRules.ValueName, store.ValueName);
        Assert.Equal(0, store.Value);
        Assert.Equal(0, MicrosoftBackgroundPolicyRules.DesiredValue(true));

        MicrosoftBackgroundPolicyRules.Apply(false, store);
        Assert.True(store.Deleted);
        Assert.Null(MicrosoftBackgroundPolicyRules.DesiredValue(false));
    }

    [Fact]
    public void Website_day_view_marks_background_and_mixed_rows()
    {
        var now = DateTime.UtcNow;
        var rules = Array.Empty<WebsiteRule>();
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var points = new List<WebsiteVisitPoint>
        {
            new("skype.com", "config.edge.skype.com", now, true, 5, "rule:always", true),
            new("roblox.com", "www.roblox.com", now, true, 5, "rule:always", false)
        };
        var rows = WebsiteDayView.Build(points, rules, categories, new Dictionary<string, DateTime>(), now.AddDays(-7));
        Assert.True(rows.Single(r => r.Domain == "skype.com").Background);
        Assert.False(rows.Single(r => r.Domain == "roblox.com").Background);

        var mixed = WebsiteDayView.Build(
            [
                new("skype.com", "config.edge.skype.com", now, true, 5, "rule:always", true),
                new("skype.com", "web.skype.com", now, true, 5, "rule:always", false)
            ], rules, categories, new Dictionary<string, DateTime>(), now.AddDays(-7));
        Assert.False(mixed.Single().Background);
    }

    private sealed class FakePolicyStore : IPolicyValueStore
    {
        public string? KeyPath { get; private set; }
        public string? ValueName { get; private set; }
        public int? Value { get; private set; }
        public bool Deleted { get; private set; }

        public void SetDword(string keyPath, string valueName, int value)
        {
            KeyPath = keyPath;
            ValueName = valueName;
            Value = value;
            Deleted = false;
        }

        public void DeleteValue(string keyPath, string valueName)
        {
            KeyPath = keyPath;
            ValueName = valueName;
            Deleted = true;
            Value = null;
        }
    }
}
