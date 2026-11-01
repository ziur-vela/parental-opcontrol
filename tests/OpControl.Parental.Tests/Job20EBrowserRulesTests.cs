// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job20EBrowserRulesTests
{
    private static ParentalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("job20e-" + Guid.NewGuid()).Options);

    private static (Family Family, Child Emma, Child Sam) Seed(ParentalDbContext db)
    {
        var family = new Family { Name = "Job20E" };
        var emma = new Child { FamilyId = family.Id, Name = "Emma" };
        var sam = new Child { FamilyId = family.Id, Name = "Sam" };
        db.Families.Add(family);
        db.Children.AddRange(emma, sam);
        db.SaveChanges();
        return (family, emma, sam);
    }

    private sealed class FakeRegistry : IPolicyRegistry
    {
        public Dictionary<string, List<string>> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> WrittenPaths { get; } = [];
        public List<string> DeletedPaths { get; } = [];

        private static string Key(string path, string name) => path + "|" + name;

        public IReadOnlyList<string>? GetMultiString(string keyPath, string valueName) =>
            Values.TryGetValue(Key(keyPath, valueName), out var value) ? value : null;

        public void SetMultiString(string keyPath, string valueName, IReadOnlyList<string> values)
        {
            WrittenPaths.Add(keyPath);
            Values[Key(keyPath, valueName)] = values.ToList();
        }

        public void DeleteValue(string keyPath, string valueName)
        {
            DeletedPaths.Add(keyPath);
            Values.Remove(Key(keyPath, valueName));
        }

        public string? Value(string sid, string subKey) =>
            GetMultiString($@"{sid}\{subKey}", BrowserPolicyRules.UrlBlocklistValue) is { } list
                ? string.Join("|", list)
                : null;
    }

    private sealed class FakeBaselines : IBrowserPolicyBaselineStore
    {
        public Dictionary<string, IReadOnlyList<string>> Data { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, IReadOnlyList<string>> Load() =>
            Data.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        public void Save(IReadOnlyDictionary<string, IReadOnlyList<string>> baselines)
        {
            Data.Clear();
            foreach (var pair in baselines) Data[pair.Key] = pair.Value;
        }
    }

    [Fact]
    public void Shorts_rule_produces_exactly_three_patterns()
    {
        var patterns = BrowserPolicyRules.BuildPatterns(new BrowserPolicyDto(true, []));

        Assert.Equal(3, patterns.Count);
        Assert.Equal(["youtube.com/shorts", "www.youtube.com/shorts", "m.youtube.com/shorts"], patterns);
    }

    [Fact]
    public void Shorts_off_and_no_custom_rules_produce_no_patterns()
    {
        Assert.Empty(BrowserPolicyRules.BuildPatterns(new BrowserPolicyDto(false, [])));
        Assert.Empty(BrowserPolicyRules.BuildPatterns(null));
    }

    [Theory]
    [InlineData("x.com/i/grok", "x.com", "/i/grok")]
    [InlineData("https://X.com/Path", "x.com", "/Path")]
    [InlineData("youtube.com/shorts/", "youtube.com", "/shorts/")]
    [InlineData("news.example.com", "news.example.com", null)]
    public void Valid_page_rules_are_normalized(string input, string host, string? path)
    {
        Assert.True(BrowserPolicyRules.TryParsePageRule(input, out var parsedHost, out var parsedPath, out var error));
        Assert.Equal(BrowserRuleParseError.None, error);
        Assert.Equal(host, parsedHost);
        Assert.Equal(path, parsedPath);
    }

    [Theory]
    [InlineData("", BrowserRuleParseError.Empty)]
    [InlineData("not a host", BrowserRuleParseError.InvalidHost)]
    [InlineData("*", BrowserRuleParseError.InvalidHost)]
    [InlineData(".x.com", BrowserRuleParseError.InvalidHost)]
    [InlineData("x.com/i/grok?q=1", BrowserRuleParseError.InvalidPath)]
    [InlineData("x.com/a b", BrowserRuleParseError.InvalidPath)]
    [InlineData("x.com/a/../b", BrowserRuleParseError.InvalidPath)]
    [InlineData("x.com/a*b", BrowserRuleParseError.InvalidPath)]
    public void Invalid_page_rules_are_rejected(string input, BrowserRuleParseError expected)
    {
        Assert.False(BrowserPolicyRules.TryParsePageRule(input, out _, out _, out var error));
        Assert.Equal(expected, error);
    }

    [Fact]
    public void Policy_is_written_only_to_the_childs_own_user_hive()
    {
        var registry = new FakeRegistry();
        var manager = new BrowserPolicyManager(registry, new FakeBaselines());

        manager.Apply("S-1-5-21-1111", BrowserPolicyRules.ShortsPatterns);

        Assert.Equal(3, registry.WrittenPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(registry.WrittenPaths, path =>
        {
            Assert.StartsWith(@"S-1-5-21-1111\Software\Policies\", path, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("S-1-5-21-2222", path, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("HKEY_LOCAL_MACHINE", path, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(registry.WrittenPaths, p => p.EndsWith(BrowserPolicyTargets.ChromeSubKey, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(registry.WrittenPaths, p => p.EndsWith(BrowserPolicyTargets.EdgeSubKey, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(registry.WrittenPaths, p => p.EndsWith(BrowserPolicyTargets.BraveSubKey, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("youtube.com/shorts|www.youtube.com/shorts|m.youtube.com/shorts",
            registry.Value("S-1-5-21-1111", BrowserPolicyTargets.ChromeSubKey));
    }

    [Fact]
    public void Entries_are_added_and_removed_per_rule()
    {
        var registry = new FakeRegistry();
        var manager = new BrowserPolicyManager(registry, new FakeBaselines());

        manager.Apply("S-1-5-21-1111", ["x.com/i/grok"]);
        Assert.Equal("x.com/i/grok", registry.Value("S-1-5-21-1111", BrowserPolicyTargets.ChromeSubKey));

        manager.Apply("S-1-5-21-1111", []);
        Assert.Null(registry.Value("S-1-5-21-1111", BrowserPolicyTargets.ChromeSubKey));
        Assert.Contains(registry.DeletedPaths, p => p.EndsWith(BrowserPolicyTargets.ChromeSubKey, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Existing_entries_are_kept_and_restored()
    {
        var registry = new FakeRegistry();
        registry.SetMultiString($@"S-1-5-21-1111\{BrowserPolicyTargets.ChromeSubKey}",
            BrowserPolicyRules.UrlBlocklistValue, ["school.example/blocked"]);
        var manager = new BrowserPolicyManager(registry, new FakeBaselines());

        manager.Apply("S-1-5-21-1111", BrowserPolicyRules.ShortsPatterns);
        Assert.Equal("school.example/blocked|youtube.com/shorts|www.youtube.com/shorts|m.youtube.com/shorts",
            registry.Value("S-1-5-21-1111", BrowserPolicyTargets.ChromeSubKey));

        manager.Apply("S-1-5-21-1111", []);
        Assert.Equal("school.example/blocked", registry.Value("S-1-5-21-1111", BrowserPolicyTargets.ChromeSubKey));
    }

    [Fact]
    public void Removed_policy_is_detected_and_repaired()
    {
        var registry = new FakeRegistry();
        var manager = new BrowserPolicyManager(registry, new FakeBaselines());
        var sid = "S-1-5-21-1111";

        manager.Apply(sid, BrowserPolicyRules.ShortsPatterns);
        Assert.False(manager.VerifyAndRepair(sid, BrowserPolicyRules.ShortsPatterns));

        registry.DeleteValue($@"{sid}\{BrowserPolicyTargets.ChromeSubKey}", BrowserPolicyRules.UrlBlocklistValue);
        Assert.True(manager.VerifyAndRepair(sid, BrowserPolicyRules.ShortsPatterns));
        Assert.Equal("youtube.com/shorts|www.youtube.com/shorts|m.youtube.com/shorts",
            registry.Value(sid, BrowserPolicyTargets.ChromeSubKey));
        Assert.False(manager.VerifyAndRepair(sid, BrowserPolicyRules.ShortsPatterns));
    }

    [Fact]
    public async Task Browser_policy_is_off_and_empty_by_default()
    {
        using var db = NewDb();
        var (family, emma, _) = Seed(db);
        var rules = new RuleService(db, null!);

        var policy = await rules.GetBrowserPolicyAsync(family.Id, emma.Id);

        Assert.False(policy.BlockYouTubeShorts);
        Assert.Empty(policy.Rules);
        Assert.False(await db.ChildBrowserSettings.AnyAsync());
    }

    [Fact]
    public async Task Shorts_toggle_is_per_child()
    {
        using var db = NewDb();
        var (family, emma, sam) = Seed(db);
        var rules = new RuleService(db, null!);

        await rules.SetBlockYouTubeShortsAsync(family.Id, emma.Id, null, true);

        Assert.True((await rules.GetBrowserPolicyAsync(family.Id, emma.Id)).BlockYouTubeShorts);
        Assert.False((await rules.GetBrowserPolicyAsync(family.Id, sam.Id)).BlockYouTubeShorts);
    }

    [Fact]
    public async Task Custom_rules_can_be_added_and_removed()
    {
        using var db = NewDb();
        var (family, emma, _) = Seed(db);
        var rules = new RuleService(db, null!);

        var added = await rules.AddBrowserPageRuleAsync(family.Id, emma.Id, null, "https://x.com/i/grok");
        Assert.Equal("x.com", added.Host);
        Assert.Equal("/i/grok", added.Path);

        var policy = await rules.GetBrowserPolicyAsync(family.Id, emma.Id);
        Assert.Single(policy.Rules);
        Assert.Equal(["x.com/i/grok"], BrowserPolicyRules.BuildPatterns(policy));

        await rules.RemoveBrowserPageRuleAsync(family.Id, emma.Id, added.Id);
        Assert.Empty((await rules.GetBrowserPolicyAsync(family.Id, emma.Id)).Rules);
    }

    [Fact]
    public async Task Duplicate_and_invalid_custom_rules_are_rejected()
    {
        using var db = NewDb();
        var (family, emma, _) = Seed(db);
        var rules = new RuleService(db, null!);

        await rules.AddBrowserPageRuleAsync(family.Id, emma.Id, null, "x.com/i/grok");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rules.AddBrowserPageRuleAsync(family.Id, emma.Id, null, "x.com/i/grok"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rules.AddBrowserPageRuleAsync(family.Id, emma.Id, null, "x.com/i/grok?q=1"));
    }

    [Fact]
    public void Device_state_dto_carries_browser_policy()
    {
        var policy = new BrowserPolicyDto(true, [new BrowserPageRuleDto(Guid.NewGuid(), "x.com", "/i/grok", DateTime.UtcNow)]);
        var state = new DeviceStateDto(Guid.NewGuid(), "Emma", null, false, null, null, null, null,
            [], [], [], [], [], DateTime.UtcNow, BrowserPolicy: policy);

        Assert.Same(policy, state.BrowserPolicy);
        Assert.Equal(["youtube.com/shorts", "www.youtube.com/shorts", "m.youtube.com/shorts", "x.com/i/grok"],
            BrowserPolicyRules.BuildPatterns(state.BrowserPolicy));
    }

    [Fact]
    public void Online_notification_is_off_by_default_and_skips_short_reconnects()
    {
        var now = DateTime.UtcNow;
        var device = new Device { Name = "X1", LastSeenUtc = now.AddMinutes(-10) };
        Assert.False(device.NotifyOnline);
        Assert.False(DevicePresenceService.ShouldNotifyOnline(device, now, TimeSpan.FromMinutes(5)));

        device.NotifyOnline = true;
        Assert.True(DevicePresenceService.ShouldNotifyOnline(device, now, TimeSpan.FromMinutes(5)));

        device.LastSeenUtc = now.AddMinutes(-4);
        Assert.False(DevicePresenceService.ShouldNotifyOnline(device, now, TimeSpan.FromMinutes(5)));

        device.LastSeenUtc = now.AddMinutes(-10);
        device.IsRevoked = true;
        Assert.False(DevicePresenceService.ShouldNotifyOnline(device, now, TimeSpan.FromMinutes(5)));

        device.IsRevoked = false;
        device.LastSeenUtc = null;
        Assert.False(DevicePresenceService.ShouldNotifyOnline(device, now, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Other_browser_helper_skips_supported_browsers()
    {
        var apps = new List<InstalledApp>
        {
            new() { Identity = "win32:googlechrome", DisplayName = "Google Chrome" },
            new() { Identity = "win32:microsoftedge", DisplayName = "Microsoft Edge" },
            new() { Identity = "win32:brave", DisplayName = "Brave Browser" },
            new() { Identity = "win32:firefox", DisplayName = "Mozilla Firefox" },
            new() { Identity = "win32:opera", DisplayName = "Opera GX" },
            new() { Identity = "win32:vivaldi", DisplayName = "Vivaldi" }
        };

        var found = UnsupportedBrowsers.Find(apps);

        Assert.Equal(["Mozilla Firefox", "Opera GX", "Vivaldi"], found.Select(f => f.DisplayName).ToArray());
    }

    [Fact]
    public void Browser_rule_strings_exist_in_both_languages()
    {
        var root = FindRepoRoot();
        var en = File.ReadAllText(Path.Combine(root, "src/OpControl.Parental.Server/Resources/AppStrings.resx"));
        var es = File.ReadAllText(Path.Combine(root, "src/OpControl.Parental.Server/Resources/AppStrings.es-MX.resx"));

        foreach (var key in new[]
        {
            "Websites.BrowserRules.Title",
            "Websites.BrowserRules.Shorts",
            "Websites.BrowserRules.ShortsHelp",
            "Websites.BrowserRules.Custom",
            "Websites.BrowserRules.BlockOthers",
            "Devices.NotifyOnline",
            "Alert.DeviceOnline.Title",
            "Alert.DeviceOnline.Detail"
        })
        {
            Assert.Contains($"name=\"{key}\"", en);
            Assert.Contains($"name=\"{key}\"", es);
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
