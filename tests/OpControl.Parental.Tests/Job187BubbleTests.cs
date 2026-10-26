// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.VisualTree;
using OpControl.Parental.Contracts;
using OpControl.Parental.Luna;
using LunaPalettes = OpControl.Parental.Luna.LunaThemes;
using RaTeX.Avalonia;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public class Job187BubbleTests
{
    // Copied read-only from the production "Multiplying Fractions Together"
    // chat (2026-10-26 03:51-03:53 UTC, Ziur's X1 test on 2026-10-25).
    private const string ReplyOne = """
        Let's work it out together. Here's a similar problem first.

        **Example:** $\frac{1}{2} \times \frac{3}{4}$

        1. Multiply the top numbers: $1 \times 3 = 3$
        2. Multiply the bottom numbers: $2 \times 4 = 8$
        3. Put them together: $\frac{3}{8}$

        **Your turn:** Try the same steps with $\frac{1}{4} \times \frac{5}{6}$.

        - What is $1 \times 5$?
        - What is $4 \times 6$?

        Tell me what you get, and we can check it together. Then we can see if the answer can be made simpler.
        """;

    private const string ReplyTwo = """
        You got the top and bottom numbers right: $1 \times 5 = 5$ and $4 \times 6 = 24$. So the answer is $\frac{5}{24}$.

        Now let's check if it can be made simpler. Can you find a number that divides both $5$ and $24$ evenly, other than $1$?
        """;

    private const string ReplyThree = """
        Here is $\frac{2}{6}$. The bar has $6$ equal pieces, and $2$ of them are shaded.

        [[bar 2/6]]

        Look at the shaded part. Can you think of a fraction with a smaller bottom number that covers the same amount?
        """;

    private static readonly string[] Replies = [ReplyOne, ReplyTwo, ReplyThree];

    [Fact]
    public async Task Exact_math_replies_fit_the_bubble_at_every_width_scale_and_theme()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var widths = new[] { 300.0, 420.0, 560.0 };
            var scales = new[] { 1.0, 1.25, 1.5, 2.0 };
            foreach (var palette in new[] { LunaPalettes.Night, LunaPalettes.Gray })
            {
                foreach (var width in widths)
                {
                    foreach (var scale in scales)
                    {
                        foreach (var text in Replies)
                        {
                            var label = $"{palette.Key} width={width} scale={scale} reply={Array.IndexOf(Replies, text) + 1}";
                            var window = new Window
                            {
                                Width = width,
                                Height = 1400,
                                WindowDecorations = WindowDecorations.None
                            };
                            var frame = LunaAppView.BubbleFrame(
                                new LunaRichContent(text, palette, "math"), palette, width);
                            window.Content = new StackPanel { Children = { frame } };
                            window.Show();
                            window.SetRenderScaling(scale);
                            UiHarness.Pump(120);

                            Assert.True(frame.Bounds.Width <= width + 0.5,
                                $"{label}: bubble is {frame.Bounds.Width} wide");
                            AssertInside(frame, label);
                            Assert.Empty(frame.GetVisualDescendants().OfType<ScrollViewer>());
                            Assert.True(frame.Bounds.Height >= 30, $"{label}: bubble is empty");

                            var formulas = frame.GetVisualDescendants().OfType<FormulaControl>().ToList();
                            if (text == ReplyOne || text == ReplyTwo)
                            {
                                Assert.NotEmpty(formulas);
                                Assert.All(formulas, f =>
                                    Assert.Equal(LunaMarkdown.InlineMathSize, f.FontSize, 1));
                            }

                            if (text == ReplyThree)
                            {
                                var picture = frame.GetVisualDescendants().OfType<LunaPictureView>().Single();
                                Assert.Equal(5, picture.LayoutForTest.Shapes.Count(s => s.Kind == "bar-divider"));
                                Assert.Equal(1, picture.LayoutForTest.Shapes.Count(s => s.Kind == "bar" && s.ColorRole == "term1"));
                            }

                            window.Close();
                        }
                    }
                }
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Display_math_is_larger_and_centred()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var window = new Window
            {
                Width = 420,
                Height = 600,
                WindowDecorations = WindowDecorations.None
            };
            var frame = LunaAppView.BubbleFrame(
                new LunaRichContent("The answer is:\n\n$$\n\\frac{1}{2} + \\frac{1}{3}\n$$", LunaPalettes.Night, "math"),
                LunaPalettes.Night, 420);
            window.Content = new StackPanel { Children = { frame } };
            window.Show();
            UiHarness.Pump(200);

            var display = frame.GetVisualDescendants().OfType<FormulaControl>()
                .Single(f => f.Classes.Contains("MathBlock"));
            Assert.Equal(LunaMarkdown.DisplayMathSize, display.FontSize, 1);
            Assert.NotNull(display.Parent);
            Assert.Equal(HorizontalAlignment.Center, ((Control)display.Parent!).HorizontalAlignment);
            var center = display.TranslatePoint(new Point(display.Bounds.Width / 2, 0), frame);
            Assert.NotNull(center);
            Assert.True(Math.Abs(center!.Value.X - frame.Bounds.Width / 2) <= 1,
                $"display math centre {center.Value.X} differs from bubble centre {frame.Bounds.Width / 2}");
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Tables_wrap_and_never_scroll_inside_the_bubble()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var table = """
                | Name | What it means | Example |
                | --- | --- | --- |
                | Numerator | The top number of a fraction, the one being counted | 3 in 3/4 |
                | Denominator | The bottom number, how many equal pieces there are | 4 in 3/4 |
                """;
            var window = new Window
            {
                Width = 300,
                Height = 800,
                WindowDecorations = WindowDecorations.None
            };
            var frame = LunaAppView.BubbleFrame(
                new LunaRichContent(table, LunaPalettes.Night, "math"), LunaPalettes.Night, 300);
            window.Content = new StackPanel { Children = { frame } };
            window.Show();
            UiHarness.Pump(250);

            AssertInside(frame, "table width=300");
            foreach (var viewer in frame.GetVisualDescendants().OfType<ScrollViewer>())
            {
                Assert.Equal(ScrollBarVisibility.Disabled, viewer.HorizontalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Disabled, viewer.VerticalScrollBarVisibility);
                Assert.True(viewer.Extent.Width <= viewer.Viewport.Width + 1,
                    $"table extent {viewer.Extent.Width} exceeds viewport {viewer.Viewport.Width}");
            }
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public void Picture_layouts_have_one_visible_piece_per_denominator_piece()
    {
        Assert.True(LunaPictures.TryParse("bar 2/6", out var bar));
        var barLayout = LunaPictures.Layout(bar);
        Assert.Equal(5, barLayout.Shapes.Count(s => s.Kind == "bar-divider"));
        Assert.Equal(6, barLayout.Shapes.Count(s => s.Kind == "bar-divider") + 1);

        Assert.True(LunaPictures.TryParse("bars 1/6 + 2/6", out var bars));
        var barsLayout = LunaPictures.Layout(bars);
        Assert.Equal(15, barsLayout.Shapes.Count(s => s.Kind == "bar-divider"));
        Assert.Equal(4, barsLayout.Shapes.Count(s => s.Kind == "bar" && s.ColorRole is "term1" or "term2"));

        Assert.True(LunaPictures.TryParse("pie 2/6", out var pie));
        var pieLayout = LunaPictures.Layout(pie);
        Assert.Equal(6, pieLayout.Shapes.Count(s => s.Kind == "pie-slice"));
        Assert.Equal(2, pieLayout.Shapes.Count(s => s.Kind == "pie-slice" && s.ColorRole == "term1"));
        Assert.Equal(4, pieLayout.Shapes.Count(s => s.Kind == "pie-slice" && s.ColorRole == "empty"));

        Assert.True(LunaPictures.TryParse("pie 1/12", out var twelfths));
        Assert.Equal(12, LunaPictures.Layout(twelfths).Shapes.Count(s => s.Kind == "pie-slice"));
    }

    private static void AssertInside(Control root, string label)
    {
        foreach (var child in root.GetVisualDescendants().OfType<Control>())
        {
            if (child.Bounds.Width <= 0 || child.Bounds.Height <= 0) continue;
            var topLeft = child.TranslatePoint(new Point(0, 0), root);
            var bottomRight = child.TranslatePoint(
                new Point(child.Bounds.Width, child.Bounds.Height), root);
            if (topLeft is null || bottomRight is null) continue;
            Assert.True(topLeft.Value.X >= -0.5 && topLeft.Value.Y >= -0.5,
                $"{label}: {child.GetType().Name} starts outside the bubble at {topLeft}");
            Assert.True(bottomRight.Value.X <= root.Bounds.Width + 0.5,
                $"{label}: {child.GetType().Name} overflows horizontally to {bottomRight.Value.X} " +
                $"(bubble {root.Bounds.Width})");
            Assert.True(bottomRight.Value.Y <= root.Bounds.Height + 0.5,
                $"{label}: {child.GetType().Name} overflows vertically to {bottomRight.Value.Y} " +
                $"(bubble {root.Bounds.Height})");
        }
    }
}
