// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using OpControl.Parental.Server.Components.Shared;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job182WelcomeTests
{
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

    [Fact]
    public async Task Luna_header_link_renders_no_stray_braces()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        services.AddSingleton<AuthenticationStateProvider>(new AnonymousAuthStateProvider());
        services.AddSingleton<IJSRuntime>(new NoopJsRuntime());
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        await using var provider = services.BuildServiceProvider();

        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<LunaWelcome>(ParameterView.Empty);
            return output.ToHtmlString();
        });

        Assert.Contains("luna-what-link", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{", html, StringComparison.Ordinal);
        Assert.DoesNotContain("}", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_markup_has_no_unbalanced_braces()
    {
        var components = Path.Combine(Root, "src", "OpControl.Parental.Server", "Components");
        foreach (var file in Directory.EnumerateFiles(components, "*.razor", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.Equal(text.Count(c => c == '{'), text.Count(c => c == '}'));
        }
    }

    [Fact]
    public void Welcome_waits_then_opens_with_a_quick_reversible_animation()
    {
        var component = File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Server",
            "Components", "Shared", "LunaWelcome.razor"));
        Assert.Contains("AutoOpenDelayMs = 600", component, StringComparison.Ordinal);
        Assert.Contains("firstRender && _autoOpenPending", component, StringComparison.Ordinal);
        Assert.Contains("ocpLunaWelcomeHide", component, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Server",
            "wwwroot", "css", "parental.css"));
        Assert.Contains("@keyframes lw-backdrop-in", css, StringComparison.Ordinal);
        Assert.Contains("lw-backdrop-in 200ms", css, StringComparison.Ordinal);
        Assert.Contains("lw-dialog-in 220ms", css, StringComparison.Ordinal);
        Assert.Contains(".luna-welcome-backdrop.lw-closing", css, StringComparison.Ordinal);
        Assert.Contains("lw-dialog-out 150ms", css, StringComparison.Ordinal);
        Assert.Contains(".luna-welcome { animation: none !important; }", css, StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(Root, "src", "OpControl.Parental.Server",
            "wwwroot", "js", "app.js"));
        Assert.Contains("ocpLunaWelcomeHide", js, StringComparison.Ordinal);
        Assert.Contains("lw-closing", js, StringComparison.Ordinal);
        Assert.Contains("setTimeout(finish, 180)", js, StringComparison.Ordinal);
    }

    private sealed class AnonymousAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private sealed class NoopJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken,
            object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
