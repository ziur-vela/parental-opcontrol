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
    private readonly AlertService? _alerts;

    public LunaSettingsService(ParentalDbContext db, ILunaProviderFactory providers, ILunaZdrCatalog zdr,
        ILogger<LunaSettingsService> logger, AlertService? alerts = null)
    {
        _db = db;
        _providers = providers;
        _zdr = zdr;
        _logger = logger;
        _alerts = alerts;
    }

    public async Task<LunaFamilySettings> EnsureAsync(Guid familyId, CancellationToken ct = default)
    {
        var settings = await _db.LunaFamilySettings.FirstOrDefaultAsync(s => s.FamilyId == familyId, ct);
        if (settings is not null) return settings;

        settings = new LunaFamilySettings
        {
            FamilyId = familyId,
            Setup = LunaSetup.A,
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
        var moderationUsesChatKey = settings.Setup == LunaSetup.A || settings.ChatProvider == LunaChatProviders.OpenAi;
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
        if (setup == LunaSetup.A)
        {
            if (provider != LunaChatProviders.OpenRouter) return "Luna.Error.SetupAProvider";
            return null;
        }

        if (provider == LunaChatProviders.OpenRouter) return "Luna.Error.SetupBProvider";
        if (!LunaChatProviders.All.Contains(provider)) return "Luna.Error.ProviderDown";
        if (string.IsNullOrWhiteSpace(moderationKey) && provider != LunaChatProviders.OpenAi)
            return "Luna.Error.ModerationKeyMissing";
        return null;
    }

    /// <summary>
    /// The hard rule: every key the chosen setup needs is saved and the last test passed.
    /// </summary>
    public bool IsReady(LunaFamilySettings settings)
    {
        if (settings.LastTestOk != true) return false;
        var chatKey = _providers.ChatKey(settings);
        var moderationKey = _providers.ModerationKey(settings);
        return Validate(settings, settings.Setup, settings.ChatProvider, settings.ChatModel, chatKey, moderationKey) is null;
    }

    public async Task<bool> IsReadyAsync(Guid familyId, CancellationToken ct = default)
    {
        var settings = await _db.LunaFamilySettings.AsNoTracking().FirstOrDefaultAsync(s => s.FamilyId == familyId, ct);
        return settings is not null && IsReady(settings);
    }

    public async Task<bool> SetupValidAsync(Guid familyId, CancellationToken ct = default) =>
        await IsReadyAsync(familyId, ct);

    /// <summary>
    /// A key failed at runtime (or a test failed). Pause the family setup and alert parents once.
    /// A later successful Test connection sets <see cref="LunaFamilySettings.LastTestOk"/> back to true.
    /// </summary>
    public async Task MarkBrokenAsync(Guid familyId, string errorKey, CancellationToken ct = default)
    {
        var settings = await EnsureAsync(familyId, ct);
        settings.LastTestUtc = DateTime.UtcNow;
        settings.LastTestOk = false;
        var message = AppText.Get(errorKey, AppCulture.English);
        settings.LastTestError = message.Length > 500 ? message[..500] : message;
        settings.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await RaiseSetupBrokenAsync(familyId, ct);
    }

    private async Task RaiseSetupBrokenAsync(Guid familyId, CancellationToken ct)
    {
        if (_alerts is null) return;
        try
        {
            var child = await _db.Children.AsNoTracking()
                .Where(c => c.FamilyId == familyId)
                .OrderBy(c => c.CreatedUtc)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(ct);
            if (child is null) return;
            await _alerts.AddAsync(familyId, child.Id, null, "luna-setup-broken",
                new AlertContent("Alert.Luna.SetupBroken.Title", "Alert.Luna.SetupBroken.Detail"),
                TimeSpan.FromHours(24), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not raise the Luna setup-broken alert for family {FamilyId}", familyId);
        }
    }

    public async Task<LunaTestResult> TestAsync(Guid familyId, CancellationToken ct = default)
    {
        var settings = await EnsureAsync(familyId, ct);
        var chatKey = _providers.ChatKey(settings);
        var moderationKey = _providers.ModerationKey(settings);
        var error = Validate(settings, settings.Setup, settings.ChatProvider, settings.ChatModel, chatKey, moderationKey);
        if (error is not null)
            return await FailAsync(settings, error, ct);

        var zdrOk = true;
        if (settings.Setup == LunaSetup.A || settings.ChatProvider == LunaChatProviders.OpenRouter)
        {
            var missing = await _zdr.MissingModelAsync(settings.ChatModel, ct);
            if (missing is not null)
            {
                zdrOk = false;
                return missing.StartsWith("ZDR list", StringComparison.Ordinal)
                    ? await FailAsync(settings, "Luna.Error.ProviderDown", ct, zdrOk: false)
                    : await FailAsync(settings, "Luna.Error.ZdrMissing", ct, missing, zdrOk: false);
            }
        }

        var chatOk = false;
        var safetyOk = false;
        try
        {
            var chat = _providers.GetChat(settings.ChatProvider);
            await chat.CompleteAsync(
                new LunaProviderConfig(settings.ChatProvider, settings.ChatModel, chatKey!),
                new LunaChatRequest(
                    "You are a setup health check. Reply with the single word OK.",
                    [new LunaChatTurn("user", "Reply with OK.")], 8),
                ct);
            chatOk = true;

            var moderator = _providers.GetModerator(settings);
            await moderator.ModerateAsync("This is a Luna setup test.", ct);
            safetyOk = true;

            settings.LastTestUtc = DateTime.UtcNow;
            settings.LastTestOk = true;
            settings.LastTestError = null;
            settings.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Luna setup test passed for family {FamilyId}", familyId);
            return new LunaTestResult(true, null, true, true, zdrOk);
        }
        catch (LunaProviderException ex)
        {
            _logger.LogWarning("Luna setup test failed for family {FamilyId}: {Key}", familyId, ex.MessageKey);
            return await FailAsync(settings, ex.MessageKey, ct, chatOk: chatOk, safetyOk: safetyOk, zdrOk: zdrOk);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Luna setup test failed unexpectedly for family {FamilyId}", familyId);
            return await FailAsync(settings, "Luna.Error.ProviderDown", ct, chatOk: chatOk, safetyOk: safetyOk, zdrOk: zdrOk);
        }
    }

    private async Task<LunaTestResult> FailAsync(LunaFamilySettings settings, string key, CancellationToken ct,
        string? arg = null, bool chatOk = false, bool safetyOk = false, bool zdrOk = false)
    {
        var message = arg is null ? AppText.Get(key, AppCulture.English) : AppText.Get(key, AppCulture.English, arg);
        settings.LastTestUtc = DateTime.UtcNow;
        settings.LastTestOk = false;
        settings.LastTestError = message.Length > 500 ? message[..500] : message;
        settings.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await RaiseSetupBrokenAsync(settings.FamilyId, ct);
        return new LunaTestResult(false, arg is null ? AppText.Get(key) : AppText.Get(key, arg), chatOk, safetyOk, zdrOk);
    }

}
