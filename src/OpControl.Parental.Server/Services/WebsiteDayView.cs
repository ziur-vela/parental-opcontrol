// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services;

public sealed record WebsiteVisitPoint(
    string Domain,
    string Host,
    DateTime VisitedUtc,
    bool Blocked,
    int BlockedAttempts,
    string? BlockReason);

public sealed record WebsiteSubdomainRow(
    string Host,
    int Count,
    DateTime LastUtc,
    int BlockedAttempts,
    string? BlockReason,
    bool IsBlocked,
    bool IsAllowed,
    Guid RuleId,
    bool HasOwnRule);

public sealed record WebsiteSiteRow(
    string Domain,
    int Count,
    DateTime LastUtc,
    int BlockedAttempts,
    string? BlockReason,
    bool IsBlocked,
    bool IsAllowed,
    Guid RuleId,
    bool IsNewThisWeek,
    IReadOnlyList<WebsiteSubdomainRow> Subdomains);

/// <summary>
/// Groups one day's visits by registrable domain, keeping the full hosts as
/// subdomain rows. Rules and category blocks follow the closest parent so a
/// subdomain rule only affects that host and its own subdomains.
/// </summary>
public static class WebsiteDayView
{
    public static IReadOnlyList<WebsiteSiteRow> Build(
        IReadOnlyList<WebsiteVisitPoint> visits,
        IReadOnlyList<WebsiteRule> rules,
        IReadOnlySet<string> categoryBlockedDomains,
        IReadOnlyDictionary<string, DateTime> firstSeenByDomain,
        DateTime weekStart)
    {
        var rows = new List<WebsiteSiteRow>();
        foreach (var group in visits.GroupBy(v => v.Domain, StringComparer.OrdinalIgnoreCase))
        {
            var domain = group.Key;
            var main = Resolve(domain, rules, categoryBlockedDomains);
            var subdomains = group
                .Where(v => !string.Equals(v.Host, domain, StringComparison.OrdinalIgnoreCase))
                .GroupBy(v => v.Host, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Max(v => v.VisitedUtc))
                .Select(g =>
                {
                    var host = g.Key;
                    var resolved = Resolve(host, rules, categoryBlockedDomains);
                    return new WebsiteSubdomainRow(
                        host,
                        g.Count(),
                        g.Max(v => v.VisitedUtc),
                        g.Sum(v => v.BlockedAttempts),
                        g.Select(v => v.BlockReason).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)),
                        resolved.Blocked,
                        resolved.Allowed,
                        resolved.Rule?.Id ?? Guid.Empty,
                        rules.Any(r => string.Equals(r.Domain, host, StringComparison.OrdinalIgnoreCase)));
                })
                .ToList();

            rows.Add(new WebsiteSiteRow(
                domain,
                group.Count(),
                group.Max(v => v.VisitedUtc),
                group.Sum(v => v.BlockedAttempts),
                group.Select(v => v.BlockReason).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)),
                main.Blocked,
                main.Allowed,
                main.Rule?.Id ?? Guid.Empty,
                firstSeenByDomain.TryGetValue(domain, out var first) && first >= weekStart,
                subdomains));
        }

        return rows.OrderByDescending(r => r.LastUtc).ToList();
    }

    private static (bool Blocked, bool Allowed, WebsiteRule? Rule) Resolve(
        string domain,
        IReadOnlyList<WebsiteRule> rules,
        IReadOnlySet<string> categoryBlockedDomains)
    {
        foreach (var parent in Parents(domain))
        {
            var rule = rules.FirstOrDefault(r => string.Equals(r.Domain, parent, StringComparison.OrdinalIgnoreCase));
            if (rule is null) continue;
            return (rule.IsBlocked, !rule.IsBlocked, rule);
        }

        var categoryBlocked = Parents(domain).Any(categoryBlockedDomains.Contains);
        return (categoryBlocked, false, null);
    }

    private static IEnumerable<string> Parents(string domain)
    {
        var value = (domain ?? "").Trim().TrimEnd('.').ToLowerInvariant();
        while (value.Length > 0)
        {
            yield return value;
            var dot = value.IndexOf('.');
            if (dot < 0) break;
            value = value[(dot + 1)..];
        }
    }
}

/// <summary>
/// Monotonic token for async page loads. A response may only apply if no newer
/// request has started since it began (Job 19j stale day results).
/// </summary>
public sealed class LatestRequestGate
{
    private int _current;

    public int Next() => Interlocked.Increment(ref _current);

    public bool IsCurrent(int token) => Volatile.Read(ref _current) == token;
}
