// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Server.Services.Luna;

public sealed record LunaChatTurn(string Role, string Content);

public sealed record LunaChatRequest(
    string SystemPrompt,
    IReadOnlyList<LunaChatTurn> Messages,
    int MaxTokens,
    bool Stricter = false);

public sealed record LunaChatResult(string Text, int PromptTokens, int CompletionTokens);

public sealed record LunaProviderConfig(string Provider, string Model, string ApiKey);

public sealed record LunaModerationResult(
    bool Flagged,
    IReadOnlyList<string> Categories,
    IReadOnlyDictionary<string, double> Scores);

public interface ILunaChatProvider
{
    string ProviderId { get; }
    Task<LunaChatResult> CompleteAsync(LunaProviderConfig config, LunaChatRequest request, CancellationToken ct);
}

public interface ILunaModerator
{
    Task<LunaModerationResult> ModerateAsync(string text, CancellationToken ct);
}

public interface ILunaProviderFactory
{
    ILunaChatProvider GetChat(string providerId);
    ILunaModerator GetModerator(Data.LunaFamilySettings settings);
    string? ChatKey(Data.LunaFamilySettings settings);
    string? ModerationKey(Data.LunaFamilySettings settings);
    string? Protect(string? value);
}

public sealed class LunaProviderException : Exception
{
    public LunaProviderException(string messageKey, Exception? inner = null) : base(messageKey, inner)
    {
        MessageKey = messageKey;
    }

    /// <summary>A server localization key. The message itself is never technical English.</summary>
    public string MessageKey { get; }

    public static LunaProviderException FromStatus(int status, string? body)
    {
        var text = body ?? "";
        if (status is 401 or 403) return new LunaProviderException("Luna.Error.BadKey");
        if (status == 404) return new LunaProviderException("Luna.Error.ModelNotFound");
        if (status == 402) return new LunaProviderException("Luna.Error.NoCredit");
        if (status == 429)
            return new LunaProviderException(text.Contains("quota", StringComparison.OrdinalIgnoreCase)
                || text.Contains("credit", StringComparison.OrdinalIgnoreCase)
                ? "Luna.Error.NoCredit"
                : "Luna.Error.RateLimited");
        if (status >= 500) return new LunaProviderException("Luna.Error.ProviderDown");
        if (status == 400 && (text.Contains("model", StringComparison.OrdinalIgnoreCase)
            || text.Contains("does not exist", StringComparison.OrdinalIgnoreCase)))
            return new LunaProviderException("Luna.Error.ModelNotFound");
        return new LunaProviderException("Luna.Error.ProviderDown");
    }
}
