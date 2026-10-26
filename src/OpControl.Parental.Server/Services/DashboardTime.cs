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

    public static async Task<TimeZoneInfo> FamilyZoneAsync(
        ParentalDbContext db, Guid familyId, CancellationToken ct = default)
    {
        var id = await db.Devices.AsNoTracking()
            .Where(d => d.FamilyId == familyId)
            .Select(d => d.TimeZoneId)
            .FirstOrDefaultAsync(ct);
        return Resolve(id);
    }
}
