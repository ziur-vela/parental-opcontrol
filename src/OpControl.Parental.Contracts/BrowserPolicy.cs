// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.RegularExpressions;

namespace OpControl.Parental.Contracts;

public sealed record BrowserPageRuleDto(
    Guid Id,
    string Host,
    string? Path,
    DateTime UpdatedUtc);

public sealed record BrowserPolicyDto(
    bool BlockYouTubeShorts,
    IReadOnlyList<BrowserPageRuleDto> Rules);

public enum BrowserRuleParseError
{
    None = 0,
    Empty = 1,
    InvalidHost = 2,
    InvalidPath = 3,
    TooLong = 4
}

/// <summary>
/// Pure rules for the browser policy layer: the exact YouTube Shorts patterns,
/// custom page-rule validation and the URLBlocklist patterns sent to the client.
/// No Windows API is used here so the same logic is unit-tested on Linux.
/// </summary>
public static class BrowserPolicyRules
{
    public const string UrlBlocklistValue = "URLBlocklist";
    public const int MaxRuleLength = 512;

    public static readonly IReadOnlyList<string> ShortsPatterns =
    [
        "youtube.com/shorts",
        "www.youtube.com/shorts",
        "m.youtube.com/shorts"
    ];

    /// <summary>The three Shorts patterns only. Regular YouTube paths are untouched.</summary>
    public static IReadOnlyList<string> BuildPatterns(BrowserPolicyDto? policy)
    {
        if (policy is null) return [];
        var patterns = new List<string>();
        if (policy.BlockYouTubeShorts) patterns.AddRange(ShortsPatterns);
        foreach (var rule in policy.Rules ?? [])
        {
            if (string.IsNullOrWhiteSpace(rule.Host)) continue;
            var host = rule.Host.Trim().TrimEnd('.').ToLowerInvariant();
            if (host.Length == 0) continue;
            var path = NormalizePath(rule.Path);
            patterns.Add(path is null ? host : host + path);
        }
        return Deduplicate(patterns);
    }

    /// <summary>Accept a host with an optional path, with or without a scheme.</summary>
    public static bool TryParsePageRule(string input, out string host, out string? path, out BrowserRuleParseError error)
    {
        host = "";
        path = null;
        error = BrowserRuleParseError.None;

        var value = (input ?? "").Trim();
        if (value.Length == 0)
        {
            error = BrowserRuleParseError.Empty;
            return false;
        }
        if (value.Length > MaxRuleLength)
        {
            error = BrowserRuleParseError.TooLong;
            return false;
        }

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) value = value[7..];
        else if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) value = value[8..];

        if (value.Contains('?', StringComparison.Ordinal) || value.Contains('#', StringComparison.Ordinal)
            || value.Contains('@', StringComparison.Ordinal) || value.Contains('\\', StringComparison.Ordinal))
        {
            error = BrowserRuleParseError.InvalidPath;
            return false;
        }

        var slash = value.IndexOf('/');
        var hostPart = slash >= 0 ? value[..slash] : value;
        var pathPart = slash >= 0 ? value[slash..] : null;

        hostPart = hostPart.Trim().TrimEnd('.').ToLowerInvariant();
        if (hostPart.StartsWith('.') || hostPart.Contains('*') || hostPart.Contains(':'))
        {
            error = BrowserRuleParseError.InvalidHost;
            return false;
        }
        if (!HostPattern.IsMatch(hostPart))
        {
            error = BrowserRuleParseError.InvalidHost;
            return false;
        }

        var normalizedPath = NormalizePath(pathPart);
        if (pathPart is not null && normalizedPath is null)
        {
            error = BrowserRuleParseError.InvalidPath;
            return false;
        }

        host = hostPart;
        path = normalizedPath;
        return true;
    }

    /// <summary>Reject control characters, spaces, wildcards, queries and dot segments.</summary>
    private static string? NormalizePath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();
        if (value.Length == 0) return null;
        if (!value.StartsWith('/')) value = "/" + value;
        if (value.Length > MaxRuleLength) return null;
        foreach (var ch in value)
        {
            if (char.IsControl(ch) || char.IsWhiteSpace(ch)) return null;
            if (ch is '?' or '#' or '@' or '\\' or '*') return null;
        }
        var segments = value.Split('/');
        foreach (var segment in segments)
        {
            if (segment == ".." || segment == ".") return null;
        }
        return value;
    }

    private static IReadOnlyList<string> Deduplicate(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (seen.Add(value)) list.Add(value);
        }
        return list;
    }

    private static readonly Regex HostPattern = new(
        @"^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
}

/// <summary>One browser and the registry key that holds its user-level policies.</summary>
public sealed record BrowserPolicyTarget(string Browser, string PolicyKeyPath);

public static class BrowserPolicyTargets
{
    public const string ChromeSubKey = @"Software\Policies\Google\Chrome";
    public const string EdgeSubKey = @"Software\Policies\Microsoft\Edge";
    public const string BraveSubKey = @"Software\Policies\BraveSoftware\Brave";

