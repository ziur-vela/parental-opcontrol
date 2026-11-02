// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Client;

public sealed class ClientSettings
{
    public ClientOptions Value { get; set; } = new();
}

public sealed class LockRuntime
{
    private readonly object _gate = new();
    private readonly DeviceStateStore _store;
    private readonly StateStore _writer;
    private readonly ClientSettings _settings;
    private readonly ILogger<LockRuntime> _logger;
    private DeviceStateDto? _state;
    private RuntimeLockStateDto? _runtime;
    private string _policyFingerprint = "";
    private int _serverUsedSeconds;
    private int _localActiveSeconds;
    private readonly HashSet<Guid> _appliedUsageIds = [];
    private readonly Dictionary<string, int> _localAppSeconds = new(StringComparer.OrdinalIgnoreCase);

    public LockRuntime(DeviceStateStore store, StateStore writer, ClientSettings settings, ILogger<LockRuntime> logger)
    {
        _store = store;
        _writer = writer;
        _settings = settings;
        _logger = logger;
    }

    public DeviceStateDto? DeviceState
    {
        get { lock (_gate) return _state; }
    }

    public RuntimeLockStateDto? Current
    {
        get { lock (_gate) return _runtime; }
    }

    public void Initialize(DeviceStateDto state)
    {
        lock (_gate)
        {
            _state = state;
            _serverUsedSeconds = state.DailyLimit?.UsedTodaySeconds ?? 0;
            _localActiveSeconds = 0;
            _appliedUsageIds.Clear();
            ComputeAndWriteLocked();
        }
    }

    public void ApplyServerState(DeviceStateDto state)
    {
        lock (_gate)
        {
            var fingerprint = PolicyFingerprint(state);
            var policyChanged = _policyFingerprint.Length > 0 && fingerprint != _policyFingerprint;
            _policyFingerprint = fingerprint;

            var previous = _state?.DailyLimit;
            var next = state.DailyLimit;
            var sameDay = previous is not null && next is not null && previous.DayStartUtc == next.DayStartUtc;
            var localUsed = _serverUsedSeconds + _localActiveSeconds;
            var serverUsed = next?.UsedTodaySeconds ?? 0;
            _localActiveSeconds = sameDay ? Math.Max(0, localUsed - serverUsed) : 0;
            _serverUsedSeconds = serverUsed;
            if (!sameDay) _appliedUsageIds.Clear();

            _state = state;
            _localAppSeconds.Clear();
            _store.Save(state);
            ComputeAndWriteLocked();
            if (policyChanged) DnsCache.Flush(_logger);
        }
    }

    private static string PolicyFingerprint(DeviceStateDto state)
    {
        var parts = state.WebsiteRules.Select(r => $"s|{r.Domain}|{r.IsBlocked}|{r.Mode}|{r.ScheduleId}")
            .Concat(state.AppRules.Select(r => $"a|{r.AppIdentity}|{r.IsBlocked}|{r.Mode}|{r.ScheduleId}"))
            .Concat(state.ItemBonuses.Select(b => $"b|{b.RuleType}|{b.Identity}|{b.UntilUtc.Ticks}"))
            .Concat(state.Categories.Select(c => $"c|{c.Key}|{c.Enabled}|{c.Version}"))
            .OrderBy(s => s, StringComparer.Ordinal);
        return string.Join('\n', parts);
    }

