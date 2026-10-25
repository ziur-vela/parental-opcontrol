// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Client;

/// <summary>
/// Saves the child's Night sky / Moon gray choice with the device credential.
/// The Helper and Luna share one setting; Luna writes it directly with its Luna token.
/// </summary>
public sealed class ChildThemeReporter
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly CredentialStore _credentials;
    private readonly ClientSettings _settings;
    private readonly ILogger<ChildThemeReporter> _logger;

    public ChildThemeReporter(IHttpClientFactory httpFactory, CredentialStore credentials,
        ClientSettings settings, ILogger<ChildThemeReporter> logger)
    {
        _httpFactory = httpFactory;
        _credentials = credentials;
        _settings = settings;
        _logger = logger;
    }

    public async Task<bool> SetAsync(string? theme, CancellationToken ct)
    {
        if (!LunaThemes.IsValid(theme)) return false;
        var credential = _credentials.Load();
        if (credential is null || !credential.IsUsable) return false;
        var server = _settings.Value.ServerUrl.TrimEnd('/');
        try
        {
            using var client = _httpFactory.CreateClient("device");
            using var request = new HttpRequestMessage(HttpMethod.Post, server + "/api/device/luna/theme")
            {
                Content = new StringContent(JsonSerializer.Serialize(new LunaThemeRequest(LunaThemes.Normalize(theme))),
                    Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Credential);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Child theme request failed status={Status}", (int)response.StatusCode);
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Child theme request failed");
            return false;
        }
    }
}
