// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.VisualTree;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public class Job20CAboutPolishTests
{
    private static readonly Guid Child = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Server_version_is_shown_with_a_single_v()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var data = new AboutKidData(AboutWithServer("v0.17.18"));
            var window = new MyOpControlWindow(data, new KidWindowOptions(Tab: "about"));
            window.Show();
            UiHarness.Pump(300);

            Assert.StartsWith("v0.17.18", window.AboutServerValueForTest);
            Assert.DoesNotContain("vv", window.AboutServerValueForTest);
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData("en", "Built with AI", "Claude", "Anthropic", "OpenCode", "DeepSeek")]
    [InlineData("es-MX", "Construido con IA", "Claude", "Anthropic", "OpenCode", "DeepSeek")]
    public void Ai_credits_are_present_in_both_languages(string language, string title, params string[] names)
    {
        Loc.SetLanguage(language);
        try
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
            session.Dispatch(() =>
            {
                var window = new MyOpControlWindow(new AboutKidData(AboutWithServer("v0.17.18")),
                    new KidWindowOptions(Tab: "about", Language: language));
                window.Show();
                UiHarness.Pump(300);

                var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(TextOf).ToList();
                Assert.Contains(texts, text => text.Contains(title, StringComparison.Ordinal));
                Assert.Contains(texts, text => names.All(name => text.Contains(name, StringComparison.Ordinal)));
                Assert.Contains(texts, text => text == (language == "es-MX" ? "Inicio" : "Home"));
                Assert.Contains(texts, text => text == (language == "es-MX" ? "Acerca de" : "About"));
                window.Close();
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            Loc.SetLanguage(Loc.English);
        }
    }

    [Fact]
    public void Ai_credits_are_above_the_third_party_list()
    {
        var source = File.ReadAllText(Path.Combine(FindRoot(), "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        var ai = source.IndexOf("Client.Kid.About.BuiltWithAiTitle", StringComparison.Ordinal);
        var thirdParty = source.IndexOf("Client.Kid.About.ThirdParty", StringComparison.Ordinal);
        Assert.True(ai >= 0);
        Assert.True(thirdParty > ai);
    }

    [Fact]
    public void Sidebar_message_button_is_centered_and_raised()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new MyOpControlWindow(new AboutKidData(AboutWithServer("v0.17.18")),
                new KidWindowOptions(Tab: "about"));
            window.Show();
            UiHarness.Pump(300);
            var button = window.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(candidate => candidate.Name == "MessageParentsButton");
            Assert.NotNull(button);
            Assert.Equal(HorizontalAlignment.Center, button!.HorizontalAlignment);
            Assert.True(button.Margin.Bottom >= 16, "The button needs at least 16 px below it");
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void About_header_opcontrol_logo_loads_at_the_right_aspect_ratio()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new MyOpControlWindow(new AboutKidData(AboutWithServer("v0.17.18")),
                new KidWindowOptions(Tab: "about"));
            window.Show();
            UiHarness.Pump(300);
            var logo = window.GetVisualDescendants().OfType<Image>()
                .FirstOrDefault(candidate => candidate.Name == "OpControlLogo");
            Assert.NotNull(logo);
            Assert.NotNull(logo!.Source);
            var size = AboutContent.OpControlLogoPixelSize();
            Assert.NotNull(size);
            Assert.True(size!.Value.Width > size.Value.Height,
                $"The OpControl lockup should be wider than it is tall (source {size.Value.Width}x{size.Value.Height})");
            Assert.Equal(40d, logo.Height);
            Assert.Equal(40d * size.Value.Width / size.Value.Height, logo.Width, 1);
            var ratio = logo.Width / logo.Height;
            Assert.InRange(ratio, (double)size.Value.Width / size.Value.Height - 0.05,
                (double)size.Value.Width / size.Value.Height + 0.05);
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void Dashboard_footer_has_the_ai_line_in_both_languages()
    {
        var root = FindRoot();
        var layout = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Components", "Layout", "MainLayout.razor"));
        Assert.Contains("Layout.BuiltWithAi", layout, StringComparison.Ordinal);
        var english = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Resources", "AppStrings.resx"));
        var spanish = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Resources", "AppStrings.es-MX.resx"));
        Assert.Contains("code written with **OpenCode**, the open-source AI coding agent, using **DeepSeek V4.1 Flash** by DeepSeek", english, StringComparison.Ordinal);
        Assert.Contains("código escrito con **OpenCode**, el agente de programación con IA de código abierto, usando **DeepSeek V4.1 Flash** de DeepSeek", spanish, StringComparison.Ordinal);
    }

    private static string TextOf(TextBlock block) =>
        block.Text ?? (block.Inlines is null
            ? string.Empty
            : string.Concat(block.Inlines.OfType<Run>().Select(run => run.Text)));

    private static AboutInfoDto AboutWithServer(string serverVersion)
    {
        var local = BuildMetadata.Read(typeof(MyOpControlWindow).Assembly);
        return local with
        {
            ServerVersion = serverVersion,
            ServerBuildDate = "2026-10-27",
            ServerDisplayVersion = serverVersion + " · build 2026-10-27"
        };
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
        public AboutKidData(AboutInfoDto about) => About = about;

        public AboutInfoDto About { get; }

        public Task<KidSnapshot> LoadAsync(CancellationToken ct)
        {
            var summary = new ChildSummaryDto(Child, "Ana", "X1", DateTime.UtcNow, true, 360, 4800, 30000,
                Weekdays: [new SummaryWeekdayDto(1, 360)],
                Schedules: [], Categories: [], Websites: [], Apps: [], AppsToday: [], AppsWeek: [],
                Visits: [], History: [], RecentMessages: [], UnreadMessages: 0);
            var state = new RuntimeLockStateDto(Child, "Ana", "Mom", false, "none", null, null,
                DateTime.UtcNow.AddHours(2), null, null, DateTime.UtcNow,
                DailyLimitRemainingSeconds: 16800, DailyLimitMinutes: 360);
            return Task.FromResult(new KidSnapshot(summary, state, [], true));
        }

        public Task<PinPipeResponse> SendMessageAsync(string text, CancellationToken ct) =>
            Task.FromResult(new PinPipeResponse(true));

        public Task MarkSeenAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public Task MarkAcknowledgedAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public Task SetThemeAsync(string theme, CancellationToken ct) => Task.CompletedTask;
        public Task<AboutInfoDto> GetAboutAsync(CancellationToken ct) => Task.FromResult(About);
    }
}
