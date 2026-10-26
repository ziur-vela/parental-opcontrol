// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Xml.Linq;
using OpControl.Parental.Server.Services.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job184WebsiteTipTests
{
    [Theory]
    [InlineData(true, null)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, null)]
    public void Public_ai_tip_is_hidden_until_job_19(bool lunaOn, bool? blocked)
    {
        Assert.False(LunaUi.PublicAiBlockAvailable);
        Assert.False(LunaUi.ShowPublicAiBlockTip(lunaOn, blocked));
    }

    [Fact]
    public void Welcome_keeps_the_text_and_shows_a_disabled_coming_soon_button()
    {
        var root = FindRoot();
        var welcome = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Components", "Shared", "LunaWelcome.razor"));
        Assert.Contains("Luna.Why.Footer", welcome, StringComparison.Ordinal);
        Assert.Contains("LunaUi.PublicAiBlockAvailable", welcome, StringComparison.Ordinal);
        Assert.Contains("disabled", welcome, StringComparison.Ordinal);
        Assert.Contains("Luna.Common.ComingSoon", welcome, StringComparison.Ordinal);
        Assert.Contains("Luna.Child.AiBlockTip", File.ReadAllText(Path.Combine(root, "src",
            "OpControl.Parental.Server", "Components", "Pages", "LunaChild.razor")), StringComparison.Ordinal);
    }

    [Fact]
    public void Coming_soon_strings_exist_in_both_languages()
    {
        var root = FindRoot();
        foreach (var file in new[] { "AppStrings.resx", "AppStrings.es-MX.resx" })
        {
            var doc = XDocument.Load(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", file));
            var value = doc.Root!.Elements("data")
                .FirstOrDefault(e => (string?)e.Attribute("name") == "Luna.Common.ComingSoon")
                ?.Element("value")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(value));
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
