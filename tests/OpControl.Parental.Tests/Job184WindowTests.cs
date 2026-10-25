// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Xunit;

namespace OpControl.Parental.Tests;

public class Job184WindowTests
{
    [Fact]
    public void Chat_window_uses_a_rounded_native_region_and_hides_the_rectangular_backdrop()
    {
        var root = FindRoot();
        var win32 = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "Win32.cs"));
        var window = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaWindow.axaml.cs"));
        Assert.Contains("CreateRoundRectRgn", win32, StringComparison.Ordinal);
        Assert.Contains("CreateRoundRectRgn", window, StringComparison.Ordinal);
        Assert.Contains("radius = 22;", window, StringComparison.Ordinal);
        Assert.Contains("_blob.IsVisible = false;", window, StringComparison.Ordinal);
        Assert.Contains("_blob.IsVisible = true;", window, StringComparison.Ordinal);
        Assert.Contains("Math.Ceiling(radius * 2 * s)", window, StringComparison.Ordinal);
    }

    [Fact]
    public void Window_is_transparent_at_its_root()
    {
        var root = FindRoot();
        var axaml = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaWindow.axaml"));
        Assert.Contains("WindowDecorations=\"None\"", axaml, StringComparison.Ordinal);
        Assert.Contains("TransparencyLevelHint=\"Transparent\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"Transparent\"", axaml, StringComparison.Ordinal);
        var view = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaAppView.cs"));
        Assert.Contains("CornerRadius = new CornerRadius(22)", view, StringComparison.Ordinal);
        Assert.Contains("ClipToBounds = true", view, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
