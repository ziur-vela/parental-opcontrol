// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;
using Avalonia.Headless;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public class Job20BAboutTests
{
    private static readonly Guid Child = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Device = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Client_version_shown_matches_the_assembly_version()
    {
        var local = BuildMetadata.Read(typeof(MyOpControlWindow).Assembly);
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var data = new AboutKidData(local with
            {
                ServerVersion = "0.17.17",
                ServerBuildDate = "2026-10-27",
                ServerDisplayVersion = "v0.17.17 · build 2026-10-27"
            });
            var window = new MyOpControlWindow(data, new KidWindowOptions(Tab: "about"));
            window.Show();
            UiHarness.Pump(300);

            Assert.Equal("About", window.CurrentScreenName);
            Assert.StartsWith(local.ClientVersion, window.AboutClientValueForTest);
            Assert.Contains("v0.17.17", window.AboutServerValueForTest);
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void License_text_loads_from_the_embedded_repo_license()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new MyOpControlWindow(new AboutKidData(LocalAbout()), new KidWindowOptions(Tab: "about"));
            window.Show();
            UiHarness.Pump(300);

            Assert.Equal(AboutContent.LicenseText, window.LicenseTextForTest);
            Assert.True(window.LicenseTextForTest.Length > 1000);
            Assert.Contains("PolyForm Noncommercial License 1.0.0", window.LicenseTextForTest);
            Assert.Contains("Required Notice: Copyright (c) 2026 Ziur / OpControl IT Solutions",
                window.LicenseTextForTest);
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void Credits_include_every_package_in_the_build_graph_and_the_fonts()
    {
        var credits = AboutContent.Credits.Components;
        Assert.NotEmpty(credits);
        var names = credits.Select(credit => credit.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Avalonia", names);
        Assert.Contains("LiveMarkdown.Avalonia", names);
        Assert.Contains("LiveMarkdown.Avalonia.Math", names);
        Assert.Contains("Fredoka (font)", names);
        Assert.Contains("Nunito (font)", names);
        Assert.All(credits, credit =>
        {
            Assert.False(string.IsNullOrWhiteSpace(credit.Name), "A credit has no name");
            Assert.False(string.IsNullOrWhiteSpace(credit.License), $"{credit.Name} has no license");
            Assert.NotEqual("Unknown", credit.License);
        });

        foreach (var package in BundledPackages())
            Assert.Contains(package, names);
    }

    [Fact]
    public void Third_party_packages_all_have_a_known_license()
    {
        var credits = AboutContent.Credits.Components;
        var unknown = credits.Where(credit => credit.License is "See license URL" or "Unknown").ToList();
        Assert.True(unknown.Count == 0,
            "These components need a resolved license: " + string.Join(", ", unknown.Select(c => c.Name)));
    }

    [Fact]
    public void Unchanged_poll_does_not_rebuild_home_or_lose_the_live_countdown()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var data = new AboutKidData(LocalAbout());
            var window = new MyOpControlWindow(data, new KidWindowOptions(Tab: "home"));
            window.Show();
            UiHarness.Pump(300);
            var renders = window.RenderCountForTest;
            var before = window.HomeTimeLeftForTest;
            Assert.False(string.IsNullOrWhiteSpace(before));

            data.State = data.State! with
            {
                UpdatedUtc = DateTime.UtcNow.AddMinutes(1),
                DailyLimitRemainingSeconds = 15_600
            };
            window.RefreshForTestAsync().GetAwaiter().GetResult();
            UiHarness.Pump(80);

            Assert.Equal(renders, window.RenderCountForTest);
            Assert.NotEqual(before, window.HomeTimeLeftForTest);
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData("night")]
    [InlineData("gray")]
    public void About_renders_in_both_themes(string theme)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new MyOpControlWindow(new AboutKidData(LocalAbout()), new KidWindowOptions(Tab: "about", Theme: theme));
            window.Show();
            UiHarness.Pump(300);
            Assert.Equal("About", window.CurrentScreenName);
            Assert.False(string.IsNullOrWhiteSpace(window.AboutClientValueForTest));
            Assert.NotEmpty(window.AboutCreditNamesForTest);
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void Sidebar_link_tray_item_and_bilingual_strings_are_wired()
    {
        var root = FindRoot();
        var window = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        Assert.Contains("AboutLink", window, StringComparison.Ordinal);
        Assert.Contains("ShowAboutAsync", window, StringComparison.Ordinal);
        Assert.Contains("\"about\" => Screen.About", window, StringComparison.Ordinal);

        var helper = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "HelperContext.cs"));
        Assert.Contains("_trayAboutItem", helper, StringComparison.Ordinal);
        Assert.Contains("ShowMyOpControl(\"about\"", helper, StringComparison.Ordinal);

        using var english = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "src", "OpControl.Parental.Localization", "en.json")));
        using var spanish = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "src", "OpControl.Parental.Localization", "es-MX.json")));
        foreach (var key in new[]
                 {
                     "Client.Kid.About.Link", "Client.Kid.About.Version", "Client.Kid.About.License",
                     "Client.Kid.About.Credits", "Client.Kid.About.ReadFullLicense", "Client.Kid.About.Close"
                 })
        {
            Assert.True(english.RootElement.TryGetProperty(key, out _), "en.json missing " + key);
            Assert.True(spanish.RootElement.TryGetProperty(key, out _), "es-MX.json missing " + key);
        }
    }

    private static AboutInfoDto LocalAbout()
    {
        var local = BuildMetadata.Read(typeof(MyOpControlWindow).Assembly);
        return local with
        {
            ServerVersion = local.ClientVersion,
            ServerBuildDate = local.ClientBuildDate,
            ServerDisplayVersion = $"v{local.ClientVersion} · build {local.ClientBuildDate}"
        };
    }

    private static HashSet<string> BundledPackages()
    {
        var assetsPath = Path.Combine(FindRoot(), "src", "OpControl.Parental.Luna", "obj", "project.assets.json");
        Assert.True(File.Exists(assetsPath), "Luna project.assets.json not found: " + assetsPath);
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!document.RootElement.TryGetProperty("targets", out var targets)) return result;
        if (!document.RootElement.TryGetProperty("libraries", out var libraries)) return result;
        foreach (var target in targets.EnumerateObject())
        {
            if (!target.Name.StartsWith("net10.0", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var package in target.Value.EnumerateObject())
            {
                var slash = package.Name.IndexOf('/');
                if (slash <= 0) continue;
                if (!libraries.TryGetProperty(package.Name, out var library) ||
                    library.GetProperty("type").GetString() != "package") continue;
                var assets = package.Value;
                var bundled = assets.TryGetProperty("runtime", out _) ||
                              assets.TryGetProperty("compile", out _) ||
                              assets.TryGetProperty("runtimeTargets", out _) ||
                              assets.TryGetProperty("native", out _);
                if (bundled) result.Add(package.Name[..slash]);
            }
        }
        return result;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class AboutKidData : IKidDataSource
    {
        private readonly List<ChildMessageDto> _messages = [];

        public AboutKidData(AboutInfoDto about)
        {
            About = about;
            Summary = new ChildSummaryDto(Child, "Ana", "X1", DateTime.UtcNow, true, 360, 4800, 30000,
                Weekdays: [new SummaryWeekdayDto(1, 360)],
                Schedules: [], Categories: [], Websites: [], Apps: [], AppsToday: [], AppsWeek: [],
                Visits: [], History: [], RecentMessages: [], UnreadMessages: 0);
            State = new RuntimeLockStateDto(Child, "Ana", "Mom", false, "none", null, null,
                DateTime.UtcNow.AddHours(2), null, null, DateTime.UtcNow,
                DailyLimitRemainingSeconds: 16800, DailyLimitMinutes: 360);
        }

        public AboutInfoDto About { get; }
        public ChildSummaryDto Summary { get; }
        public RuntimeLockStateDto? State { get; set; }

        public Task<KidSnapshot> LoadAsync(CancellationToken ct) =>
            Task.FromResult(new KidSnapshot(Summary, State, _messages.ToList(), true));

        public Task<PinPipeResponse> SendMessageAsync(string text, CancellationToken ct) =>
            Task.FromResult(new PinPipeResponse(true));

        public Task MarkSeenAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public Task MarkAcknowledgedAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public Task SetThemeAsync(string theme, CancellationToken ct) => Task.CompletedTask;
        public Task<AboutInfoDto> GetAboutAsync(CancellationToken ct) => Task.FromResult(About);
    }
}
