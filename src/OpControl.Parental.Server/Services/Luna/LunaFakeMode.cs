// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services.Luna;

/// <summary>
/// Development-only fake providers for the Luna preview. The guard is deliberately
/// explicit: a Production or Staging process must refuse to start in this mode.
/// </summary>
public static class LunaFakeMode
{
    public const string ConfigurationKey = "Luna:FakeMode";
    public const string SeedConfigurationKey = "Luna:SeedDemo";

    public static void EnsureAllowed(IHostEnvironment environment, bool requested)
    {
        if (requested && !environment.IsDevelopment())
            throw new InvalidOperationException(
                "Luna fake provider mode is only allowed when ASPNETCORE_ENVIRONMENT=Development.");
    }

    public static void EnsureSeedAllowed(IHostEnvironment environment, bool fakeModeRequested, bool seedRequested)
    {
        EnsureAllowed(environment, fakeModeRequested);
        if (seedRequested && !environment.IsDevelopment())
            throw new InvalidOperationException(
                "The Luna demo seed is only allowed when ASPNETCORE_ENVIRONMENT=Development.");
        if (seedRequested && !fakeModeRequested)
            throw new InvalidOperationException(
                "The Luna demo seed requires fake provider mode.");
    }
}

public sealed class FakeLunaChatProvider : ILunaChatProvider
{
    public string ProviderId => "fake";

    public async Task<LunaChatResult> CompleteAsync(LunaProviderConfig config, LunaChatRequest request, CancellationToken ct)
    {
        if (request.SystemPrompt == LunaPrompts.TitlePrompt)
        {
            var snippet = request.Messages.LastOrDefault()?.Content ?? "";
            var spanishSnippet = snippet.Contains('¿') || snippet.Contains("fraccion", StringComparison.OrdinalIgnoreCase)
                || snippet.Contains("ayuda", StringComparison.OrdinalIgnoreCase);
            return new LunaChatResult(spanishSnippet ? "Práctica de fracciones" : "Fractions practice", 4, 2);
        }
        if (request.SystemPrompt.StartsWith(LunaPrompts.SummaryPrompt, StringComparison.Ordinal))
            return new LunaChatResult("The child practised fractions and times tables.", 8, 6);
        if (request.SystemPrompt.StartsWith(LunaPrompts.ProfilePrompt, StringComparison.Ordinal))
            return new LunaChatResult("Grade 5. Practising fractions and the 7 and 8 times tables.", 8, 6);

        if (request.SystemPrompt.Contains("setup health check", StringComparison.Ordinal))
            await Task.Delay(1_200, ct);

        // Slow enough for the preview to show the thinking animation by hand.
        await Task.Delay(1_200, ct);
        var last = request.Messages.LastOrDefault()?.Content ?? "";
        var spanish = last.Contains('¿') || last.Contains("fraccion", StringComparison.OrdinalIgnoreCase)
            || last.Contains("ayuda", StringComparison.OrdinalIgnoreCase);
        var reply = spanish
            ? last.Contains("fraccion", StringComparison.OrdinalIgnoreCase)
                ? "¡Buena pregunta! Para sumar fracciones, primero los números de abajo tienen que coincidir.\n\n$$\\frac{3}{4} = \\frac{6}{8}$$\n\n¿Por qué número multiplicas 4 para obtener 8?"
                : last.Contains("7", StringComparison.Ordinal)
                    ? "Vamos a resolverlo juntos. ¿Cuánto es 7 × 8? Prueba contar de siete en siete: 7, 14, 21…"
                    : "Buena pregunta. ¿Qué sabes ya sobre esto? Podemos resolver el siguiente paso juntos."
            : last.Contains("fraction", StringComparison.OrdinalIgnoreCase)
                ? "Great question! To add fractions, the bottom numbers have to match first.\n\n$$\\frac{3}{4} = \\frac{6}{8}$$\n\nWhat do you multiply 4 by to get 8?"
                : last.Contains("7", StringComparison.Ordinal)
                    ? "Let's work it out together. What is 7 × 8? Try counting in sevens: 7, 14, 21…"
                    : "That's a good question. What do you already know about it? We can work out the next step together.";
        return new LunaChatResult(reply, 12, 8);
    }
}

public sealed class FakeLunaModerator : ILunaModerator
{
    public Task<LunaModerationResult> ModerateAsync(string text, CancellationToken ct)
    {
        if (text.Contains("[demo-block]", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new LunaModerationResult(true, ["S12"], new Dictionary<string, double> { ["S12"] = 1.0 }));
        return Task.FromResult(new LunaModerationResult(false, [], new Dictionary<string, double>()));
    }
}

public sealed class FakeLunaZdrCatalog : ILunaZdrCatalog
{
    public Task<string?> MissingModelAsync(string chatModel, CancellationToken ct) => Task.FromResult<string?>(null);
}

public sealed class FakeLunaProviderFactory : ILunaProviderFactory
{
    public const string KeyProtector = "OpControl.Parental.Luna.Keys";

    private readonly IDataProtector _protector;
    private readonly FakeLunaChatProvider _chat = new();
    private readonly FakeLunaModerator _moderator = new();

    public FakeLunaProviderFactory(IDataProtectionProvider protection) =>
        _protector = protection.CreateProtector(KeyProtector);

    public ILunaChatProvider GetChat(string providerId) => _chat;

    public ILunaModerator GetModerator(LunaFamilySettings settings) => _moderator;

    public string? ChatKey(LunaFamilySettings settings) => Unprotect(settings.ChatKeyProtected);

    public string? ModerationKey(LunaFamilySettings settings) =>
        Unprotect(settings.ModerationKeyProtected)
        ?? (settings.ChatProvider == LunaChatProviders.OpenAi ? ChatKey(settings) : null);

    public string? Protect(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : _protector.Protect(value.Trim());

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return _protector.Unprotect(value); }
        catch { return null; }
    }
}