    /// <summary>
    /// Policy keys are always under the child's own HKEY_USERS hive. The service
    /// never writes HKLM, so parents and other Windows users are unaffected.
    /// </summary>
    public static IReadOnlyList<BrowserPolicyTarget> ForUser(string sid) =>
    [
        new("Chrome", $@"{sid}\{ChromeSubKey}"),
        new("Edge", $@"{sid}\{EdgeSubKey}"),
        new("Brave", $@"{sid}\{BraveSubKey}")
    ];
}

/// <summary>Minimal registry surface so the policy manager is testable off Windows.</summary>
public interface IPolicyRegistry
{
    IReadOnlyList<string>? GetMultiString(string keyPath, string valueName);
    void SetMultiString(string keyPath, string valueName, IReadOnlyList<string> values);
    void DeleteValue(string keyPath, string valueName);
}

/// <summary>Persists the values that existed before the first policy write.</summary>
public interface IBrowserPolicyBaselineStore
{
    IReadOnlyDictionary<string, IReadOnlyList<string>> Load();
    void Save(IReadOnlyDictionary<string, IReadOnlyList<string>> baselines);
}

/// <summary>
/// Writes and verifies the URLBlocklist value for Chrome, Edge and Brave in the
/// child's own user hive. Existing entries in those values are preserved so a
/// rule we did not create is never lost.
/// </summary>
public sealed class BrowserPolicyManager
{
    private readonly IPolicyRegistry _registry;
    private readonly IBrowserPolicyBaselineStore _baselines;
    private readonly Dictionary<string, List<string>> _baseline;

    public BrowserPolicyManager(IPolicyRegistry registry, IBrowserPolicyBaselineStore baselines)
    {
        _registry = registry;
        _baselines = baselines;
        _baseline = baselines.Load().ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Applies the desired patterns. Returns true when a value changed.</summary>
    public bool Apply(string sid, IReadOnlyList<string> desired, bool removeAll = false)
    {
        var changed = false;
        foreach (var target in BrowserPolicyTargets.ForUser(sid))
        {
            var expected = removeAll ? Baseline(target) : Merge(Baseline(target), desired);
            changed |= WriteIfDifferent(target, expected);
        }
        SaveBaselines();
        return changed;
    }

    /// <summary>
    /// Re-applies the expected value when it was removed or edited. Returns true
    /// only when a repair actually happened, so the caller can raise the
    /// browser-policy-removed tamper alert.
    /// </summary>
    public bool VerifyAndRepair(string sid, IReadOnlyList<string> desired)
    {
        var repaired = false;
        foreach (var target in BrowserPolicyTargets.ForUser(sid))
        {
            var expected = Merge(Baseline(target), desired);
            var current = _registry.GetMultiString(target.PolicyKeyPath, BrowserPolicyRules.UrlBlocklistValue);
            if (Same(current, expected)) continue;
            if (expected.Count == 0)
                _registry.DeleteValue(target.PolicyKeyPath, BrowserPolicyRules.UrlBlocklistValue);
            else
                _registry.SetMultiString(target.PolicyKeyPath, BrowserPolicyRules.UrlBlocklistValue, expected);
            repaired = true;
        }
        return repaired;
    }

    private bool WriteIfDifferent(BrowserPolicyTarget target, IReadOnlyList<string> expected)
    {
        var current = _registry.GetMultiString(target.PolicyKeyPath, BrowserPolicyRules.UrlBlocklistValue);
        if (Same(current, expected)) return false;
        if (expected.Count == 0)
            _registry.DeleteValue(target.PolicyKeyPath, BrowserPolicyRules.UrlBlocklistValue);
        else
            _registry.SetMultiString(target.PolicyKeyPath, BrowserPolicyRules.UrlBlocklistValue, expected);
        return true;
    }

    private List<string> Baseline(BrowserPolicyTarget target)
    {
        var key = BaselineKey(target);
        if (_baseline.TryGetValue(key, out var existing)) return existing;
        var current = _registry.GetMultiString(target.PolicyKeyPath, BrowserPolicyRules.UrlBlocklistValue) ?? [];
        var copy = current.ToList();
        _baseline[key] = copy;
        return copy;
    }

    private void SaveBaselines() =>
        _baselines.Save(_baseline.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase));

    private static string BaselineKey(BrowserPolicyTarget target) =>
        target.PolicyKeyPath + "|" + BrowserPolicyRules.UrlBlocklistValue;

    private static IReadOnlyList<string> Merge(IReadOnlyList<string> baseline, IReadOnlyList<string> desired)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<string>();
        foreach (var value in baseline.Concat(desired))
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (seen.Add(value)) merged.Add(value);
        }
        return merged;
    }

    private static bool Same(IReadOnlyList<string>? left, IReadOnlyList<string> right)
    {
        if (left is null) return right.Count == 0;
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
            if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
