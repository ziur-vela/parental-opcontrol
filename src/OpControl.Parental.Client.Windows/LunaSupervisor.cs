// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;

namespace OpControl.Parental.Client;

/// <summary>
/// Starts and stops the Luna app in the monitored child's session.
/// Luna is off by default and only runs while the server says it is enabled
/// and the device is unlocked. Restarts after a crash are rate-limited.
/// </summary>
public sealed class LunaSupervisor : BackgroundService
{
    private readonly ClientSettings _settings;
    private readonly LockRuntime _runtime;
    private readonly ILogger<LunaSupervisor> _logger;
    private DateTime _lastLaunchUtc = DateTime.MinValue;
    private DateTime _backoffUntilUtc = DateTime.MinValue;
    private DateTime _lastStopUtc = DateTime.MinValue;
    private readonly Queue<DateTime> _recentStops = new();
    private bool _wasRunning;

    public LunaSupervisor(ClientSettings settings, LockRuntime runtime, ILogger<LunaSupervisor> logger)
    {
        _settings = settings;
        _runtime = runtime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { Tick(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Luna supervisor failed"); }
        }
    }

    private void Tick()
    {
        var options = _settings.Value;
        var session = WindowsSession.GetMonitoredSessionId(options.MonitoredAccount);
        if (session == 0)
        {
            _wasRunning = false;
            return;
        }

        var enabled = _runtime.DeviceState?.LunaEnabled == true;
        var locked = _runtime.Current?.IsLocked == true;
        var shouldRun = LunaLaunchPolicy.ShouldRun(enabled, locked);
        var running = WindowsSession.IsLunaRunning(session);

        if (!shouldRun)
        {
            if (running)
            {
                WindowsSession.StopLuna(session, _logger);
                _lastStopUtc = DateTime.UtcNow;
            }
            _wasRunning = false;
            return;
        }

        var now = DateTime.UtcNow;
        if (_wasRunning && !running)
        {
            _recentStops.Enqueue(now);
            while (_recentStops.Count > 0 && now - _recentStops.Peek() > TimeSpan.FromMinutes(10))
                _recentStops.Dequeue();
            if (_recentStops.Count >= 3)
            {
                _backoffUntilUtc = now.AddMinutes(5);
                _logger.LogWarning("Luna stopped {Count} times in 10 minutes; backing off", _recentStops.Count);
            }
            else
            {
                _backoffUntilUtc = now.AddSeconds(20);
            }
        }
        _wasRunning = running;

        if (running || now < _backoffUntilUtc || now - _lastLaunchUtc < TimeSpan.FromSeconds(10))
            return;

        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Luna", "OpControl.Parental.Luna.exe"));
        if (WindowsSession.LaunchLuna(session, path, _logger))
        {
            _lastLaunchUtc = now;
            _wasRunning = true;
        }
        else
        {
            _backoffUntilUtc = now.AddSeconds(30);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        var session = WindowsSession.GetMonitoredSessionId(_settings.Value.MonitoredAccount);
        if (session != 0) WindowsSession.StopLuna(session, _logger);
        return base.StopAsync(cancellationToken);
    }
}
