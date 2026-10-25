// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job181Tests
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

    private static ParentalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("job181-" + Guid.NewGuid())
            .Options);

    [Fact]
    public void Kid_theme_store_round_trips_the_shared_choice()
    {
        var path = Path.Combine(Path.GetTempPath(), "opcontrol-kid-theme-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            Assert.Null(KidThemeStore.ReadLocal(path));
            KidThemeStore.WriteLocal("gray", path);
            Assert.Equal(LunaThemes.Gray, KidThemeStore.ReadLocal(path));
            Assert.Equal(LunaThemes.Night, KidThemeStore.Toggle(KidThemeStore.ReadLocal(path)));
            Assert.Equal(LunaThemes.Night, KidThemeStore.Normalize("anything-else"));
            Assert.Equal(LunaThemes.Gray, KidThemeStore.Normalize("GRAY"));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Tray_open_luna_is_present_only_when_luna_is_enabled(bool enabled, bool expected) =>
        Assert.Equal(expected, KidTrayPolicy.ShowOpenLuna(enabled));

    [Fact]
    public async Task Child_summary_carries_the_shared_theme_for_the_helper()
    {
        await using var db = NewDb();
        var family = new Family { Name = "Theme family", DefaultLanguage = "en" };
        var child = new Child { FamilyId = family.Id, Name = "Ana" };
        var device = new Device { FamilyId = family.Id, ChildId = child.Id, Name = "X1 laptop", TimeZoneId = "UTC" };
        db.Families.Add(family);
        db.Children.Add(child);
        db.Devices.Add(device);
        db.ChildLunaSettings.Add(new ChildLunaSettings
        {
            FamilyId = family.Id,
            ChildId = child.Id,
            Theme = LunaThemes.Gray
        });
        await db.SaveChangesAsync();

        var summary = await new ChildSummaryService(db).BuildAsync(device);
        Assert.Equal(LunaThemes.Gray, summary.Theme);
    }

    [Fact]
    public async Task Child_summary_defaults_to_night_sky()
    {
        await using var db = NewDb();
        var family = new Family { Name = "Default family", DefaultLanguage = "en" };
        var child = new Child { FamilyId = family.Id, Name = "Leo" };
        var device = new Device { FamilyId = family.Id, ChildId = child.Id, Name = "Yoga", TimeZoneId = "UTC" };
        db.Families.Add(family);
        db.Children.Add(child);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var summary = await new ChildSummaryService(db).BuildAsync(device);
        Assert.Equal(LunaThemes.Night, summary.Theme);
    }

    [Fact]
    public void New_kid_strings_are_neutral_and_use_IA_in_spanish()
    {
        var english = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Localization", "en.json")))!;
        var spanish = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Localization", "es-MX.json")))!;
        foreach (var (_, value) in english.Where(p => p.Key.StartsWith("Client.Kid.", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("Luna AI", value, StringComparison.Ordinal);
            Assert.DoesNotMatch(new Regex(@"\b(boy|girl|son|daughter)\b", RegexOptions.IgnoreCase), value);
        }
        foreach (var (_, value) in spanish.Where(p => p.Key.StartsWith("Client.Kid.", StringComparison.Ordinal)))
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
