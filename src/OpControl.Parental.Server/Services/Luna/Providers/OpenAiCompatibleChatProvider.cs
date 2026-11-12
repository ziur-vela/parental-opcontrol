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
        if (config.Provider == Contracts.LunaChatProviders.OpenRouter && request.WebSearch is { } search)
        {
            var parameters = new Dictionary<string, object>
            {
                ["engine"] = search.Engine,
                ["max_results"] = search.MaxResults,
                ["max_total_results"] = search.MaxTotalResults
            };
            if (search.ExcludedDomains.Count > 0)
                parameters["excluded_domains"] = search.ExcludedDomains;
            body["tools"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "openrouter:web_search",
                    ["parameters"] = parameters
                }
            };
        }

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
            var messageJson = root.GetProperty("choices")[0].GetProperty("message");
            var content = messageJson.GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
                throw new LunaProviderException("Luna.Error.ProviderDown");
            var promptTokens = 0;
            var completionTokens = 0;
            var webSearches = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var prompt)) promptTokens = prompt.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var completion)) completionTokens = completion.GetInt32();
                if (usage.TryGetProperty("server_tool_use_details", out var toolUse)
                    && toolUse.TryGetProperty("web_search_requests", out var searches)
                    && searches.TryGetInt32(out var count))
                    webSearches = count;
            }
            var sources = ReadSources(messageJson);
            var queries = ReadQueries(messageJson);
            return new LunaChatResult(content.Trim(), promptTokens, completionTokens, sources, webSearches, queries);
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

    private static List<LunaChatSource> ReadSources(JsonElement message)
    {
        var sources = new List<LunaChatSource>();
        if (!message.TryGetProperty("annotations", out var annotations) || annotations.ValueKind != JsonValueKind.Array)
            return sources;
        foreach (var annotation in annotations.EnumerateArray())
        {
            if (!annotation.TryGetProperty("type", out var type)
                || !string.Equals(type.GetString(), "url_citation", StringComparison.OrdinalIgnoreCase))
                continue;
            JsonElement citation = annotation;
            if (annotation.TryGetProperty("url_citation", out var nested)) citation = nested;
            if (!citation.TryGetProperty("url", out var url) || url.GetString() is not { } value) continue;
            var title = citation.TryGetProperty("title", out var t) ? t.GetString() : null;
            sources.Add(new LunaChatSource(value, title));
        }
        return sources;
    }

    private static List<string> ReadQueries(JsonElement message)
    {
        var queries = new List<string>();
        if (!message.TryGetProperty("tool_calls", out var calls) || calls.ValueKind != JsonValueKind.Array)
            return queries;
        foreach (var call in calls.EnumerateArray())
        {
            if (!call.TryGetProperty("function", out var function)
                || !function.TryGetProperty("arguments", out var arguments)
                || arguments.GetString() is not { } raw)
                continue;
            try
            {
                using var json = JsonDocument.Parse(raw);
                if ((json.RootElement.TryGetProperty("query", out var query)
                     || json.RootElement.TryGetProperty("search_query", out query))
                    && query.GetString() is { } text && text.Trim().Length > 0)
                    queries.Add(text.Trim());
            }
            catch (JsonException) { }
        }
        return queries;
    }

    internal static void AddOpenRouterHeaders(HttpRequestMessage message, string provider)
    {
        if (provider != Contracts.LunaChatProviders.OpenRouter) return;
        message.Headers.TryAddWithoutValidation("HTTP-Referer", "https://parental.opcontrol.net");
        message.Headers.TryAddWithoutValidation("X-Title", "OpControl Parental Luna");
    }
}
