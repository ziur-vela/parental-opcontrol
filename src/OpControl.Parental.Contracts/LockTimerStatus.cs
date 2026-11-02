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

        if (current.Reason == "override" && current.EndsUtc is { } unlockedUntil && unlockedUntil > nowUtc)
        {
            if (previous.Reason != "override" || previous.EndsUtc != unlockedUntil)
                return new LockTimerChange(LockTimerChangeKind.Unlocked, AtUtc: unlockedUntil);
            return LockTimerChange.None;
        }

        if (current.BonusUntilUtc is { } bonus && bonus > nowUtc && current.BonusMinutes is { } minutes)
        {
            if (previous.BonusUntilUtc != current.BonusUntilUtc || previous.BonusMinutes != current.BonusMinutes)
                return new LockTimerChange(LockTimerChangeKind.BonusAdded, AtUtc: bonus, Minutes: minutes);
            return LockTimerChange.None;
        }

        if (current.NextLockUtc is null && current.Reason is not ("override" or "bonus"))
        {
            if (!previous.IsLocked
                && (previous.Reason is "override" or "bonus" || previous.NextLockUtc is not null))
                return new LockTimerChange(LockTimerChangeKind.NoLockScheduled, HideAfter: true);
            return LockTimerChange.None;
        }

        if (current.NextLockUtc is { } next && next > nowUtc
            && !previous.IsLocked
            && previous.Reason is not ("override" or "bonus")
            && current.Reason is not ("override" or "bonus")
            && previous.NextLockUtc != next)
        {
            var earlier = previous.NextLockUtc is not { } before || next < before;
            return new LockTimerChange(LockTimerChangeKind.LockTimeChanged, AtUtc: next, MakesLockEarlier: earlier);
        }

        return LockTimerChange.None;
    }
}
