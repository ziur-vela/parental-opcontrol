// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Luna;
using OpControl.Parental.Server.Services.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job184MarkdownTests
{
    [Fact]
    public void Fenced_code_becomes_plain_lines_without_a_code_block()
    {
        var text = LunaMarkdown.Prepare("Before\n```\nA | B\n*C*\n```\nAfter");
        Assert.DoesNotContain("```", text, StringComparison.Ordinal);
        Assert.Contains("A \\| B  \n\\*C\\*", text, StringComparison.Ordinal);
        Assert.StartsWith("Before", text, StringComparison.Ordinal);
        Assert.EndsWith("After", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Inline_code_links_and_images_are_plain_text()
    {
        var text = LunaMarkdown.Prepare("Use `x * y`, see [Google](https://google.com), <https://example.com> and ![diagram](x.png).");
        Assert.DoesNotContain("`", text, StringComparison.Ordinal);
        Assert.DoesNotContain("https://google.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<", text, StringComparison.Ordinal);
        Assert.DoesNotContain(">", text, StringComparison.Ordinal);
        Assert.Contains("https://example.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain("x.png", text, StringComparison.Ordinal);
        Assert.Contains("Use x \\* y", text, StringComparison.Ordinal);
        Assert.Contains("Google", text, StringComparison.Ordinal);
        Assert.Contains("diagram", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Luna_content_has_no_inner_horizontal_scroller_and_wraps_text()
    {
        var root = FindRoot();
        var markdown = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaMarkdown.cs"));
        var blockStart = markdown.IndexOf("public static Control Block(", StringComparison.Ordinal);
        Assert.True(blockStart > 0);
        var block = markdown[blockStart..Math.Min(markdown.Length, blockStart + 700)];
        Assert.DoesNotContain("ScrollViewer", block, StringComparison.Ordinal);
        Assert.Contains("OfType<MarkdownTextBlock>", block, StringComparison.Ordinal);
        Assert.Contains("TextWrapping.Wrap", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Kid_markdown_styles_cover_lists_tables_tips_headings_and_code_chrome()
    {
        var root = FindRoot();
        var axaml = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "App.axaml"));
        Assert.Contains("Border.Heading1Block", axaml, StringComparison.Ordinal);
        Assert.Contains("Border.Heading2Block", axaml, StringComparison.Ordinal);
        Assert.Contains("Border.Heading3Block", axaml, StringComparison.Ordinal);
        Assert.Contains("Border.QuoteBlock", axaml, StringComparison.Ordinal);
        Assert.Contains("Border.Table", axaml, StringComparison.Ordinal);
        Assert.Contains("Border.TableCell", axaml, StringComparison.Ordinal);
        Assert.Contains("PART_CopyButton", axaml, StringComparison.Ordinal);
        Assert.Contains("PART_ToggleTextWrapButton", axaml, StringComparison.Ordinal);
        Assert.Contains("PART_LanguageTextBlock", axaml, StringComparison.Ordinal);
        Assert.Contains("ListBlockBullet", axaml, StringComparison.Ordinal);
        Assert.Contains("md|Link", axaml, StringComparison.Ordinal);
        Assert.Contains("Nunito", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Core_prompt_allows_simple_markdown_and_short_answers()
    {
        Assert.Contains("simple markdown", LunaPrompts.Core, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Keep answers", LunaPrompts.Core, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not use headings, links or code formatting", LunaPrompts.Core, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