    public RuntimeLockStateDto? ApplyLocalOverride(string option, int? minutes)
    {
        lock (_gate)
        {
            if (_state is null) return null;
            var now = DateTime.UtcNow;
            if (option == "unlock-until-next")
            {
                _state = _state with
                {
                    ManualLock = false,
                    UnlockStartUtc = now,
                    UnlockUntilUtc = LockRules.NextLockCycleUtc(_state.Schedules, TimeZoneInfo.Local, now, _state.DailyLimit),
                    BonusStartUtc = null,
                    BonusUntilUtc = null,
                    BonusMinutes = null
                };
            }
            else if (option == "bonus")
            {
                var bonusMinutes = Math.Clamp(minutes ?? 15, 1, 1440);
                var (bonusStart, bonusEnd) = LockEngine.ExtendBonus(
                    _state.BonusStartUtc, _state.BonusUntilUtc, now, bonusMinutes);
                _state = _state with
                {
                    ManualLock = false,
                    UnlockStartUtc = null,
                    UnlockUntilUtc = null,
                    BonusStartUtc = bonusStart,
                    BonusUntilUtc = bonusEnd,
                    BonusMinutes = bonusMinutes
                };
            }
            _store.Save(_state);
            var runtime = ComputeAndWriteLocked();
            _logger.LogInformation("Local override {Option} applied: unlockUntil={UnlockUntilUtc} locked={Locked} reason={Reason}",
                option, _state.UnlockUntilUtc, runtime?.IsLocked, runtime?.Reason);
            return runtime;
        }
    }

    public void AddLocalUsage(Guid id, string identity, int seconds, bool alwaysAllowed, bool countsTowardLimit)
    {
        if (seconds <= 0) return;
        lock (_gate)
        {
            if (id != Guid.Empty && !_appliedUsageIds.Add(id)) return;
            if (countsTowardLimit) _localActiveSeconds += seconds;
            if (alwaysAllowed)
                _localAppSeconds.TryAdd(identity, 0);
            if (!string.IsNullOrWhiteSpace(identity))
                _localAppSeconds[identity] = _localAppSeconds.GetValueOrDefault(identity) + seconds;
        }
    }

    public bool IsAppBlocked(string identity, string? path, out string? reason)
    {
        lock (_gate)
        {
            reason = null;
            var state = AdjustedStateLocked();
            if (state is null) return false;
            return PolicyRules.IsAppBlocked(state, identity, path, TimeZoneInfo.Local, DateTime.UtcNow, out reason);
        }
    }

