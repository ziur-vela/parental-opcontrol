// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services;

public sealed record AiCategoryEntry(
    string Name,
    string Group,
    string Blockable,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> Sources,
    string Checked);

public sealed record AiCategoryDocument(
    string Key,
    string Name,
    int Version,
    string Checked,
    string VersionNote,
    IReadOnlyList<AiCategoryEntry> Entries);

public sealed record AiCategoryExclusion(string Name, string Reason);

public sealed record AiCategoryBuildResult(
    IReadOnlyList<AiCategoryEntry> Entries,
    IReadOnlyList<string> Domains,
    IReadOnlyList<AiCategoryExclusion> Excluded);

/// <summary>
/// One repo-owned list for the public-AI-chat category. The embedded JSON is the
/// only place domains are edited; the guard list and validation live here so a
/// bad edit cannot silently block a shared site or a school tool.
/// </summary>
public static class AiCategoryCatalog
{
    public const string Key = "ai";
    public const string DisplayName = "Public AI chats";
    public const string SourceUrl = "repo:src/OpControl.Parental.Server/Resources/ai-category.json";

    private static readonly string[] IncludedGroups =
        ["standalone", "companion", "wrapper", "search", "browser", "windows"];

    private static readonly string[] IncludedBlockable = ["full", "partial"];

    private static readonly Regex DomainPattern = new(
        @"^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Shared domains and school/kid-safe domains that must never be blocked by
    /// this category. A dedicated subdomain (for example gemini.google.com) is
    /// allowed; the parent itself, or an entry that would block the parent of a
    /// guarded domain, is rejected.
    /// </summary>
    public static readonly IReadOnlySet<string> GuardDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Shared consumer/search/social domains.
        "google.com", "bing.com", "duckduckgo.com", "yahoo.com", "yandex.ru", "baidu.com",
        "youtube.com", "x.com", "instagram.com", "cdninstagram.com", "facebook.com",
        "messenger.com", "whatsapp.com", "web.whatsapp.com", "snapchat.com", "web.snapchat.com",
        "discord.com", "discordapp.com", "discord.gg", "telegram.org", "web.telegram.org",
        "github.com", "microsoft.com", "apple.com", "amazon.com",
        // Every domain in the reviewed kid-safe-exception entries.
        "khanmigo.ai", "khanacademy.org", "magicschool.ai", "schoolai.com", "briskteaching.com",
        "diffit.me", "web.diffit.me", "ello.com", "duolingo.com", "canva.com", "quizizz.com",
        "classroom.google.com", "teams.microsoft.com"
    };

    private static readonly Lazy<AiCategoryDocument> Document = new(LoadDocument);

    public static AiCategoryDocument Current => Document.Value;

    public static AiCategoryBuildResult BuildDefault() => Build(Current.Entries);

    public static AiCategoryBuildResult Build(IEnumerable<AiCategoryEntry> entries)
    {
        var included = new List<AiCategoryEntry>();
        var domains = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var excluded = new List<AiCategoryExclusion>();

        foreach (var entry in entries)
        {
            if (!IncludedBlockable.Contains(entry.Blockable, StringComparer.OrdinalIgnoreCase))
            {
                excluded.Add(new AiCategoryExclusion(entry.Name, $"blockable={entry.Blockable}"));
                continue;
            }
            if (!IncludedGroups.Contains(entry.Group, StringComparer.OrdinalIgnoreCase))
            {
                excluded.Add(new AiCategoryExclusion(entry.Name, $"group={entry.Group}"));
                continue;
            }

            var local = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var kept = new List<string>();
            foreach (var raw in entry.Domains)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var domain = NormalizeDomain(raw);
                if (IsGuarded(domain))
                    throw new InvalidOperationException(
                        AppText.Get("Error.AiCategoryGuard", null, domain, entry.Name));

                if (!local.Add(domain)) continue;
                kept.Add(domain);
            }

            if (kept.Count == 0)
            {
                excluded.Add(new AiCategoryExclusion(entry.Name, "no safe domains"));
                continue;
            }

            included.Add(entry with { Domains = kept });
            foreach (var domain in kept)
                if (seen.Add(domain)) domains.Add(domain);
        }

        return new AiCategoryBuildResult(included, domains, excluded);
    }

    public static async Task<int> ApplyAsync(
        ParentalDbContext db,
        AiCategoryBuildResult build,
        int catalogVersion,
        CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Key == Key, ct);
        if (category is null)
        {
            category = new Category { Key = Key, Name = DisplayName, Version = 0, SourceUrl = SourceUrl };
            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
        }

        var existing = await db.CategoryDomains.Where(d => d.CategoryKey == Key).ToListAsync(ct);
        var current = existing.Where(d => d.RemovedVersion is null)
            .Select(d => d.Domain)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var target = build.Domains.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = !current.SetEquals(target);
        var incomingVersion = Math.Max(1, catalogVersion);
        var newVersion = category.Version;

        if (changed || incomingVersion > category.Version)
            newVersion = Math.Max(category.Version + 1, incomingVersion);

        var added = 0;
        var removed = 0;
        if (changed && newVersion > category.Version)
        {
            var byDomain = existing
                .GroupBy(d => d.Domain, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.AddedVersion).First(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var domain in target.Where(d => !current.Contains(d)))
            {
                if (byDomain.TryGetValue(domain, out var previous))
                {
                    previous.RemovedVersion = null;
                    previous.AddedVersion = newVersion;
                }
                else
                {
                    db.CategoryDomains.Add(new CategoryDomain
                    {
                        CategoryKey = Key,
                        Domain = domain,
                        AddedVersion = newVersion
                    });
                }
                added++;
            }

            foreach (var row in existing.Where(d => d.RemovedVersion is null && !target.Contains(d.Domain)))
            {
                row.RemovedVersion = newVersion;
                removed++;
            }
        }

        category.Name = DisplayName;
        category.SourceUrl = SourceUrl;
        category.FetchedUtc = DateTime.UtcNow;
        if (newVersion > category.Version) category.Version = newVersion;

        await db.SaveChangesAsync(ct);
        return category.Version;
    }

    private static bool IsGuarded(string domain) =>
        GuardDomains.Contains(domain)
        || GuardDomains.Any(guard => guard.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeDomain(string raw)
    {
        var value = raw.Trim().TrimEnd('.').ToLowerInvariant();
        if (value.StartsWith("www.", StringComparison.Ordinal)) value = value[4..];
        if (!DomainPattern.IsMatch(value))
            throw new InvalidOperationException(AppText.Get("Error.AiCategoryInvalidDomain", null, raw));
        return value;
    }

    private static AiCategoryDocument LoadDocument()
    {
        var assembly = typeof(AiCategoryCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("ai-category.json")
            ?? throw new InvalidOperationException(AppText.Get("Error.AiCategoryMissing"));
        var document = JsonSerializer.Deserialize<AiCategoryDocument>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException(AppText.Get("Error.AiCategoryEmpty"));

        if (!string.Equals(document.Key, Key, StringComparison.Ordinal))
            throw new InvalidOperationException(AppText.Get("Error.AiCategoryKey", null, Key));
        if (document.Entries is null || document.Entries.Count == 0)
            throw new InvalidOperationException(AppText.Get("Error.AiCategoryNoEntries"));
        if (document.Version < 1)
            throw new InvalidOperationException(AppText.Get("Error.AiCategoryVersion"));
        return document;
    }
}
