// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.AspNetCore.DataProtection;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services.Luna.Providers;

namespace OpControl.Parental.Server.Services.Luna;

public sealed class LunaProviderFactory : ILunaProviderFactory
{
    public const string KeyProtector = "OpControl.Parental.Luna.Keys";

    private readonly IHttpClientFactory _http;
    private readonly IDataProtector _protector;
    private readonly OpenAiCompatibleChatProvider _openAiCompatible;
    private readonly AnthropicChatProvider _anthropic;

    public LunaProviderFactory(IHttpClientFactory http, IDataProtectionProvider protection)
    {
        _http = http;
        _protector = protection.CreateProtector(KeyProtector);
        _openAiCompatible = new OpenAiCompatibleChatProvider(http);
        _anthropic = new AnthropicChatProvider(http);
    }

    public ILunaChatProvider GetChat(string providerId) => providerId switch
    {
        LunaChatProviders.Anthropic => _anthropic,
        LunaChatProviders.OpenAi or LunaChatProviders.DeepSeek or LunaChatProviders.Gemini or LunaChatProviders.OpenRouter => _openAiCompatible,
        _ => throw new LunaProviderException("Luna.Error.ProviderDown")
    };

    public ILunaModerator GetModerator(LunaFamilySettings settings)
    {
        if (settings.Setup == LunaSetup.A || settings.ChatProvider == LunaChatProviders.OpenRouter)
        {
            var key = ChatKey(settings) ?? "";
            return new LlamaGuardModerator(_http, key);
        }

        var moderationKey = ModerationKey(settings);
        if (string.IsNullOrWhiteSpace(moderationKey))
            throw new LunaProviderException("Luna.Error.MissingModerationKey");
        return new OpenAiModerator(_http, moderationKey);
    }

    public string? ChatKey(LunaFamilySettings settings) => Unprotect(settings.ChatKeyProtected);

    public string? ModerationKey(LunaFamilySettings settings)
    {
        var explicitKey = Unprotect(settings.ModerationKeyProtected);
        if (!string.IsNullOrWhiteSpace(explicitKey)) return explicitKey;
        // Setup B with OpenAI as the chat provider may reuse the same key.
        return settings.ChatProvider == LunaChatProviders.OpenAi ? ChatKey(settings) : null;
    }

    public string? Protect(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : _protector.Protect(value.Trim());

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return _protector.Unprotect(value); }
        catch { return null; }
    }

    public static string Last4(string? value) =>
        string.IsNullOrEmpty(value) ? "" : value.Length <= 4 ? value : value[^4..];
}
