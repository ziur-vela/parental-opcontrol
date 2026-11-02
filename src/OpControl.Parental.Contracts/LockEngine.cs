// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public enum LockEngineReason
{
    None,
    Schedule,
    DailyLimit,
    LockNow
}

public enum LockEngineTimeKind
{
    None,
    Schedule,
    DailyLimit,
    LockNow
}

public enum LockOverrideKind
{
    None,
    Bonus,
    Unlock,
    LockNow
}

public sealed record LockEngineInput(
    bool ManualLock,
    DateTime? UnlockStartUtc,
    DateTime? UnlockUntilUtc,
    DateTime? BonusStartUtc,
    DateTime? BonusUntilUtc,
    int? BonusMinutes,
    IReadOnlyList<ScheduleDto> Schedules,
    DailyLimitDto? DailyLimit,
    DateTime NowUtc,
    TimeZoneInfo TimeZone);

public sealed record LockEngineResult(
    bool IsLocked,
    LockEngineReason Reason,
    string? ScheduleName,
    DateTime? EndsUtc,
    LockEngineTimeKind TimeUntilLockKind,
    int? TimeUntilLockSeconds,
    bool TimeUntilLockIsUsage,
    DateTime? NextLockAtUtc,
    DateTime? NextScheduleLockUtc,
    string? NextScheduleName,
    LockOverrideKind ActiveOverride,
    DateTime? ActiveOverrideStartUtc,
    DateTime? ActiveOverrideEndUtc,
    int? LimitRemainingSeconds,
    int? LimitMinutes,
    DateTime? UnlockedUntilUtc,
    bool HasPill);

public static class LockEngine
{
    private const int MaxPillSeconds = 24 * 60 * 60;

    public static LockEngineResult Evaluate(LockEngineInput input)
    {
        var now = input.NowUtc;
        var tz = input.TimeZone;
        var schedules = input.Schedules ?? [];
        var daily = input.DailyLimit;
        var limitRemaining = LimitRemaining(daily, now);
        var limitMinutes = daily is { Enabled: true, TodayMinutes: > 0 } ? daily.TodayMinutes : (int?)null;
        var nextSchedule = LockRules.NextScheduleStartWithName(schedules, tz, now);

        if (input.ManualLock)
            return Locked(LockEngineReason.LockNow, LockEngineTimeKind.LockNow, null, null, null,
                LockOverrideKind.LockNow, null, null, limitRemaining, limitMinutes, nextSchedule);

        if (input.BonusUntilUtc is { } bonus && bonus > now)
        {
            var start = input.BonusStartUtc
                ?? (input.BonusMinutes is { } minutes ? bonus.AddMinutes(-minutes) : now);
            return Free(input, start, bonus, LockOverrideKind.Bonus, limitRemaining, limitMinutes, nextSchedule);
        }

        if (input.UnlockUntilUtc is { } unlock && unlock > now)
        {
            var start = input.UnlockStartUtc ?? now;
            return Free(input, start, unlock, LockOverrideKind.Unlock, limitRemaining, limitMinutes, nextSchedule);
        }

        return Normal(input, now, limitRemaining, limitMinutes, nextSchedule);
    }

