// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services.Luna.Providers;

namespace OpControl.Parental.Server.Services.Luna;

public interface ILunaZdrCatalog
{
    /// <summary>Returns null when both the chat model and Llama Guard 4 have a ZDR endpoint.</summary>
    Task<string?> MissingModelAsync(string chatModel, CancellationToken ct);
}

public sealed class OpenRouterZdrCatalog : ILunaZdrCatalog
{
    public const string Endpoint = "https://openrouter.ai/api/v1/endpoints/zdr";
    private readonly IHttpClientFactory _http;

    public OpenRouterZdrCatalog(IHttpClientFactory http) => _http = http;

    public async Task<string?> MissingModelAsync(string chatModel, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var http = _http.CreateClient("luna");
            using var response = await http.GetAsync(Endpoint, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return "ZDR list unavailable";
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (json.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("model_id", out var id) && id.GetString() is { } value)
                        models.Add(value);
                }
            }
            if (!models.Contains(chatModel)) return chatModel;
            if (!models.Contains(LlamaGuardModerator.Model)) return LlamaGuardModerator.Model;
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return "ZDR list timeout";
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return "ZDR list error";
        }
    }
}

public sealed class LunaSettingsService
{
    public const string DefaultHelplineKey = "Luna.Helpline.Default";

    private readonly ParentalDbContext _db;
    private readonly ILunaProviderFactory _providers;
    private readonly ILunaZdrCatalog _zdr;
    private readonly ILogger<LunaSettingsService> _logger;

    public LunaSettingsService(ParentalDbContext db, ILunaProviderFactory providers, ILunaZdrCatalog zdr,
        ILogger<LunaSettingsService> logger)
    {
        _db = db;
        _providers = providers;
        _zdr = zdr;
        _logger = logger;
    }

