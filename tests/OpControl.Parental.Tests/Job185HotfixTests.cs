// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job185HotfixTests
{
    [Fact]
    public async Task Moon_click_opens_the_chat_and_it_stays_visible_for_at_least_ten_seconds()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new LunaWindow();
            window.Show();
            UiHarness.Pump(350, window.TickForTest);

            window.MouseDown(new Point(60, 60), MouseButton.Left);
            window.MouseUp(new Point(60, 60), MouseButton.Left);
            UiHarness.Pump(1300, window.TickForTest);

            Assert.Equal("Chat", window.ModeForTest);
            Assert.True(window.ChatRootVisibleForTest, "the chat root must stay visible after the morph");
            var chat = window.FindControl<Grid>("ChatRoot");
            Assert.NotNull(chat);
            Assert.True(IsEffectivelyVisible(chat!), "ChatRoot must not be hidden by a hidden parent");

            UiHarness.Pump(10_000, window.TickForTest);
            Assert.Equal("Chat", window.ModeForTest);
            Assert.True(window.ChatRootVisibleForTest, "the chat must still be visible after 10 seconds");
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Each_kid_tab_can_be_clicked_repeatedly_and_after_a_refresh()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var data = new FakeKidDataSource();
            var window = new MyOpControlWindow(data, new KidWindowOptions());
            window.Show();
            UiHarness.Pump(400);
            Assert.Equal("Home", window.CurrentScreenName);

            foreach (var (name, screen) in new[]
                     {
                         ("NavSchedule", "Schedule"), ("NavRules", "Rules"),
                         ("NavActivity", "Activity"), ("NavMessages", "Messages"),
                         ("NavHome", "Home")
                     })
            {
                for (var i = 0; i < 3; i++)
                {
                    Click(window, name);
                    Assert.Equal(screen, window.CurrentScreenName);
                }
            }

            // The window refreshes every 3 seconds; tabs must keep working afterwards.
            UiHarness.Pump(3_500);
            Click(window, "NavSchedule");
            Assert.Equal("Schedule", window.CurrentScreenName);
            Click(window, "NavHome");
            Assert.Equal("Home", window.CurrentScreenName);

            // The yellow button is the one control that worked in the report.
            Click(window, "MessageParentsButton");
            Assert.Equal("Messages", window.CurrentScreenName);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Kid_title_bar_holds_the_view_only_subtitle_and_theme_button()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new MyOpControlWindow(new FakeKidDataSource(), new KidWindowOptions());
            window.Show();
            UiHarness.Pump(300);
            var titleBar = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "TitleBar");
            Assert.NotNull(titleBar);
            var subtitle = window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "ViewOnlySubtitle");
            Assert.NotNull(subtitle);
            Assert.Equal(Loc.T("Client.Kid.ViewOnly"), subtitle!.Text);
            Assert.NotNull(window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "ThemeButton"));
            Assert.NotNull(window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "MessageParentsButton"));
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public void Luna_process_policy_keeps_the_moon_and_the_kid_window_separate()
    {
        var moon = new LunaProcessInfo(LunaProcessPolicy.ExecutableName, 1, "\"C:\\Program Files\\OpControlParental\\Luna\\OpControl.Parental.Luna.exe\"");
        var kid = new LunaProcessInfo(LunaProcessPolicy.ExecutableName, 1,
            "\"C:\\Program Files\\OpControlParental\\Luna\\OpControl.Parental.Luna.exe\" --kid-window --tab home");
        var other = new LunaProcessInfo("notepad.exe", 1, "notepad.exe");

        Assert.Equal(LunaProcessMode.Moon, LunaProcessPolicy.Classify(moon.Name, moon.CommandLine));
        Assert.Equal(LunaProcessMode.KidWindow, LunaProcessPolicy.Classify(kid.Name, kid.CommandLine));
        Assert.Equal(LunaProcessMode.Other, LunaProcessPolicy.Classify(other.Name, other.CommandLine));

        Assert.True(LunaProcessPolicy.CountsAsRunningMoon(moon, 1));
        Assert.False(LunaProcessPolicy.CountsAsRunningMoon(kid, 1));
        Assert.True(LunaProcessPolicy.ShouldStop(moon, 1));
        Assert.False(LunaProcessPolicy.ShouldStop(kid, 1), "the supervisor must never stop the kid window");
        Assert.False(LunaProcessPolicy.ShouldStop(moon, 2), "processes in another session are not ours");
        Assert.False(LunaProcessPolicy.IsKidCommandLine(null));
        Assert.True(LunaProcessPolicy.IsMoonCommandLine(null));

        // Both modes running at the same time: the moon is running, the kid
        // window is untouched, and only the moon is a stop target.
        var both = new[] { moon, kid, other };
        Assert.True(both.Any(p => LunaProcessPolicy.CountsAsRunningMoon(p, 1)));
        var targets = both.Where(p => LunaProcessPolicy.ShouldStop(p, 1)).ToList();
        Assert.Single(targets);
        Assert.Equal(moon, targets[0]);
        Assert.DoesNotContain(both.Where(p => p != moon),
            p => LunaProcessPolicy.CountsAsRunningMoon(p, 1));
        Assert.DoesNotContain(new[] { kid }, p => LunaProcessPolicy.CountsAsRunningMoon(p, 1));
        Assert.Empty(new[] { kid }.Where(p => LunaProcessPolicy.ShouldStop(p, 1)));
    }

    [Fact]
    public void Supervisor_source_uses_the_shared_policy_and_never_uses_the_title()
    {
        var root = FindRoot();
        var session = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows", "WindowsSession.cs"));
        var helper = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "HelperContext.cs"));
        Assert.Contains("LunaProcessPolicy.IsMoonCommandLine", session, StringComparison.Ordinal);
        Assert.Contains("LunaProcessPolicy.IsKidCommandLine", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("MainWindowTitle", session, StringComparison.Ordinal);
        Assert.DoesNotContain("MainWindowTitle", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Lock_logo_keeps_aspect_ratio_and_the_content_block_fits()
    {
        foreach (var (width, height) in new[] { (1600.0, 900.0), (1620.0, 1080.0), (2256.0, 1504.0) })
        {
            var layout = LockSceneLayout.For(width, height, dpiScale: height > 1400 ? 1.5 : 1.0);
            Assert.Equal(LockSceneLayout.LogoAspectRatio, layout.Logo.Width / layout.Logo.Height, 3);
            Assert.True(layout.PrimaryButton.Y > layout.TextColumn.Bottom);
            Assert.True(layout.SecondaryButton.Y >= layout.PrimaryButton.Bottom);
            Assert.True(layout.SecondaryButton.Bottom < height);
            Assert.True(layout.Logo.Right <= width && layout.Logo.Bottom <= height);
            Assert.True(layout.TextColumn.X >= 0 && layout.TextColumn.Right <= width);
        }
        var small = LockSceneLayout.For(1366, 768);
        Assert.Equal(LockSceneLayout.LogoAspectRatio, small.Logo.Width / small.Logo.Height, 3);
        Assert.True(small.SecondaryButton.Bottom < 768);
    }

    [Fact]
    public void Lock_messages_are_a_panel_owned_by_the_overlay_not_a_separate_topmost_window()
    {
        var root = FindRoot();
        var overlay = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "LockOverlay.cs"));
        var helper = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "HelperContext.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "LockMessagesForm.cs"));
        Assert.Contains("new LockMessagesForm()", overlay, StringComparison.Ordinal);
        Assert.Contains("panel.Show(owner)", overlay, StringComparison.Ordinal);
        Assert.Contains("ShowMessages", overlay, StringComparison.Ordinal);
        Assert.DoesNotContain("aboveLock: true", helper, StringComparison.Ordinal);
        Assert.Contains("PinPipeClient.SendMessageAsync", panel, StringComparison.Ordinal);
        Assert.Contains("Client.Messages.Status.Delivered", "Client." + MessageStatusRules.LabelKey(MessageUiStatus.Delivered));
        Assert.Contains("MarkSeenAsync", panel, StringComparison.Ordinal);
        Assert.Contains("MarkAcknowledgedAsync", panel, StringComparison.Ordinal);
        Assert.Contains("MaxLength = 500", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Luna_chat_root_is_not_inside_a_hidden_blob_after_the_morph()
    {
        var root = FindRoot();
        var window = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaWindow.axaml.cs"));
        var start = window.IndexOf("private void UpdateMorphOpen", StringComparison.Ordinal);
        var end = window.IndexOf("private void UpdateMorphClose", start, StringComparison.Ordinal);
        Assert.True(start > 0 && end > start);
        var body = window[start..end];
        Assert.Contains("SetBlobChrome(false)", body, StringComparison.Ordinal);
        Assert.Contains("_blob.IsVisible = true;", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_blob.IsVisible = false;", body, StringComparison.Ordinal);
        var xaml = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaWindow.axaml"));
        Assert.Contains("<Grid x:Name=\"ChatRoot\"", xaml, StringComparison.Ordinal);
    }

    private static void Click(Window window, string name)
    {
        var button = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == name);
        Assert.NotNull(button);
        var point = button!.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window);
        Assert.NotNull(point);
        window.MouseDown(point!.Value, MouseButton.Left);
        window.MouseUp(point.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static bool IsEffectivelyVisible(Visual visual)
    {
        if (!visual.IsVisible) return false;
        foreach (var ancestor in visual.GetVisualAncestors())
            if (!ancestor.IsVisible) return false;
        return true;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class FakeKidDataSource : IKidDataSource
    {
        private static readonly Guid Child = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private readonly List<ChildMessageDto> _messages =
        [
            new(Child, Guid.Empty, null, MessageDirection.ParentToChild, null, "Mom",
                "Hello!", null, false, false, DateTime.UtcNow.AddMinutes(-5), null, null, null, null,
                MessageStatus.Delivered, false)
        ];

        public Task<KidSnapshot> LoadAsync(CancellationToken ct)
        {
            var summary = new ChildSummaryDto(Child, "Ana", "X1", DateTime.UtcNow, true, 360, 4800, 30000,
                Weekdays: [new SummaryWeekdayDto(1, 360)],
                Schedules: [], Categories: [], Websites: [], Apps: [], AppsToday: [], AppsWeek: [],
                Visits: [], History: [], RecentMessages: [], UnreadMessages: 0);
            var state = new RuntimeLockStateDto(Child, "Ana", "Mom", false, "none", null, null,
                DateTime.UtcNow.AddHours(2), null, null, DateTime.UtcNow, DailyLimitRemainingSeconds: 16800);
            return Task.FromResult(new KidSnapshot(summary, state, _messages.ToList(), true));
        }

        public Task<PinPipeResponse> SendMessageAsync(string text, CancellationToken ct)
        {
            var message = new ChildMessageDto(Guid.NewGuid(), Child, null, MessageDirection.ChildToParent,
                null, string.Empty, text, null, false, false, DateTime.UtcNow, null, null, null, null,
                MessageStatus.Sent, false);
            _messages.Add(message);
            return Task.FromResult(new PinPipeResponse(true, Message: message));
        }

        public Task MarkSeenAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public Task MarkAcknowledgedAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public Task SetThemeAsync(string theme, CancellationToken ct) => Task.CompletedTask;
    }
}
