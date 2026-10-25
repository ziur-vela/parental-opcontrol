// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpControl.Parental.Server.Services;

/// <summary>
/// Produces JSON that can be assigned inside an inline &lt;script&gt; tag.
/// Quotes stay literal so the result is valid JavaScript, while HTML-sensitive
/// characters and JavaScript line separators are escaped so a value can never
/// close the script tag or break the statement.
/// </summary>
public static class ScriptJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        return json
            .Replace("&", "\\u0026", StringComparison.Ordinal)
            .Replace("<", "\\u003C", StringComparison.Ordinal)
            .Replace(">", "\\u003E", StringComparison.Ordinal)
            .Replace("\u2028", "\\u2028", StringComparison.Ordinal)
            .Replace("\u2029", "\\u2029", StringComparison.Ordinal);
    }
}