    public async Task<LunaFamilySettings> EnsureAsync(Guid familyId, CancellationToken ct = default)
    {
        var settings = await _db.LunaFamilySettings.FirstOrDefaultAsync(s => s.FamilyId == familyId, ct);
        if (settings is not null) return settings;

        settings = new LunaFamilySettings
        {
            FamilyId = familyId,
            Setup = LunaSetup.B,
            ChatProvider = LunaChatProviders.OpenRouter,
            ChatModel = "",
            HelplineText = AppText.Get(DefaultHelplineKey, AppCulture.English)
        };
        _db.LunaFamilySettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<LunaSettingsDto> GetAsync(Guid familyId, CancellationToken ct = default)
    {
        var settings = await EnsureAsync(familyId, ct);
        return ToDto(settings);
    }

    public LunaSettingsDto ToDto(LunaFamilySettings settings)
    {
        var chatKey = _providers.ChatKey(settings);
        var moderationKey = _providers.ModerationKey(settings);
        var moderationUsesChatKey = settings.Setup == LunaSetup.B || settings.ChatProvider == LunaChatProviders.OpenAi;
        return new LunaSettingsDto(
            settings.Setup == LunaSetup.A ? "A" : "B",
            settings.ChatProvider,
            settings.ChatModel,
            !string.IsNullOrWhiteSpace(settings.ChatKeyProtected),
            LunaProviderFactory.Last4(chatKey),
            moderationUsesChatKey
                ? !string.IsNullOrWhiteSpace(settings.ChatKeyProtected)
                : !string.IsNullOrWhiteSpace(settings.ModerationKeyProtected),
            LunaProviderFactory.Last4(moderationUsesChatKey ? chatKey : moderationKey),
            settings.LastTestUtc,
            settings.LastTestOk,
            settings.LastTestError,
            settings.MonthlyTokenCap,
            settings.HelplineText);
    }

    public async Task<LunaSettingsDto> SaveAsync(Guid familyId, LunaSettingsRequest request, CancellationToken ct = default)
    {
        var settings = await EnsureAsync(familyId, ct);
        var setup = string.Equals(request.Setup, "A", StringComparison.OrdinalIgnoreCase) ? LunaSetup.A : LunaSetup.B;
        var provider = LunaChatProviders.Normalize(request.ChatProvider ?? "");
        if (!LunaChatProviders.IsValid(provider))
            throw new InvalidOperationException(AppText.Get("Luna.Error.ProviderDown"));

        var model = (request.ChatModel ?? "").Trim();
        if (model.Length > 160) model = model[..160];
        if (model.Length == 0)
            throw new InvalidOperationException(AppText.Get("Luna.Error.ModelMissing"));

        var connectionChanged = settings.Setup != setup
            || !string.Equals(settings.ChatProvider, provider, StringComparison.Ordinal)
            || !string.Equals(settings.ChatModel, model, StringComparison.Ordinal);

        var chatKey = _providers.ChatKey(settings);
        if (!string.IsNullOrWhiteSpace(request.ChatKey))
        {
            settings.ChatKeyProtected = _providers.Protect(request.ChatKey);
            chatKey = request.ChatKey.Trim();
            connectionChanged = true;
        }

        var moderationKey = _providers.ModerationKey(settings);
        if (!string.IsNullOrWhiteSpace(request.ModerationKey))
        {
            settings.ModerationKeyProtected = _providers.Protect(request.ModerationKey);
            moderationKey = request.ModerationKey.Trim();
            connectionChanged = true;
        }

        var error = Validate(settings, setup, provider, model, chatKey, moderationKey);
        if (error is not null) throw new InvalidOperationException(AppText.Get(error));

        settings.Setup = setup;
        settings.ChatProvider = provider;
        settings.ChatModel = model;
        settings.MonthlyTokenCap = request.MonthlyTokenCap is > 0 ? request.MonthlyTokenCap : null;
        settings.HelplineText = string.IsNullOrWhiteSpace(request.HelplineText)
            ? AppText.Get(DefaultHelplineKey, AppCulture.English)
            : request.HelplineText.Trim()[..Math.Min(request.HelplineText.Trim().Length, 500)];
        if (connectionChanged)
        {
            settings.LastTestUtc = null;
            settings.LastTestOk = null;
            settings.LastTestError = null;
        }
        settings.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    public static string? Validate(LunaFamilySettings settings, LunaSetup setup, string provider, string model,
        string? chatKey, string? moderationKey)
    {
        if (string.IsNullOrWhiteSpace(model)) return "Luna.Error.ModelMissing";
        if (string.IsNullOrWhiteSpace(chatKey)) return "Luna.Error.ChatKeyMissing";
        if (setup == LunaSetup.B)
        {
            if (provider != LunaChatProviders.OpenRouter) return "Luna.Error.SetupBProvider";
            return null;
        }

        if (provider == LunaChatProviders.OpenRouter) return "Luna.Error.SetupAProvider";
        if (!LunaChatProviders.All.Contains(provider)) return "Luna.Error.ProviderDown";
        if (string.IsNullOrWhiteSpace(moderationKey) && provider != LunaChatProviders.OpenAi)
            return "Luna.Error.ModerationKeyMissing";
        return null;
    }

    public async Task<bool> SetupValidAsync(Guid familyId, CancellationToken ct = default)
    {
        var settings = await _db.LunaFamilySettings.AsNoTracking().FirstOrDefaultAsync(s => s.FamilyId == familyId, ct);
        return settings is { LastTestOk: true };
    }

    public async Task<LunaTestResult> TestAsync(Guid familyId, CancellationToken ct = default)
    {
        var settings = await EnsureAsync(familyId, ct);
        var chatKey = _providers.ChatKey(settings);
        var moderationKey = _providers.ModerationKey(settings);
        var error = Validate(settings, settings.Setup, settings.ChatProvider, settings.ChatModel, chatKey, moderationKey);
        if (error is not null)
            return await FailAsync(settings, error, ct);

        if (settings.Setup == LunaSetup.B || settings.ChatProvider == LunaChatProviders.OpenRouter)
        {
            var missing = await _zdr.MissingModelAsync(settings.ChatModel, ct);
            if (missing is not null)
                return missing.StartsWith("ZDR list", StringComparison.Ordinal)
                    ? await FailAsync(settings, "Luna.Error.ProviderDown", ct)
                    : await FailAsync(settings, "Luna.Error.ZdrMissing", ct, missing);
        }

        try
        {
            var chat = _providers.GetChat(settings.ChatProvider);
            await chat.CompleteAsync(
                new LunaProviderConfig(settings.ChatProvider, settings.ChatModel, chatKey!),
                new LunaChatRequest(
                    "You are a setup health check. Reply with the single word OK.",
                    [new LunaChatTurn("user", "Reply with OK.")], 8),
                ct);

            var moderator = _providers.GetModerator(settings);
            await moderator.ModerateAsync("This is a Luna setup test.", ct);

            settings.LastTestUtc = DateTime.UtcNow;
            settings.LastTestOk = true;
            settings.LastTestError = null;
            settings.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Luna setup test passed for family {FamilyId}", familyId);
            return new LunaTestResult(true, null);
        }
        catch (LunaProviderException ex)
        {
            _logger.LogWarning("Luna setup test failed for family {FamilyId}: {Key}", familyId, ex.MessageKey);
            return await FailAsync(settings, ex.MessageKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Luna setup test failed unexpectedly for family {FamilyId}", familyId);
            return await FailAsync(settings, "Luna.Error.ProviderDown", ct);
        }
    }

    private async Task<LunaTestResult> FailAsync(LunaFamilySettings settings, string key, CancellationToken ct, string? arg = null)
    {
        var message = arg is null ? AppText.Get(key, AppCulture.English) : AppText.Get(key, AppCulture.English, arg);
        settings.LastTestUtc = DateTime.UtcNow;
        settings.LastTestOk = false;
        settings.LastTestError = message.Length > 500 ? message[..500] : message;
        settings.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new LunaTestResult(false, arg is null ? AppText.Get(key) : AppText.Get(key, arg));
    }

}
