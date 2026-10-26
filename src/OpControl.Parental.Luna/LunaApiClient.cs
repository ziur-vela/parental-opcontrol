// Copyright (c) 2026 Ziur / OpControl IT Solutions
using OpControl.Parental.Localization;
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.IO.Pipes;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Luna;

public sealed class LunaApiException : Exception
{
    public HttpStatusCode Status { get; }
    public LunaApiException(HttpStatusCode status, string message) : base(message) => Status = status;
}

public interface ILunaApiClient
{
    Task ConnectAsync(bool dev, CancellationToken ct = default);
    Task<LunaStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<List<LunaChatDto>> GetChatsAsync(bool includeHidden = false, CancellationToken ct = default);
    Task<LunaChatDto> CreateChatAsync(string subject, CancellationToken ct = default);
    Task<LunaChatDto> UpdateChatAsync(Guid chatId, LunaUpdateChatRequest request, CancellationToken ct = default);
    Task<List<LunaChildMessageDto>> GetMessagesAsync(Guid chatId, CancellationToken ct = default);
    Task<LunaMessageResponse> SendMessageAsync(Guid chatId, string text, CancellationToken ct = default);
    Task<LunaMoreRequestDto> RequestMoreAsync(CancellationToken ct = default);
    Task SetThemeAsync(string theme, CancellationToken ct = default);
}

public sealed class LunaApiClient : ILunaApiClient, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private const string PipeName = "OpControlParental";
#if LUNA_DEV
    private const string DevServer = "http://100.109.25.49:8098";
#endif
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private string? _baseUrl;
    private string? _token;
    private DateTime _expiresUtc;
    private bool _dev = false;

    public bool IsDev => _dev;
    public string? BaseUrl => _baseUrl;
    public string? Token => _token;

    public async Task ConnectAsync(bool dev, CancellationToken ct = default)
    {
#if LUNA_DEV
        if (dev)
        {
            _dev = true;
            _baseUrl = DevServer;
            await RefreshDevTokenAsync(ct);
            return;
        }
#endif
        await RefreshPipeTokenAsync(ct);
    }

    private async Task RefreshPipeTokenAsync(CancellationToken ct)
    {
        using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000, ct);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new PinPipeRequest(LunaPipePolicy.TokenRequest), Json));
        var line = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(line)) throw new LunaApiException(HttpStatusCode.ServiceUnavailable, Loc.T("Luna.Chat.Offline"));
        var response = JsonSerializer.Deserialize<PinPipeResponse>(line, Json);
        if (response is not { Ok: true } || string.IsNullOrWhiteSpace(response.Token) ||
            string.IsNullOrWhiteSpace(response.ServerUrl) || response.ExpiresUtc is not { } expires)
            throw new LunaApiException(HttpStatusCode.Forbidden, Loc.T("Luna.Chat.Paused"));
        _token = response.Token;
        _expiresUtc = expires;
        _baseUrl = response.ServerUrl.TrimEnd('/');
    }

#if LUNA_DEV
    private async Task RefreshDevTokenAsync(CancellationToken ct)
    {
        using var response = await _http.PostAsync(DevServer + "/api/dev/luna-token", content: null, ct);
        if (!response.IsSuccessStatusCode)
            throw new LunaApiException(response.StatusCode, Loc.T("Luna.Chat.Paused"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;
        _token = root.GetProperty("token").GetString();
        _expiresUtc = root.TryGetProperty("expiresUtc", out var expires) && expires.TryGetDateTime(out var at)
            ? at
            : DateTime.UtcNow.AddMinutes(14);
        _baseUrl = DevServer;
    }
#endif

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (_baseUrl is null || string.IsNullOrWhiteSpace(_token) || DateTime.UtcNow >= _expiresUtc.AddMinutes(-2))
        {
#if LUNA_DEV
            if (_dev) { await RefreshDevTokenAsync(ct); return; }
#endif
            await RefreshPipeTokenAsync(ct);
        }
    }

    public Task<LunaStatusDto> GetStatusAsync(CancellationToken ct = default) => GetAsync<LunaStatusDto>("/api/luna/status", ct);

    public Task<List<LunaChatDto>> GetChatsAsync(bool includeHidden = false, CancellationToken ct = default) =>
        GetAsync<List<LunaChatDto>>("/api/luna/chats" + (includeHidden ? "?includeHidden=true" : ""), ct);

    public Task<LunaChatDto> CreateChatAsync(string subject, CancellationToken ct = default) =>
        SendAsync<LunaChatDto>(HttpMethod.Post, "/api/luna/chats", new LunaCreateChatRequest(subject), ct);

    public Task<LunaChatDto> UpdateChatAsync(Guid chatId, LunaUpdateChatRequest request, CancellationToken ct = default) =>
        SendAsync<LunaChatDto>(HttpMethod.Patch, "/api/luna/chats/" + chatId, request, ct);

    public Task<List<LunaChildMessageDto>> GetMessagesAsync(Guid chatId, CancellationToken ct = default) =>
        GetAsync<List<LunaChildMessageDto>>($"/api/luna/chats/{chatId}/messages", ct);

    public Task<LunaMessageResponse> SendMessageAsync(Guid chatId, string text, CancellationToken ct = default) =>
        SendAsync<LunaMessageResponse>(HttpMethod.Post, $"/api/luna/chats/{chatId}/messages", new LunaMessageRequest(text), ct);

    public Task<LunaMoreRequestDto> RequestMoreAsync(CancellationToken ct = default) =>
        SendAsync<LunaMoreRequestDto>(HttpMethod.Post, "/api/luna/more-requests", null, ct);

    public Task SetThemeAsync(string theme, CancellationToken ct = default) =>
        SendAsync<JsonElement>(HttpMethod.Post, "/api/luna/theme", new LunaThemeRequest(theme), ct);

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var response = await SendCoreAsync(HttpMethod.Get, path, null, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, path, body, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        await EnsureTokenAsync(ct);
        var response = await SendOnceAsync(method, path, body, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            await EnsureTokenAsync(ct);
            response = await SendOnceAsync(method, path, body, ct);
        }
        return response;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, _baseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            var message = text;
            try
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.TryGetProperty("error", out var error)) message = error.GetString() ?? text;
            }
            catch { }
            var status = response.StatusCode;
            response.Dispose();
            throw new LunaApiException(status, string.IsNullOrWhiteSpace(message) ? Loc.T("Luna.Chat.Offline") : message);
        }
        return response;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, Json, ct);
        if (value is null) throw new LunaApiException(HttpStatusCode.BadGateway, Loc.T("Luna.Chat.Offline"));
        return value;
    }

    public void Dispose() => _http.Dispose();
}
