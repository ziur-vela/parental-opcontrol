// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpControl.Parental.Server.Components.Shared;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job20DAboutPageTests
{
    private static readonly Guid Family = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Child = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Device = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    [Theory]
    [InlineData("Owner", 511, "en")]
    [InlineData("Owner", 511, "es-MX")]
    [InlineData("Parent", 511, "en")]
    [InlineData("Parent", 511, "es-MX")]
    [InlineData("Parent", 16, "en")]
    [InlineData("Parent", 16, "es-MX")]
    public async Task About_renders_for_every_parent_role_in_both_languages(string role, int permissions,
        string language)
    {
        var html = await RenderAboutAsync(role, permissions, language);
        var spanish = language == "es-MX";
        Assert.Contains("about-brand", html, StringComparison.Ordinal);
        Assert.Contains("Hendry (Ziur)", html, StringComparison.Ordinal);
        Assert.Contains(spanish ? "OpControl IT Solutions" : "OpControl IT Solutions", html, StringComparison.Ordinal);
        Assert.Contains(spanish ? "Acerca de" : "About", html, StringComparison.Ordinal);
        Assert.Contains("PolyForm Noncommercial License 1.0.0", html, StringComparison.Ordinal);
        Assert.Contains(spanish ? "Construido con IA" : "Built with AI", html, StringComparison.Ordinal);
        Assert.Contains("License", html, StringComparison.Ordinal);
        Assert.Contains("EMMA-LAPTOP", html, StringComparison.Ordinal);
        Assert.Equal(1, CountOf(html, "Hendry (Ziur)"));
    }

    [Fact]
    public async Task About_shows_the_server_version_the_device_versions_and_the_license_text()
    {
        var html = await RenderAboutAsync("Owner", 511, "en");
        Assert.Contains("/releases/tag/v", html, StringComparison.Ordinal);
        Assert.Contains("v0.17.19", html, StringComparison.Ordinal);
        Assert.Contains("EMMA-LAPTOP", html, StringComparison.Ordinal);
        Assert.Contains("v0.17.18", html, StringComparison.Ordinal);
        Assert.Contains("PolyForm Noncommercial License 1.0.0", html, StringComparison.Ordinal);
        Assert.Contains("All rights reserved, except as granted by the PolyForm Noncommercial License 1.0.0.", html,
            StringComparison.Ordinal);
        Assert.Contains("Read the full license", html, StringComparison.Ordinal);
        Assert.Contains("Required Notice: Copyright (c) 2026 Ziur / OpControl IT Solutions", html,
            StringComparison.Ordinal);
        Assert.Contains("about-license", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task About_lists_the_generated_third_party_components_and_fonts()
    {
        var html = await RenderAboutAsync("Owner", 511, "en");
        var credits = AboutCredits.Credits.Components;
        Assert.NotEmpty(credits);
        var names = credits.Select(component => component.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ASP.NET Core", names);
        Assert.Contains(".NET Runtime", names);
        Assert.Contains("Microsoft.EntityFrameworkCore", names);
        Assert.Contains("Npgsql", names);
        Assert.Contains("Avalonia", names);
        Assert.Contains("LiveMarkdown.Avalonia", names);
        Assert.Contains("Fredoka (font)", names);
        Assert.Contains("Nunito (font)", names);
        Assert.Contains("IBM Plex Mono (font)", names);
        Assert.Contains("Manrope (font)", names);
        Assert.Contains("PostgreSQL (server)", names);
        Assert.All(credits, component =>
        {
            Assert.False(string.IsNullOrWhiteSpace(component.License));
            Assert.NotEqual("Unknown", component.License);
            Assert.NotEqual("See license URL", component.License);
        });
        Assert.Contains("Microsoft.EntityFrameworkCore", html, StringComparison.Ordinal);
        Assert.Contains("SIL OFL 1.1", html, StringComparison.Ordinal);
        Assert.Contains("credit-list", html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_real_name_appears_only_in_the_about_author_line()
    {
        var allowed = new[]
        {
            Path.Combine("src", "OpControl.Parental.Server", "Resources", "AppStrings.resx"),
            Path.Combine("src", "OpControl.Parental.Server", "Resources", "AppStrings.es-MX.resx")
        };
        var files = PublicTextFiles().ToList();
        var matches = files.Where(file => File.ReadAllText(file).Contains("Hendry (Ziur)", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(Root, file))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(allowed.OrderBy(path => path), matches.OrderBy(path => path));
        foreach (var file in allowed)
            Assert.Equal(1, CountOf(File.ReadAllText(Path.Combine(Root, file)), "Hendry (Ziur)"));
    }

    [Fact]
    public void Public_text_never_uses_the_internal_agent_name()
    {
        var pattern = new Regex(@"(?<![A-Za-z])OC(?![A-Za-z])", RegexOptions.CultureInvariant);
        foreach (var file in PublicTextFiles())
        {
            var text = File.ReadAllText(file);
            var match = pattern.Match(text);
            Assert.False(match.Success,
                $"{Path.GetRelativePath(Root, file)} contains the internal name at offset {match.Index}");
        }
    }

    [Fact]
    public void Sidebar_version_and_license_links_open_the_about_page()
    {
        var layout = File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Server", "Components",
            "Layout", "MainLayout.razor"));
        Assert.Contains("<a class=\"pill-soft\" href=\"/about\">", layout, StringComparison.Ordinal);
        Assert.Contains("href=\"/about\">@L[\"Layout.VersionDetails\"]", layout, StringComparison.Ordinal);
        Assert.Contains("href=\"/about\">@L[\"Legal.ShortLine\"]", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Layout.ProductTagline", layout, StringComparison.Ordinal);
        Assert.Contains("Layout.BuiltWithAi", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void About_page_is_authorized_and_has_responsive_styles()
    {
        var page = File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Server", "Components", "Pages",
            "About.razor"));
        Assert.Contains("@page \"/about\"", page, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", page, StringComparison.Ordinal);
        var css = File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Server", "wwwroot", "css",
            "parental.css"));
        Assert.Contains(".about-brand", css, StringComparison.Ordinal);
        Assert.Contains(".about-opc-logo", css, StringComparison.Ordinal);
        Assert.Contains(".credit-list", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Ai_credits_use_the_new_wording_everywhere()
    {
        var english = File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Server", "Resources",
            "AppStrings.resx"));
        var spanish = File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Server", "Resources",
            "AppStrings.es-MX.resx"));
        var readme = File.ReadAllText(Path.Combine(Root, "README.md"));
        var readmeEs = File.ReadAllText(Path.Combine(Root, "README.es.md"));
        var kidEnglish = File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Localization", "en.json"));
        var kidSpanish = File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Localization", "es-MX.json"));

        const string englishPhrase = "code written with **OpenCode**, the open-source AI coding agent, using **DeepSeek V4.1 Flash** by DeepSeek";
        const string spanishPhrase = "código escrito con **OpenCode**, el agente de programación con IA de código abierto, usando **DeepSeek V4.1 Flash** de DeepSeek";
        Assert.Contains(englishPhrase.Replace("**", string.Empty), WithoutBold(english), StringComparison.Ordinal);
        Assert.Contains(spanishPhrase.Replace("**", string.Empty), WithoutBold(spanish), StringComparison.Ordinal);
        Assert.Contains("DeepSeek V4.1 Flash", NormalizeWhitespace(readme), StringComparison.Ordinal);
        Assert.Contains("DeepSeek V4.1 Flash", NormalizeWhitespace(readmeEs), StringComparison.Ordinal);
        Assert.Contains("DeepSeek V4.1 Flash", kidEnglish, StringComparison.Ordinal);
        Assert.Contains("DeepSeek V4.1 Flash", kidSpanish, StringComparison.Ordinal);
        Assert.DoesNotContain("models. Every change", readme, StringComparison.Ordinal);
    }

    private static string WithoutBold(string text) => text.Replace("**", string.Empty);

    private static string NormalizeWhitespace(string text) => Regex.Replace(text, @"\s+", " ");

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static IEnumerable<string> PublicTextFiles()
    {
        var roots = new[]
        {
            Path.Combine(Root, "src"),
            Path.Combine(Root, "README.md"),
            Path.Combine(Root, "README.es.md"),
            Path.Combine(Root, ".github", "release-notes")
        };
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".cs", ".razor", ".cshtml", ".resx", ".json", ".md", ".axaml", ".xaml" };
        foreach (var root in roots)
        {
            if (File.Exists(root))
            {
                if (extensions.Contains(Path.GetExtension(root))) yield return root;
                continue;
            }
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Root, file);
                if (relative.Split(Path.DirectorySeparatorChar).Any(part =>
                        part is "bin" or "obj" or ".git")) continue;
                if (extensions.Contains(Path.GetExtension(file))) yield return file;
            }
        }
    }

    private static async Task<string> RenderAboutAsync(string role, int permissions, string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(language);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        try
        {
            var database = "about-" + Guid.NewGuid();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddLocalization();
            services.AddDbContext<ParentalDbContext>(options => options.UseInMemoryDatabase(database));
            services.AddSingleton<BuildInfo>();
            services.AddSingleton<AuthenticationStateProvider>(
                new FixedAuthStateProvider(role, permissions));

            await using var provider = services.BuildServiceProvider();
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ParentalDbContext>();
                db.Families.Add(new Family { Id = Family, Name = "Ruiz" });
                db.Children.Add(new Child { Id = Child, FamilyId = Family, Name = "Emma" });
                db.Devices.Add(new Device
                {
                    Id = Device,
                    FamilyId = Family,
                    ChildId = Child,
                    Name = "EMMA-LAPTOP",
                    ClientVersion = "v0.17.18",
                    LastSeenUtc = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc)
                });
                await db.SaveChangesAsync();
            }

            await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
            return await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await renderer.RenderComponentAsync<AboutPanel>(ParameterView.Empty);
                return output.ToHtmlString();
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private sealed class FixedAuthStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state;

        public FixedAuthStateProvider(string role, int permissions)
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("family_id", Family.ToString()),
                new Claim("family_role", role),
                new Claim("permissions", permissions.ToString(CultureInfo.InvariantCulture)),
                new Claim("display_name", "Test Parent")
            ], "Test");
            _state = new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
    }
}
