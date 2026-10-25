using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using LiveMarkdown.Avalonia;

namespace OpControl.Parental.Luna;

/// <summary>Kid-friendly markdown: plain code, no clickable links, no code chrome.</summary>
public static class LunaMarkdown
{
    private static readonly Regex Fence = new(@"```[^\r\n]*\r?\n(.*?)```", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex InlineCode = new(@"`([^`\r\n]+)`", RegexOptions.Compiled);
    private static readonly Regex Image = new(@"!\[([^\]]*)\]\([^)]+\)", RegexOptions.Compiled);
    private static readonly Regex Link = new(@"\[([^\]]+)\]\([^)]+\)", RegexOptions.Compiled);
    private static readonly Regex AutoLink = new(@"<((?:https?|mailto):[^>]+)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    public static Control Block(string? text)
    {
        var renderer = Renderer(text);
        return new ScrollViewer
        {
            Content = renderer,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
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
