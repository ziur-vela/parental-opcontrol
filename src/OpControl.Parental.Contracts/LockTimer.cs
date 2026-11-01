// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;

namespace OpControl.Parental.Contracts;

public enum LockTimerPhase
{
    Normal,
    Warn,
    Last
}

/// <summary>
/// The child's own lock-timer display preferences. Stored per Windows user;
/// they only change how the pill looks, never the schedule or the lock.
/// </summary>
public sealed record LockTimerSettings
{
    public const string MyTheme = "my";
    public const string NightLook = "night";
    public const string GrayLook = "gray";
    public const int DefaultTuckSeconds = 10;

    public bool Enabled { get; init; } = true;
    public bool ShowSeconds { get; init; }
    public bool DaysFormat { get; init; } = true;
    public string Look { get; init; } = MyTheme;
    public bool Tuck { get; init; } = true;
    public int TuckAfterSeconds { get; init; } = DefaultTuckSeconds;

    public static LockTimerSettings Default => new();

    public LockTimerSettings Normalize() => this with
    {
        Look = Look is NightLook or GrayLook ? Look : MyTheme,
        TuckAfterSeconds = TuckAfterSeconds is 5 or 10 or 30 ? TuckAfterSeconds : DefaultTuckSeconds
    };

    /// <summary>"My theme" follows the child's My OpControl theme.</summary>
    public string ResolveTheme(string? kidTheme) => Look switch
    {
        NightLook => NightLook,
        GrayLook => GrayLook,
        _ => LunaThemes.Normalize(kidTheme)
    };
}

/// <summary>Local, per-child-Windows-user storage for the timer settings.</summary>
public static class LockTimerStore
{
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpControlParental");

    public static string FilePath => Path.Combine(Directory, "timer-settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static LockTimerSettings Read(string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            if (!File.Exists(file)) return LockTimerSettings.Default;
            return Parse(File.ReadAllText(file));
        }
        catch
        {
            return LockTimerSettings.Default;
        }
    }

    public static LockTimerSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return LockTimerSettings.Default;
        try
        {
            return (JsonSerializer.Deserialize<LockTimerSettings>(json, JsonOptions)
                    ?? LockTimerSettings.Default).Normalize();
        }
        catch
        {
            return LockTimerSettings.Default;
        }
    }

    public static string Serialize(LockTimerSettings settings) =>
        JsonSerializer.Serialize(settings.Normalize(), JsonOptions);

    public static void Write(LockTimerSettings settings, string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var temp = file + ".tmp";
            File.WriteAllText(temp, Serialize(settings));
            File.Move(temp, file, overwrite: true);
        }
        catch
        {
            // Display preferences are cosmetic; never break the UI over them.
        }
    }
}

/// <summary>
/// Countdown formatting shared by the pill and its tests. Formatting never
/// looks at the settings for warnings; those stay on regardless of the timer.
/// </summary>
public static class LockTimerFormat
{
    public static int WholeSeconds(TimeSpan remaining) =>
        (int)Math.Floor(Math.Max(0, remaining.TotalSeconds));

    public static LockTimerPhase Phase(TimeSpan remaining)
    {
        var seconds = WholeSeconds(remaining);
        if (seconds < 60) return LockTimerPhase.Last;
        return seconds < 300 ? LockTimerPhase.Warn : LockTimerPhase.Normal;
    }

    public static bool ForcesSeconds(TimeSpan remaining) => WholeSeconds(remaining) < 60;

    public static string Format(TimeSpan remaining, LockTimerSettings settings, string? language = null)
    {
        var seconds = WholeSeconds(remaining);
        var spanish = (language ?? "en").TrimStart().StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var showSeconds = settings.ShowSeconds || seconds < 60;

        if (seconds >= 86400 && settings.DaysFormat)
        {
            var days = seconds / 86400;
            var hours = seconds % 86400 / 3600;
            var minutes = seconds % 3600 / 60;
            var rest = seconds % 60;
            var text = spanish
                ? $"{days} d {hours} h {minutes} min"
                : $"{days}d {hours}h {minutes}m";
            return showSeconds
                ? text + (spanish ? $" {rest} s" : $" {rest}s")
                : text;
        }

        if (seconds >= 3600)
        {
            var hours = seconds / 3600;
            var minutes = seconds % 3600 / 60;
            var rest = seconds % 60;
            return showSeconds ? $"{hours}:{minutes:00}:{rest:00}" : $"{hours}:{minutes:00}";
        }

        if (seconds >= 60)
        {
            var minutes = seconds / 60;
            var rest = seconds % 60;
            if (showSeconds) return $"{minutes}:{rest:00}";
            return $"{minutes} min";
        }

        return $"0:{seconds:00}";
    }
}

/// <summary>
/// The 5- and 1-minute heads-ups fire from the same remaining time the pill
/// uses, but they are deliberately independent of the display settings.
/// </summary>
public static class LockTimerPolicy
{
    public static readonly int[] WarningMinutes = [5, 1];

    public static bool CanTuck(TimeSpan remaining, LockTimerSettings settings) =>
        settings.Tuck && LockTimerFormat.Phase(remaining) == LockTimerPhase.Normal;

    public static bool IsWarningDue(int totalSeconds, int thresholdMinutes) =>
        totalSeconds <= thresholdMinutes * 60 && totalSeconds > (thresholdMinutes - 1) * 60;
}
