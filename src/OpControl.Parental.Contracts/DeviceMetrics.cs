// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public enum BatteryPowerState
{
    NoBattery = 0,
    OnBattery = 1,
    Charging = 2,
    PluggedInFull = 3
}

/// <summary>One lightweight system-resource sample reported by a device.</summary>
public sealed record DeviceMetricsDto(
    int CpuPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    long DiskFreeBytes,
    long DiskTotalBytes,
    int? BatteryPercent,
    BatteryPowerState BatteryState,
    long UptimeSeconds,
    DateTime CollectedUtc);

public enum DeviceWarningKind
{
    Cpu = 0,
    Ram = 1,
    Disk = 2,
    Battery = 3
}

public sealed record DeviceWarningDto(
    Guid Id,
    DeviceWarningKind Kind,
    DateTime StartedUtc,
    DateTime? EndedUtc,
    double PeakValue,
    double Threshold,
    int? DurationSeconds);

public static class DeviceMetricsLimits
{
    public const int CpuPercent = 90;
    public const int RamPercent = 90;
    public const int DiskFreePercent = 10;
    public const int BatteryPercent = 10;
    public static readonly TimeSpan SustainedFor = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan WarningRetention = TimeSpan.FromDays(30);
}

public enum DeviceMetricsState
{
    Offline = 0,
    Waiting = 1,
    Ready = 2
}

public static class DeviceMetricsMath
{
    public static int Percent(long used, long total) =>
        total <= 0 ? 0 : (int)Math.Clamp(Math.Round(used * 100.0 / total), 0, 100);

    public static int RamPercent(long usedBytes, long totalBytes) => Percent(usedBytes, totalBytes);

    public static int DiskUsedPercent(long freeBytes, long totalBytes) =>
        Percent(Math.Max(0, totalBytes - freeBytes), totalBytes);

    public static int DiskFreePercent(long freeBytes, long totalBytes) =>
        totalBytes <= 0 ? 0 : (int)Math.Clamp(Math.Round(freeBytes * 100.0 / totalBytes), 0, 100);

    /// <summary>
    /// Total CPU usage from two cumulative GetSystemTimes samples. The kernel
    /// time includes idle time, so total = kernel + user.
    /// </summary>
    public static int CpuPercent(long previousIdle, long previousKernel, long previousUser, long idle, long kernel, long user)
    {
        var idleDelta = idle - previousIdle;
        var totalDelta = (kernel - previousKernel) + (user - previousUser);
        if (totalDelta <= 0) return 0;
        var busy = totalDelta - Math.Max(0, idleDelta);
        return (int)Math.Clamp(Math.Round(busy * 100.0 / totalDelta), 0, 100);
    }

    /// <summary>Maps GetSystemPowerStatus flags to a battery state, including no-battery fallbacks.</summary>
    public static BatteryPowerState BatteryState(byte acLineStatus, byte batteryFlag, byte batteryLifePercent)
    {
        // 128 = no system battery, 255 = unknown on either field.
        if (batteryFlag == 128 || batteryFlag == 255 || batteryLifePercent == 255)
            return BatteryPowerState.NoBattery;
        if (acLineStatus == 0)
            return BatteryPowerState.OnBattery;
        return (batteryFlag & 8) != 0 ? BatteryPowerState.Charging : BatteryPowerState.PluggedInFull;
    }

    public static DeviceMetricsState State(bool online, bool hasMetrics) =>
        !online ? DeviceMetricsState.Offline : hasMetrics ? DeviceMetricsState.Ready : DeviceMetricsState.Waiting;
}

public enum DeviceWarningEventType
{
    Started = 0,
    Updated = 1,
    Ended = 2
}

public sealed record DeviceWarningEvent(
    DeviceWarningKind Kind,
    DeviceWarningEventType Type,
    DateTime StartedUtc,
    DateTime OccurredUtc,
    double ExtremeValue,
    double Threshold,
    int? DurationSeconds);

/// <summary>
/// Pure episode detector for device resource thresholds. An episode starts once
/// (after the sustained window where required) and ends once; peak values are
/// tracked while it is active. No database access.
/// </summary>
public sealed class DeviceWarningEvaluator
{
    private sealed class Episode
    {
        public DateTime StartUtc;
        public double Extreme;
        public double Threshold;
        public bool Committed;
    }

    private readonly Dictionary<(Guid DeviceId, DeviceWarningKind Kind), Episode> _episodes = new();
    private readonly object _gate = new();

