// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services;

/// <summary>
/// Browsers that do not honor the user-level Chrome/Edge/Brave policy, or that
/// are commonly run portable. The dashboard can add normal blocked App rules
/// for the ones it finds installed. Nothing is blocked until a parent clicks.
/// </summary>
public static class UnsupportedBrowsers
{
    public sealed record Candidate(string Identity, string DisplayName);

    private static readonly string[] Supported = ["chrome", "microsoft edge", "msedge", "brave"];

    private static readonly (string Token, string Label)[] Known =
    [
        ("firefox", "Firefox"),
        ("librewolf", "LibreWolf"),
        ("waterfox", "Waterfox"),
        ("tor browser", "Tor Browser"),
        ("opera", "Opera / Opera GX"),
        ("vivaldi", "Vivaldi"),
        ("chromium", "Chromium / Ungoogled Chromium"),
        ("thorium", "Thorium"),
        ("catsxp", "Catsxp"),
        ("yandex", "Yandex Browser"),
        ("maxthon", "Maxthon"),
        ("pale moon", "Pale Moon"),
        ("seamonkey", "SeaMonkey"),
        ("falkon", "Falkon"),
        ("midori", "Midori"),
        ("slimjet", "Slimjet"),
        ("srware iron", "SRWare Iron"),
        ("comodo dragon", "Comodo Dragon"),
        ("epic privacy", "Epic Privacy Browser"),
        ("avast secure", "Avast Secure Browser"),
        ("avg secure", "AVG Secure Browser"),
        ("360 secure", "360 Secure Browser"),
        ("qq browser", "QQ Browser"),
        ("uc browser", "UC Browser"),
        ("duckduckgo", "DuckDuckGo Browser"),
        ("zen browser", "Zen Browser"),
        ("floorp", "Floorp"),
        ("mullvad", "Mullvad Browser"),
        ("k-meleon", "K-Meleon"),
        ("otter browser", "Otter Browser"),
        ("basilisk", "Basilisk"),
        ("icecat", "GNU IceCat")
    ];

    /// <summary>The common unsupported or portable browsers this helper covers.</summary>
    public static IReadOnlyList<string> Labels => Known.Select(k => k.Label).ToArray();

    public static IReadOnlyList<Candidate> Find(IEnumerable<InstalledApp> apps)
    {
        var found = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in apps)
        {
            if (app.RemovedUtc is not null) continue;
            var name = (app.DisplayName ?? "").ToLowerInvariant();
            var identity = (app.Identity ?? "").ToLowerInvariant();
            if (name.Length == 0) continue;
            if (Supported.Any(s => name.Contains(s, StringComparison.Ordinal) || identity.Contains(s, StringComparison.Ordinal)))
                continue;
            if (name.Contains("opcontrol", StringComparison.Ordinal) || name.Contains("luna", StringComparison.Ordinal))
                continue;

            var matched = false;
            foreach (var known in Known)
            {
                if (name.Contains(known.Token, StringComparison.Ordinal)
                    || identity.Contains(known.Token.Replace(" ", ""), StringComparison.Ordinal))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched) continue;
            var appIdentity = app.Identity ?? "";
            var appDisplay = app.DisplayName ?? "";
            if (appIdentity.Length == 0) continue;
            found.TryAdd(appIdentity, new Candidate(appIdentity, appDisplay));
        }
        return found.Values.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
