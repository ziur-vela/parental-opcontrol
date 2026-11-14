// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.AspNetCore.SignalR;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Hubs;

namespace OpControl.Parental.Server.Services;

/// <summary>
/// Tracks how many dashboard viewers are watching each device. While at least
/// one parent has the Devices page open the device is told to report every 30 s;
/// otherwise it reports every 5 minutes.
/// </summary>
public sealed class DeviceMetricsFocusService
{
    private readonly IHubContext<ParentalHub> _hub;
    private readonly Dictionary<Guid, int> _counts = new();
    private readonly object _gate = new();

    public DeviceMetricsFocusService(IHubContext<ParentalHub> hub)
    {
        _hub = hub;
    }

    public bool IsFast(Guid deviceId)
    {
        lock (_gate) return _counts.TryGetValue(deviceId, out var count) && count > 0;
    }

    public async Task EnterAsync(IEnumerable<Guid> deviceIds)
    {
        foreach (var deviceId in Changed(deviceIds, +1))
            await SendAsync(deviceId, true);
    }

    public async Task LeaveAsync(IEnumerable<Guid> deviceIds)
    {
        foreach (var deviceId in Changed(deviceIds, -1))
            await SendAsync(deviceId, false);
    }

    /// <summary>Sends the current mode to a device that just connected.</summary>
    public Task SendCurrentAsync(Guid deviceId) => SendAsync(deviceId, IsFast(deviceId));

    private List<Guid> Changed(IEnumerable<Guid> deviceIds, int delta)
    {
        var changed = new List<Guid>();
        lock (_gate)
        {
            foreach (var deviceId in deviceIds.Distinct())
            {
                var was = _counts.TryGetValue(deviceId, out var current) && current > 0;
                var next = Math.Max(0, current + delta);
                if (next == 0) _counts.Remove(deviceId);
                else _counts[deviceId] = next;
                var now = next > 0;
                if (was != now) changed.Add(deviceId);
            }
        }
        return changed;
    }

    private Task SendAsync(Guid deviceId, bool fast) =>
        _hub.Clients.Group(DeviceGroups.Device(deviceId)).SendAsync(DeviceHubMethods.MetricsModeChanged, fast);
}
