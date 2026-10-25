// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class ScriptJsonTests
{
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void Script_json_is_valid_javascript_json_and_cannot_break_out_of_the_tag()
    {
        var json = ScriptJson.Serialize(new Dictionary<string, string>
        {
            ["quote"] = "He said \"hola\"",
            ["html"] = "</script><script>alert(1)</script> & < >",
            ["separators"] = "a\u2028b\u2029c",
            ["unicode"] = "Solicitando…"
        });

        // Quotes must be literal so the object literal is valid JavaScript. The
        // default System.Text.Json encoder writes \u0022 and breaks it.
        Assert.DoesNotContain("\\u0022", json);
        Assert.DoesNotContain("<", json);
        Assert.DoesNotContain(">", json);
        Assert.DoesNotContain("&", json);
        Assert.Contains("\\u2028", json);
        Assert.Contains("\\u2029", json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("He said \"hola\"", root.GetProperty("quote").GetString());
        Assert.Equal("</script><script>alert(1)</script> & < >", root.GetProperty("html").GetString());
        Assert.Equal("a\u2028b\u2029c", root.GetProperty("separators").GetString());
        Assert.Equal("Solicitando…", root.GetProperty("unicode").GetString());

        var assignment = $"window.ocpStrings = {json};";
        Assert.StartsWith("window.ocpStrings = {\"quote\":", assignment);
    }

    [Fact]
    public void Localized_js_strings_render_as_valid_inline_json()
    {
        using (CultureScope.Use("es-MX"))
        {
            var json = AppText.JsStringsJson();
            Assert.DoesNotContain("\\u0022", json);
            using var document = JsonDocument.Parse(json);
            Assert.Equal("Solicitando…", document.RootElement.GetProperty("requesting").GetString());
            Assert.Equal("La sesión terminó.", document.RootElement.GetProperty("sessionEnded").GetString());
        }

        using (CultureScope.Use("en"))
        {
            var json = AppText.JsStringsJson();
            using var document = JsonDocument.Parse(json);
            Assert.Equal("Requesting…", document.RootElement.GetProperty("requesting").GetString());
            Assert.Equal("Session ended.", document.RootElement.GetProperty("sessionEnded").GetString());
        }
    }

    [Fact]
    public void Every_layout_emits_js_strings_as_raw_safe_json()
    {
        var root = FindRoot();
        var server = Path.Combine(root, "src", "OpControl.Parental.Server");
        var blazor = File.ReadAllText(Path.Combine(server, "Components", "App.razor"));
        var layout = File.ReadAllText(Path.Combine(server, "Pages", "Shared", "_Layout.cshtml"));
        var auth = File.ReadAllText(Path.Combine(server, "Pages", "Shared", "_AuthLayout.cshtml"));

        Assert.Contains("MarkupString)OpControl.Parental.Server.Services.AppText.JsStringsJson()", blazor);
        Assert.Contains("@Html.Raw(OpControl.Parental.Server.Services.AppText.JsStringsJson())", layout);
        Assert.Contains("@Html.Raw(OpControl.Parental.Server.Services.AppText.JsStringsJson())", auth);
        Assert.DoesNotContain("HtmlString", blazor);
        Assert.DoesNotContain("HtmlString", layout);
        Assert.DoesNotContain("HtmlString", auth);
    }
}
