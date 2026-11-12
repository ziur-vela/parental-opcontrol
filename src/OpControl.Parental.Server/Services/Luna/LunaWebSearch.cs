// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services.Luna;

/// <summary>
/// Builds the web-search tool payload and checks the sources Luna cites.
/// The excluded-domain list is capped at the Exa engine limit (1,200); the
/// post-check is the guarantee, the list only reduces wasted results.
/// </summary>
public sealed class LunaWebSearchGuard
{
    public const int MaxExcludedDomains = 1200;
    public const string Engine = "exa";
    public const int MaxResults = 5;
    public const int MaxTotalResults = 10;
    public static readonly string[] BlockedCategoryKeys = ["adult", "gambling", AiCategoryCatalog.Key];
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);

    private readonly ParentalDbContext _db;
    private readonly IMemoryCache _cache;

    public LunaWebSearchGuard(ParentalDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>
    /// The per-child domain set that must never appear in an answer: adult,
    /// gambling and public AI chat categories, every category the child has
    /// switched on, and every website rule that blocks a domain.
    /// </summary>
    public async Task<LunaSearchGuardResult> BuildAsync(Guid familyId, Guid childId, CancellationToken ct = default)
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categoryLists = new List<(string Key, HashSet<string> Domains)>();

        foreach (var key in BlockedCategoryKeys)
        {
            var domains = await CategoryDomainsAsync(key, ct);
            categoryLists.Add((key, domains));
            blocked.UnionWith(domains);
        }

        var toggles = await _db.ChildCategories.AsNoTracking()
            .Where(c => c.FamilyId == familyId && c.ChildId == childId && c.Enabled)
            .Select(c => c.CategoryKey)
            .ToListAsync(ct);
        foreach (var key in toggles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (BlockedCategoryKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;
            blocked.UnionWith(await CategoryDomainsAsync(key, ct));
        }

        var rules = await _db.WebsiteRules.AsNoTracking()
            .Where(r => r.FamilyId == familyId && r.ChildId == childId && r.IsBlocked)
            .Select(r => r.Domain)
            .ToListAsync(ct);
        blocked.UnionWith(rules.Select(NormalizeDomain).Where(d => d.Length > 0));

        var excluded = BuildExcluded(rules, categoryLists);
        return new LunaSearchGuardResult(blocked, excluded);
    }

    /// <summary>Checks whether a cited URL is on a domain the child may not see.</summary>
    public static bool IsBlocked(string? url, IReadOnlySet<string> blocked)
    {
        var host = HostOf(url);
        if (host is null) return true;
        return HostOrParentBlocked(host, blocked);
    }

    public static string? HostOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;
        return NormalizeDomain(uri.Host);
    }

    public static string NormalizeDomain(string? value)
    {
        var domain = (value ?? "").Trim().Trim('.').ToLowerInvariant();
        if (domain.StartsWith("www.", StringComparison.Ordinal)) domain = domain[4..];
        return domain;
    }

    public static bool HostOrParentBlocked(string host, IReadOnlySet<string> blocked)
    {
        var value = NormalizeDomain(host);
        while (value.Length > 0)
        {
            if (blocked.Contains(value)) return true;
            var dot = value.IndexOf('.');
            if (dot < 0) return false;
            value = value[(dot + 1)..];
        }
        return false;
    }

    private static List<string> BuildExcluded(IEnumerable<string> childRules,
        IReadOnlyList<(string Key, HashSet<string> Domains)> categories)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string domain)
        {
            var value = NormalizeDomain(domain);
            if (value.Length < 4 || !value.Contains('.') || value.Length > 253) return;
            if (seen.Add(value) && result.Count < MaxExcludedDomains) result.Add(value);
        }

        foreach (var rule in childRules.OrderBy(r => r, StringComparer.OrdinalIgnoreCase)) Add(rule);

        // The upstream lists are far larger than the engine cap. Prefer root
        // hostnames (the highest-signal entries) and shorter names first.
        var roots = categories
            .SelectMany(c => c.Domains.Select(d => NormalizeDomain(d)))
            .Where(d => d.Length > 3 && d.Contains('.'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d.Count(ch => ch == '.'))
            .ThenBy(d => d.Length)
            .ThenBy(d => d, StringComparer.OrdinalIgnoreCase);
        foreach (var domain in roots)
        {
            if (result.Count >= MaxExcludedDomains) break;
            Add(domain);
        }
        return result;
    }

    private async Task<HashSet<string>> CategoryDomainsAsync(string key, CancellationToken ct)
    {
        var version = await _db.Categories.AsNoTracking()
            .Where(c => c.Key == key)
            .Select(c => (int?)c.Version)
            .FirstOrDefaultAsync(ct) ?? 0;
        var cacheKey = $"luna-websearch-category-{key}-{version}";
        if (_cache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached is not null) return cached;

        var rows = await _db.CategoryDomains.AsNoTracking()
            .Where(d => d.CategoryKey == key && d.RemovedVersion == null)
            .Select(d => d.Domain)
            .ToListAsync(ct);
        var set = new HashSet<string>(rows.Select(NormalizeDomain).Where(d => d.Length > 0), StringComparer.OrdinalIgnoreCase);
        _cache.Set(cacheKey, set, CacheLifetime);
        return set;
    }
}

public sealed record LunaSearchGuardResult(
    IReadOnlySet<string> BlockedDomains,
    IReadOnlyList<string> ExcludedDomains);

public static class LunaWebSearchData
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string? SerializeSources(IEnumerable<LunaChatSource>? sources)
    {
        var rows = (sources ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.Url))
            .GroupBy(s => s.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(20)
            .ToList();
        return rows.Count == 0 ? null : JsonSerializer.Serialize(rows, Json);
    }

    public static IReadOnlyList<LunaSourceDto> ParseSources(string? json, int max = 20)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var rows = JsonSerializer.Deserialize<List<LunaChatSource>>(json, Json) ?? [];
            return rows
                .Where(s => !string.IsNullOrWhiteSpace(s.Url))
                .Select(ToDto)
                .Take(max)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<LunaSourceDto> ToDtos(IEnumerable<LunaChatSource>? sources, int max = 3) =>
        (sources ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.Url))
            .Select(ToDto)
            .Take(max)
            .ToList();

    public static string? SerializeQueries(IEnumerable<string>? queries)
    {
        var rows = (queries ?? [])
            .Select(q => (q ?? "").Trim())
            .Where(q => q.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        return rows.Count == 0 ? null : string.Join("\n", rows);
    }

    private static LunaSourceDto ToDto(LunaChatSource source)
    {
        var host = LunaWebSearchGuard.HostOf(source.Url) ?? "";
        var title = (source.Title ?? "").Trim();
        if (title.Length > 200) title = title[..200];
        var site = (source.Site ?? "").Trim();
        if (site.Length == 0) site = host;
        return new LunaSourceDto(source.Url.Trim(), title.Length == 0 ? site : title, site);
    }
}
