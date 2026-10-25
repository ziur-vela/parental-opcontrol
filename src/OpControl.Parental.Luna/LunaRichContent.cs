using Avalonia.Controls;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Luna;

/// <summary>Luna reply content: markdown, LaTeX and fraction pictures in one bubble.</summary>
public sealed class LunaRichContent : UserControl
{
    public LunaRichContent(string? text, LunaPalette palette, string subject)
    {
        var style = palette.Subject(subject);
        var stack = new StackPanel { Spacing = 8 };
        foreach (var part in LunaPictures.Split(text))
        {
            if (part.Picture is { } picture)
                stack.Children.Add(new LunaPictureView(picture, palette, style));
            else if (!string.IsNullOrWhiteSpace(part.Text))
                stack.Children.Add(LunaMarkdown.Block(part.Text));
        }
        Content = stack;
    }

    public static bool HasPictures(string? text) =>
        LunaPictures.Split(text).Any(p => p.Picture is not null);
}
