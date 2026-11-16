// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Server.Services;

/// <summary>
/// Operating-system and application background hosts that are not a child's
/// web attempt. These are still blocked and stored as visits, but they are
/// muted in the reports and never count toward "Repeated blocked attempts"
/// alerts or push notifications.
///
/// One list, easy to extend. A bare entry matches the host and its subdomains;
/// a "*." entry matches the suffix and any subdomain. Entries are based on the
/// blocked-query records from the family devices plus well-documented Microsoft
/// Edge/Windows background endpoints.
/// </summary>
public static class BackgroundHosts
{
    private static readonly string[] Patterns =
    [
        // Edge Experimentation and Configuration Service (the skype.com noise).
        "config.edge.skype.com",
        "*.edge.skype.com",

        // Edge configuration, experimentation, telemetry and updates.
        "edge.microsoft.com",
        "msedge.api.cdp.microsoft.com",
        "*.msedge.api.cdp.microsoft.com",
        "*.msedge.net",
        "*.edgeupdate.microsoft.com",
        "*.events.data.microsoft.com",
        "settings-win.data.microsoft.com",
        "watson.telemetry.microsoft.com",
        "*.telemetry.microsoft.com",

        // SmartScreen reputation and lookup endpoints.
        "smartscreen.microsoft.com",
        "*.smartscreen.microsoft.com",
        "*.smartscreen-prod.microsoft.com",

        // Windows Update, delivery optimisation and Store background traffic.
        "*.windowsupdate.com",
        "ctldl.windowsupdate.com",
        "*.download.windowsupdate.com",
        "*.update.microsoft.com",
        "*.delivery.mp.microsoft.com",
        "*.dl.delivery.mp.microsoft.com",
        "*.tlu.dl.delivery.mp.microsoft.com",
        "displaycatalog.mp.microsoft.com",
        "*.displaycatalog.mp.microsoft.com",
        "storeedgefd.dsx.mp.microsoft.com",

        // Defender definition updates and activation endpoints.
        "*.wdcp.microsoft.com",
        "*.activation.sls.microsoft.com",

        // Microsoft links, connectivity checks and notification channels.
        "go.microsoft.com",
        "*.go.microsoft.com",
        "*.msftconnecttest.com",
        "*.msftncsi.com",
        "time.windows.com",
        "time.nist.gov",
        "*.activity.windows.com",
        "*.wns.windows.com",
        "*.notify.windows.com"
    ];

    public static bool IsBackground(string? host)
    {
        var value = (host ?? "").Trim().TrimEnd('.').ToLowerInvariant();
        if (value.Length == 0) return false;
        foreach (var pattern in Patterns)
        {
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = pattern[2..];
                if (value.Equals(suffix, StringComparison.OrdinalIgnoreCase)
                    || value.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (value.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                || value.EndsWith("." + pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static IReadOnlyList<string> All => Patterns;
}
