// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Xml.Linq;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job181WelcomeTests
{
    private static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    [Fact]
    public void New_welcome_strings_are_present_in_both_languages_and_neutral()
    {
        var english = ReadResx("AppStrings.resx");
        var spanish = ReadResx("AppStrings.es-MX.resx");
        var keys = english.Keys.Where(k => k.StartsWith("Luna.Welcome.", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(keys);
        foreach (var key in keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(english[key]), key);
            Assert.True(spanish.ContainsKey(key), key);
            Assert.False(string.IsNullOrWhiteSpace(spanish[key]), key);
        }
        foreach (var (_, value) in spanish.Where(p => p.Key.StartsWith("Luna.Welcome.", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("Luna AI", value, StringComparison.Ordinal);
            Assert.DoesNotContain("hijo o hija", value, StringComparison.Ordinal);
            Assert.DoesNotContain("hijos e hijas", value, StringComparison.Ordinal);
            Assert.DoesNotContain("niño o niña", value, StringComparison.Ordinal);
            Assert.DoesNotContain("niños y niñas", value, StringComparison.Ordinal);
        }
    }

    private static Dictionary<string, string> ReadResx(string name)
    {
        var path = Path.Combine(Root, "src", "OpControl.Parental.Server", "Resources", name);
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => e.Element("value")?.Value ?? "");
    }
}
