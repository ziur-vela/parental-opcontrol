// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job22cTests
{
    [Theory]
    [InlineData("0.17.28", "0.17.28", true, AboutStatusKind.UpToDate, false)]
    [InlineData("v0.17.28", "0.17.28", true, AboutStatusKind.UpToDate, false)]
    [InlineData("0.17.29", "0.17.28", true, AboutStatusKind.UpToDate, false)]
    [InlineData("0.17.19", "0.17.28", true, AboutStatusKind.UpdateAvailable, false)]
    [InlineData("0.17.19", "0.17.28", false, AboutStatusKind.UpdateAvailable, true)]
    [InlineData(null, "0.17.28", true, AboutStatusKind.UpdateAvailable, false)]
    [InlineData("", "0.17.28", true, AboutStatusKind.UpdateAvailable, false)]
    [InlineData("0.17.28", null, true, AboutStatusKind.UpToDate, false)]
    public void About_status_matches_the_latest_release_and_presence(
        string? client, string? latest, bool online, AboutStatusKind expected, bool offline)
    {
        var status = VersionDisplay.Status(client, latest, online);
        Assert.Equal(expected, status.Kind);
        Assert.Equal(offline, status.Offline);
    }

    [Fact]
    public void Version_labels_are_built_without_double_v_or_commit()
    {
        Assert.Equal("v0.17.28", VersionDisplay.VersionLabel("0.17.28"));
        Assert.Equal("v0.17.28", VersionDisplay.VersionLabel("v0.17.28"));
        Assert.Equal("v0.17.28", VersionDisplay.VersionLabel(" V0.17.28 "));
        Assert.Equal("", VersionDisplay.VersionLabel(null));
        Assert.Equal("14 Nov 2026", VersionDisplay.BuildDate("2026-11-14", "en"));
        Assert.Equal("14 Nov 2026 · 9e74a5f", VersionDisplay.BuildLine("2026-11-14", "9e74a5f", "en"));
        Assert.Contains("14", VersionDisplay.BuildDate("2026-11-14", "es-MX"), StringComparison.Ordinal);
    }

    [Fact]
    public void Last_seen_uses_today_yesterday_and_dates_in_the_family_zone()
    {
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var zone = TimeZoneInfo.Utc;
        Assert.Equal("Today", VersionDisplay.LastSeen(new DateTime(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc), zone, "Today", "Yesterday", "en", now));
        Assert.Equal("Yesterday", VersionDisplay.LastSeen(new DateTime(2026, 11, 13, 23, 0, 0, DateTimeKind.Utc), zone, "Today", "Yesterday", "en", now));
        Assert.Equal("31 Oct", VersionDisplay.LastSeen(new DateTime(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc), zone, "Today", "Yesterday", "en", now));
        Assert.Equal("31 Dec 2025", VersionDisplay.LastSeen(new DateTime(2025, 12, 31, 10, 0, 0, DateTimeKind.Utc), zone, "Today", "Yesterday", "en", now));
        Assert.Equal("", VersionDisplay.LastSeen(null, zone, "Today", "Yesterday", "en", now));
    }

    [Fact]
    public void Version_strings_exist_in_both_languages()
    {
        var root = FindRoot();
        var en = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", "AppStrings.resx"));
        var es = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", "AppStrings.es-MX.resx"));
        foreach (var key in new[]
                 {
                     "AboutPage.Component", "AboutPage.VersionColumn", "AboutPage.StatusColumn",
                     "AboutPage.LastSeenColumn", "AboutPage.Running", "AboutPage.UpToDate",
                     "AboutPage.UpdateAvailable", "AboutPage.OfflineSuffix", "AboutPage.Today",
                     "AboutPage.Yesterday", "AboutPage.Build", "Layout.VersionLabel", "Layout.Beta"
                 })
        {
            Assert.Contains($"name=\"{key}\"", en, StringComparison.Ordinal);
            Assert.Contains($"name=\"{key}\"", es, StringComparison.Ordinal);
        }
        Assert.Contains("Parental OpControl is in beta. Features may change. Report anything that looks wrong to Hendry.", en, StringComparison.Ordinal);
        Assert.Contains("Parental OpControl está en beta. Las funciones pueden cambiar. Reporta a Hendry cualquier cosa que se vea mal.", es, StringComparison.Ordinal);
        Assert.Contains("Up to date", en, StringComparison.Ordinal);
        Assert.Contains("Actualización disponible", es, StringComparison.Ordinal);
        Assert.Contains("Hoy", es, StringComparison.Ordinal);
        Assert.Contains("Ayer", es, StringComparison.Ordinal);

        var kidEn = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "en.json"));
        var kidEs = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "es-MX.json"));
        Assert.Contains("\"Client.Kid.About.Client\": \"This laptop\"", kidEn, StringComparison.Ordinal);
        Assert.Contains("\"Client.Kid.About.Client\": \"Esta laptop\"", kidEs, StringComparison.Ordinal);
        Assert.Contains("This app is still being built, so some things may change.", kidEn, StringComparison.Ordinal);
        Assert.Contains("Esta app todavía se está construyendo, así que algunas cosas pueden cambiar.", kidEs, StringComparison.Ordinal);
    }

    [Fact]
    public void Sidebar_about_and_kid_source_use_the_approved_layout()
    {
        var root = FindRoot();
        var layout = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Layout", "MainLayout.razor"));
        Assert.Contains("sidebar-version-card", layout, StringComparison.Ordinal);
        Assert.Contains("sidebar-version-card-head", layout, StringComparison.Ordinal);
        Assert.Contains("sidebar-version-value", layout, StringComparison.Ordinal);
        Assert.Contains("sidebar-version-meta", layout, StringComparison.Ordinal);
        Assert.Contains("VersionDisplay.VersionLabel(Build.Version)", layout, StringComparison.Ordinal);
        Assert.Contains("VersionDisplay.BuildLine(Build.BuildDate, Build.ShortCommit, CurrentLanguage)", layout, StringComparison.Ordinal);

        var about = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Shared", "AboutPanel.razor"));
        Assert.Contains("about-beta-banner", about, StringComparison.Ordinal);
        Assert.Contains("about-version-table", about, StringComparison.Ordinal);
        Assert.Contains("about-version-row-head", about, StringComparison.Ordinal);
        Assert.Contains("about-cell-label", about, StringComparison.Ordinal);
        Assert.Contains("VersionDisplay.Status", about, StringComparison.Ordinal);
        Assert.Contains("AboutPage.UpToDate", about, StringComparison.Ordinal);
        Assert.Contains("AboutPage.UpdateAvailable", about, StringComparison.Ordinal);
        Assert.Contains("AboutPage.OfflineSuffix", about, StringComparison.Ordinal);
        Assert.DoesNotContain("about-beta-note", about, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "wwwroot", "css", "parental.css"));
        Assert.Contains(".sidebar-version-value", css, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap", css, StringComparison.Ordinal);
        Assert.Contains(".about-version-row", css, StringComparison.Ordinal);
        Assert.Contains(".about-version-row-head { display: none; }", css, StringComparison.Ordinal);
        Assert.Contains(".about-cell-label { display: block; }", css, StringComparison.Ordinal);

        var kid = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        Assert.Contains("KidVersionValue", kid, StringComparison.Ordinal);
        Assert.Contains("versionHead", kid, StringComparison.Ordinal);
        Assert.Contains("Client.Kid.About.VersionNote", kid, StringComparison.Ordinal);
        Assert.Contains("Client.Kid.About.Client", kid, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
