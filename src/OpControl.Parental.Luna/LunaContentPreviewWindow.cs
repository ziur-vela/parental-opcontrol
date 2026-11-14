using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
        if (options.Sample is "sources" or "sources-open")
        {
            Width = 620;
            Height = 860;
            var view = new LunaAppView(new LunaApiClient(), options.Theme);
            view.MarkInitializedForTest();
            var chat = new LunaChatDto(Guid.NewGuid(), "science", "Mount Everest", false, false,
                DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow);
            var child = new LunaChildMessageDto(Guid.NewGuid(), chat.Id, LunaChatRoles.Child,
                "How tall is Mount Everest?", DateTime.UtcNow.AddMinutes(-4), "none");
            var luna = new LunaChildMessageDto(Guid.NewGuid(), chat.Id, LunaChatRoles.Luna,
                "Mount Everest is about 8,849 meters tall. That is almost 9 kilometers!",
                DateTime.UtcNow.AddMinutes(-3), "none",
                [
                    new LunaSourceDto("https://en.wikipedia.org/wiki/Mount_Everest",
                        "Mount Everest - Wikipedia", "en.wikipedia.org"),
                    new LunaSourceDto("https://www.nationalgeographic.com/environment/article/mount-everest",
                        "Mount Everest facts - National Geographic", "nationalgeographic.com"),
                    new LunaSourceDto("https://www.britannica.com/place/Mount-Everest",
                        "Mount Everest - Britannica", "britannica.com")
                ]);
            view.OpenChatForTest(chat, [child, luna]);
            Content = view;
            KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) Close(); };
#if LUNA_DEV
            if (options.Sample == "sources-open")
            {
                var openTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                openTimer.Tick += (_, _) =>
                {
                    openTimer.Stop();
                    var header = view.GetVisualDescendants().OfType<Button>()
                        .FirstOrDefault(b => b.Content is StackPanel panel
                            && panel.Children.OfType<TextBlock>().Any(t => t.Text?.StartsWith(
                                "Where I found this", StringComparison.Ordinal) == true));
                    header?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                };
                openTimer.Start();
            }
            if (!string.IsNullOrWhiteSpace(App.LunaScreenshotPath))
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1700) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    Capture(App.LunaScreenshotPath!);
                    Close();
                };
                timer.Start();
            }
#endif
            return;
        }
        var content = options.Sample == "markdown" ? MarkdownSample : MathSample;
        var stack = new StackPanel { Spacing = 12, Margin = new Thickness(24) };
        var title = options.Sample switch
        {
            "markdown" => "Luna markdown",
            "searching" => "Luna web search",
            "thinking" => "Luna thinking",
            _ => "Luna math pictures"
        };
        stack.Children.Add(new TextBlock
        {
            Text = title, FontFamily = LunaThemes.Display, FontSize = 22,
            FontWeight = FontWeight.Bold, Foreground = LunaBrushes.B(palette.Text)
        });
        stack.Children.Add(new Border
        {
            Background = LunaBrushes.B(palette.ChildChatBg),
            CornerRadius = new CornerRadius(18, 18, 6, 18),
            Padding = new Thickness(12, 10),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock
            {
                Text = "How tall is the newest tallest building?",
                FontFamily = LunaThemes.Body, FontSize = 15, FontWeight = FontWeight.Bold,
                Foreground = LunaBrushes.B(palette.ChildChatInk), TextWrapping = TextWrapping.Wrap
            }
        });
        if (options.Sample == "searching")
            stack.Children.Add(new LunaSearchingView(palette));
        else if (options.Sample == "thinking")
            stack.Children.Add(new ThinkingView(palette));
        else
        {
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
        }
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
#if LUNA_DEV
        if (!string.IsNullOrWhiteSpace(App.LunaScreenshotPath))
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Capture(App.LunaScreenshotPath!);
                Close();
            };
            timer.Start();
        }
#endif
    }

#if LUNA_DEV
    private void Capture(string path)
    {
        try
        {
            var scale = 1.5;
            var visual = (Visual)Content!;
            var width = Math.Max(1, (int)Math.Round(visual.Bounds.Width * scale));
            var height = Math.Max(1, (int)Math.Round(visual.Bounds.Height * scale));
            var pixel = new PixelSize(width, height);
            var bitmap = new RenderTargetBitmap(pixel, new Vector(96 * scale, 96 * scale));
            bitmap.Render(visual);
            bitmap.Save(path);
        }
        catch (Exception ex)
        {
            LunaLog.Exception("preview-screenshot", ex);
        }
    }
#endif

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
