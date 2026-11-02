// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public enum LockTimerChangeKind
{
    None,
    Unlocked,
    BonusAdded,
    LockTimeChanged,
    NoLockScheduled
}

public sealed record LockTimerChange(
    LockTimerChangeKind Kind,
    DateTime? AtUtc = null,
    int? Minutes = null,
    bool MakesLockEarlier = false,
    bool HideAfter = false)
{
    public static readonly LockTimerChange None = new(LockTimerChangeKind.None);

    public bool HasMessage => Kind != LockTimerChangeKind.None;
}

public static class LockTimerStatus
{
    public static LockTimerChange Detect(RuntimeLockStateDto? previous, RuntimeLockStateDto current, DateTime nowUtc)
    {
        if (previous is null || current.IsLocked)
            return LockTimerChange.None;

        if (current.ActiveOverride == LockOverrideKind.Unlock
            && current.ActiveOverrideEndUtc is { } unlockedUntil && unlockedUntil > nowUtc)
        {
            if (previous.ActiveOverride != LockOverrideKind.Unlock || previous.ActiveOverrideEndUtc != unlockedUntil)
                return new LockTimerChange(LockTimerChangeKind.Unlocked, AtUtc: unlockedUntil);
            return LockTimerChange.None;
        }

        if (current.ActiveOverride == LockOverrideKind.Bonus
            && current.ActiveOverrideEndUtc is { } bonusUntil && bonusUntil > nowUtc)
        {
            var minutes = current.BonusMinutes ?? 0;
            if (previous.ActiveOverride != LockOverrideKind.Bonus
                || previous.ActiveOverrideEndUtc != bonusUntil
                || previous.BonusMinutes != current.BonusMinutes)
                return new LockTimerChange(LockTimerChangeKind.BonusAdded, AtUtc: bonusUntil, Minutes: minutes);
            return LockTimerChange.None;
        }

        if (current.TimeUntilLockSeconds is null && current.NextScheduleLockUtc is null
            && current.ActiveOverride == LockOverrideKind.None)
        {
            if (!previous.IsLocked
                && (previous.ActiveOverride is LockOverrideKind.Unlock or LockOverrideKind.Bonus
                    || previous.TimeUntilLockSeconds is not null
                    || previous.NextScheduleLockUtc is not null))
                return new LockTimerChange(LockTimerChangeKind.NoLockScheduled, HideAfter: true);
            return LockTimerChange.None;
        }

        if (current.NextLockAtUtc is { } next && next > nowUtc
            && !previous.IsLocked
            && previous.ActiveOverride == LockOverrideKind.None
            && current.ActiveOverride == LockOverrideKind.None
            && previous.NextLockAtUtc != next)
        {
            var earlier = previous.NextLockAtUtc is not { } before || next < before;
            return new LockTimerChange(LockTimerChangeKind.LockTimeChanged, AtUtc: next, MakesLockEarlier: earlier);
        }

        return LockTimerChange.None;
    }
}
