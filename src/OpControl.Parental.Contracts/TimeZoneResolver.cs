// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public static class TimeZoneResolver
{
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
        try { return TimeZoneInfo.FindSystemTimeZoneById(value).DisplayName; }
        catch { }
        var iana = ToIanaId(value);
        try { return TimeZoneInfo.FindSystemTimeZoneById(iana).DisplayName; }
        catch { return value; }
    }
}
