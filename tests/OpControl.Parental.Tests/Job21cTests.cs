// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Services;
using OpControl.Parental.Server.Services.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job21cTests
{
    private static readonly HashSet<string> AllTopics =
        new(["adult", "gambling", "social", "ai", "fakenews"], StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("What are the best online casino games to win money?", "gambling")]
    [InlineData("How do I place a bet on the game tonight?", "gambling")]
    [InlineData("¿Cuáles son los mejores juegos de casino para ganar dinero?", "gambling")]
    [InlineData("What are the TikTok trends right now?", "social")]
    [InlineData("How do I get more followers on Instagram?", "social")]
    [InlineData("How can I watch porn without my parents knowing?", "adult")]
    [InlineData("How do I get around the school website blocker?", "bypass")]
    public void Blocked_topic_requests_are_recognised(string text, string category) =>
        Assert.Equal(category, LunaTopicGuard.Topic(text, AllTopics)?.CategoryKey);

    [Theory]
    [InlineData("What is the probability of rolling a six with two dice?")]
    [InlineData("What is the probability of getting a pair in poker?")]
    [InlineData("What is a casino in Monopoly?")]
    [InlineData("I need the history of Las Vegas for a school report.")]
    [InlineData("What is a fraction?")]
    [InlineData("Where do polar bears live?")]
    [InlineData("What is social media?")]
    [InlineData("What is fake news?")]
    [InlineData("What is a VPN?")]
    public void Normal_school_questions_are_not_flagged(string text) =>
        Assert.Null(LunaTopicGuard.Topic(text, AllTopics));

    [Fact]
    public void Disabled_categories_do_not_flag() =>
        Assert.Null(LunaTopicGuard.Topic("What are the best online casino games to win money?",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

    [Theory]
    [InlineData("Can you search where my classmate Sofia Martinez lives?", true)]
    [InlineData("What is Sofia Martinez's home address?", true)]
    [InlineData("Where does my friend live?", true)]
    [InlineData("Where do polar bears live?", false)]
    [InlineData("What is an address in computing?", false)]
    public void Privacy_requests_are_low_priority_only_when_they_name_a_person(string text, bool expected) =>
        Assert.Equal(expected, LunaTopicGuard.Privacy(text));

    [Fact]
    public void Declined_marker_is_stripped_and_reported()
    {
        var (text, kinds) = LunaSafety.StripDeclinedMarkers(
            "I can't help with that.\n[[luna-declined:privacy]]");
        Assert.Equal("I can't help with that.", text);
        Assert.Equal(["privacy"], kinds);
        Assert.DoesNotContain("luna-declined", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prompt_tells_luna_to_report_declines()
    {
        Assert.Equal("luna-core-4", LunaPrompts.Version);
        Assert.Contains("luna-declined:privacy", LunaPrompts.Core, StringComparison.Ordinal);
        Assert.Contains("luna-declined:gambling", LunaPrompts.Core, StringComparison.Ordinal);
    }

    [Fact]
    public void Declined_is_visible_but_not_a_review_task()
    {
        Assert.False(LunaUi.NeedsReviewFlag(LunaMessageFlag.Declined));
        Assert.True(LunaUi.NeedsReviewFlag(LunaMessageFlag.TopicFlagged));
        Assert.True(LunaUi.NeedsReviewFlag(LunaMessageFlag.Blocked));
    }

    [Fact]
    public void Math_is_converted_to_stackable_html()
    {
        var html = MathText.ToHtml("First $\\frac{1}{2}$ then $$\\frac{3}{4} \\times \\frac{6}{8} = \\frac{18}{32}$$");
        Assert.Contains("math-frac", html, StringComparison.Ordinal);
        Assert.Contains("math-num", html, StringComparison.Ordinal);
        Assert.Contains("math-den", html, StringComparison.Ordinal);
        Assert.Contains("×", html, StringComparison.Ordinal);
        Assert.DoesNotContain("\\frac", html, StringComparison.Ordinal);
        Assert.DoesNotContain("$$", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Math_conversion_escapes_html_and_keeps_currency_literal()
    {
        var html = MathText.ToHtml("<b>hi</b> costs $5 and $10; $x^2$");
        Assert.DoesNotContain("<b>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;", html, StringComparison.Ordinal);
        Assert.Contains("$5 and $10", html, StringComparison.Ordinal);
        Assert.Contains("<sup>2</sup>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Parent_review_and_messages_render_math()
    {
        var root = FindRoot();
        var review = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Components", "Pages", "LunaReview.razor"));
        var messages = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Components", "Pages", "Messages.razor"));
        Assert.Contains("MathText.ToHtml", review, StringComparison.Ordinal);
        Assert.Contains("MathText.ToHtml", messages, StringComparison.Ordinal);
    }

    [Fact]
    public void Sources_are_a_collapsed_counted_row()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaAppView.cs"));
        var en = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "en.json"));
        var es = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "es-MX.json"));
        Assert.Contains("Luna.Chat.SourcesTitleCount", view, StringComparison.Ordinal);
        Assert.Contains("links.IsVisible = !links.IsVisible", view, StringComparison.Ordinal);
        Assert.Contains("chevron.Text = links.IsVisible ? \"▾\" : \"▸\"", view, StringComparison.Ordinal);
        Assert.Contains("\"Luna.Chat.SourcesTitleCount\": \"Where I found this ({0})\"", en, StringComparison.Ordinal);
        Assert.Contains("\"Luna.Chat.SourcesTitleCount\": \"Dónde lo encontré ({0})\"", es, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
