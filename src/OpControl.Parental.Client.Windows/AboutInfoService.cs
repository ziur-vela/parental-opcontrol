// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Net.Http.Json;
using System.Text.Json;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Client;

/// <summary>
/// Read-only information for the kid view's About screen: the client build
/// and the server it is connected to. The server version is fetched from the
/// anonymous /version endpoint and cached briefly so opening About does not
/// hammer the server.
/// </summary>
public sealed class AboutInfoService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);
    private readonly IHttpClientFactory _httpFactory;
    private readonly ClientSettings _settings;
    private readonly ILogger<AboutInfoService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AboutInfoDto? _cached;
    private DateTime _cachedUtc = DateTime.MinValue;

    public AboutInfoService(IHttpClientFactory httpFactory, ClientSettings settings, ILogger<AboutInfoService> logger)
    {
        _httpFactory = httpFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task<AboutInfoDto> GetAsync(CancellationToken ct)
    {
        if (_cached is not null && DateTime.UtcNow - _cachedUtc < CacheLifetime)
            return _cached;

        if (!await _gate.WaitAsync(0, ct))
            return _cached ?? Local();

        try
        {
            var previous = _cached;
            var server = await FetchServerAsync(ct);
            var client = BuildMetadata.Read(typeof(AboutInfoService).Assembly);
            _cached = client with
            {
                ServerVersion = NormalizeVersion(server?.Version) ?? previous?.ServerVersion,
                ServerBuildDate = server?.BuildDate ?? previous?.ServerBuildDate,
                ServerDisplayVersion = server?.DisplayVersion ?? previous?.ServerDisplayVersion
            };
            _cachedUtc = DateTime.UtcNow;
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string? NormalizeVersion(string? version)
    {
        var trimmed = version?.Trim().TrimStart('v', 'V');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private AboutInfoDto Local()
    {
        var client = BuildMetadata.Read(typeof(AboutInfoService).Assembly);
        return _cached is null
            ? client
            : client with
            {
                ServerVersion = _cached.ServerVersion,
                ServerBuildDate = _cached.ServerBuildDate,
                ServerDisplayVersion = _cached.ServerDisplayVersion
            };
    }

    private async Task<VersionInfoDto?> FetchServerAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpFactory.CreateClient("device");
            var server = _settings.Value.ServerUrl.TrimEnd('/');
            using var response = await client.GetAsync($"{server}/version", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<VersionInfoDto>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read the server version for About");
            return null;
        }
    }
}
