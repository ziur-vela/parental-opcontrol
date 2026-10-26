// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public class Job19EGreetingTests
{
    [Fact]
    public async Task Greeting_text_fits_at_every_scaling_in_both_themes_and_languages()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            foreach (var (language, first, second) in new[]
                     {
                         (Loc.English, "Hi, I'm Luna!", "Got a question? I'm all ears!"),
                         (Loc.Spanish, "¡Hola, soy Luna!", "¿Tienes una pregunta? ¡Te escucho!")
                     })
            {
                Loc.SetLanguage(language);
                foreach (var theme in new[] { "night", "gray" })
                {
                    var bubble = new LunaGreetingBubble();
                    var window = new Window
                    {
                        Width = 420,
                        Height = 220,
                        WindowDecorations = WindowDecorations.None,
                        Content = new Canvas { Children = { bubble } }
                    };
                    bubble.Configure(theme, Loc.T("Luna.Greeting.Line1"), Loc.T("Luna.Greeting.Line2"));
                    window.Show();
                    foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
                    {
                        window.SetRenderScaling(scale);
                        UiHarness.Pump(60);
                        var label = $"{language}/{theme}/{scale}";
                        Assert.Equal(first, bubble.FirstLine);
                        Assert.Equal(second, bubble.SecondLine);
                        Assert.Equal(3, bubble.LineCount);
                        Assert.True(bubble.TextWidth <= bubble.BodyWidth - LunaGreetingBubble.PadLeft -
                            LunaGreetingBubble.PadRight,
                            $"{label}: text {bubble.TextWidth} does not fit body {bubble.BodyWidth}");
                        Assert.True(bubble.TextHeight <= bubble.BodyHeight - 2 * LunaGreetingBubble.PadVertical,
                            $"{label}: text {bubble.TextHeight} does not fit body {bubble.BodyHeight}");
                        Assert.All(bubble.LineWidths, width => Assert.True(
                            width <= bubble.BodyWidth - LunaGreetingBubble.PadLeft - LunaGreetingBubble.PadRight,
                            $"{label}: a line is wider than the padded box"));
                        Assert.InRange(bubble.Bounds.Width, bubble.TotalWidth - 1.5, bubble.TotalWidth + 1.5);
                        Assert.InRange(bubble.Bounds.Height, bubble.TotalHeight - 1.5, bubble.TotalHeight + 1.5);
                    }

                    window.Close();
                }
            }

            Loc.SetLanguage(Loc.English);
        }, CancellationToken.None);
    }

    [Fact]
    public void The_second_line_breaks_after_the_question_mark()
    {
        Assert.Equal(new[] { "Got a question?", "I'm all ears!" },
            LunaGreetingBubble.SplitSecondLine("Got a question? I'm all ears!"));
        Assert.Equal(new[] { "¿Tienes una pregunta?", "¡Te escucho!" },
            LunaGreetingBubble.SplitSecondLine("¿Tienes una pregunta? ¡Te escucho!"));
        Assert.Equal(new[] { "No question here" },
            LunaGreetingBubble.SplitSecondLine("No question here"));
    }

    [Fact]
    public async Task Tail_sits_on_the_right_then_flips_when_the_screen_edge_is_close()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
        var bubble = new LunaGreetingBubble();
        bubble.Configure("night", "Hi, I'm Luna!", "Got a question? I'm all ears!");
        var width = bubble.TotalWidth;
        var height = bubble.TotalHeight;
        var bodyHeight = bubble.BodyHeight;

        var roomy = LunaGreetingLayout.Compute(60, 56, width, height, bodyHeight, 1200, 800);
        Assert.Equal(LunaGreetingTailSide.Left, roomy.TailSide);
        Assert.True(roomy.Left > 60);
        Assert.True(roomy.Left >= LunaGreetingLayout.EdgeMargin);
        Assert.True(roomy.Top >= LunaGreetingLayout.EdgeMargin);

        // Moon near the right edge: the bubble must flip to the left of Luna.
        var nearRight = LunaGreetingLayout.Compute(330, 56, width, height, bodyHeight, 420, 800);
        Assert.Equal(LunaGreetingTailSide.Right, nearRight.TailSide);
        Assert.True(nearRight.Left < 330);
        Assert.True(nearRight.Left >= LunaGreetingLayout.EdgeMargin);
        Assert.True(nearRight.Left + width <= 420 - LunaGreetingLayout.EdgeMargin);

        // Too narrow for either side: it still stays on screen.
        var cramped = LunaGreetingLayout.Compute(60, 56, width, height, bodyHeight, 260, 800);
        Assert.Equal(LunaGreetingTailSide.Left, cramped.TailSide);
        Assert.True(cramped.Left >= LunaGreetingLayout.EdgeMargin);
        Assert.True(cramped.Left + width <= 260 - LunaGreetingLayout.EdgeMargin);

        // Vertical clamping near the bottom edge.
        var low = LunaGreetingLayout.Compute(60, 790, width, height, bodyHeight, 1200, 800);
        Assert.True(low.Top + height <= 800 - LunaGreetingLayout.EdgeMargin);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Theme_colors_match_the_approved_palettes()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
        var night = LunaGreetingBubble.ForTheme("night");
        Assert.Equal(Color.Parse("#fffdf3"), night.Fill);
        Assert.Equal(Color.Parse("#15103a"), night.Outline);
        Assert.Equal(Color.Parse("#e8dcc6"), night.Shadow);
        Assert.Equal(Color.Parse("#15103a"), night.Ink);

        var gray = LunaGreetingBubble.ForTheme("gray");
        Assert.Equal(Color.Parse("#ffffff"), gray.Fill);
        Assert.Equal(Color.Parse("#23273a"), gray.Outline);
        Assert.Equal(Color.Parse("#e2bd52"), gray.Shadow);
        Assert.Equal(Color.Parse("#23273a"), gray.Ink);

        var bubble = new LunaGreetingBubble();
        bubble.Configure("night", "Hi, I'm Luna!", "Got a question? I'm all ears!");
        Assert.Equal(night, bubble.Style);
        bubble.Configure("gray", "Hi, I'm Luna!", "Got a question? I'm all ears!");
        Assert.Equal(gray, bubble.Style);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task The_bubble_pops_in_and_a_click_opens_the_chat()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new LunaWindow();
            window.Show();
            UiHarness.Pump(150, window.TickForTest);
            window.SetGreetingThemeForTest("night");
            window.WakeForTest();
            UiHarness.Pump(2400, window.TickForTest);

            Assert.Equal("Awake", window.ModeForTest);
            Assert.True(window.BubbleOpacityForTest > 0.99, "the bubble must be fully visible after waking");
            Assert.Equal(1, window.BubbleScaleForTest, 2);
            Assert.Equal(3, window.GreetingForTest.LineCount);

            var greeting = window.GreetingForTest;
            var x = Canvas.GetLeft(greeting) + greeting.TotalWidth / 2;
            var y = Canvas.GetTop(greeting) + greeting.TotalHeight / 2;
            window.MouseDown(new Point(x, y), MouseButton.Left);
            window.MouseUp(new Point(x, y), MouseButton.Left);
            UiHarness.Pump(400, window.TickForTest);

            Assert.Equal("MorphOpen", window.ModeForTest);
            Assert.True(window.BubbleOpacityForTest < 0.2, "the bubble must shrink away when the chat opens");
            UiHarness.Pump(900, window.TickForTest);
            Assert.Equal("Chat", window.ModeForTest);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public void Reduced_motion_falls_back_to_a_plain_fade()
    {
        var previous = LunaMotion.ForceReducedMotionForTest;
        try
        {
            LunaMotion.ForceReducedMotionForTest = true;
            Assert.True(LunaMotion.ReducedMotion);
            LunaMotion.ForceReducedMotionForTest = false;
            Assert.False(LunaMotion.ReducedMotion);
        }
        finally
        {
            LunaMotion.ForceReducedMotionForTest = previous;
        }
    }
}