    private static LockEngineResult Normal(
        LockEngineInput input, DateTime now, int? limitRemaining, int? limitMinutes,
        LockCycle? nextSchedule)
    {
        var active = LockRules.ActiveSchedule(input.Schedules ?? [], input.TimeZone, now);
        if (active is not null)
        {
            return Locked(LockEngineReason.Schedule, LockEngineTimeKind.Schedule, active.Name,
                LockRules.CurrentScheduleEnd(active, input.TimeZone, now), null,
                LockOverrideKind.None, null, null, limitRemaining, limitMinutes, nextSchedule);
        }

        if (limitRemaining is <= 0)
        {
            return Locked(LockEngineReason.DailyLimit, LockEngineTimeKind.DailyLimit, null,
                input.DailyLimit?.DayEndUtc, null,
                LockOverrideKind.None, null, null, limitRemaining, limitMinutes, nextSchedule);
        }

        var scheduleSeconds = nextSchedule is { } next
            ? (int)Math.Max(0, Math.Ceiling((next.Utc - now).TotalSeconds))
            : (int?)null;
        var limitSeconds = limitRemaining;
        LockEngineTimeKind kind;
        int? time;
        DateTime? at;
        bool usage;

        if (scheduleSeconds is { } s && (limitSeconds is null || s <= limitSeconds))
        {
            kind = LockEngineTimeKind.Schedule;
            time = s;
            at = nextSchedule!.Utc;
            usage = false;
        }
        else if (limitSeconds is { } l)
        {
            kind = LockEngineTimeKind.DailyLimit;
            time = l;
            at = null;
            usage = true;
        }
        else
        {
            kind = LockEngineTimeKind.None;
            time = null;
            at = null;
            usage = false;
        }

        var hasPill = !input.ManualLock && time is > 0 && IsPillTime(kind, at, now);
        return Result(false, LockEngineReason.None, null, null, kind, time, usage, at,
            nextSchedule, LockOverrideKind.None, null, null, limitRemaining, limitMinutes, null, hasPill);
    }

    private static LockEngineResult Free(
        LockEngineInput input, DateTime start, DateTime end, LockOverrideKind kind,
        int? limitRemaining, int? limitMinutes, LockCycle? nextSchedule)
    {
        var now = input.NowUtc;
        var overrideSeconds = Math.Max(0, (int)Math.Ceiling((end - now).TotalSeconds));
        var post = NormalAt(input, end);
        int? total = post.TimeUntilLockSeconds is { } after ? overrideSeconds + after : null;
        var postKind = post.TimeUntilLockKind;
        DateTime? nextLockAt = postKind switch
        {
            LockEngineTimeKind.Schedule => post.NextLockAtUtc ?? end,
            _ => null
        };
        var hasPill = total is > 0 && IsPillTime(postKind, nextLockAt, now);
        return Result(false, LockEngineReason.None, null, end, postKind, total,
            false, nextLockAt, nextSchedule,
            kind, start, end, limitRemaining, limitMinutes, end, hasPill);
    }

    private static LockEngineResult NormalAt(LockEngineInput input, DateTime atUtc)
    {
        var schedules = input.Schedules ?? [];
        var active = LockRules.ActiveSchedule(schedules, input.TimeZone, atUtc);
        var limit = LimitRemaining(input.DailyLimit, atUtc);
        var next = LockRules.NextScheduleStartWithName(schedules, input.TimeZone, atUtc);
        if (active is not null)
            return Result(true, LockEngineReason.Schedule, active.Name,
                LockRules.CurrentScheduleEnd(active, input.TimeZone, atUtc),
                LockEngineTimeKind.Schedule, 0, false, null, next,
                LockOverrideKind.None, null, null, limit,
                LimitMinutes(input.DailyLimit), null, false);
        if (limit is <= 0)
            return Result(true, LockEngineReason.DailyLimit, null, input.DailyLimit?.DayEndUtc,
                LockEngineTimeKind.DailyLimit, 0, false, null, next,
                LockOverrideKind.None, null, null, limit,
                LimitMinutes(input.DailyLimit), null, false);

        var scheduleSeconds = next is { } n ? (int)Math.Max(0, Math.Ceiling((n.Utc - atUtc).TotalSeconds)) : (int?)null;
        if (scheduleSeconds is { } s && (limit is null || s <= limit))
            return Result(false, LockEngineReason.None, null, null,
                LockEngineTimeKind.Schedule, s, false, next!.Utc, next,
                LockOverrideKind.None, null, null, limit,
                LimitMinutes(input.DailyLimit), null, false);
        if (limit is { } l)
            return Result(false, LockEngineReason.None, null, null,
                LockEngineTimeKind.DailyLimit, l, true, null, next,
                LockOverrideKind.None, null, null, limit,
                LimitMinutes(input.DailyLimit), null, false);
        return Result(false, LockEngineReason.None, null, null,
            LockEngineTimeKind.None, null, false, null, next,
            LockOverrideKind.None, null, null, limit,
            LimitMinutes(input.DailyLimit), null, false);
    }

