// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services;

/// <summary>Stores the latest sample and maintains peak-warning episodes.</summary>
public sealed class DeviceMetricsService
{
    private readonly ParentalDbContext _db;
    private readonly DeviceWarningEvaluator _evaluator;
    private readonly AlertService _alerts;
    private readonly DashboardEventBus _events;
    private readonly ILogger<DeviceMetricsService> _logger;

    public DeviceMetricsService(ParentalDbContext db, DeviceWarningEvaluator evaluator, AlertService alerts,
        DashboardEventBus events, ILogger<DeviceMetricsService> logger)
    {
        _db = db;
        _evaluator = evaluator;
        _alerts = alerts;
        _events = events;
        _logger = logger;
    }

    public async Task RecordAsync(Guid deviceId, DeviceMetricsDto metrics, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsRevoked, ct);
        if (device is null) return;

        device.MetricsCpuPercent = Math.Clamp(metrics.CpuPercent, 0, 100);
        device.MetricsRamUsedBytes = Math.Max(0, metrics.RamUsedBytes);
        device.MetricsRamTotalBytes = Math.Max(0, metrics.RamTotalBytes);
        device.MetricsDiskFreeBytes = Math.Max(0, metrics.DiskFreeBytes);
        device.MetricsDiskTotalBytes = Math.Max(0, metrics.DiskTotalBytes);
        device.MetricsBatteryPercent = metrics.BatteryPercent is { } percent ? Math.Clamp(percent, 0, 100) : null;
        device.MetricsBatteryState = metrics.BatteryState;
        device.MetricsUptimeSeconds = Math.Max(0, metrics.UptimeSeconds);
        device.MetricsUpdatedUtc = now;

        var active = await _db.DeviceWarnings
            .Where(w => w.DeviceId == deviceId && w.EndedUtc == null)
            .ToListAsync(ct);
        foreach (var row in active)
            _evaluator.Seed(deviceId, row.Kind, row.StartedUtc, row.PeakValue, row.Threshold);

        var events = _evaluator.Evaluate(deviceId, metrics, now);
        foreach (var warning in events)
        {
            var row = active.FirstOrDefault(w => w.Kind == warning.Kind);
            if (warning.Type == DeviceWarningEventType.Started && row is null)
            {
                row = new DeviceWarning
                {
                    FamilyId = device.FamilyId,
                    ChildId = device.ChildId,
                    DeviceId = device.Id,
                    Kind = warning.Kind,
                    StartedUtc = warning.StartedUtc,
                    PeakValue = warning.ExtremeValue,
                    Threshold = warning.Threshold,
                    CreatedUtc = now
                };
                _db.DeviceWarnings.Add(row);
                active.Add(row);
                await MaybeAlertAsync(device, warning, ct);
            }
            else if (row is not null && warning.Type is DeviceWarningEventType.Started or DeviceWarningEventType.Updated)
            {
                row.PeakValue = Worst(row.PeakValue, warning.ExtremeValue, warning.Kind);
            }
            else if (row is not null && warning.Type == DeviceWarningEventType.Ended)
            {
                row.EndedUtc = warning.OccurredUtc;
                row.DurationSeconds = warning.DurationSeconds;
                row.PeakValue = Worst(row.PeakValue, warning.ExtremeValue, warning.Kind);
                active.Remove(row);
            }
        }

        var cutoff = DeviceWarningRetention.Cutoff(now);
        var expired = await _db.DeviceWarnings
            .Where(w => w.DeviceId == deviceId && w.StartedUtc < cutoff)
            .ToListAsync(ct);
        if (expired.Count > 0) _db.DeviceWarnings.RemoveRange(expired);

        await _db.SaveChangesAsync(ct);
        _events.PublishDeviceMetrics(deviceId);
    }

    private async Task MaybeAlertAsync(Device device, DeviceWarningEvent warning, CancellationToken ct)
    {
        try
        {
            var settings = await _db.FamilySettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.FamilyId == device.FamilyId, ct);
            if (settings is null || !settings.AlertOnDeviceWarnings) return;
            var value = Math.Round(warning.ExtremeValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
            await _alerts.AddAsync(device.FamilyId, device.ChildId, device.Id, "device-warning",
                new AlertContent("Alert.DeviceWarning.Title", "Alert.DeviceWarning.Detail",
                    device.Name, warning.Kind.ToString(), value, warning.Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                TimeSpan.FromMinutes(1), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Device-warning alert failed for {DeviceId}", device.Id);
        }
    }

    /// <summary>Peak means the worst sample: highest for CPU/RAM/disk use, lowest for battery/disk free.</summary>
    public static double Worst(double current, double candidate, DeviceWarningKind kind) =>
        kind is DeviceWarningKind.Battery or DeviceWarningKind.Disk
            ? Math.Min(current, candidate)
            : Math.Max(current, candidate);
}

/// <summary>Localized names for the warning kinds.</summary>
public static class DeviceWarningText
{
    public static string Kind(DeviceWarningKind kind, string? language = null) => kind switch
    {
        DeviceWarningKind.Cpu => AppText.Get("Devices.WarningCpu", language),
        DeviceWarningKind.Ram => AppText.Get("Devices.WarningRam", language),
        DeviceWarningKind.Disk => AppText.Get("Devices.WarningDisk", language),
        DeviceWarningKind.Battery => AppText.Get("Devices.WarningBattery", language),
        _ => kind.ToString()
    };

    public static string Kind(string key, string? language = null) =>
        Enum.TryParse<DeviceWarningKind>(key, true, out var kind)
            ? Kind(kind, language)
            : key;
}
