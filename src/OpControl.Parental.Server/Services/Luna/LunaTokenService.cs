// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services.Luna;

public sealed record LunaTokenContext(
    Guid TokenId,
    Guid FamilyId,
    Guid ChildId,
    Guid DeviceId,
    DateTime ExpiresUtc);

public sealed class LunaTokenService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private readonly ParentalDbContext _db;

    public LunaTokenService(ParentalDbContext db) => _db = db;

    public async Task<(string Token, DateTime ExpiresUtc)> IssueAsync(Device device, CancellationToken ct = default)
    {
        var settings = await _db.ChildLunaSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ChildId == device.ChildId && s.FamilyId == device.FamilyId, ct);
        if (settings is not { Enabled: true })
            throw new InvalidOperationException(AppText.Get("Luna.Error.Disabled"));

        var setupValid = await _db.LunaFamilySettings.AsNoTracking()
            .AnyAsync(s => s.FamilyId == device.FamilyId && s.LastTestOk == true, ct);
        if (!setupValid)
            throw new InvalidOperationException(AppText.Get("Luna.Error.SetupNotTested"));

        var now = DateTime.UtcNow;
        var expired = await _db.LunaTokens
            .Where(t => t.ExpiresUtc < now || (t.DeviceId == device.Id && t.ExpiresUtc < now.AddMinutes(1)))
            .ToListAsync(ct);
        if (expired.Count > 0) _db.LunaTokens.RemoveRange(expired);

        var token = SecretToken.NewToken();
        var expires = now.Add(Lifetime);
        _db.LunaTokens.Add(new LunaToken
        {
            FamilyId = device.FamilyId,
            ChildId = device.ChildId,
            DeviceId = device.Id,
            TokenHash = SecretToken.Hash(token),
            CreatedUtc = now,
            ExpiresUtc = expires
        });
        await _db.SaveChangesAsync(ct);
        return (token, expires);
    }

    public async Task<LunaTokenContext?> ValidateAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512) return null;
        var hash = SecretToken.Hash(token);
        var now = DateTime.UtcNow;
        var row = await _db.LunaTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.ExpiresUtc > now, ct);
        if (row is null) return null;

        var device = await _db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == row.DeviceId && d.FamilyId == row.FamilyId && d.ChildId == row.ChildId && !d.IsRevoked, ct);
        if (device is null) return null;

        var familyActive = await _db.Families.AsNoTracking()
            .AnyAsync(f => f.Id == row.FamilyId && f.Status == FamilyStatus.Active, ct);
        if (!familyActive) return null;

        var enabled = await _db.ChildLunaSettings.AsNoTracking()
            .AnyAsync(s => s.ChildId == row.ChildId && s.FamilyId == row.FamilyId && s.Enabled, ct);
        if (!enabled) return null;

        return new LunaTokenContext(row.Id, row.FamilyId, row.ChildId, row.DeviceId, row.ExpiresUtc);
    }
}

/// <summary>Deletes expired Luna tokens so the table stays small.</summary>
public sealed class LunaTokenSweeper : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LunaTokenSweeper> _logger;

    public LunaTokenSweeper(IServiceScopeFactory scopes, ILogger<LunaTokenSweeper> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ParentalDbContext>();
                var expired = await db.LunaTokens.Where(t => t.ExpiresUtc < DateTime.UtcNow).ToListAsync(stoppingToken);
                if (expired.Count > 0)
                {
                    db.LunaTokens.RemoveRange(expired);
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Removed {Count} expired Luna tokens", expired.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Luna token sweep failed");
            }
        }
    }
}
