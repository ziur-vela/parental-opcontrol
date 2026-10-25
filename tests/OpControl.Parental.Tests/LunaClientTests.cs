// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;
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
        Assert.True(LunaPipePolicy.IsAllowed(isHelperImage: true, isLunaImage: false, "set-theme"));
        Assert.False(LunaPipePolicy.IsAllowed(isHelperImage: false, isLunaImage: true, "set-theme"));
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
    public void Send_is_guarded_and_calls_the_api_once_per_click_or_enter()
    {
        var root = FindRoot();
        var text = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaAppView.cs"));
        var start = text.IndexOf("private async Task SendFromInputAsync()", StringComparison.Ordinal);
        Assert.True(start > 0);
        var body = text[start..Math.Min(text.Length, start + 1200)];
        Assert.Contains("if (_sending ||", body, StringComparison.Ordinal);
        Assert.Contains("_sending = true;", body, StringComparison.Ordinal);
        Assert.True(body.IndexOf("_sending = true;", StringComparison.Ordinal)
                    < body.IndexOf("_api.SendMessageAsync", StringComparison.Ordinal));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(body, "_api\\.SendMessageAsync"));
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

        var kidPreviewIndex = helperProgram.IndexOf("--kid-preview", StringComparison.Ordinal);
        Assert.True(kidPreviewIndex > 0);
        Assert.True(helperProgram.LastIndexOf("#if DEBUG", kidPreviewIndex, StringComparison.Ordinal) >= 0);
        Assert.True(helperProgram.IndexOf("#endif", kidPreviewIndex, StringComparison.Ordinal) > kidPreviewIndex);
    }

    [Fact]
    public void New_luna_strings_are_neutral_and_use_IA_in_spanish()
    {
        var root = FindRoot();
        var english = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "en.json")))!;
        var spanish = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "es-MX.json")))!;
        var pronoun = new System.Text.RegularExpressions.Regex(@"\b(he|him|his|she|her|hers)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var (key, value) in english.Where(p => p.Key.StartsWith("Luna.", StringComparison.Ordinal)))
        {
            // "Luna" is the speaker; the child is never described with a pronoun.
            Assert.DoesNotMatch(pronoun, value);
        }
        foreach (var (key, value) in spanish.Where(p => p.Key.StartsWith("Luna.", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("Luna AI", value, StringComparison.Ordinal);
            Assert.DoesNotContain("niños y niñas", value, StringComparison.Ordinal);
            Assert.DoesNotContain("hijo o hija", value, StringComparison.Ordinal);
            Assert.DoesNotContain("hijos e hijas", value, StringComparison.Ordinal);
        }
    }
}
