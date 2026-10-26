// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services;
using OpControl.Parental.Server.Services.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job19BAiCategoryTests
{
    private static ParentalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("job19b-" + Guid.NewGuid()).Options);

    private static AiCategoryEntry Entry(string name, string group, string blockable, params string[] domains) =>
        new(name, group, blockable, domains, ["https://example.test/source"], "2026-10-26");

    private static (Family Family, Child Alex, Child Sam) Seed(ParentalDbContext db)
    {
        var family = new Family { Name = "Job19B" };
        var alex = new Child { FamilyId = family.Id, Name = "Alex" };
        var sam = new Child { FamilyId = family.Id, Name = "Sam" };
        db.Families.Add(family);
        db.Children.AddRange(alex, sam);
        db.Categories.Add(new Category { Key = AiCategoryCatalog.Key, Name = AiCategoryCatalog.DisplayName, Version = 1 });
        db.CategoryDomains.AddRange(
            new CategoryDomain { CategoryKey = AiCategoryCatalog.Key, Domain = "chatgpt.com", AddedVersion = 1 },
            new CategoryDomain { CategoryKey = AiCategoryCatalog.Key, Domain = "character.ai", AddedVersion = 1 },
            new CategoryDomain { CategoryKey = AiCategoryCatalog.Key, Domain = "duck.ai", AddedVersion = 1 });
        db.SaveChanges();
        return (family, alex, sam);
    }

    private static DeviceStateDto State(Child child, IReadOnlyList<WebsiteRuleDto> sites, IReadOnlyList<CategoryToggleDto> categories) =>
        new(child.Id, child.Name, null, false, null, null, null, null,
            [], sites, [], [], categories, DateTime.UtcNow, [], null, false);

    private static Func<string, string?> CategoryMatcher(IEnumerable<string> domains)
    {
        var list = domains.ToList();
        return domain => list.Any(d => PolicyRules.Matches(d, domain)) ? AiCategoryCatalog.Key : null;
    }

    [Fact]
    public void Embedded_catalog_only_contains_safe_in_scope_domains()
    {
        var build = AiCategoryCatalog.BuildDefault();

        Assert.True(build.Entries.Count >= 45, $"expected at least 45 entries, got {build.Entries.Count}");
        Assert.True(build.Domains.Count >= 80, $"expected at least 80 domains, got {build.Domains.Count}");
        Assert.Contains("huggingface.co", build.Domains);
        Assert.DoesNotContain("google.com", build.Domains);
        Assert.DoesNotContain("duckduckgo.com", build.Domains);
        Assert.DoesNotContain("bing.com", build.Domains);
        Assert.DoesNotContain("x.com", build.Domains);
        Assert.DoesNotContain("github.com", build.Domains);
        Assert.DoesNotContain("khanacademy.org", build.Domains);

        foreach (var domain in build.Domains)
        {
            Assert.DoesNotContain(domain, AiCategoryCatalog.GuardDomains);
            Assert.DoesNotContain(AiCategoryCatalog.GuardDomains,
                guard => guard.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Importer_skips_unverified_and_out_of_scope_entries_and_dedupes()
    {
        var build = AiCategoryCatalog.Build(
        [
            Entry("Verified chat", "standalone", "full", "chatgpt.com", "CHATGPT.com", "chatgpt.com"),
            Entry("Unverified companion", "companion", "unverified", "sakura.example"),
            Entry("Not by domain", "search", "not-by-domain", "search.example"),
            Entry("Social app", "social", "partial", "social.example"),
            Entry("No safe domains", "standalone", "partial")
        ]);

        Assert.Single(build.Entries);
        Assert.Single(build.Domains);
        Assert.Equal("chatgpt.com", build.Domains[0]);
        Assert.Contains(build.Excluded, e => e.Name == "Unverified companion" && e.Reason.Contains("unverified"));
        Assert.Contains(build.Excluded, e => e.Name == "Not by domain" && e.Reason.Contains("not-by-domain"));
        Assert.Contains(build.Excluded, e => e.Name == "Social app" && e.Reason.Contains("group=social"));
        Assert.Contains(build.Excluded, e => e.Name == "No safe domains" && e.Reason == "no safe domains");
    }

    [Theory]
    [InlineData("google.com")]
    [InlineData("bing.com")]
    [InlineData("khanacademy.org")]
    [InlineData("duckduckgo.com")]
    [InlineData("x.com")]
    public void Importer_rejects_guarded_or_parent_domains(string domain)
    {
        Assert.Throws<InvalidOperationException>(() =>
            AiCategoryCatalog.Build([Entry("Bad", "standalone", "full", domain)]));
    }

    [Fact]
    public void Importer_allows_a_dedicated_subdomain_of_a_guarded_parent()
    {
        var build = AiCategoryCatalog.Build([Entry("Gemini", "standalone", "full", "gemini.google.com")]);
        Assert.Single(build.Domains);
        Assert.Equal("gemini.google.com", build.Domains[0]);
    }

    [Fact]
    public void Importer_rejects_invalid_domains()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AiCategoryCatalog.Build([Entry("Bad", "standalone", "full", "https://bad.example/path")]));
        Assert.Throws<InvalidOperationException>(() =>
            AiCategoryCatalog.Build([Entry("Bad", "standalone", "full", "not a domain")]));
    }

    [Fact]
    public async Task Category_toggle_is_per_child_and_off_by_default()
    {
        await using var db = NewDb();
        var (family, alex, sam) = Seed(db);
        var rules = new RuleService(db, null!);

        var before = await rules.GetDevicePolicyAsync(family.Id, alex.Id);
        Assert.False(before.Categories.Single(c => c.Key == AiCategoryCatalog.Key).Enabled);

        await rules.ToggleCategoryAsync(family.Id, alex.Id, null, AiCategoryCatalog.Key, true);
        var alexOn = await rules.GetDevicePolicyAsync(family.Id, alex.Id);
        var samOff = await rules.GetDevicePolicyAsync(family.Id, sam.Id);
        Assert.True(alexOn.Categories.Single(c => c.Key == AiCategoryCatalog.Key).Enabled);
        Assert.False(samOff.Categories.Single(c => c.Key == AiCategoryCatalog.Key).Enabled);

        await rules.ToggleCategoryAsync(family.Id, alex.Id, null, AiCategoryCatalog.Key, false);
        var alexOff = await rules.GetDevicePolicyAsync(family.Id, alex.Id);
        Assert.False(alexOff.Categories.Single(c => c.Key == AiCategoryCatalog.Key).Enabled);
    }

    [Fact]
    public async Task Allow_rule_wins_over_the_ai_category_and_its_subdomains()
    {
        await using var db = NewDb();
        var (family, alex, _) = Seed(db);
        var rules = new RuleService(db, null!);
        await rules.ToggleCategoryAsync(family.Id, alex.Id, null, AiCategoryCatalog.Key, true);
        await rules.UpsertWebsiteRuleAsync(family.Id, alex.Id, null, "chatgpt.com", blocked: false, RuleMode.Always, null);
        var policy = await rules.GetDevicePolicyAsync(family.Id, alex.Id);
        var state = State(alex, policy.Sites, policy.Categories);
        var match = CategoryMatcher(["chatgpt.com", "character.ai", "duck.ai"]);
        var now = DateTime.UtcNow;

        Assert.Null(PolicyRules.GetWebsiteBlockReason(state, "chatgpt.com", TimeZoneInfo.Utc, now, match));
        Assert.Null(PolicyRules.GetWebsiteBlockReason(state, "chat.openai.com", TimeZoneInfo.Utc, now, match));
        Assert.Equal("category:ai", PolicyRules.GetWebsiteBlockReason(state, "character.ai", TimeZoneInfo.Utc, now, match));
    }

    [Fact]
    public void Category_blocks_sample_ai_domains_and_subdomains_but_not_guarded_domains()
    {
        var build = AiCategoryCatalog.BuildDefault();
        var match = CategoryMatcher(build.Domains);
        var child = new Child { Name = "Alex" };
        var state = State(child, [], []);
        var now = DateTime.UtcNow;

        Assert.Equal("category:ai", PolicyRules.GetWebsiteBlockReason(state, "chatgpt.com", TimeZoneInfo.Utc, now, match));
        Assert.Equal("category:ai", PolicyRules.GetWebsiteBlockReason(state, "chat.openai.com", TimeZoneInfo.Utc, now, match));
        Assert.Equal("category:ai", PolicyRules.GetWebsiteBlockReason(state, "character.ai", TimeZoneInfo.Utc, now, match));
        Assert.Equal("category:ai", PolicyRules.GetWebsiteBlockReason(state, "duck.ai", TimeZoneInfo.Utc, now, match));
        Assert.Equal("category:ai", PolicyRules.GetWebsiteBlockReason(state, "gemini.google.com", TimeZoneInfo.Utc, now, match));

        Assert.Null(PolicyRules.GetWebsiteBlockReason(state, "google.com", TimeZoneInfo.Utc, now, match));
        Assert.Null(PolicyRules.GetWebsiteBlockReason(state, "duckduckgo.com", TimeZoneInfo.Utc, now, match));
        Assert.Null(PolicyRules.GetWebsiteBlockReason(state, "bing.com", TimeZoneInfo.Utc, now, match));
        Assert.Null(PolicyRules.GetWebsiteBlockReason(state, "x.com", TimeZoneInfo.Utc, now, match));
        Assert.Null(PolicyRules.GetWebsiteBlockReason(state, "khanacademy.org", TimeZoneInfo.Utc, now, match));
        Assert.Null(PolicyRules.GetWebsiteBlockReason(state, "classroom.google.com", TimeZoneInfo.Utc, now, match));
    }

    [Fact]
    public async Task Category_version_bumps_only_on_change_or_manifest_bump_and_reactivates_rows()
    {
        await using var db = NewDb();
        var first = AiCategoryCatalog.Build([Entry("One", "standalone", "full", "chatgpt.com")]);
        Assert.Equal(1, await AiCategoryCatalog.ApplyAsync(db, first, 1));
        Assert.Equal(1, await AiCategoryCatalog.ApplyAsync(db, first, 1));

        var second = AiCategoryCatalog.Build([Entry("Two", "standalone", "full", "chatgpt.com", "character.ai")]);
        Assert.Equal(2, await AiCategoryCatalog.ApplyAsync(db, second, 1));
        Assert.Equal(3, await AiCategoryCatalog.ApplyAsync(db, second, 3));

        var third = AiCategoryCatalog.Build([Entry("Three", "standalone", "full", "character.ai")]);
        Assert.Equal(4, await AiCategoryCatalog.ApplyAsync(db, third, 3));
        var fourth = AiCategoryCatalog.Build([Entry("Four", "standalone", "full", "character.ai", "chatgpt.com")]);
        Assert.Equal(5, await AiCategoryCatalog.ApplyAsync(db, fourth, 3));

        var active = await db.CategoryDomains
            .Where(d => d.CategoryKey == AiCategoryCatalog.Key && d.RemovedVersion == null)
            .Select(d => d.Domain)
            .ToListAsync();
        Assert.Contains("chatgpt.com", active);
        Assert.Contains("character.ai", active);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, null, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void Child_tip_shows_only_when_luna_is_on_and_ai_is_off(bool lunaOn, bool? blocked, bool expected)
    {
        Assert.Equal(expected, LunaUi.ShowPublicAiBlockTip(lunaOn, blocked));
    }

    [Fact]
    public void Welcome_button_opens_the_ai_category_and_the_tip_uses_the_real_state()
    {
        var root = FindRoot();
        var welcome = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Components", "Shared", "LunaWelcome.razor"));
        Assert.Contains("href=\"/websites#category-ai\"", welcome, StringComparison.Ordinal);
        Assert.DoesNotContain("Luna.Common.ComingSoon", welcome, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", welcome, StringComparison.Ordinal);

        var child = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Components", "Pages", "LunaChild.razor"));
        Assert.Contains("AiCategoryCatalog.Key", child, StringComparison.Ordinal);
        Assert.Contains("_publicAiChatsBlocked", child, StringComparison.Ordinal);
        Assert.Contains("href=\"/websites#category-ai\"", child, StringComparison.Ordinal);

        var websites = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Components", "Pages", "Websites.razor"));
        Assert.Contains("Websites.Ai.WhatTitle", websites, StringComparison.Ordinal);
        Assert.Contains("ai-category-help", websites, StringComparison.Ordinal);
        Assert.Contains("href=\"@_reportMailto\"", websites, StringComparison.Ordinal);
        Assert.Contains("id=\"@($\"category-{category.Key}\")\"", websites, StringComparison.Ordinal);
    }

    [Fact]
    public void New_category_and_help_strings_exist_in_both_languages()
    {
        var root = FindRoot();
        var english = ReadResx(root, "AppStrings.resx");
        var spanish = ReadResx(root, "AppStrings.es-MX.resx");
        foreach (var key in new[]
                 {
                     "Category.Ai", "Websites.Ai.Subtitle", "Websites.Ai.WhatTitle",
                     "Websites.Ai.BlocksLabel", "Websites.Ai.Blocks", "Websites.Ai.DoesNotBlockLabel",
                     "Websites.Ai.DoesNotBlock", "Websites.Ai.Updates", "Websites.Ai.Report",
                     "Websites.Ai.ReportSubject"
                 })
        {
            Assert.False(string.IsNullOrWhiteSpace(english.GetValueOrDefault(key)), key);
            Assert.False(string.IsNullOrWhiteSpace(spanish.GetValueOrDefault(key)), key);
        }
        Assert.Equal("Chats públicos de IA", spanish["Category.Ai"]);
    }

    private static Dictionary<string, string> ReadResx(string root, string name)
    {
        var path = Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", name);
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => e.Element("value")?.Value ?? "");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
