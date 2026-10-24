// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpControl.Parental.Server.Services.Luna.Providers;

/// <summary>OpenAI-compatible chat completions: OpenAI, DeepSeek, Gemini (OpenAI-compatible endpoint) and OpenRouter.</summary>
public sealed class OpenAiCompatibleChatProvider : ILunaChatProvider
{
    private readonly IHttpClientFactory _http;

    public OpenAiCompatibleChatProvider(IHttpClientFactory http) => _http = http;

    public string ProviderId => "openai-compatible";

    public static string BaseUrl(string provider) => provider switch
    {
        Contracts.LunaChatProviders.OpenAi => "https://api.openai.com/v1",
        Contracts.LunaChatProviders.DeepSeek => "https://api.deepseek.com/v1",
        Contracts.LunaChatProviders.Gemini => "https://generativelanguage.googleapis.com/v1beta/openai",
        Contracts.LunaChatProviders.OpenRouter => "https://openrouter.ai/api/v1",
        _ => throw new LunaProviderException("Luna.Error.ProviderDown")
    };

    public async Task<LunaChatResult> CompleteAsync(LunaProviderConfig config, LunaChatRequest request, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        var messages = new List<object> { new { role = "system", content = request.SystemPrompt } };
        messages.AddRange(request.Messages.Select(m => (object)new { role = m.Role, content = m.Content }));
        var body = new Dictionary<string, object>
        {
            ["model"] = config.Model,
            ["messages"] = messages
        };
        // Newer OpenAI reasoning models require max_completion_tokens and the default temperature.
        var reasoningModel = config.Provider == Contracts.LunaChatProviders.OpenAi
            && (config.Model.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
                || config.Model.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
                || config.Model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase));
        body[config.Provider == Contracts.LunaChatProviders.OpenAi && reasoningModel
            ? "max_completion_tokens"
            : "max_tokens"] = Math.Clamp(request.MaxTokens, 8, 2000);
        if (!reasoningModel) body["temperature"] = 0.4;
        if (config.Provider == Contracts.LunaChatProviders.OpenRouter)
            body["provider"] = new { zdr = true };

        using var http = _http.CreateClient("luna");
        using var message = new HttpRequestMessage(HttpMethod.Post, BaseUrl(config.Provider) + "/chat/completions")
        {
            Content = JsonContent.Create(body)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        AddOpenRouterHeaders(message, config.Provider);

        try
        {
            using var response = await http.SendAsync(message, timeout.Token);
            var text = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw LunaProviderException.FromStatus((int)response.StatusCode, text);

            using var json = JsonDocument.Parse(text);
            var root = json.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
                throw new LunaProviderException("Luna.Error.ProviderDown");
            var promptTokens = 0;
            var completionTokens = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var prompt)) promptTokens = prompt.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var completion)) completionTokens = completion.GetInt32();
            }
            return new LunaChatResult(content.Trim(), promptTokens, completionTokens);
        }
        catch (LunaProviderException) { throw; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LunaProviderException("Luna.Error.ProviderDown");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new LunaProviderException("Luna.Error.ProviderDown", ex);
        }
    }

    internal static void AddOpenRouterHeaders(HttpRequestMessage message, string provider)
    {
        if (provider != Contracts.LunaChatProviders.OpenRouter) return;
        message.Headers.TryAddWithoutValidation("HTTP-Referer", "https://parental.opcontrol.net");
        message.Headers.TryAddWithoutValidation("X-Title", "OpControl Parental Luna");
    }
}
