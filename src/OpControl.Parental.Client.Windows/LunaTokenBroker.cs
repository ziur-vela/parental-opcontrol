// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Net.Http.Headers;
using System.Text.Json;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Client;

/// <summary>
/// Gets a short-lived Luna token from the server with the device credential.
/// The token lives in memory for this request only; it is never written to disk.
/// </summary>
public sealed class LunaTokenBroker
{
    public sealed record Token(string Value, DateTime ExpiresUtc, string ServerUrl);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly CredentialStore _credentials;
    private readonly ClientSettings _settings;
    private readonly LockRuntime _runtime;
    private readonly ILogger<LunaTokenBroker> _logger;

    public LunaTokenBroker(IHttpClientFactory httpFactory, CredentialStore credentials,
        ClientSettings settings, LockRuntime runtime, ILogger<LunaTokenBroker> logger)
    {
        _httpFactory = httpFactory;
        _credentials = credentials;
        _settings = settings;
        _runtime = runtime;
        _logger = logger;
    }

    public async Task<Token?> GetAsync(CancellationToken ct)
    {
        var enabled = _runtime.DeviceState?.LunaEnabled == true;
        var locked = _runtime.Current?.IsLocked == true;
        if (!LunaLaunchPolicy.ShouldRun(enabled, locked))
        {
            _logger.LogDebug("Luna token refused: enabled={Enabled} locked={Locked}", enabled, locked);
            return null;
        }

        var credential = _credentials.Load();
        if (credential is null || !credential.IsUsable)
        {
            _logger.LogWarning("Luna token refused: no usable device credential");
            return null;
        }

        var server = _settings.Value.ServerUrl.TrimEnd('/');
        try
        {
            using var client = _httpFactory.CreateClient("device");
            using var request = new HttpRequestMessage(HttpMethod.Post, server + "/api/device/luna/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Credential);
            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Luna token request failed status={Status}", (int)response.StatusCode);
                return null;
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("token", out var tokenNode) ||
                !root.TryGetProperty("expiresUtc", out var expiresNode) ||
                string.IsNullOrWhiteSpace(tokenNode.GetString()) ||
                !expiresNode.TryGetDateTime(out var expires))
            {
                _logger.LogWarning("Luna token response was malformed");
                return null;
            }

            return new Token(tokenNode.GetString()!, expires, server);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Luna token request failed");
            return null;
        }
    }
}
