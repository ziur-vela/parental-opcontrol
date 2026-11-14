// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Hubs;

namespace OpControl.Parental.Server.Services;

public sealed class DevicePowerException : Exception
{
    public DevicePowerException(string message) : base(message) { }
}

/// <summary>Schedules, cancels and records parent-requested power actions.</summary>
public sealed class DevicePowerService
{
    private readonly ParentalDbContext _db;
    private readonly IHubContext<ParentalHub> _hub;
    private readonly DevicePresenceService _presence;
    private readonly DashboardEventBus _events;
    private readonly ILogger<DevicePowerService> _logger;

    public DevicePowerService(ParentalDbContext db, IHubContext<ParentalHub> hub, DevicePresenceService presence,
        DashboardEventBus events, ILogger<DevicePowerService> logger)
    {
        _db = db;
        _hub = hub;
        _presence = presence;
        _events = events;
        _logger = logger;
    }

    public async Task<DevicePowerCommand> RequestAsync(Guid familyId, Guid deviceId, Guid parentId, string parentName,
        DevicePowerAction action, int delaySeconds, CancellationToken ct = default)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && d.FamilyId == familyId && !d.IsRevoked, ct)
            ?? throw new DevicePowerException(AppText.Get("Error.PowerNotFound"));
        if (device.Platform != DevicePlatform.Windows)
            throw new DevicePowerException(AppText.Get("Error.PowerWindowsOnly"));
        if (!DevicePowerRules.CanRequest(_presence.IsOnline(device.Id), device.Platform))
            throw new DevicePowerException(AppText.Get("Error.PowerOffline"));

        var now = DateTime.UtcNow;
        var existing = await _db.DevicePowerCommands
            .Where(c => c.DeviceId == device.Id
                && (c.Status == DevicePowerStatus.Pending || c.Status == DevicePowerStatus.Accepted)
                && c.RunAtUtc > now)
            .OrderByDescending(c => c.CreatedUtc)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            throw new DevicePowerException(AppText.Get("Error.PowerPending"));

        var seconds = action == DevicePowerAction.Sleep
            ? DevicePowerRules.SleepNoticeSeconds
            : DevicePowerRules.NormalizeDelay(delaySeconds);
        var command = new DevicePowerCommand
        {
            FamilyId = device.FamilyId,
            ChildId = device.ChildId,
            DeviceId = device.Id,
            Action = action,
            RunAtUtc = now.AddSeconds(seconds),
            NoticeSeconds = seconds,
            RequestedByParentId = parentId,
            RequestedByName = parentName,
            Status = DevicePowerStatus.Pending,
            CreatedUtc = now
        };
        _db.DevicePowerCommands.Add(command);
        await _db.SaveChangesAsync(ct);

        var payload = new DevicePowerCommandDto(command.Id, command.Action, command.RunAtUtc, command.NoticeSeconds);
        try
        {
            await _hub.Clients.Group(DeviceGroups.Device(device.Id))
                .SendAsync(DeviceHubMethods.PowerCommand, payload, ct);
        }
        catch (Exception ex)
        {
            command.Status = DevicePowerStatus.Failed;
            command.CompletedUtc = DateTime.UtcNow;
            command.ResultDetail = AppText.Get("Error.PowerSendFailed");
            await _db.SaveChangesAsync(ct);
            _events.PublishPowerChanged(device.Id);
            _logger.LogWarning(ex, "Power command {CommandId} could not be sent to {DeviceId}", command.Id, device.Id);
            throw new DevicePowerException(AppText.Get("Error.PowerSendFailed"));
        }

        _events.PublishPowerChanged(device.Id);
        _logger.LogInformation("Power command {Action} {CommandId} for {DeviceId} by {Parent} runs at {RunAt:u}",
            action, command.Id, device.Id, parentName, command.RunAtUtc);
        return command;
    }

    public async Task CancelAsync(Guid familyId, Guid deviceId, Guid commandId, Guid parentId, string parentName,
        CancellationToken ct = default)
    {
        var command = await _db.DevicePowerCommands
            .FirstOrDefaultAsync(c => c.Id == commandId && c.FamilyId == familyId && c.DeviceId == deviceId, ct)
            ?? throw new DevicePowerException(AppText.Get("Error.PowerNotFound"));
        if (!DevicePowerRules.CanCancel(command.Status, command.RunAtUtc, DateTime.UtcNow))
            throw new DevicePowerException(AppText.Get("Error.PowerCancelTooLate"));
        if (!_presence.IsOnline(deviceId))
            throw new DevicePowerException(AppText.Get("Error.PowerOffline"));

        try
        {
            await _hub.Clients.Group(DeviceGroups.Device(deviceId))
                .SendAsync(DeviceHubMethods.PowerCancel, commandId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Power cancel {CommandId} could not be sent to {DeviceId}", commandId, deviceId);
        }

        command.Status = DevicePowerStatus.Cancelled;
        command.CompletedUtc = DateTime.UtcNow;
        command.CancelledByParentId = parentId;
        command.CancelledByName = parentName;
        command.ResultDetail = AppText.Get("Power.CancelledBy", parentName);
        await _db.SaveChangesAsync(ct);
        _events.PublishPowerChanged(deviceId);
        _logger.LogInformation("Power command {CommandId} cancelled by {Parent}", commandId, parentName);
    }

    public async Task ApplyResultAsync(Guid deviceId, DevicePowerResultDto result, CancellationToken ct = default)
    {
        var command = await _db.DevicePowerCommands
            .FirstOrDefaultAsync(c => c.Id == result.Id && c.DeviceId == deviceId, ct);
        if (command is null) return;

        var now = DateTime.UtcNow;
        if (result.Status == DevicePowerStatus.Accepted)
        {
            if (command.Status != DevicePowerStatus.Pending) return;
            command.Status = DevicePowerStatus.Accepted;
            command.AcceptedUtc = now;
        }
        else
        {
            command.Status = result.Status;
            command.CompletedUtc = now;
            if (!string.IsNullOrWhiteSpace(result.Detail))
                command.ResultDetail = result.Detail.Length > 500 ? result.Detail[..500] : result.Detail;
        }
        await _db.SaveChangesAsync(ct);
        _events.PublishPowerChanged(deviceId);
        _logger.LogInformation("Power command {CommandId} {Status}: {Detail}", command.Id, command.Status, command.ResultDetail ?? "");
    }

    /// <summary>Marks commands that never reached the device or never reported a result.</summary>
    public async Task<int> SweepAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var stale = await _db.DevicePowerCommands
            .Where(c => (c.Status == DevicePowerStatus.Pending && c.CreatedUtc < now.AddMinutes(-2))
                || (c.Status == DevicePowerStatus.Accepted && c.RunAtUtc < now.AddMinutes(-2)))
            .Take(100)
            .ToListAsync(ct);
        if (stale.Count == 0) return 0;
        foreach (var command in stale)
        {
            command.Status = DevicePowerStatus.Failed;
            command.CompletedUtc = now;
            command.ResultDetail = command.AcceptedUtc is null
                ? AppText.Get("Power.NoResponse")
                : AppText.Get("Power.NoResult");
        }
        await _db.SaveChangesAsync(ct);
        foreach (var deviceId in stale.Select(c => c.DeviceId).Distinct())
            _events.PublishPowerChanged(deviceId);
        return stale.Count;
    }
}

public sealed class DevicePowerSweeper : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DevicePowerSweeper> _logger;

    public DevicePowerSweeper(IServiceScopeFactory scopes, ILogger<DevicePowerSweeper> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DevicePowerService>().SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Power command sweep failed");
            }
        }
    }
}
