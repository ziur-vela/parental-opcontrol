// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;
using Xunit;

namespace OpControl.Parental.Tests;

/// <summary>
/// Job 18.6: the lock text block is centred line by line, follows the reference
/// rhythm (18 / 10 / 40 / 16 design px) and never overlaps at 100-200% scaling.
/// </summary>
public class Job186LockTextTests
{
    public static TheoryData<int, int, double> Screens => new()
    {
        { 1600, 900, 1.00 },
        { 1600, 900, 1.25 },
        { 1600, 900, 1.50 },
        { 1600, 900, 2.00 },
        { 1620, 1080, 1.00 },
        { 1620, 1080, 1.25 },
        { 1620, 1080, 1.50 },
        { 1620, 1080, 2.00 },
        { 1366, 768, 1.00 },
        { 1366, 768, 1.25 },
        { 1366, 768, 1.50 },
        { 1366, 768, 2.00 }
    };

    [Theory]
    [MemberData(nameof(Screens))]
    public void Every_text_line_and_button_is_centred_on_the_screen(int width, int height, double scale)
    {
        foreach (var (layout, content) in Cases(width, height, scale))
        {
            Assert.InRange(content.Title.CenterX, width / 2.0 - 1, width / 2.0 + 1);
            Assert.InRange(content.Reason.CenterX, width / 2.0 - 1, width / 2.0 + 1);
            Assert.InRange(content.Hint.CenterX, width / 2.0 - 1, width / 2.0 + 1);
            Assert.InRange(content.PrimaryButton.CenterX, width / 2.0 - 1, width / 2.0 + 1);
            Assert.InRange(content.SecondaryButton.CenterX, width / 2.0 - 1, width / 2.0 + 1);
            Assert.Equal(width / 2.0, content.Title.CenterX, 6);
            Assert.Equal(width / 2.0, content.Reason.CenterX, 6);
            Assert.Equal(width / 2.0, content.Hint.CenterX, 6);
        }
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void No_two_elements_overlap_and_the_block_fits(int width, int height, double scale)
    {
        foreach (var (layout, content) in Cases(width, height, scale))
        {
            var boxes = content.InOrder().ToArray();
            for (var i = 0; i < boxes.Length - 1; i++)
                Assert.True(boxes[i].Box.Bottom <= boxes[i + 1].Box.Y + 0.001,
                    $"{boxes[i].Name} overlaps {boxes[i + 1].Name} on {width}x{height} @{scale:P0}");

            Assert.True(content.Title.Y >= 0);
            Assert.True(content.SecondaryButton.Bottom < height);
            Assert.True(content.PrimaryButton.Width <= width - 48 + 0.001);
            Assert.True(content.SecondaryButton.Width <= width - 48 + 0.001);
            Assert.False(Overlaps(content.PrimaryButton, layout.Logo),
                $"primary button overlaps the logo on {width}x{height} @{scale:P0}");
            Assert.False(Overlaps(content.SecondaryButton, layout.Logo),
                $"secondary button overlaps the logo on {width}x{height} @{scale:P0}");
        }
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void Rhythm_matches_the_reference_gaps(int width, int height, double scale)
    {
        foreach (var (layout, content) in Cases(width, height, scale))
        {
            var ui = layout.UiScale;
            Assert.Equal(18 * ui, content.Reason.Y - content.Title.Bottom, 6);
            Assert.Equal(10 * ui, content.Hint.Y - content.Reason.Bottom, 6);
            Assert.Equal(40 * ui, content.PrimaryButton.Y - content.Hint.Bottom, 6);
            Assert.Equal(16 * ui, content.SecondaryButton.Y - content.PrimaryButton.Bottom, 6);
            Assert.Equal(72 * ui, content.PrimaryButton.Height, 6);
            Assert.Equal(52 * ui, content.SecondaryButton.Height, 6);
        }
    }

    [Fact]
    public void Spanish_title_wraps_to_two_lines_without_pushing_the_buttons_off_screen()
    {
        const int width = 1366;
        const int height = 768;
        var layout = LockSceneLayout.For(width, height, 2.0);
        var ui = layout.UiScale;
        var firstLine = LockSceneLayout.TitleLineHeight * ui;
        var content = layout.ArrangeContent(
            900 * ui, firstLine * 2,
            220 * ui, LockSceneLayout.ReasonLineHeight * ui,
            380 * ui, LockSceneLayout.HintLineHeight * ui,
            260 * ui, 140 * ui);

        Assert.True(content.Title.Height > firstLine);
        Assert.True(content.SecondaryButton.Bottom < height);
        Assert.Equal(width / 2.0, content.Title.CenterX, 6);
        Assert.True(content.Title.Bottom <= content.Reason.Y + 0.001);
    }

    [Fact]
    public void A_short_reason_and_a_long_hint_still_share_the_same_centre()
    {
        var layout = LockSceneLayout.For(1600, 900);
        var content = layout.ArrangeContent(
            300, LockSceneLayout.TitleLineHeight,
            120, LockSceneLayout.ReasonLineHeight,
            520, LockSceneLayout.HintLineHeight,
            280, 150);
        Assert.Equal(800, content.Title.CenterX, 6);
        Assert.Equal(800, content.Reason.CenterX, 6);
        Assert.Equal(800, content.Hint.CenterX, 6);
    }

    private static bool Overlaps(LockBox a, LockBox b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    private static IEnumerable<(LockSceneLayout Layout, LockContentLayout Content)> Cases(
        int width, int height, double scale)
    {
        var layout = LockSceneLayout.For(width, height, scale);
        var ui = layout.UiScale;
        var max = layout.TextColumn.Width;

        // English: one line each.
        yield return (layout, layout.ArrangeContent(
            Math.Min(max, 440 * ui), LockSceneLayout.TitleLineHeight * ui,
            Math.Min(max, 240 * ui), LockSceneLayout.ReasonLineHeight * ui,
            Math.Min(max, 420 * ui), LockSceneLayout.HintLineHeight * ui,
            300 * ui, 160 * ui));

        // es-MX: the title is much longer. At narrow widths it wraps, exactly as
        // text-wrap: balance does in the reference, and the rhythm grows by one line.
        var titleLines = max < 700 * ui ? 2 : 1;
        yield return (layout, layout.ArrangeContent(
            max, LockSceneLayout.TitleLineHeight * ui * titleLines,
            250 * ui, LockSceneLayout.ReasonLineHeight * ui,
            520 * ui, LockSceneLayout.HintLineHeight * ui,
            300 * ui, 190 * ui));
    }
}
