// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public enum LunaProcessMode
{
    Other,
    Moon,
    KidWindow
}

public readonly record struct LunaProcessInfo(string Name, int SessionId, string? CommandLine);

/// <summary>
/// The moon and My OpControl are the same executable. These rules keep the two
/// modes separate for the service supervisor and the Helper: only the process
/// without --kid-window is the moon, and only the moon may be stopped/restarted.
/// </summary>
public static class LunaProcessPolicy
{
    public const string ExecutableName = "OpControl.Parental.Luna.exe";
    public const string KidSwitch = "--kid-window";

    public static bool IsKidCommandLine(string? commandLine) =>
        commandLine?.Contains(KidSwitch, StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsMoonCommandLine(string? commandLine) => !IsKidCommandLine(commandLine);

    public static LunaProcessMode Classify(string? name, string? commandLine)
    {
        if (!string.Equals(name, ExecutableName, StringComparison.OrdinalIgnoreCase))
            return LunaProcessMode.Other;
        return IsKidCommandLine(commandLine) ? LunaProcessMode.KidWindow : LunaProcessMode.Moon;
    }

    public static bool CountsAsRunningMoon(LunaProcessInfo process, int sessionId) =>
        process.SessionId == sessionId
        && Classify(process.Name, process.CommandLine) == LunaProcessMode.Moon;

    public static bool ShouldStop(LunaProcessInfo process, int sessionId) =>
        CountsAsRunningMoon(process, sessionId);
}
