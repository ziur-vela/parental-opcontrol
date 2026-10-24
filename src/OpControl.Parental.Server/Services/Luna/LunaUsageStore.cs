// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services.Luna;

public sealed class LunaUsageStore
{
    public const decimal PromptUsdPerMillion = 0.15m;
    public const decimal CompletionUsdPerMillion = 0.60m;

    private readonly ParentalDbContext _db;

    public LunaUsageStore(ParentalDbContext db) => _db = db;

    public static TimeZoneInfo ResolveZone(string? id) => TimeZoneResolver.Resolve(id);

    public static DateOnly LocalDate(DateTime utcNow, TimeZoneInfo tz) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz));

    public async Task<string?> FamilyTimeZoneIdAsync(Guid familyId, Guid childId, CancellationToken ct = default) =>
        await _db.Devices.AsNoTracking()
            .Where(d => d.FamilyId == familyId && d.ChildId == childId)
            .Select(d => d.TimeZoneId)
            .FirstOrDefaultAsync(ct)
        ?? await _db.Devices.AsNoTracking()
            .Where(d => d.FamilyId == familyId)
            .Select(d => d.TimeZoneId)
            .FirstOrDefaultAsync(ct);

    public async Task<LunaUsageDay> EnsureAsync(Guid familyId, Guid childId, DateOnly date, CancellationToken ct = default)
    {
        var row = await _db.LunaUsageDays.FirstOrDefaultAsync(u => u.ChildId == childId && u.Date == date, ct);
        if (row is not null) return row;
        row = new LunaUsageDay { FamilyId = familyId, ChildId = childId, Date = date };
        _db.LunaUsageDays.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public static int MessageLimit(ChildLunaSettings settings, LunaUsageDay usage) =>
        Math.Max(0, settings.DailyMessageLimit) + Math.Max(0, usage.ExtraAllowed);

    public static int MessagesLeft(ChildLunaSettings settings, LunaUsageDay usage) =>
        Math.Max(0, MessageLimit(settings, usage) - usage.Messages);

    public async Task<(int PromptTokens, int CompletionTokens)> MonthlyTokensAsync(Guid familyId, DateOnly today, CancellationToken ct = default)
    {
        var start = new DateOnly(today.Year, today.Month, 1);
        var rows = await _db.LunaUsageDays.AsNoTracking()
            .Where(u => u.FamilyId == familyId && u.Date >= start && u.Date <= today)
            .Select(u => new { u.PromptTokens, u.CompletionTokens })
            .ToListAsync(ct);
        return (rows.Sum(r => r.PromptTokens), rows.Sum(r => r.CompletionTokens));
    }

    public static decimal ApproxCost(int promptTokens, int completionTokens) =>
        Math.Round(promptTokens / 1_000_000m * PromptUsdPerMillion
            + completionTokens / 1_000_000m * CompletionUsdPerMillion, 4);
}