    public void Seed(Guid deviceId, DeviceWarningKind kind, DateTime startedUtc, double extremeValue, double threshold)
    {
        lock (_gate)
        {
            _episodes[(deviceId, kind)] = new Episode
            {
                StartUtc = startedUtc,
                Extreme = extremeValue,
                Threshold = threshold,
                Committed = true
            };
        }
    }

    public bool HasEpisode(Guid deviceId, DeviceWarningKind kind)
    {
        lock (_gate) return _episodes.ContainsKey((deviceId, kind));
    }

    public IReadOnlyList<DeviceWarningEvent> Evaluate(Guid deviceId, DeviceMetricsDto metrics, DateTime nowUtc)
    {
        var events = new List<DeviceWarningEvent>();
        lock (_gate)
        {
            var ramPercent = DeviceMetricsMath.RamPercent(metrics.RamUsedBytes, metrics.RamTotalBytes);
            var diskFreePercent = DeviceMetricsMath.DiskFreePercent(metrics.DiskFreeBytes, metrics.DiskTotalBytes);
            var batteryActive = metrics.BatteryState == BatteryPowerState.OnBattery
                && metrics.BatteryPercent is { } battery
                && battery <= DeviceMetricsLimits.BatteryPercent;

            EvaluateOne(deviceId, DeviceWarningKind.Cpu, metrics.CpuPercent >= DeviceMetricsLimits.CpuPercent,
                metrics.CpuPercent, DeviceMetricsLimits.CpuPercent, higherIsWorse: true, sustained: true, nowUtc, events);
            EvaluateOne(deviceId, DeviceWarningKind.Ram, ramPercent >= DeviceMetricsLimits.RamPercent,
                ramPercent, DeviceMetricsLimits.RamPercent, higherIsWorse: true, sustained: true, nowUtc, events);
            EvaluateOne(deviceId, DeviceWarningKind.Disk, diskFreePercent < DeviceMetricsLimits.DiskFreePercent,
                diskFreePercent, DeviceMetricsLimits.DiskFreePercent, higherIsWorse: false, sustained: false, nowUtc, events);
            EvaluateOne(deviceId, DeviceWarningKind.Battery, batteryActive,
                metrics.BatteryPercent ?? 100, DeviceMetricsLimits.BatteryPercent, higherIsWorse: false, sustained: false, nowUtc, events);
        }
        return events;
    }

    private void EvaluateOne(Guid deviceId, DeviceWarningKind kind, bool active, double value, double threshold,
        bool higherIsWorse, bool sustained, DateTime nowUtc, List<DeviceWarningEvent> events)
    {
        var key = (deviceId, kind);
        if (active)
        {
            if (!_episodes.TryGetValue(key, out var episode))
            {
                episode = new Episode
                {
                    StartUtc = nowUtc,
                    Extreme = value,
                    Threshold = threshold,
                    Committed = !sustained
                };
                _episodes[key] = episode;
                if (episode.Committed)
                    events.Add(new DeviceWarningEvent(kind, DeviceWarningEventType.Started, nowUtc, nowUtc, value, threshold, null));
                return;
            }

            var improved = higherIsWorse ? value > episode.Extreme : value < episode.Extreme;
            if (improved)
            {
                episode.Extreme = value;
                if (episode.Committed)
                    events.Add(new DeviceWarningEvent(kind, DeviceWarningEventType.Updated, episode.StartUtc, nowUtc, value, threshold, null));
            }

            if (!episode.Committed && nowUtc - episode.StartUtc >= DeviceMetricsLimits.SustainedFor)
            {
                episode.Committed = true;
                events.Add(new DeviceWarningEvent(kind, DeviceWarningEventType.Started, episode.StartUtc, nowUtc, episode.Extreme, threshold, null));
            }
            return;
        }

        if (!_episodes.Remove(key, out var ending)) return;
        if (!ending.Committed) return;
        var duration = (int)Math.Max(0, Math.Round((nowUtc - ending.StartUtc).TotalSeconds));
        events.Add(new DeviceWarningEvent(kind, DeviceWarningEventType.Ended, ending.StartUtc, nowUtc, ending.Extreme, ending.Threshold, duration));
    }
}

public static class DeviceWarningRetention
{
    public static DateTime Cutoff(DateTime nowUtc) => nowUtc - DeviceMetricsLimits.WarningRetention;

    public static bool IsExpired(DateTime startedUtc, DateTime nowUtc) => startedUtc < Cutoff(nowUtc);
}
