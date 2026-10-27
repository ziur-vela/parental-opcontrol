// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services;

/// <summary>One place for dashboard time conversion so pages do not fall back to UTC.</summary>
public static class DashboardTime
{
    public static TimeZoneInfo Resolve(string? timeZoneId) => TimeZoneResolver.Resolve(timeZoneId);

    public static DateTime Convert(DateTime utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);

    public static DateTime Convert(DateTime utc, string? timeZoneId) =>
        Convert(utc, Resolve(timeZoneId));

    public static string Format(DateTime utc, TimeZoneInfo timeZone) =>
        Convert(utc, timeZone).ToString("d MMM, HH:mm", CultureInfo.CurrentCulture);

    public static string Format(DateTime utc, string? timeZoneId) =>
        Format(utc, Resolve(timeZoneId));

    public sealed record FamilyZoneInfo(
        TimeZoneInfo Zone,
        string? ExplicitId,
        Guid? DeviceId,
        string? DeviceName,
        bool Automatic);

    /// <summary>
    /// Explicit family setting when present; otherwise the time zone of the
    /// family device that was seen most recently (online or last seen).
    /// </summary>
    public static async Task<FamilyZoneInfo> FamilyZoneInfoAsync(
        ParentalDbContext db, Guid familyId, CancellationToken ct = default)
    {
        var explicitId = await db.FamilySettings.AsNoTracking()
            .Where(s => s.FamilyId == familyId)
            .Select(s => s.TimeZoneId)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(explicitId))
            return new FamilyZoneInfo(Resolve(explicitId), explicitId, null, null, false);

        var device = await db.Devices.AsNoTracking()
            .Where(d => d.FamilyId == familyId)
            .OrderByDescending(d => d.LastSeenUtc ?? DateTime.MinValue)
            .Select(d => new { d.Id, d.Name, d.TimeZoneId })
            .FirstOrDefaultAsync(ct);
        return new FamilyZoneInfo(Resolve(device?.TimeZoneId), null, device?.Id, device?.Name, true);
    }

    public static async Task<TimeZoneInfo> FamilyZoneAsync(
        ParentalDbContext db, Guid familyId, CancellationToken ct = default) =>
        (await FamilyZoneInfoAsync(db, familyId, ct)).Zone;

    public static async Task<string?> FamilyZoneIdAsync(
        ParentalDbContext db, Guid familyId, CancellationToken ct = default) =>
        (await FamilyZoneInfoAsync(db, familyId, ct)).Zone.Id;
}
