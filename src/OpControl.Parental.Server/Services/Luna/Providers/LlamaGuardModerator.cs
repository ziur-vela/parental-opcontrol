// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpControl.Parental.Server.Services.Luna.Providers;

/// <summary>Llama Guard 4 through OpenRouter. Every OpenRouter request carries provider.zdr = true.</summary>
public sealed class LlamaGuardModerator : ILunaModerator
{
    public const string Model = "meta-llama/llama-guard-4-12b";
    private readonly IHttpClientFactory _http;
    private readonly string _apiKey;

    public LlamaGuardModerator(IHttpClientFactory http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<LunaModerationResult> ModerateAsync(string text, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        var body = new Dictionary<string, object>
        {
            ["model"] = Model,
            ["messages"] = new[] { new { role = "user", content = text } },
            ["max_tokens"] = 24,
            ["temperature"] = 0,
            ["provider"] = new { zdr = true }
        };

        using var http = _http.CreateClient("luna");
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
        {
            Content = JsonContent.Create(body)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        OpenAiCompatibleChatProvider.AddOpenRouterHeaders(message, Contracts.LunaChatProviders.OpenRouter);

        try
        {
            using var response = await http.SendAsync(message, timeout.Token);
            var raw = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw LunaProviderException.FromStatus((int)response.StatusCode, raw);

            using var json = JsonDocument.Parse(raw);
            var content = (json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "").Trim();
            if (!content.StartsWith("safe", StringComparison.OrdinalIgnoreCase)
                && !content.StartsWith("unsafe", StringComparison.OrdinalIgnoreCase))
                throw new LunaProviderException("Luna.Error.ProviderDown");
            var classification = LunaSafety.ClassifyLlamaGuard(content);
            return new LunaModerationResult(classification.Blocked, classification.Categories, classification.Scores);
        }
        catch (LunaProviderException) { throw; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LunaProviderException("Luna.Error.ModerationTimeout");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new LunaProviderException("Luna.Error.ProviderDown", ex);
        }
    }
}
