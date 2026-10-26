using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Luna;

public sealed record ContentPreviewOptions(string Theme = "night", string Sample = "math");

public sealed class LunaContentPreviewWindow : Window
{
    public LunaContentPreviewWindow(ContentPreviewOptions options)
    {
        Title = "Luna content preview";
        Width = 780;
        Height = 920;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowDecorations = WindowDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        var palette = LunaThemes.From(options.Theme);
        var content = options.Sample == "markdown" ? MarkdownSample : MathSample;
        var stack = new StackPanel { Spacing = 12, Margin = new Thickness(24) };
        stack.Children.Add(new TextBlock
        {
            Text = options.Sample == "markdown" ? "Luna markdown" : "Luna math pictures",
            FontFamily = LunaThemes.Display,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = LunaBrushes.B(palette.Text)
        });
        var bubble = new Border
        {
            Background = LunaBrushes.B(palette.LunaBubble),
            BorderBrush = LunaBrushes.B(palette.LunaBubbleBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14),
            MaxWidth = 520,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new LunaRichContent(content, palette, "math")
        };
        stack.Children.Add(bubble);
        var root = new Border
        {
            CornerRadius = new CornerRadius(22),
            BorderThickness = new Thickness(2),
            BorderBrush = LunaBrushes.B(palette.Border),
            Background = LunaBrushes.B(palette.Bg),
            ClipToBounds = true,
            Child = new ScrollViewer
            {
                Content = stack,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            }
        };
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) Close(); };
    }

    private const string MathSample = """
        First multiply the tops and the bottoms: $\frac{1}{4} \times \frac{5}{6} = \frac{5}{24}$.

        Here is the bar (2 of 6 pieces):
        [[bar 2/6]]

        And two terms with the answer:
        [[bars 1/4 + 2/4]]

        A pie with 3 of 8 slices:
        [[pie 3/8]]

        The same fraction on a number line:
        [[line 1/2]]

        **Tip:** shade the same number of pieces before you add.
        """;

    private const string MarkdownSample = """
        ## A short heading

        **Bold** and *italic* text, then an inline `code` word.

        - First bullet
        - Second bullet with **a key word**

        1. First step
        2. Second step

        | Fraction | Percent |
        | --- | --- |
        | 1/4 | 25% |
        | 1/2 | 50% |

        > Tip: a fraction is part of a whole.

        ```
        A | B
        *C*
        ```

        [A link is shown as text](https://example.com)
        """;
}
