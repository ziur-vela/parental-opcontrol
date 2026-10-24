// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Net.Http.Json;
using System.Text.Json;

namespace OpControl.Parental.Server.Services.Luna.Providers;

public sealed class AnthropicChatProvider : ILunaChatProvider
{
    private readonly IHttpClientFactory _http;

    public AnthropicChatProvider(IHttpClientFactory http) => _http = http;

    public string ProviderId => Contracts.LunaChatProviders.Anthropic;

    public async Task<LunaChatResult> CompleteAsync(LunaProviderConfig config, LunaChatRequest request, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        // Anthropic requires the conversation to start with a user turn and to alternate.
        // Consecutive same-role turns are merged.
        var turns = new List<(string Role, System.Text.StringBuilder Content)>();
        foreach (var turnMessage in request.Messages)
        {
            var role = turnMessage.Role == "assistant" ? "assistant" : "user";
            if (turns.Count == 0 && role == "assistant") continue;
            if (turns.Count > 0 && turns[^1].Role == role)
                turns[^1].Content.Append("\n\n").Append(turnMessage.Content);
            else
                turns.Add((role, new System.Text.StringBuilder(turnMessage.Content)));
        }
        if (turns.Count == 0 || turns[0].Role != "user")
            throw new LunaProviderException("Luna.Error.ProviderDown");

        var body = new
        {
            model = config.Model,
            max_tokens = Math.Clamp(request.MaxTokens, 8, 2000),
            temperature = 0.4,
            system = request.SystemPrompt,
            messages = turns.Select(t => new { role = t.Role, content = t.Content.ToString() }).ToList()
        };

        using var http = _http.CreateClient("luna");
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = JsonContent.Create(body)
        };
        message.Headers.TryAddWithoutValidation("x-api-key", config.ApiKey);
        message.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        try
        {
            using var response = await http.SendAsync(message, timeout.Token);
            var text = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw LunaProviderException.FromStatus((int)response.StatusCode, text);

            using var json = JsonDocument.Parse(text);
            var root = json.RootElement;
            var builder = new System.Text.StringBuilder();
            foreach (var block in root.GetProperty("content").EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) && type.GetString() == "text"
                    && block.TryGetProperty("text", out var value))
                    builder.Append(value.GetString());
            }
            var content = builder.ToString().Trim();
            if (content.Length == 0)
                throw new LunaProviderException("Luna.Error.ProviderDown");

            var promptTokens = 0;
            var completionTokens = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("input_tokens", out var input)) promptTokens = input.GetInt32();
                if (usage.TryGetProperty("output_tokens", out var output)) completionTokens = output.GetInt32();
            }
            return new LunaChatResult(content, promptTokens, completionTokens);
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
}
