// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace OpControl.Parental.Server.Services;

/// <summary>
/// Converts the small LaTeX subset Luna uses into readable HTML for the parent
/// dashboard. The child app draws math with LiveMarkdown; the dashboard does
/// not ship a math renderer, so stacked fractions are built from spans here.
/// </summary>
public static class MathText
{
    private static readonly Regex Span = new(
        @"(?<display>\$\$(?<d>.+?)\$\$)|(?<inline>\$(?<i>[^$]+?)\$)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex Frac = new(
        @"\\(?:d|t)?frac\s*\{([^{}]*)\}\s*\{([^{}]*)\}",
        RegexOptions.Compiled);

    private static readonly Regex Sqrt = new(
        @"\\sqrt\s*\{([^{}]*)\}",
        RegexOptions.Compiled);

    private static readonly Regex TextCommand = new(
        @"\\(?:text|mathrm|operatorname)\s*\{([^{}]*)\}",
        RegexOptions.Compiled);

    private static readonly Regex Superscript = new(
        @"\^\{([^{}]*)\}|\^([0-9A-Za-z+\-])",
        RegexOptions.Compiled);

    private static readonly Regex Subscript = new(
        @"_\{([^{}]*)\}|_([0-9A-Za-z+\-])",
        RegexOptions.Compiled);

    private static readonly Regex Command = new(@"\\([A-Za-z]+)", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Commands = new(StringComparer.Ordinal)
    {
        ["times"] = "×",
        ["cdot"] = "·",
        ["div"] = "÷",
        ["pm"] = "±",
        ["mp"] = "∓",
        ["le"] = "≤",
        ["leq"] = "≤",
        ["ge"] = "≥",
        ["geq"] = "≥",
        ["neq"] = "≠",
        ["ne"] = "≠",
        ["approx"] = "≈",
        ["infty"] = "∞",
        ["pi"] = "π",
        ["theta"] = "θ",
        ["alpha"] = "α",
        ["beta"] = "β",
        ["gamma"] = "γ",
        ["delta"] = "δ",
        ["lambda"] = "λ",
        ["mu"] = "μ",
        ["sigma"] = "σ",
        ["omega"] = "ω",
        ["sum"] = "∑",
        ["int"] = "∫",
        ["log"] = "log",
        ["ln"] = "ln",
        ["sin"] = "sin",
        ["cos"] = "cos",
        ["tan"] = "tan",
        ["left"] = "",
        ["right"] = "",
        ["quad"] = "  ",
        ["qquad"] = "    ",
        ["%"] = "%",
        ["$"] = "$",
        ["_"] = "_",
        ["&"] = "&"
    };

    public static string ToHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var output = new StringBuilder(text.Length + 32);
        var last = 0;
        foreach (Match match in Span.Matches(text))
        {
            if (match.Index > last) output.Append(Escape(text[last..match.Index]));
            var value = match.Groups["d"].Success ? match.Groups["d"].Value : match.Groups["i"].Value;
            if (!LooksLikeMath(value))
            {
                output.Append(Escape(match.Value));
            }
            else
            {
                var cls = match.Groups["display"].Success ? "math-display" : "math-inline";
                output.Append("<span class=\"").Append(cls).Append("\">")
                    .Append(Latex(value)).Append("</span>");
            }
            last = match.Index + match.Length;
        }
        if (last < text.Length) output.Append(Escape(text[last..]));
        return output.ToString();
    }

    private static bool LooksLikeMath(string value) =>
        value.Contains('\\') || value.Contains('^') || value.Contains('_')
        || value.Contains('{') || value.Contains('}') || value.Contains('=');

    private static string Escape(string text) => WebUtility.HtmlEncode(text);

    private static string Latex(string raw)
    {
        var value = Escape(raw.Trim());
        value = Frac.Replace(value, m =>
            "<span class=\"math-frac\"><span class=\"math-num\">" + m.Groups[1].Value +
            "</span><span class=\"math-den\">" + m.Groups[2].Value + "</span></span>");
        value = Sqrt.Replace(value, m =>
            "<span class=\"math-sqrt\">√<span class=\"math-rad\">" + m.Groups[1].Value + "</span></span>");
        value = TextCommand.Replace(value, "$1");
        value = Superscript.Replace(value, m =>
            "<sup>" + (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) + "</sup>");
        value = Subscript.Replace(value, m =>
            "<sub>" + (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) + "</sub>");
        value = Command.Replace(value, m =>
            Commands.TryGetValue(m.Groups[1].Value, out var replacement) ? replacement : m.Value);
        value = value.Replace("\\\\", "<br/>", StringComparison.Ordinal)
            .Replace("\\,", " ", StringComparison.Ordinal)
            .Replace("\\;", " ", StringComparison.Ordinal)
            .Replace("\\:", " ", StringComparison.Ordinal)
            .Replace("\\!", "", StringComparison.Ordinal)
            .Replace("\\ ", " ", StringComparison.Ordinal)
            .Replace("{", "", StringComparison.Ordinal)
            .Replace("}", "", StringComparison.Ordinal);
        return value.Trim();
    }
}
