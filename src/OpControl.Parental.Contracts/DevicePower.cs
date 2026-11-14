// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public enum DevicePowerAction
{
    Restart = 0,
    Sleep = 1,
    PowerOff = 2
}

public enum DevicePowerStatus
{
    Pending = 0,
    Accepted = 1,
    Done = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>A parent-requested power action sent to the Windows client.</summary>
public sealed record DevicePowerCommandDto(
    Guid Id,
    DevicePowerAction Action,
    DateTime RunAtUtc,
    int NoticeSeconds);

/// <summary>The client's accepted / done / failed report for a power action.</summary>
public sealed record DevicePowerResultDto(
    Guid Id,
    DevicePowerAction Action,
    DevicePowerStatus Status,
    string? Detail,
    DateTime Utc);

public static class DevicePowerRules
{
    public const int MinimumNoticeSeconds = 5;
    public const int OneMinuteSeconds = 60;
    public const int FiveMinutesSeconds = 300;
    public const int SleepNoticeSeconds = 5;

    /// <summary>Now maps to a 5-second notice so the child can read it.</summary>
    public static int NormalizeDelay(int seconds) =>
        seconds <= MinimumNoticeSeconds ? MinimumNoticeSeconds
        : seconds <= OneMinuteSeconds ? OneMinuteSeconds
        : FiveMinutesSeconds;

    public static bool CanCancel(DevicePowerStatus status, DateTime runAtUtc, DateTime nowUtc) =>
        status is DevicePowerStatus.Pending or DevicePowerStatus.Accepted && runAtUtc > nowUtc;

    public static bool CanRequest(bool online, DevicePlatform platform) =>
        online && platform == DevicePlatform.Windows;
}