    private DeviceStateDto? AdjustedStateLocked()
    {
        if (_state is null) return null;
        var now = DateTime.UtcNow;
        var daily = _state.DailyLimit;
        while (daily is not null && now >= daily.DayEndUtc)
        {
            daily = daily with
            {
                TodayMinutes = daily.TomorrowMinutes ?? daily.TodayMinutes,
                TomorrowMinutes = daily.TomorrowMinutes ?? daily.TodayMinutes,
                UsedTodaySeconds = 0,
                DayStartUtc = daily.DayEndUtc,
                DayEndUtc = daily.DayEndUtc.AddDays(1)
            };
            _serverUsedSeconds = 0;
            _localActiveSeconds = 0;
            _appliedUsageIds.Clear();
        }
        if (daily is not null)
        {
            var used = Math.Max(_serverUsedSeconds + _localActiveSeconds, Math.Max(0, daily.UsedTodaySeconds));
            daily = daily with { UsedTodaySeconds = used };
        }

        var usage = (_state.AppUsage ?? []).ToList();
        foreach (var pair in _localAppSeconds)
        {
            var index = usage.FindIndex(u => u.Identity.Trim().Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                usage[index] = usage[index] with { UsedTodaySeconds = usage[index].UsedTodaySeconds + pair.Value };
            else
            {
                var rule = _state.AppRules.FirstOrDefault(r => PolicyRules.MatchesApp(r.AppIdentity, pair.Key, null));
                usage.Add(new AppDailyUsageDto(pair.Key, pair.Value, rule?.DailyLimitMinutes, rule?.AlwaysAllowed ?? false));
            }
        }

        return _state with { DailyLimit = daily, AppUsage = usage };
    }

    public RuntimeLockStateDto? ComputeAndWrite()
    {
        lock (_gate)
        {
            return ComputeAndWriteLocked();
        }
    }

    private RuntimeLockStateDto? ComputeAndWriteLocked()
    {
        if (_state is null) return null;
        var now = DateTime.UtcNow;
        var state = AdjustedStateLocked() ?? _state;
        var tz = TimeZoneInfo.Local;
        var result = LockEngine.Evaluate(new LockEngineInput(
            state.ManualLock, state.UnlockStartUtc, state.UnlockUntilUtc,
            state.BonusStartUtc, state.BonusUntilUtc, state.BonusMinutes,
            state.Schedules, state.DailyLimit, now, tz));
        string? unlockSchedule = null;
        if (result.ActiveOverride == LockOverrideKind.Unlock && result.ActiveOverrideEndUtc is { } unlockEnd)
        {
            var cycle = LockRules.NextLockCycle(state.Schedules, tz, now, state.DailyLimit);
            if (cycle.Utc == unlockEnd && !string.IsNullOrWhiteSpace(cycle.ScheduleName))
                unlockSchedule = cycle.ScheduleName;
        }
        var runtime = new RuntimeLockStateDto(
            state.ChildId,
            state.ChildName,
            state.ParentDisplayName,
            result.IsLocked,
            LockEngine.LegacyReason(result),
            result.ScheduleName,
            result.EndsUtc,
            result.NextLockAtUtc,
            state.BonusUntilUtc,
            state.BonusMinutes,
            now,
            result.LimitRemainingSeconds,
            result.LimitMinutes,
            state.SpokenWarnings,
            _settings.Value.IdleThresholdSeconds,
            state.Language,
            unlockSchedule,
            state.LunaEnabled,
            state.Theme,
            result.TimeUntilLockSeconds,
            result.TimeUntilLockKind,
            result.TimeUntilLockIsUsage,
            result.NextLockAtUtc,
            result.NextScheduleLockUtc,
            result.NextScheduleName,
            result.ActiveOverride,
            result.ActiveOverrideStartUtc,
            result.ActiveOverrideEndUtc,
            result.LimitRemainingSeconds,
            result.LimitMinutes,
            result.UnlockedUntilUtc,
            result.HasPill);
        var changed = _runtime is null
            || _runtime.IsLocked != runtime.IsLocked
            || _runtime.Reason != runtime.Reason
            || _runtime.ScheduleName != runtime.ScheduleName
            || _runtime.EndsUtc != runtime.EndsUtc
            || _runtime.NextLockUtc != runtime.NextLockUtc
            || _runtime.BonusUntilUtc != runtime.BonusUntilUtc
            || _runtime.BonusMinutes != runtime.BonusMinutes
            || _runtime.DailyLimitRemainingSeconds != runtime.DailyLimitRemainingSeconds
            || _runtime.DailyLimitMinutes != runtime.DailyLimitMinutes
            || _runtime.TimeUntilLockSeconds != runtime.TimeUntilLockSeconds
            || _runtime.TimeUntilLockKind != runtime.TimeUntilLockKind
            || _runtime.ActiveOverride != runtime.ActiveOverride
            || _runtime.ActiveOverrideEndUtc != runtime.ActiveOverrideEndUtc
            || _runtime.HasPill != runtime.HasPill
            || !string.Equals(_runtime.Theme, runtime.Theme, StringComparison.Ordinal);
        _runtime = runtime;
        _writer.Save(runtime);
        if (changed)
            _logger.LogInformation("Runtime lock state: locked={Locked} reason={Reason} next={Next} timeUntil={TimeUntil} kind={Kind}",
                runtime.IsLocked, runtime.Reason, runtime.NextLockAtUtc, runtime.TimeUntilLockSeconds, runtime.TimeUntilLockKind);
        return runtime;
    }
}

public sealed class RuntimeLoopService : BackgroundService
{
    private readonly LockRuntime _runtime;
    private readonly TaskManagerPolicy _policy;
    private readonly BrowserPolicyService _browser;
    private readonly ClientSettings _settings;
    private readonly ILogger<RuntimeLoopService> _logger;

    public RuntimeLoopService(LockRuntime runtime, TaskManagerPolicy policy, BrowserPolicyService browser, ClientSettings settings, ILogger<RuntimeLoopService> logger)
    {
        _runtime = runtime;
        _policy = policy;
        _browser = browser;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var runtime = _runtime.ComputeAndWrite();
                _policy.Sync(runtime?.IsLocked ?? false, _settings.Value.MonitoredAccount);
                _browser.Sync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Runtime loop failed");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _policy.Restore(_settings.Value.MonitoredAccount);
        await base.StopAsync(cancellationToken);
    }
}
