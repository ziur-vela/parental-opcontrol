using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using LiveMarkdown.Avalonia;
using RaTeX.Avalonia;

namespace OpControl.Parental.Luna;

/// <summary>Kid-friendly markdown: plain code, no clickable links, no code chrome.</summary>
public static class LunaMarkdown
{
    private static readonly Regex Fence = new(@"```[^\r\n]*\r?\n(.*?)```", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex InlineCode = new(@"`([^`\r\n]+)`", RegexOptions.Compiled);
    private static readonly Regex Image = new(@"!\[([^\]]*)\]\([^)]+\)", RegexOptions.Compiled);
    private static readonly Regex Link = new(@"\[([^\]]+)\]\([^)]+\)", RegexOptions.Compiled);
    private static readonly Regex AutoLink = new(@"<((?:https?|mailto):[^>]+)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Inline formulas are slightly larger than the body so a stacked
    /// fraction inside them is about the same size as the surrounding words.</summary>
    internal const double InlineMathSize = 19;

    /// <summary>Display formulas stand on their own line, larger than the body.</summary>
    internal const double DisplayMathSize = 24;

    public static string Prepare(string? text)
    {
        var value = text ?? "";
        value = Fence.Replace(value, match => PlainBlock(match.Groups[1].Value));
        value = InlineCode.Replace(value, match => EscapeInline(match.Groups[1].Value));
        value = Image.Replace(value, "$1");
        value = Link.Replace(value, "$1");
        value = AutoLink.Replace(value, "$1");
        return value;
    }

    public static MarkdownRenderer Renderer(string? text)
    {
        var builder = new ObservableStringBuilder();
        builder.Append(Prepare(text));
        return new MarkdownRenderer { MarkdownBuilder = builder };
    }

    /// <summary>
    /// A bubble body. There is deliberately **no** horizontal ScrollViewer: a
    /// ScrollViewer measures its child with an infinite width, so every
    /// paragraph laid itself out on one long line and was clipped with a
    /// scrollbar. The renderer now measures against the bubble width and every
    /// text block wraps.
    /// </summary>
    public static Control Block(string? text)
    {
        var renderer = Renderer(text);
        renderer.Styles.Add(CreateBubbleTextStyle());
        renderer.AttachedToVisualTree += (_, _) => Tune(renderer);
        renderer.LayoutUpdated += (_, _) => Tune(renderer);
        return renderer;
    }

    private static Style CreateBubbleTextStyle()
    {
        var style = new Style(selector => selector.OfType<MarkdownTextBlock>());
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Left));
        return style;
    }

    /// <summary>
    /// LiveMarkdown creates its math controls with the default 14 px size, and
    /// the formula font size is a local value, so a style cannot change it.
    /// Walk the small number of controls after each layout pass and give inline
    /// math body-relative size and display math a larger, centred size.
    /// </summary>
    private static void Tune(Control root)
    {
        foreach (var formula in root.GetVisualDescendants().OfType<FormulaControl>())
        {
            var display = formula.Classes.Contains("MathBlock");
            var size = display ? DisplayMathSize : InlineMathSize;
            if (Math.Abs(formula.FontSize - size) > 0.01)
                formula.FontSize = size;

            if (display && formula.Parent is Control presenter &&
                presenter.HorizontalAlignment != HorizontalAlignment.Center)
                presenter.HorizontalAlignment = HorizontalAlignment.Center;
        }

        // LiveMarkdown measures table cells with an infinite width and puts
        // the table in a scroll viewer. Cap every cell at the bubble width so
        // the table wraps and grows downwards instead of scrolling sideways.
        foreach (var table in root.GetVisualDescendants().OfType<MarkdownTableGrid>())
        {
            var cells = table.Children.OfType<Control>().ToList();
            if (cells.Count == 0 || root.Bounds.Width <= 0) continue;
            var columns = cells.Select(Grid.GetColumn).DefaultIfEmpty(0).Max() + 1;
            var gaps = table.ColumnSpacing * Math.Max(0, columns - 1);
            var cellWidth = Math.Max(40, (root.Bounds.Width - gaps) / columns);
            foreach (var cell in cells)
            {
                if (double.IsInfinity(cell.MaxWidth) || cell.MaxWidth > cellWidth + 0.5)
                    cell.MaxWidth = cellWidth;
            }
        }

        // A bubble never scrolls; the chat list is the only scroller.
        foreach (var viewer in root.GetVisualDescendants().OfType<ScrollViewer>())
        {
            if (viewer.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
                viewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            if (viewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
                viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
    }

    private static string PlainBlock(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var escaped = lines.Select(EscapeInline);
        return string.Join("  \n", escaped).TrimEnd();
    }

    private static string EscapeInline(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '#' or '>' or '|' or '~')
                builder.Append('\\');
            builder.Append(c);
        }
        return builder.ToString();
    }
}
