// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services;

/// <summary>
/// A device reports its Windows/IANA zone; the effective zone stays automatic
/// unless a parent explicitly overrides it on the device card.
/// </summary>
public static class DeviceTimeZonePolicy
{
    public static void ApplyReported(Device device, string? reportedId)
    {
        if (string.IsNullOrWhiteSpace(reportedId)) return;
        var value = reportedId.Trim();
        if (value.Length > 80) value = value[..80];
        device.ReportedTimeZoneId = value;
        if (string.IsNullOrWhiteSpace(device.TimeZoneOverrideId))
            device.TimeZoneId = TimeZoneResolver.ToIanaId(value);
    }

    public static void ApplyOverride(Device device, string? overrideId)
    {
        if (string.IsNullOrWhiteSpace(overrideId) || string.Equals(overrideId, "auto", StringComparison.OrdinalIgnoreCase))
        {
            device.TimeZoneOverrideId = null;
            device.TimeZoneId = TimeZoneResolver.ToIanaId(device.ReportedTimeZoneId ?? device.TimeZoneId);
            return;
        }

        var zone = TimeZoneResolver.Resolve(overrideId);
        device.TimeZoneOverrideId = zone.Id;
        device.TimeZoneId = zone.Id;
    }
}
