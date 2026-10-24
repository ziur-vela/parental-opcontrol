// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpControl.Parental.Server.Services.Luna.Providers;

public sealed class OpenAiModerator : ILunaModerator
{
    public const string Model = "omni-moderation-latest";
    private readonly IHttpClientFactory _http;
    private readonly string _apiKey;

    public OpenAiModerator(IHttpClientFactory http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<LunaModerationResult> ModerateAsync(string text, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        using var http = _http.CreateClient("luna");
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/moderations")
        {
            Content = JsonContent.Create(new { model = Model, input = text })
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        try
        {
            using var response = await http.SendAsync(message, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw LunaProviderException.FromStatus((int)response.StatusCode, body);

            using var json = JsonDocument.Parse(body);
            var result = json.RootElement.GetProperty("results")[0];
            var flagged = result.TryGetProperty("flagged", out var flag) && flag.GetBoolean();
            var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (result.TryGetProperty("category_scores", out var categoryScores))
            {
                foreach (var property in categoryScores.EnumerateObject())
                    scores[property.Name] = property.Value.GetDouble();
            }
            if (scores.Count == 0)
                throw new LunaProviderException("Luna.Error.ProviderDown");
            return new LunaModerationResult(flagged, scores.Keys.ToList(), scores);
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
