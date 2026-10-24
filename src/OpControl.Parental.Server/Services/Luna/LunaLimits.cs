// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Server.Services.Luna;

public static class LunaLimits
{
    public const int DefaultDailyMessages = 60;
    public const int MinDailyMessages = 1;
    public const int MaxDailyMessages = 500;
    public const int FiveLeft = 5;
    public const int MaxExtraGrant = 500;

    public static readonly TimeSpan CooldownWindow = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan CooldownDuration = TimeSpan.FromMinutes(15);
    public const int CooldownBlockedCount = 3;

    public const int RecentTokenBudget = 2000;
    public const int SummaryAfterMessages = 24;
    public const int SummaryKeepRecent = 8;
    public const int MaxOutputTokens = 700;
    public const int ProfileEverySummaries = 3;
    public const int MaxProfileCharacters = 4000;
}
