// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using LunaPalettes = OpControl.Parental.Luna.LunaThemes;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public class Job188ChatTests
{
    [Theory]
    // Work area in DIP, after the taskbar and at the given render scaling.
    [InlineData(1920, 1040, 1.0, 960, 832, 1152, 936)]
    [InlineData(1280, 693.333, 1.0, 640, 600, 768, 623.999)]
    [InlineData(960, 520, 1.0, 520, 468, 576, 468)]
    [InlineData(1366, 728, 1.0, 683, 600, 819.6, 655.2)]
    [InlineData(911, 485.333, 1.0, 520, 436.8, 546.6, 436.8)]
    [InlineData(683, 364, 1.0, 409.8, 327.6, 409.8, 327.6)]
    public void Default_chat_size_is_half_the_width_and_eighty_percent_of_the_height(
        double workWidth, double workHeight, double unusedScale,
        double expectedWidth, double expectedHeight, double expectedMaxWidth, double expectedMaxHeight)
    {
        _ = unusedScale;
        var defaults = LunaChatSize.Default(workWidth, workHeight);
        var max = LunaChatSize.Max(workWidth, workHeight);

        Assert.Equal(expectedWidth, defaults.Width, 1);
        Assert.Equal(expectedHeight, defaults.Height, 1);
        Assert.Equal(expectedMaxWidth, max.Width, 1);
        Assert.Equal(expectedMaxHeight, max.Height, 1);
        Assert.True(defaults.Width <= max.Width + 0.01);
        Assert.True(defaults.Height <= max.Height + 0.01);
        Assert.True(max.Width <= workWidth - LunaChatSize.ScreenMargin + 0.01);
        Assert.True(max.Height <= workHeight - LunaChatSize.ScreenMargin + 0.01);
    }

    [Fact]
    public void Default_size_uses_the_screen_scaling()
    {
        // The same physical 1920x1080 working area at 100%, 150% and 200%.
        foreach (var (scale, expectedWidth, expectedHeight) in new[]
                 {
                     (1.0, 960.0, 832.0),
                     (1.5, 640.0, 600.0),
                     (2.0, 520.0, 468.0)
                 })
        {
            var workWidth = 1920 / scale;
            var workHeight = 1040 / scale;
            var size = LunaChatSize.Default(workWidth, workHeight);
            Assert.Equal(expectedWidth, size.Width, 1);
            Assert.Equal(expectedHeight, size.Height, 1);

            var end = LunaWindow.MorphRectForTest(1, opening: true, size.Width, size.Height);
            Assert.Equal(size.Width, end.W, 1);
            Assert.Equal(size.Height, end.H, 1);
            var start = LunaWindow.MorphRectForTest(0, opening: true, size.Width, size.Height);
            Assert.Equal((12, 12, 96, 96, 48), start);
        }
    }

    [Fact]
    public void A_remembered_size_is_clamped_to_the_current_screen()
    {
        var size = LunaChatSize.Clamp(2000, 2000, 1920, 1040);
        Assert.Equal(1152, size.Width, 1);
        Assert.Equal(936, size.Height, 1);

        var small = LunaChatSize.Clamp(100, 100, 911, 485);
        Assert.Equal(520, small.Width, 1);
        Assert.Equal(436.5, small.Height, 1);
    }

    [Theory]
    [InlineData(960, 832)]
    [InlineData(683, 600)]
    [InlineData(520, 436)]
    public void Morph_starts_as_the_moon_and_ends_on_the_new_chat_size(double width, double height)
    {
        var start = LunaWindow.MorphRectForTest(0, opening: true, width, height);
        Assert.Equal((12, 12, 96, 96, 48), start);

        var end = LunaWindow.MorphRectForTest(1, opening: true, width, height);
        Assert.Equal(width, end.W, 1);
        Assert.Equal(height, end.H, 1);
        Assert.Equal(0, end.X, 1);
        Assert.Equal(0, end.Y, 1);
        Assert.Equal(22, end.R, 1);

        var mid = LunaWindow.MorphRectForTest(0.55, opening: true, width, height);
        Assert.Equal(width * 400.0 / 420.0, mid.W, 1);
        Assert.Equal(height * 320.0 / 600.0, mid.H, 1);
        for (var step = 0; step <= 20; step++)
        {
            var current = LunaWindow.MorphRectForTest(step / 20.0, opening: true, width, height);
            Assert.True(current.W >= 12 - 0.01);
            Assert.True(current.H >= 96 - 0.01);
            Assert.True(current.W <= width + 0.01);
            Assert.True(current.H <= height + 0.01);
        }
    }

    [Fact]
    public void Every_subject_bubble_has_at_least_4_5_to_1_contrast_in_both_themes()
    {
        foreach (var palette in new[] { LunaPalettes.Night, LunaPalettes.Gray })
        {
            foreach (var subject in new[] { "math", "science", "history", "dictionary", "languages", "curious" })
            {
                var style = palette.Subject(subject);
                var ratio = LunaColor.ContrastRatio(style.Tile, style.Ink);
                Assert.True(ratio >= 4.5,
                    $"{palette.Key}/{subject} tile {style.Tile} vs ink {style.Ink} is {ratio:0.00}:1");
            }
        }

        Assert.True(LunaColor.ContrastRatio(
            LunaPalettes.Night.Subject("math").Tile, Color.Parse("#15103a")) >= 4.5);
    }

    [Fact]
    public async Task Luna_window_opens_at_the_new_default_size()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var window = new LunaWindow();
            window.Show();
            UiHarness.Pump(400);
            // The headless screen is 1920x1280 at 100%: 50% x 80%.
            Assert.Equal(960, window.ChatSizeForTest.Width, 1);
            Assert.Equal(1024, window.ChatSizeForTest.Height, 1);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public void Remembered_chat_size_round_trips_and_rejects_bad_values()
    {
        Assert.Equal((960.0, 832.0), LunaWindow.ParseChatSize("960x832"));
        Assert.Equal((683.0, 600.0), LunaWindow.ParseChatSize("683,600"));
        Assert.Null(LunaWindow.ParseChatSize("960"));
        Assert.Null(LunaWindow.ParseChatSize("0x600"));
        Assert.Null(LunaWindow.ParseChatSize("widexhigh"));
        Assert.Equal("960x832", LunaWindow.FormatChatSize(960, 832));
        Assert.Equal("683.5x600", LunaWindow.FormatChatSize(683.46, 600));
    }

    [Fact]
    public void The_size_is_saved_on_resize_and_loaded_on_startup()
    {
        var root = FindRoot();
        var window = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaWindow.axaml.cs"));
        Assert.Contains("luna-chat-size.txt", window, StringComparison.Ordinal);
        Assert.Contains("LoadChatSize()", window, StringComparison.Ordinal);
        Assert.Contains("SaveChatSize();", window, StringComparison.Ordinal);
        Assert.Contains("Math.Min(LunaChatSize.MinWidth, maxW)", window, StringComparison.Ordinal);
        Assert.Contains("LunaChatSize.Max(workWidth, workHeight)", window, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxChatWidth", window, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxChatHeight", window, StringComparison.Ordinal);
    }

    [Fact]
    public void Child_bubbles_use_the_design_ink_and_the_bubble_fraction_is_88_percent()
    {
        var root = FindRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaAppView.cs"));
        Assert.Contains(": style.Ink;", source, StringComparison.Ordinal);
        Assert.Contains("BubbleWidthFraction = 0.88", source, StringComparison.Ordinal);
        Assert.Contains("BubbleColumns(withFace: true)", source, StringComparison.Ordinal);
        Assert.Contains("BubbleColumns(withFace: false)", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bubbles_use_the_wider_chat_column()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var (window, view, api) = CreateView();
            try
            {
                var longChild = string.Join(" ", Enumerable.Repeat("fractions with pieces", 12));
                var longLuna = string.Join(" ", Enumerable.Repeat("Let us work through the pieces together.", 14));
                view.OpenChatForTest(Chat(), Messages(
                    (LunaChatRoles.Child, longChild),
                    (LunaChatRoles.Luna, longLuna)));
                UiHarness.Pump(500);

                var childText = view.GetVisualDescendants().OfType<TextBlock>()
                    .First(t => t.Text == longChild);
                var childBubble = childText.GetVisualAncestors().OfType<Border>().First();
                var lunaContent = view.GetVisualDescendants().OfType<LunaRichContent>().Last();
                var lunaBubble = lunaContent.GetVisualAncestors().OfType<Border>().First();
                var column = view.Bounds.Width - 24;

                Assert.True(childBubble.Bounds.Width <= column * 0.88 + 1,
                    $"child bubble {childBubble.Bounds.Width} > 88% of {column}");
                Assert.True(childBubble.Bounds.Width > column * 0.70,
                    $"child bubble {childBubble.Bounds.Width} is still narrow");
                Assert.True(lunaBubble.Bounds.Width <= column * 0.88 + 1,
                    $"Luna bubble {lunaBubble.Bounds.Width} > 88% of {column}");
                Assert.True(lunaBubble.Bounds.Width > column * 0.55,
                    $"Luna bubble {lunaBubble.Bounds.Width} is still narrow");
            }
            finally
            {
                window.Close();
                api.Dispose();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Opening_starts_at_the_latest_message_and_sending_keeps_the_bottom_visible()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var (window, view, api) = CreateView();
            try
            {
                view.OpenChatForTest(Chat(), ManyMessages(20));
                UiHarness.Pump(500);
                Assert.True(NearBottom(view), "opening an existing chat should start at the latest message");
                Assert.False(view.NewMessagePillVisibleForTest);

                view.ScrollToStartForTest();
                UiHarness.Pump(300);
                Assert.True(view.UserScrolledUpForTest);

                var pending = view.AddPendingChildForTest("What is 1/4 times 5/6?");
                Assert.NotNull(pending);
                UiHarness.Pump(400);
                Assert.True(NearBottom(view), "the child's message and the thinking animation must be visible");
                Assert.False(view.NewMessagePillVisibleForTest);
                Assert.True(view.FollowTailForTest);
            }
            finally
            {
                window.Close();
                api.Dispose();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task A_short_reply_scrolls_to_the_bottom()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var (window, view, api) = CreateView();
            try
            {
                view.OpenChatForTest(Chat(), ManyMessages(20));
                UiHarness.Pump(500);
                view.DeliverReplyForTest("Yes, 5 and 24 is right. Well done!");
                view.AdvanceStreamingForTest(100);
                UiHarness.Pump(800);
                Assert.True(NearBottom(view), "a short reply should leave the chat at the bottom");
                Assert.False(view.NewMessagePillVisibleForTest);
            }
            finally
            {
                window.Close();
                api.Dispose();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task A_reply_taller_than_the_view_keeps_its_first_line_in_view()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var (window, view, api) = CreateView();
            try
            {
                view.OpenChatForTest(Chat(), ManyMessages(20));
                UiHarness.Pump(500);
                var longReply = string.Join("\n\n", Enumerable.Range(1, 40)
                    .Select(i => $"**Step {i}.** Multiply the top numbers and then the bottom numbers, one piece at a time."));
                view.DeliverReplyForTest(longReply);
                view.AdvanceStreamingForTest(200);
                UiHarness.Pump(800);

                var scroll = view.MessagesScrollForTest!;
                var row = view.LastMessageRowForTest!;
                Assert.True(row.Bounds.Height > scroll.Viewport.Height,
                    $"reply {row.Bounds.Height} should be taller than the view {scroll.Viewport.Height}");
                var maximum = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                Assert.True(scroll.Offset.Y < maximum - 100, "a tall reply should not jump to the bottom");
                var top = row.TranslatePoint(new Point(0, 0), scroll);
                Assert.NotNull(top);
                Assert.InRange(top!.Value.Y, -1, 40);
            }
            finally
            {
                window.Close();
                api.Dispose();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task A_child_who_scrolled_up_gets_a_pill_and_is_not_moved()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var (window, view, api) = CreateView();
            try
            {
                view.OpenChatForTest(Chat(), ManyMessages(20));
                UiHarness.Pump(500);
                view.ScrollToStartForTest();
                UiHarness.Pump(300);
                Assert.True(view.UserScrolledUpForTest);
                var before = view.MessagesScrollForTest!.Offset.Y;

                view.DeliverReplyForTest("Here is the answer: five twenty-fourths.");
                view.AdvanceStreamingForTest(100);
                UiHarness.Pump(400);

                Assert.Equal(before, view.MessagesScrollForTest!.Offset.Y, 1);
                Assert.True(view.UserScrolledUpForTest);
                Assert.True(view.NewMessagePillVisibleForTest, "the New message pill should be shown");

                var pill = view.GetVisualDescendants().OfType<Button>()
                    .First(b => b.Name == "NewMessagePill");
                pill.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                UiHarness.Pump(600);
                Assert.True(NearBottom(view));
                Assert.False(view.NewMessagePillVisibleForTest);
            }
            finally
            {
                window.Close();
                api.Dispose();
            }
        }, CancellationToken.None);
    }

    private static (Window Window, LunaAppView View, LunaApiClient Api) CreateView()
    {
        var api = new LunaApiClient();
        var view = new LunaAppView(api, "night");
        view.MarkInitializedForTest();
        var window = new Window
        {
            Width = 960,
            Height = 760,
            WindowDecorations = WindowDecorations.None,
            Content = view
        };
        window.Show();
        UiHarness.Pump(250);
        return (window, view, api);
    }

    private static LunaChatDto Chat() => new(
        Guid.NewGuid(), "math", "Multiplying Fractions Together", false, false,
        DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

    private static List<LunaChildMessageDto> Messages(params (string Role, string Text)[] messages)
    {
        var chat = Chat();
        var at = DateTime.UtcNow.AddMinutes(-messages.Length);
        return messages.Select((m, i) => new LunaChildMessageDto(
            Guid.NewGuid(), chat.Id, m.Role, m.Text, at.AddMinutes(i), "none")).ToList();
    }

    private static List<LunaChildMessageDto> ManyMessages(int count)
    {
        var chat = Chat();
        var at = DateTime.UtcNow.AddMinutes(-count);
        return Enumerable.Range(0, count).Select(i => new LunaChildMessageDto(
            Guid.NewGuid(), chat.Id,
            i % 2 == 0 ? LunaChatRoles.Child : LunaChatRoles.Luna,
            i % 2 == 0
                ? $"Child question number {i}: can we work through another fraction example together?"
                : $"Luna answer number {i}: yes, let us multiply the top numbers and then the bottom numbers.",
            at.AddMinutes(i), "none")).ToList();
    }

    private static bool NearBottom(LunaAppView view)
    {
        var scroll = view.MessagesScrollForTest;
        if (scroll is null) return false;
        var maximum = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        return scroll.Offset.Y >= maximum - 30;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
