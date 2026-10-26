// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job184KidViewTests
{
    [Fact]
    public void Kid_view_lives_in_the_avalon_app_and_helper_launches_it()
    {
        var root = FindRoot();
        Assert.True(File.Exists(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs")));
        Assert.False(File.Exists(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "MyOpControlForm.cs")));
        Assert.False(File.Exists(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "KidViews.cs")));
        var helper = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "HelperContext.cs"));
        Assert.Contains("--kid-window", helper, StringComparison.Ordinal);
        Assert.Contains("--messages-only", helper, StringComparison.Ordinal);
        Assert.Contains("--above-lock", helper, StringComparison.Ordinal);
        Assert.Contains("OpControl.Parental.Luna.exe", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Kid_window_has_all_five_screens_and_keeps_the_send_behaviour()
    {
        var root = FindRoot();
        var window = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        foreach (var method in new[] { "BuildHome", "BuildSchedule", "BuildRules", "BuildActivity", "BuildMessages" })
            Assert.Contains(method, window, StringComparison.Ordinal);
        Assert.Contains("MaxLength = 500", window, StringComparison.Ordinal);
        Assert.Contains("Key.Enter", window, StringComparison.Ordinal);
        Assert.Contains("KeyModifiers.Shift", window, StringComparison.Ordinal);
        Assert.Contains("message-send", File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "KidDataSource.cs")), StringComparison.Ordinal);
        Assert.Contains("ScrollToEnd", window, StringComparison.Ordinal);
        Assert.Contains("Client.Messages.Quick.MoreTime", window, StringComparison.Ordinal);
        Assert.Contains("Client.Messages.Quick.Homework", window, StringComparison.Ordinal);
        Assert.Contains("Client.Messages.Quick.ComeHere", window, StringComparison.Ordinal);
    }

    [Fact]
    public void Kid_view_reads_data_only_through_the_helper_pipe()
    {
        var root = FindRoot();
        var data = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "KidDataSource.cs"));
        Assert.Contains("NamedPipeClientStream", data, StringComparison.Ordinal);
        Assert.Contains("LunaPipePolicy.KidSummaryRequest", data, StringComparison.Ordinal);
        Assert.Contains("message-send", data, StringComparison.Ordinal);
        Assert.Contains("message-seen", data, StringComparison.Ordinal);
        Assert.Contains("message-ack", data, StringComparison.Ordinal);
        Assert.Contains("set-theme", data, StringComparison.Ordinal);
        Assert.DoesNotContain("ClientPaths.SummaryFile", data, StringComparison.Ordinal);
        Assert.DoesNotContain("device.json", data, StringComparison.Ordinal);
    }

    [Fact]
    public void Kid_window_tracks_scaling_and_small_screens()
    {
        var root = FindRoot();
        var window = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        Assert.Contains("WorkingArea", window, StringComparison.Ordinal);
        Assert.Contains("Scaling", window, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(Math.Min(1040", window, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(Math.Min(700", window, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer", window, StringComparison.Ordinal);
    }

    [Fact]
    public void Helper_pipe_serves_the_cached_kid_summary_to_luna()
    {
        var root = FindRoot();
        var pipe = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows", "PinPipeServer.cs"));
        Assert.Contains("kid-summary", pipe, StringComparison.Ordinal);
        Assert.Contains("ChildSummaryCache.Read(ClientPaths.SummaryFile)", pipe, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Contracts", "LunaClientPolicy.cs"));
        Assert.Contains(LunaPipePolicy.KidSummaryRequest, policy, StringComparison.Ordinal);
        Assert.Contains("message-send", policy, StringComparison.Ordinal);
        Assert.Contains("message-ack", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void Kid_window_is_not_mistaken_for_the_luna_moon()
    {
        var root = FindRoot();
        var session = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows", "WindowsSession.cs"));
        Assert.Contains("IsKidWindow", session, StringComparison.Ordinal);
        Assert.Contains("MainWindowTitle", session, StringComparison.Ordinal);
        Assert.Contains("!IsKidWindow(p)", session, StringComparison.Ordinal);
        Assert.Contains("|| IsKidWindow(process)", session, StringComparison.Ordinal);
        var helper = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "HelperContext.cs"));
        Assert.Contains("private static bool IsKidWindow", helper, StringComparison.Ordinal);
        Assert.Contains("!IsKidWindow(p)", helper, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