    private static LockEngineResult Locked(
        LockEngineReason reason, LockEngineTimeKind kind, string? scheduleName, DateTime? end,
        LockCycle? nextSchedule, LockOverrideKind activeOverride, DateTime? overrideStart,
        DateTime? overrideEnd, int? limitRemaining, int? limitMinutes, LockCycle? next)
    {
        next ??= nextSchedule;
        return Result(true, reason, scheduleName, end, kind, 0, false, null, next,
            activeOverride, overrideStart, overrideEnd, limitRemaining, limitMinutes, null, false);
    }

    private static LockEngineResult Result(
        bool locked, LockEngineReason reason, string? scheduleName, DateTime? ends,
        LockEngineTimeKind timeKind, int? timeSeconds, bool usage, DateTime? nextLockAt,
        LockCycle? nextSchedule, LockOverrideKind activeOverride, DateTime? overrideStart,
        DateTime? overrideEnd, int? limitRemaining, int? limitMinutes,
        DateTime? unlockedUntil, bool hasPill) =>
        new(locked, reason, scheduleName, ends, timeKind, timeSeconds, usage, nextLockAt,
            nextSchedule?.Utc, nextSchedule?.ScheduleName, activeOverride, overrideStart,
            overrideEnd, limitRemaining, limitMinutes, unlockedUntil, hasPill);

    private static bool IsPillTime(LockEngineTimeKind kind, DateTime? nextLockAt, DateTime now) =>
        kind switch
        {
            LockEngineTimeKind.DailyLimit or LockEngineTimeKind.LockNow => true,
            LockEngineTimeKind.Schedule => nextLockAt is { } at && at - now <= TimeSpan.FromSeconds(MaxPillSeconds),
            _ => false
        };

    private static int? LimitMinutes(DailyLimitDto? daily) =>
        daily is { Enabled: true, TodayMinutes: > 0 } ? daily.TodayMinutes : null;

    private static int? LimitRemaining(DailyLimitDto? daily, DateTime now)
    {
        if (daily is not { Enabled: true }) return null;
        var minutes = now >= daily.DayEndUtc ? daily.TomorrowMinutes ?? daily.TodayMinutes : daily.TodayMinutes;
        if (minutes <= 0) return null;
        if (now >= daily.DayEndUtc || now < daily.DayStartUtc) return minutes * 60;
        return Math.Max(0, minutes * 60 - daily.UsedTodaySeconds);
    }

    public static string LegacyReason(LockEngineResult result) =>
        result.IsLocked
            ? result.Reason switch
            {
                LockEngineReason.Schedule => "schedule",
                LockEngineReason.DailyLimit => "daily-limit",
                LockEngineReason.LockNow => "manual",
                _ => "unlocked"
            }
            : result.ActiveOverride switch
            {
                LockOverrideKind.Bonus => "bonus",
                LockOverrideKind.Unlock => "override",
                LockOverrideKind.LockNow => "manual",
                _ => "unlocked"
            };

    public static (DateTime StartUtc, DateTime EndUtc) ExtendBonus(
        DateTime? currentStartUtc, DateTime? currentEndUtc, DateTime nowUtc, int minutes)
    {
        var length = Math.Clamp(minutes, 1, 1440);
        var start = currentEndUtc is { } end && end > nowUtc ? currentStartUtc ?? nowUtc : nowUtc;
        var from = currentEndUtc is { } activeEnd && activeEnd > nowUtc ? activeEnd : nowUtc;
        return (start, from.AddMinutes(length));
    }
}
