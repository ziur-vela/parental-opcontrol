// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public static class TimeZoneResolver
{

    // The server runs Linux; .NET's display for a Windows id is close but not
    // the exact Windows label. These are the Windows display names the client
    // sees for the family zones in use (US/Mexico/Latin America plus common).
    private static readonly IReadOnlyDictionary<string, string> WindowsDisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["US Mountain Standard Time"] = "(UTC-07:00) Arizona",
            ["Pacific Standard Time"] = "(UTC-08:00) Pacific Time (US & Canada)",
            ["Mountain Standard Time"] = "(UTC-07:00) Mountain Time (US & Canada)",
            ["Central Standard Time"] = "(UTC-06:00) Central Time (US & Canada)",
            ["Eastern Standard Time"] = "(UTC-05:00) Eastern Time (US & Canada)",
            ["Mountain Standard Time (Mexico)"] = "(UTC-07:00) Chihuahua, La Paz, Mazatlán",
            ["Central Standard Time (Mexico)"] = "(UTC-06:00) Guadalajara, Mexico City, Monterrey",
            ["Central America Standard Time"] = "(UTC-06:00) Central America",
            ["SA Pacific Standard Time"] = "(UTC-05:00) Bogota, Lima, Quito, Rio Branco",
            ["Atlantic Standard Time"] = "(UTC-04:00) Atlantic Time (Canada)",
            ["Newfoundland Standard Time"] = "(UTC-03:30) Newfoundland",
            ["E. South America Standard Time"] = "(UTC-03:00) Brasilia",
            ["Argentina Standard Time"] = "(UTC-03:00) City of Buenos Aires",
            ["Pacific SA Standard Time"] = "(UTC-04:00) Santiago",
            ["Venezuela Standard Time"] = "(UTC-04:00) Caracas",
            ["Alaskan Standard Time"] = "(UTC-09:00) Alaska",
            ["Hawaiian Standard Time"] = "(UTC-10:00) Hawaii",
            ["GMT Standard Time"] = "(UTC+00:00) Dublin, Edinburgh, Lisbon, London",
            ["W. Europe Standard Time"] = "(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna",
            ["Romance Standard Time"] = "(UTC+01:00) Brussels, Copenhagen, Madrid, Paris",
            ["Arabian Standard Time"] = "(UTC+04:00) Abu Dhabi, Muscat",
            ["India Standard Time"] = "(UTC+05:30) Chennai, Kolkata, Mumbai, New Delhi",
            ["China Standard Time"] = "(UTC+08:00) Beijing, Chongqing, Hong Kong, Urumqi",
            ["Tokyo Standard Time"] = "(UTC+09:00) Osaka, Sapporo, Tokyo",
            ["AUS Eastern Standard Time"] = "(UTC+10:00) Canberra, Melbourne, Sydney",
            ["UTC"] = "(UTC+00:00) UTC"
        };
    public static TimeZoneInfo Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        if (OperatingSystem.IsWindows())
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(windowsId); } catch { }
            }
        }
        else
        {
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); } catch { }
            }
        }
        return TimeZoneInfo.Utc;
    }

    /// <summary>Converts a Windows zone id to IANA (e.g. US Mountain Standard Time -> America/Phoenix).</summary>
    public static string ToIanaId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "UTC";
        var value = id.Trim();
        if (string.Equals(value, "UTC", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Etc/UTC", StringComparison.OrdinalIgnoreCase))
            return "UTC";
        // Windows Mexico zones cover several IANA zones; pin the stable choice
        // so Linux hosts with different tzdata versions agree.
        switch (value)
        {
            case "US Mountain Standard Time": return "America/Phoenix";
            case "Mountain Standard Time (Mexico)": return "America/Chihuahua";
            case "Central Standard Time (Mexico)": return "America/Mexico_City";
        }
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(value, out var iana) && !string.IsNullOrWhiteSpace(iana))
            return iana;
        try { return TimeZoneInfo.FindSystemTimeZoneById(value).Id; }
        catch { return "UTC"; }
    }

    /// <summary>The Windows/system display name for a reported id, used by the Automatic option.</summary>
    public static string DisplayName(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "UTC";
        var value = id.Trim();
        if (WindowsDisplayNames.TryGetValue(value, out var name)) return name;
        try { return TimeZoneInfo.FindSystemTimeZoneById(value).DisplayName; }
        catch { }
        var iana = ToIanaId(value);
        try { return TimeZoneInfo.FindSystemTimeZoneById(iana).DisplayName; }
        catch { return value; }
    }
}
