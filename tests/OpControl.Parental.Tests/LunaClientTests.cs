// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;
using Xunit;

namespace OpControl.Parental.Tests;

public class LunaClientTests
{
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void Luna_runs_only_when_enabled_and_unlocked(bool enabled, bool locked, bool expected) =>
        Assert.Equal(expected, LunaLaunchPolicy.ShouldRun(enabled, locked));

    [Fact]
    public void Luna_pipe_client_is_limited_to_the_token_request()
    {
        var helper = Path.GetFullPath("/Program Files/OpControlParental/Helper/OpControl.Parental.Helper.exe");
        var luna = Path.GetFullPath("/Program Files/OpControlParental/Luna/OpControl.Parental.Luna.exe");
        Assert.True(LunaPipePolicy.IsImage(helper, helper));
        Assert.True(LunaPipePolicy.IsImage(luna, luna));
        Assert.False(LunaPipePolicy.IsImage(luna, helper));
        Assert.False(LunaPipePolicy.IsImage(helper, luna));
        Assert.False(LunaPipePolicy.IsImage("/tmp/evil.exe", helper));

        Assert.True(LunaPipePolicy.IsAllowed(isHelperImage: true, isLunaImage: false, "verify-pin"));
        Assert.True(LunaPipePolicy.IsAllowed(isHelperImage: true, isLunaImage: false, "messages-thread"));
        Assert.False(LunaPipePolicy.IsAllowed(isHelperImage: false, isLunaImage: true, "verify-pin"));
        Assert.False(LunaPipePolicy.IsAllowed(isHelperImage: false, isLunaImage: true, "open-kid"));
        Assert.True(LunaPipePolicy.IsAllowed(isHelperImage: false, isLunaImage: true, LunaPipePolicy.TokenRequest));
        Assert.False(LunaPipePolicy.IsAllowed(isHelperImage: false, isLunaImage: false, LunaPipePolicy.TokenRequest));
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Dev_token_endpoint_refuses_outside_development_and_fake_mode(bool dev, bool fake, bool expected) =>
        Assert.Equal(expected, LunaDevMode.AllowsTokenEndpoint(dev, fake));

    [Fact]
    public void Lock_scene_groups_fit_16_9_and_3_2_without_cropping()
    {
        foreach (var (width, height) in new[] { (1920.0, 1080.0), (1620.0, 1080.0), (1366.0, 768.0) })
        {
            var layout = LockSceneLayout.For(width, height);
            Assert.Equal(height * 0.94, layout.GroupHeight, 3);
            Assert.Equal(height - layout.GroupHeight, layout.GroupTop, 3);
            Assert.True(layout.LeftGroup.X >= 0);
            Assert.True(layout.LeftGroup.Bottom <= height + 0.01);
            Assert.True(layout.RightGroup.Right <= width + 0.01);
            Assert.True(layout.RightGroup.X > layout.LeftGroup.Right, $"{width}x{height} groups overlap");
            Assert.True(layout.PrimaryButton.X >= 0 && layout.PrimaryButton.Right <= width);
            Assert.True(layout.SecondaryButton.Bottom <= height);
            Assert.True(layout.Logo.Right <= width && layout.Logo.Bottom <= height);
            Assert.True(layout.TextColumn.Width <= 600.01);
            Assert.True(layout.TextColumn.X >= 0 && layout.TextColumn.Right <= width);
        }
    }

    [Fact]
    public void Luna_token_broker_never_writes_the_token_to_disk()
    {
        var root = FindRoot();
        var text = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows", "LunaTokenBroker.cs"));
        Assert.DoesNotContain("File.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FileStream", text, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteAll", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CredentialStore().Save", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Dev_only_client_and_lock_preview_paths_are_compile_time_guarded()
    {
        var root = FindRoot();
        var api = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaApiClient.cs"));
        var devIndex = api.IndexOf("DevServer", StringComparison.Ordinal);
        Assert.True(devIndex > 0);
        Assert.True(api.LastIndexOf("#if LUNA_DEV", devIndex, StringComparison.Ordinal) >= 0);
        Assert.True(api.IndexOf("#endif", devIndex, StringComparison.Ordinal) > devIndex);

        var helperProgram = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "Program.cs"));
        var previewIndex = helperProgram.IndexOf("--lock-preview", StringComparison.Ordinal);
        Assert.True(previewIndex > 0);
        Assert.True(helperProgram.LastIndexOf("#if DEBUG", previewIndex, StringComparison.Ordinal) >= 0);
        Assert.True(helperProgram.IndexOf("#endif", previewIndex, StringComparison.Ordinal) > previewIndex);
    }
}
