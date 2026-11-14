// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Globalization;

namespace OpControl.Parental.Server.Services;

public enum AboutStatusKind
{
    UpToDate = 0,
    UpdateAvailable = 1
}

public sealed record AboutStatus(AboutStatusKind Kind, bool Offline);

public static class VersionDisplay
{
    public static string VersionLabel(string? version)
    {
        var trimmed = (version ?? "").Trim().TrimStart('v', 'V');
        return string.IsNullOrWhiteSpace(trimmed) ? "" : $"v{trimmed}";
    }

    public static string BuildDate(string? buildDate, string? language = null)
    {
        if (DateTime.TryParse(buildDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date.ToString("d MMM yyyy", AppCulture.Culture(language));
        return buildDate ?? "";
    }

    public static string BuildLine(string? buildDate, string? shortCommit, string? language = null)
    {
        var date = BuildDate(buildDate, language);
        return string.IsNullOrWhiteSpace(shortCommit) ? date : $"{date} · {shortCommit}";
    }

    public static string LastSeen(DateTime? lastSeenUtc, TimeZoneInfo timeZone, string todayLabel, string yesterdayLabel,
        string? language = null, DateTime? nowUtc = null)
    {
        if (lastSeenUtc is not { } seen) return "";
        var now = nowUtc ?? DateTime.UtcNow;
        var local = DashboardTime.Convert(seen, timeZone);
        var today = DashboardTime.Convert(now, timeZone).Date;
        if (local.Date == today) return todayLabel;
        if (local.Date == today.AddDays(-1)) return yesterdayLabel;
        var format = local.Year == today.Year ? "d MMM" : "d MMM yyyy";
        return local.ToString(format, AppCulture.Culture(language));
    }

    public static AboutStatus Status(string? clientVersion, string? latestVersion, bool online)
    {
        var kind = string.IsNullOrWhiteSpace(clientVersion)
            ? AboutStatusKind.UpdateAvailable
            : string.IsNullOrWhiteSpace(latestVersion)
                ? AboutStatusKind.UpToDate
                : ReleaseCatalog.ParseVersion(clientVersion) >= ReleaseCatalog.ParseVersion(latestVersion)
                    ? AboutStatusKind.UpToDate
                    : AboutStatusKind.UpdateAvailable;
        return new AboutStatus(kind, !online);
    }
}
