// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job20FTimerTests
{
    private static readonly LockTimerSettings DaysHidden = LockTimerSettings.Default with { DaysFormat = true, ShowSeconds = false };
    private static readonly LockTimerSettings DaysShown = LockTimerSettings.Default with { DaysFormat = true, ShowSeconds = true };
    private static readonly LockTimerSettings HoursHidden = LockTimerSettings.Default with { DaysFormat = false, ShowSeconds = false };
    private static readonly LockTimerSettings HoursShown = LockTimerSettings.Default with { DaysFormat = false, ShowSeconds = true };

    [Theory]
    [InlineData(24 * 3600, "1d 0h 0m", "1 d 0 h 0 min")]
    [InlineData(32 * 3600 + 45 * 60 + 52, "1d 8h 45m", "1 d 8 h 45 min")]
    [InlineData(23 * 3600 + 59 * 60 + 59, "23:59", "23:59")]
    [InlineData(3600, "1:00", "1:00")]
    [InlineData(59 * 60 + 59, "59 min", "59 min")]
    [InlineData(5 * 60, "5 min", "5 min")]
    [InlineData(60, "1 min", "1 min")]
    [InlineData(59, "0:59", "0:59")]
    [InlineData(0, "0:00", "0:00")]
    public void Days_format_with_seconds_hidden(int seconds, string english, string spanish)
    {
        var span = TimeSpan.FromSeconds(seconds);
        Assert.Equal(english, LockTimerFormat.Format(span, DaysHidden, "en"));
        Assert.Equal(spanish, LockTimerFormat.Format(span, DaysHidden, "es-MX"));
    }

    [Theory]
    [InlineData(24 * 3600, "1d 0h 0m 0s", "1 d 0 h 0 min 0 s")]
    [InlineData(32 * 3600 + 45 * 60 + 52, "1d 8h 45m 52s", "1 d 8 h 45 min 52 s")]
    [InlineData(23 * 3600 + 59 * 60 + 59, "23:59:59", "23:59:59")]
    [InlineData(3600, "1:00:00", "1:00:00")]
    [InlineData(59 * 60 + 59, "59:59", "59:59")]
    [InlineData(5 * 60, "5:00", "5:00")]
    [InlineData(60, "1:00", "1:00")]
    [InlineData(59, "0:59", "0:59")]
    [InlineData(0, "0:00", "0:00")]
    public void Days_format_with_seconds_shown(int seconds, string english, string spanish)
    {
        var span = TimeSpan.FromSeconds(seconds);
        Assert.Equal(english, LockTimerFormat.Format(span, DaysShown, "en"));
        Assert.Equal(spanish, LockTimerFormat.Format(span, DaysShown, "es-MX"));
    }

    [Theory]
    [InlineData(24 * 3600, "24:00", "32:45", 32 * 3600 + 45 * 60)]
    [InlineData(23 * 3600 + 59 * 60 + 59, "23:59", "23:59", 23 * 3600 + 59 * 60 + 59)]
    [InlineData(3600, "1:00", "1:00", 3600)]
    [InlineData(59 * 60 + 59, "59 min", "59 min", 59 * 60 + 59)]
    [InlineData(5 * 60, "5 min", "5 min", 5 * 60)]
    [InlineData(60, "1 min", "1 min", 60)]
    [InlineData(59, "0:59", "0:59", 59)]
    [InlineData(0, "0:00", "0:00", 0)]
    public void Hours_format_with_seconds_hidden(int first, string firstText, string secondText, int second)
    {
        Assert.Equal(firstText, LockTimerFormat.Format(TimeSpan.FromSeconds(first), HoursHidden, "en"));
        Assert.Equal(secondText, LockTimerFormat.Format(TimeSpan.FromSeconds(second), HoursHidden, "en"));
    }

    [Fact]
    public void Hours_format_with_seconds_shown_uses_the_same_boundaries()
    {
        Assert.Equal("32:45:52", LockTimerFormat.Format(TimeSpan.FromSeconds(32 * 3600 + 45 * 60 + 52), HoursShown, "en"));
        Assert.Equal("23:59:59", LockTimerFormat.Format(TimeSpan.FromSeconds(23 * 3600 + 59 * 60 + 59), HoursShown, "en"));
        Assert.Equal("1:00:00", LockTimerFormat.Format(TimeSpan.FromSeconds(3600), HoursShown, "en"));
        Assert.Equal("59:59", LockTimerFormat.Format(TimeSpan.FromSeconds(59 * 60 + 59), HoursShown, "en"));
        Assert.Equal("5:00", LockTimerFormat.Format(TimeSpan.FromSeconds(300), HoursShown, "en"));
        Assert.Equal("0:59", LockTimerFormat.Format(TimeSpan.FromSeconds(59), HoursShown, "en"));
    }

    [Fact]
    public void The_last_minute_always_shows_seconds_even_when_hidden()
    {
        var hidden = LockTimerSettings.Default with { ShowSeconds = false, DaysFormat = false };
        Assert.Equal("0:59", LockTimerFormat.Format(TimeSpan.FromSeconds(59), hidden, "en"));
        Assert.Equal("0:42", LockTimerFormat.Format(TimeSpan.FromSeconds(42), hidden, "es-MX"));
        Assert.True(LockTimerFormat.ForcesSeconds(TimeSpan.FromSeconds(59)));
        Assert.False(LockTimerFormat.ForcesSeconds(TimeSpan.FromSeconds(60)));
    }

    [Theory]
    [InlineData(300, LockTimerPhase.Normal)]
    [InlineData(299, LockTimerPhase.Warn)]
    [InlineData(61, LockTimerPhase.Warn)]
    [InlineData(60, LockTimerPhase.Warn)]
    [InlineData(59, LockTimerPhase.Last)]
    [InlineData(0, LockTimerPhase.Last)]
    public void Phase_boundaries(int seconds, LockTimerPhase expected) =>
        Assert.Equal(expected, LockTimerFormat.Phase(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Tucking_is_allowed_only_outside_the_warnings()
    {
        var settings = LockTimerSettings.Default;
        Assert.True(LockTimerPolicy.CanTuck(TimeSpan.FromMinutes(6), settings));
        Assert.False(LockTimerPolicy.CanTuck(TimeSpan.FromMinutes(4), settings));
        Assert.False(LockTimerPolicy.CanTuck(TimeSpan.FromSeconds(42), settings));
        Assert.False(LockTimerPolicy.CanTuck(TimeSpan.FromHours(1), settings with { Tuck = false }));
    }

    [Fact]
    public void Warning_thresholds_are_5_and_1_minute_and_ignore_the_display_settings()
    {
        Assert.Equal([5, 1], LockTimerPolicy.WarningMinutes);
        Assert.True(LockTimerPolicy.IsWarningDue(300, 5));
        Assert.False(LockTimerPolicy.IsWarningDue(301, 5));
        Assert.False(LockTimerPolicy.IsWarningDue(240, 5));
        Assert.True(LockTimerPolicy.IsWarningDue(60, 1));
        Assert.False(LockTimerPolicy.IsWarningDue(61, 1));

        // The policy has no settings parameter: hiding the timer cannot turn these off.
        var hidden = LockTimerSettings.Default with { Enabled = false };
        Assert.True(hidden.Enabled == false);
        Assert.True(LockTimerPolicy.IsWarningDue(300, 5));
    }

    [Fact]
    public void Settings_defaults_match_the_approved_mockup()
    {
        var settings = LockTimerSettings.Default;
        Assert.True(settings.Enabled);
        Assert.False(settings.ShowSeconds);
        Assert.True(settings.DaysFormat);
        Assert.Equal("my", settings.Look);
        Assert.True(settings.Tuck);
        Assert.Equal(10, settings.TuckAfterSeconds);
    }

    [Theory]
    [InlineData("night", "night")]
    [InlineData("gray", "gray")]
    [InlineData("bogus", "my")]
    public void Settings_normalize_look(string input, string expectedLook)
    {
        var settings = (LockTimerSettings.Default with { Look = input, TuckAfterSeconds = 7 }).Normalize();
        Assert.Equal(expectedLook, settings.Look);
        Assert.Equal(10, settings.TuckAfterSeconds);
    }

    [Fact]
    public void Look_follows_the_kid_theme_only_for_my_theme()
    {
        Assert.Equal("gray", (LockTimerSettings.Default with { Look = "my" }).ResolveTheme("gray"));
        Assert.Equal("night", (LockTimerSettings.Default with { Look = "my" }).ResolveTheme("night"));
        Assert.Equal("night", (LockTimerSettings.Default with { Look = "night" }).ResolveTheme("gray"));
        Assert.Equal("gray", (LockTimerSettings.Default with { Look = "gray" }).ResolveTheme("night"));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    public void Settings_keep_the_three_tuck_delays(int seconds)
    {
        var settings = (LockTimerSettings.Default with { TuckAfterSeconds = seconds }).Normalize();
        Assert.Equal(seconds, settings.TuckAfterSeconds);
    }

    [Fact]
    public void Settings_round_trip_through_the_local_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ocp-timer-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new LockTimerSettings
            {
                Enabled = false,
                ShowSeconds = true,
                DaysFormat = false,
                Look = "gray",
                Tuck = false,
                TuckAfterSeconds = 30
            };
            LockTimerStore.Write(settings, path);
            Assert.Equal(settings, LockTimerStore.Read(path));
            Assert.Contains("\"showSeconds\": true", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Missing_or_broken_settings_fall_back_to_the_defaults()
    {
        Assert.Equal(LockTimerSettings.Default, LockTimerStore.Read(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json")));
        Assert.Equal(LockTimerSettings.Default, LockTimerStore.Parse("{not json"));
        Assert.Equal(LockTimerSettings.Default, LockTimerStore.Parse(""));
    }

    [Fact]
    public void Timer_tab_is_in_the_kid_view_and_writes_local_settings()
    {
        var root = FindRoot();
        var window = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        Assert.Contains("Screen.Timer", window, StringComparison.Ordinal);
        Assert.Contains("BuildTimer", window, StringComparison.Ordinal);
        Assert.Contains("LockTimerStore.Read", window, StringComparison.Ordinal);
        Assert.Contains("LockTimerStore.Write", window, StringComparison.Ordinal);
        Assert.Contains("Client.Kid.Nav.Timer", window, StringComparison.Ordinal);
        Assert.Contains("Client.Kid.Timer.GoodToKnowBody", window, StringComparison.Ordinal);
        Assert.Contains("Client.Kid.Timer.Hidden", window, StringComparison.Ordinal);
        Assert.Contains("HourglassIcon", window, StringComparison.Ordinal);
        Assert.Contains("\"timer\" => Screen.Timer", window, StringComparison.Ordinal);
        Assert.Contains("_options.Preview", window, StringComparison.Ordinal);
    }

    [Fact]
    public void Pill_has_the_states_tuck_hover_click_and_reduced_motion()
    {
        var root = FindRoot();
        var pill = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "CountdownForm.cs"));
        Assert.Contains("OnMouseEnter", pill, StringComparison.Ordinal);
        Assert.Contains("StartSlide(1f)", pill, StringComparison.Ordinal);
        Assert.Contains("StartSlide(0f)", pill, StringComparison.Ordinal);
        Assert.Contains("Clicked", pill, StringComparison.Ordinal);
        Assert.Contains("AccessibleName", pill, StringComparison.Ordinal);
        Assert.Contains("KidMotion.Reduced", pill, StringComparison.Ordinal);
        Assert.Contains("PillArt", pill, StringComparison.Ordinal);
        Assert.Contains("28 * _factor", pill, StringComparison.Ordinal);
        Assert.Contains("Region", pill, StringComparison.Ordinal);
        var art = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "PillArt.cs"));
        Assert.Contains("DrawCalm", art, StringComparison.Ordinal);
        Assert.Contains("DrawGrimace", art, StringComparison.Ordinal);
        Assert.Contains("DrawFear", art, StringComparison.Ordinal);
        Assert.Contains("DrawClock", art, StringComparison.Ordinal);
        Assert.Contains("0.5", art, StringComparison.Ordinal);
    }

    [Fact]
    public void Helper_opens_my_opcontrol_from_the_pill_and_keeps_warnings_on()
    {
        var root = FindRoot();
        var helper = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "HelperContext.cs"));
        Assert.Contains("_countdown.Clicked", helper, StringComparison.Ordinal);
        Assert.Contains("OpenMyOpControlFromPill", helper, StringComparison.Ordinal);
        Assert.Contains("SetForegroundWindow", helper, StringComparison.Ordinal);
        Assert.Contains("CheckLockWarnings(state);", helper, StringComparison.Ordinal);
        var warningIndex = helper.IndexOf("CheckLockWarnings(state);", StringComparison.Ordinal);
        var settingsIndex = helper.IndexOf("LockTimerStore.Read();", StringComparison.Ordinal);
        Assert.True(warningIndex > 0 && settingsIndex > warningIndex);
        Assert.Contains("\"timer\" => \"timer\"", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Timer_preview_flag_exists_for_screenshots()
    {
        var root = FindRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "Program.cs"));
        Assert.Contains("--timer-preview", program, StringComparison.Ordinal);
        var preview = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "TimerPreview.cs"));
        Assert.Contains("show", preview, StringComparison.Ordinal);
        Assert.Contains("tuck", preview, StringComparison.Ordinal);
        Assert.Contains("warn", preview, StringComparison.Ordinal);
        Assert.Contains("last", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void Timer_strings_exist_in_both_languages()
    {
        var root = FindRoot();
        var en = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "en.json"));
        var es = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "es-MX.json"));
        foreach (var key in new[]
                 {
                     "Client.Pill.LockIn", "Client.Pill.Open", "Client.Pill.Bonus",
                     "Client.Pill.UnlockedUntil", "Client.Pill.LeftToday",
                     "Client.Notice.LockTimer", "Client.Notice.LockInOne", "Client.Notice.LockInMany",
                     "Client.Kid.Nav.Timer", "Client.Kid.Timer.Eyebrow", "Client.Kid.Timer.Title",
                     "Client.Kid.Timer.Show", "Client.Kid.Timer.ShowHint", "Client.Kid.Timer.Seconds",
                     "Client.Kid.Timer.SecondsHint", "Client.Kid.Timer.ShowSeconds", "Client.Kid.Timer.HideSeconds",
                     "Client.Kid.Timer.Days", "Client.Kid.Timer.DaysHint", "Client.Kid.Timer.DaysExample",
                     "Client.Kid.Timer.HoursExample", "Client.Kid.Timer.Look", "Client.Kid.Timer.MyTheme",
                     "Client.Kid.Timer.Night", "Client.Kid.Timer.Gray", "Client.Kid.Timer.Tuck",
                     "Client.Kid.Timer.TuckHint", "Client.Kid.Timer.After", "Client.Kid.Timer.Preview",
                     "Client.Kid.Timer.Hidden", "Client.Kid.Timer.GoodToKnow", "Client.Kid.Timer.GoodToKnowBody"
                 })
        {
            Assert.Contains($"\"{key}\"", en, StringComparison.Ordinal);
            Assert.Contains($"\"{key}\"", es, StringComparison.Ordinal);
        }
        Assert.Contains("Bloqueo en", es, StringComparison.Ordinal);
        Assert.Contains("Abrir Mi OpControl", es, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
