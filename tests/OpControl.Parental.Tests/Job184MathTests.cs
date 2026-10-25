// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Services.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job184MathTests
{
    [Theory]
    [InlineData("bar 3/4", LunaPictureKind.Bar, 3, 4)]
    [InlineData("bar 5/4", LunaPictureKind.Bar, 5, 4)]
    [InlineData("pie 2/6", LunaPictureKind.Pie, 2, 6)]
    [InlineData("line 1/2", LunaPictureKind.Line, 1, 2)]
    public void Valid_fraction_tags_parse(string tag, LunaPictureKind kind, int numerator, int denominator)
    {
        Assert.True(LunaPictures.TryParse(tag, out var picture));
        Assert.Equal(kind, picture.Kind);
        Assert.Equal(numerator, picture.Terms[0].Numerator);
        Assert.Equal(denominator, picture.Terms[0].Denominator);
        Assert.Equal(LunaPictures.FractionLatex(numerator, denominator), picture.CaptionLatex);
    }

    [Fact]
    public void Bars_tag_has_two_terms_and_an_answer()
    {
        Assert.True(LunaPictures.TryParse("bars 1/4 + 2/4", out var picture));
        Assert.Equal(LunaPictureKind.Bars, picture.Kind);
        Assert.Equal(2, picture.Terms.Count);
        Assert.Equal(new LunaFraction(3, 4), picture.Answer);
        Assert.Equal("\\frac{1}{4} + \\frac{2}{4}", picture.CaptionLatex);
        Assert.Equal("\\frac{3}{4}", picture.AnswerLatex);
    }

    [Theory]
    [InlineData("bar 0/0")]
    [InlineData("bar 25/12")]
    [InlineData("pie 5/4")]
    [InlineData("line 13/12")]
    [InlineData("bars 1/4 + 2/3")]
    [InlineData("bars 3/4 + 2/4")]
    [InlineData("bar 3/4 extra")]
    [InlineData("bar <script>alert(1)</script>")]
    [InlineData("pie -1/2")]
    [InlineData("bar 1.5/2")]
    public void Invalid_or_injection_tags_are_rejected(string tag)
    {
        Assert.False(LunaPictures.TryParse(tag, out _));
    }

    [Fact]
    public void Invalid_tags_fall_back_to_stacked_fraction_text()
    {
        var parts = LunaPictures.Split("Try this [[pie 5/4]] and [[bars 1/4 + 2/3]].");
        var text = string.Concat(parts.Select(p => p.Text));
        Assert.Contains("$\\frac{5}{4}$", text, StringComparison.Ordinal);
        Assert.Contains("$\\frac{1}{4} + \\frac{2}{3}$", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[[", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Injection_text_is_left_as_plain_text_and_never_becomes_a_picture()
    {
        var parts = LunaPictures.Split("[[bar <script>alert(1)</script>]]");
        Assert.Single(parts);
        Assert.Null(parts[0].Picture);
        Assert.Contains("<script>", parts[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Split_keeps_text_around_multiple_pictures()
    {
        var parts = LunaPictures.Split("Look:\\n[[bar 1/2]]\\nand a pie:\\n[[pie 1/4]]\\nDone.");
        Assert.Equal(5, parts.Count);
        Assert.Equal("Look:\\n", parts[0].Text);
        Assert.Equal(LunaPictureKind.Bar, parts[1].Picture!.Kind);
        Assert.Equal("\\nand a pie:\\n", parts[2].Text);
        Assert.Equal(LunaPictureKind.Pie, parts[3].Picture!.Kind);
        Assert.Equal("\\nDone.", parts[4].Text);
    }

    [Fact]
    public void Picture_layout_snapshot_is_stable()
    {
        Assert.True(LunaPictures.TryParse("bar 1/2", out var bar));
        var barLayout = LunaPictures.Layout(bar);
        Assert.Equal(300, barLayout.Width);
        Assert.Equal(54, barLayout.Height);
        Assert.Equal("bar|0|0|300|54|empty|False|0|0;bar|0|0|150|54|term1|True|0|0", Describe(barLayout));

        Assert.True(LunaPictures.TryParse("bars 1/4 + 2/4", out var bars));
        var barsLayout = LunaPictures.Layout(bars);
        Assert.Equal(182, barsLayout.Height);
        Assert.Equal("\\frac{1}{4} + \\frac{2}{4}", barsLayout.CaptionLatex);
        Assert.Equal("\\frac{3}{4}", barsLayout.AnswerLatex);
        Assert.Contains(barsLayout.Shapes, s => s.Kind == "bar" && s.ColorRole == "answer-outline");

        Assert.True(LunaPictures.TryParse("pie 1/4", out var pie));
        var pieLayout = LunaPictures.Layout(pie);
        Assert.Single(pieLayout.Shapes, s => s.Kind == "pie");
        Assert.Single(pieLayout.Shapes, s => s.Kind == "pie-slice");
        Assert.Equal(-90, pieLayout.Shapes.First(s => s.Kind == "pie-slice").StartAngle);
        Assert.Equal(0, pieLayout.Shapes.First(s => s.Kind == "pie-slice").EndAngle);

        Assert.True(LunaPictures.TryParse("line 1/2", out var line));
        var lineLayout = LunaPictures.Layout(line);
        var mark = lineLayout.Shapes.Single(s => s.Kind == "line-mark");
        Assert.Equal(150, mark.X);
    }

    [Fact]
    public void Core_prompt_uses_latex_pictures_and_no_code_blocks()
    {
        Assert.Equal("luna-core-2", LunaPrompts.Version);
        Assert.Contains("\\frac{1}{4}", LunaPrompts.Core, StringComparison.Ordinal);
        Assert.Contains("Never write fractions like 1/4 * 5/6", LunaPrompts.Core, StringComparison.Ordinal);
        Assert.Contains("Never use code blocks", LunaPrompts.Core, StringComparison.Ordinal);
        Assert.Contains("[[bar a/b]]", LunaPrompts.Core, StringComparison.Ordinal);
        Assert.Contains("[[bars a/b + c/b]]", LunaPrompts.Core, StringComparison.Ordinal);
        Assert.Contains("[[pie a/b]]", LunaPrompts.Core, StringComparison.Ordinal);
        Assert.Contains("[[line a/b]]", LunaPrompts.Core, StringComparison.Ordinal);
        Assert.Contains("denominators 1 to 12", LunaPrompts.Core, StringComparison.Ordinal);
    }

    [Fact]
    public void Picture_tags_only_render_in_luna_bubbles()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaAppView.cs"));
        var lunaStart = view.IndexOf("private Control LunaBubble(", StringComparison.Ordinal);
        var childStart = view.IndexOf("private Control ChildBubble(", StringComparison.Ordinal);
        Assert.True(lunaStart > 0 && childStart > lunaStart);
        var luna = view[lunaStart..childStart];
        var child = view[childStart..view.IndexOf("private static bool Flagged(", childStart, StringComparison.Ordinal)];
        Assert.Contains("new LunaRichContent", luna, StringComparison.Ordinal);
        Assert.DoesNotContain("LunaRichContent", child, StringComparison.Ordinal);
        Assert.DoesNotContain("LunaPictures", child, StringComparison.Ordinal);
        Assert.Contains("TextBlock", child, StringComparison.Ordinal);
    }

    private static string Describe(LunaPictureLayout layout)
    {
        var builder = new StringBuilder();
        foreach (var shape in layout.Shapes)
        {
            if (builder.Length > 0) builder.Append(';');
            builder.Append(shape.Kind).Append('|')
                .Append(shape.X.ToString("0.##")).Append('|')
                .Append(shape.Y.ToString("0.##")).Append('|')
                .Append(shape.Width.ToString("0.##")).Append('|')
                .Append(shape.Height.ToString("0.##")).Append('|')
                .Append(shape.ColorRole).Append('|')
                .Append(shape.Filled).Append('|')
                .Append(shape.StartAngle.ToString("0.##")).Append('|')
                .Append(shape.EndAngle.ToString("0.##"));
        }
        return builder.ToString();
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
