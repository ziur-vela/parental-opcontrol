// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Contracts.Games;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services.Luna;

/// <summary>
/// Game results and the optional "Ask Luna" box. Results are small records for the
/// parent summary; Ask Luna uses the normal moderation path and the monthly token
/// counter, but never the daily chat-message limit.
/// </summary>
public sealed class LunaGameService
{
    private readonly ParentalDbContext _db;
    private readonly ILunaProviderFactory _providers;
    private readonly LunaSettingsService _settings;
    private readonly LunaUsageStore _usage;
    private readonly AlertService _alerts;
    private readonly ILogger<LunaGameService> _logger;

    public LunaGameService(ParentalDbContext db, ILunaProviderFactory providers, LunaSettingsService settings,
        LunaUsageStore usage, AlertService alerts, ILogger<LunaGameService> logger)
    {
        _db = db;
        _providers = providers;
        _settings = settings;
        _usage = usage;
        _alerts = alerts;
        _logger = logger;
    }

    public async Task<LunaGameResultDto> RecordResultAsync(LunaTokenContext ctx, LunaGameResultRequest request,
        CancellationToken ct = default)
    {
        var (device, child, settings) = await LoadAsync(ctx, ct);
        if (!settings.GamesEnabled)
            throw new InvalidOperationException(AppText.Get("Luna.Error.GamesDisabled"));

        var game = LunaGameCatalog.Normalize(request.Game);
        if (!LunaGameCatalog.IsValid(game))
            throw new InvalidOperationException(AppText.Get("Luna.Error.InvalidGame"));
        var mode = LunaGameModes.Normalize(request.Mode);
        if (!LunaGameModes.IsPlayable(mode))
            throw new InvalidOperationException(AppText.Get("Luna.Error.InvalidGameMode"));
        var level = NormalizeLevel(request.Level);
        var result = NormalizeResult(request.Result);

        var row = new LunaGameResult
        {
            FamilyId = ctx.FamilyId,
            ChildId = ctx.ChildId,
            DeviceId = device.Id,
            Game = game,
            Mode = mode,
            Level = level,
            Result = result,
            Score = Math.Clamp(request.Score, 0, 100_000),
            DurationSeconds = Math.Clamp(request.DurationSeconds, 0, 4 * 60 * 60),
            CreatedUtc = DateTime.UtcNow
        };
        _db.LunaGameResults.Add(row);
        await _db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<LunaGamesSummaryDto> SummaryAsync(Guid familyId, Guid childId, CancellationToken ct = default)
    {
        var childExists = await _db.Children.AsNoTracking()
            .AnyAsync(c => c.Id == childId && c.FamilyId == familyId, ct);
        if (!childExists) throw new InvalidOperationException(AppText.Get("Error.ChildNotFound"));

        var since = DateTime.UtcNow.AddDays(-7);
        var rows = await _db.LunaGameResults.AsNoTracking()
            .Where(r => r.FamilyId == familyId && r.ChildId == childId && r.CreatedUtc >= since)
            .Select(r => new { r.Result, r.DurationSeconds })
            .ToListAsync(ct);
        var wins = rows.Count(r => r.Result == "win");
        var losses = rows.Count(r => r.Result == "loss");
        var draws = rows.Count(r => r.Result == "draw");
        var minutes = (int)Math.Round(rows.Sum(r => r.DurationSeconds) / 60.0);
        return new LunaGamesSummaryDto(rows.Count, wins, losses, draws, minutes);
    }

    public async Task<List<LunaGameResultDto>> RecentAsync(Guid familyId, Guid childId, CancellationToken ct = default)
    {
        var childExists = await _db.Children.AsNoTracking()
            .AnyAsync(c => c.Id == childId && c.FamilyId == familyId, ct);
        if (!childExists) throw new InvalidOperationException(AppText.Get("Error.ChildNotFound"));
        var rows = await _db.LunaGameResults.AsNoTracking()
            .Where(r => r.FamilyId == familyId && r.ChildId == childId)
            .OrderByDescending(r => r.CreatedUtc)
            .Take(50)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<LunaGameAskResponse> AskAsync(LunaTokenContext ctx, LunaGameAskRequest request,
        CancellationToken ct = default)
    {
        var (device, child, settings) = await LoadAsync(ctx, ct);
        if (!settings.GamesEnabled)
            throw new InvalidOperationException(AppText.Get("Luna.Error.GamesDisabled"));

        var text = (request.Text ?? "").Trim();
        if (text.Length == 0) throw new InvalidOperationException(AppText.Get("Luna.Error.EmptyMessage"));
        if (text.Length > LunaPrompts.MaxInputCharacters)
            throw new InvalidOperationException(AppText.Get("Luna.Error.MessageTooLong", LunaPrompts.MaxInputCharacters));

        var game = LunaGameCatalog.Normalize(request.Game);
        if (!LunaGameCatalog.IsValid(game))
            throw new InvalidOperationException(AppText.Get("Luna.Error.InvalidGame"));
        var summary = (request.StateSummary ?? "").Trim();
        if (summary.Length > 2_000) summary = summary[..2_000];

        var language = await ChildLanguageAsync(ctx.FamilyId, device, ct);
        var familySettings = await _settings.EnsureAsync(ctx.FamilyId, ct);
        var today = LunaUsageStore.LocalDate(DateTime.UtcNow, LunaUsageStore.ResolveZone(device.TimeZoneId));
        var usage = await _usage.EnsureAsync(ctx.FamilyId, ctx.ChildId, today, ct);
        var month = await _usage.MonthlyUsageAsync(ctx.FamilyId, today, ct);
        if (familySettings.MonthlyTokenCap is int cap && cap > 0 && month.CapTokens >= cap)
            return new LunaGameAskResponse(AppText.Get("Luna.Child.MonthlyCap", language), true,
                AppText.Get("Luna.Child.MonthlyCap", language));

        var chat = await ResolveChatAsync(ctx, request.ChatId, language, ct);
        var now = DateTime.UtcNow;

        if (!_settings.IsReady(familySettings))
            return await SaveRefusalAsync(ctx, chat, text, AppText.Get("Luna.Child.Refusal", language), now, ct);

        ILunaModerator moderator;
        ILunaChatProvider chatProvider;
        LunaProviderConfig config;
        try
        {
            moderator = _providers.GetModerator(familySettings);
            chatProvider = _providers.GetChat(familySettings.ChatProvider);
            var key = _providers.ChatKey(familySettings);
            if (string.IsNullOrWhiteSpace(key)) throw new LunaProviderException("Luna.Error.ChatKeyMissing");
            config = new LunaProviderConfig(familySettings.ChatProvider, familySettings.ChatModel, key);
        }
        catch (LunaProviderException ex)
        {
            return await SaveRefusalAsync(ctx, chat, text, AppText.Get(ex.MessageKey, language), now, ct);
        }

        LunaModerationResult input;
        try
        {
            input = await moderator.ModerateAsync(text, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Luna game ask moderation failed for child {ChildId}", ctx.ChildId);
            return await SaveRefusalAsync(ctx, chat, text, AppText.Get("Luna.Child.Refusal", language), now, ct);
        }

        var classification = Classify(familySettings, input);
        if (classification.Urgent)
        {
            var urgentText = UrgentText(familySettings, language);
            await SaveTurnAsync(chat, text, urgentText, LunaMessageFlag.Urgent,
                LunaSafety.Describe(classification.Categories), ScoresJson(classification.Scores),
                LunaMessageFlag.None, null, 0, 0, now, ct);
            await _alerts.AddAsync(ctx.FamilyId, ctx.ChildId, ctx.DeviceId, "luna-urgent",
                new AlertContent("Alert.Luna.Urgent.Title", "Alert.Luna.Urgent.Detail", FirstName(child.Name)),
                TimeSpan.FromMinutes(30), ct, immediateEmail: true);
            return new LunaGameAskResponse(urgentText, true, null, chat.Id);
        }
        if (classification.Blocked)
        {
            var blockedText = AppText.Get("Luna.Child.Blocked", language);
            await SaveTurnAsync(chat, text, blockedText, LunaMessageFlag.Blocked,
                LunaSafety.Describe(classification.Categories), ScoresJson(classification.Scores),
                LunaMessageFlag.None, null, 0, 0, now, ct);
            await FlaggedAlertAsync(ctx, child.Name, chat.Id, false, ct);
            return new LunaGameAskResponse(blockedText, true, null, chat.Id);
        }

        var system = LunaPrompts.BuildGame(LunaGameCatalog.DisplayName(game), summary,
            LunaPrompts.AgeFrom(child.DateOfBirth, DateTime.UtcNow), language);
        LunaChatResult result;
        try
        {
            result = await chatProvider.CompleteAsync(config,
                new LunaChatRequest(system, [new LunaChatTurn("user", text)], LunaLimits.MaxOutputTokens), ct);
        }
        catch (LunaProviderException ex)
        {
            return await SaveRefusalAsync(ctx, chat, text, AppText.Get(ex.MessageKey, language), now, ct);
        }

        usage.PromptTokens += result.PromptTokens;
        usage.CompletionTokens += result.CompletionTokens;
        usage.UpdatedUtc = now;

        var (firstClean, markerKinds) = LunaSafety.StripConcernMarkers(result.Text);
        var (clean, _) = LunaSafety.StripDeclinedMarkers(firstClean);
        if (clean.Length == 0) clean = AppText.Get("Luna.Child.ReplyFallback", language);

        LunaModerationResult output;
        try
        {
            output = await moderator.ModerateAsync(clean, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Luna game ask output moderation failed for child {ChildId}", ctx.ChildId);
            return await SaveRefusalAsync(ctx, chat, text, AppText.Get("Luna.Child.Refusal", language), now, ct);
        }

        var outputClass = Classify(familySettings, output);
        if (markerKinds.Count > 0 || outputClass.Urgent)
        {
            var urgentText = UrgentText(familySettings, language);
            var categories = markerKinds.Count > 0
                ? markerKinds.Select(k => "marker:" + k).ToList()
                : outputClass.Categories;
            await SaveTurnAsync(chat, text, urgentText, LunaMessageFlag.Urgent,
                LunaSafety.Describe(categories), ScoresJson(outputClass.Scores),
                LunaMessageFlag.Urgent, LunaSafety.Describe(categories),
                result.PromptTokens, result.CompletionTokens, now, ct);
            await _alerts.AddAsync(ctx.FamilyId, ctx.ChildId, ctx.DeviceId, "luna-urgent",
                new AlertContent("Alert.Luna.Urgent.Title", "Alert.Luna.Urgent.Detail", FirstName(child.Name)),
                TimeSpan.FromMinutes(30), ct, immediateEmail: true);
            return new LunaGameAskResponse(urgentText, true, null, chat.Id);
        }

        var lunaFlag = LunaMessageFlag.None;
        string? lunaCategories = null;
        if (outputClass.Blocked)
        {
            clean = AppText.Get("Luna.Child.ReplyFallback", language);
            lunaFlag = LunaMessageFlag.ReplyBlocked;
            lunaCategories = LunaSafety.Describe(outputClass.Categories);
        }

        // Counts toward the monthly token cap and cost, never toward the daily chat limit.
        await SaveTurnAsync(chat, text, clean, LunaMessageFlag.None, null, null,
            lunaFlag, lunaCategories, result.PromptTokens, result.CompletionTokens, now, ct);
        if (lunaFlag == LunaMessageFlag.ReplyBlocked)
            await FlaggedAlertAsync(ctx, child.Name, chat.Id, true, ct);
        return new LunaGameAskResponse(clean, false, null, chat.Id);
    }

    /// <summary>One chat per game session; the first Ask Luna creates it.</summary>
    private async Task<LunaChat> ResolveChatAsync(LunaTokenContext ctx, Guid? chatId, string language,
        CancellationToken ct)
    {
        if (chatId is { } id)
        {
            var existing = await _db.LunaChats.FirstOrDefaultAsync(
                c => c.Id == id && c.FamilyId == ctx.FamilyId && c.ChildId == ctx.ChildId, ct);
            if (existing is not null) return existing;
        }

        var now = DateTime.UtcNow;
        var chat = new LunaChat
        {
            FamilyId = ctx.FamilyId,
            ChildId = ctx.ChildId,
            DeviceId = ctx.DeviceId,
            Subject = LunaSubjects.Games,
            Title = AppText.Get("Games.AskChatTitle", language),
            CreatedUtc = now,
            LastMessageUtc = now,
            UpdatedUtc = now
        };
        _db.LunaChats.Add(chat);
        return chat;
    }

    private async Task SaveTurnAsync(LunaChat chat, string question, string answer,
        LunaMessageFlag childFlag, string? childCategories, string? childScores,
        LunaMessageFlag lunaFlag, string? lunaCategories,
        int promptTokens, int completionTokens, DateTime now, CancellationToken ct)
    {
        var childCreated = NextMessageUtc(chat, now);
        _db.LunaMessages.Add(new LunaMessage
        {
            FamilyId = chat.FamilyId,
            ChildId = chat.ChildId,
            ChatId = chat.Id,
            Role = LunaChatRoles.Child,
            Text = question,
            ShownText = question,
            CreatedUtc = childCreated,
            Flag = childFlag,
            FlagCategories = childCategories,
            FlagScores = childScores
        });
        var lunaCreated = childCreated.AddTicks(1);
        _db.LunaMessages.Add(new LunaMessage
        {
            FamilyId = chat.FamilyId,
            ChildId = chat.ChildId,
            ChatId = chat.Id,
            Role = LunaChatRoles.Luna,
            Text = answer,
            ShownText = answer,
            CreatedUtc = lunaCreated,
            Flag = lunaFlag,
            FlagCategories = lunaCategories,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens
        });
        chat.LastMessageUtc = lunaCreated;
        chat.UpdatedUtc = lunaCreated;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<LunaGameAskResponse> SaveRefusalAsync(LunaTokenContext ctx, LunaChat chat, string question,
        string notice, DateTime now, CancellationToken ct)
    {
        await SaveTurnAsync(chat, question, notice, LunaMessageFlag.None, null, null,
            LunaMessageFlag.None, null, 0, 0, now, ct);
        return new LunaGameAskResponse(notice, true, null, chat.Id);
    }

    private static DateTime NextMessageUtc(LunaChat chat, DateTime now) =>
        now > chat.LastMessageUtc ? now : chat.LastMessageUtc.AddTicks(1);

    private static LunaClassification Classify(LunaFamilySettings settings, LunaModerationResult result) =>
        settings.Setup == LunaSetup.A || settings.ChatProvider == LunaChatProviders.OpenRouter
            ? LunaSafety.ClassifyCategories(result.Categories, result.Flagged)
            : LunaSafety.ClassifyOpenAi(result.Scores);

    private static string ScoresJson(IReadOnlyDictionary<string, double> scores) =>
        JsonSerializer.Serialize(scores);

    private async Task FlaggedAlertAsync(LunaTokenContext ctx, string childName, Guid chatId, bool replyBlocked,
        CancellationToken ct)
    {
        await _alerts.AddAsync(ctx.FamilyId, ctx.ChildId, ctx.DeviceId, "luna-flagged",
            replyBlocked
                ? new AlertContent("Alert.Luna.ReplyBlocked.Title", "Alert.Luna.ReplyBlocked.Detail", FirstName(childName), chatId.ToString())
                : new AlertContent("Alert.Luna.Flagged.Title", "Alert.Luna.Flagged.Detail", FirstName(childName), chatId.ToString()),
            TimeSpan.FromMinutes(30), ct);
    }

    private static string UrgentText(LunaFamilySettings settings, string language) =>
        AppText.Get("Luna.Child.Urgent", language) + "\n\n" + HelplineFor(settings, language);

    private static string HelplineFor(LunaFamilySettings settings, string language)
    {
        var value = (settings.HelplineText ?? "").Trim();
        var defaultEn = AppText.Get(LunaSettingsService.DefaultHelplineKey, AppCulture.English);
        if (value.Length == 0 || string.Equals(value, defaultEn, StringComparison.Ordinal))
            return AppText.Get(LunaSettingsService.DefaultHelplineKey, language);
        return value;
    }

    private static string NormalizeLevel(string? level) =>
        level?.Trim().ToLowerInvariant() switch
        {
            "medium" => "medium",
            "hard" => "hard",
            _ => "easy"
        };

    private static string NormalizeResult(string? result) =>
        result?.Trim().ToLowerInvariant() switch
        {
            "win" => "win",
            "loss" => "loss",
            _ => "draw"
        };

    private static string FirstName(string name)
    {
        var trimmed = (name ?? "").Trim();
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }

    private static LunaGameResultDto ToDto(LunaGameResult row) =>
        new(row.Id, row.Game, row.Mode, row.Level, row.Result, row.Score, row.DurationSeconds, row.CreatedUtc);

    private async Task<(Device Device, Child Child, ChildLunaSettings Settings)> LoadAsync(LunaTokenContext ctx,
        CancellationToken ct)
    {
        var device = await _db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == ctx.DeviceId && d.FamilyId == ctx.FamilyId && !d.IsRevoked, ct)
            ?? throw new InvalidOperationException(AppText.Get("Error.DeviceNotFound"));
        var child = await _db.Children.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == ctx.ChildId && c.FamilyId == ctx.FamilyId, ct)
            ?? throw new InvalidOperationException(AppText.Get("Error.ChildNotFound"));
        var settings = await _db.ChildLunaSettings.FirstOrDefaultAsync(s => s.ChildId == ctx.ChildId, ct);
        if (settings is null)
        {
            settings = new ChildLunaSettings { FamilyId = ctx.FamilyId, ChildId = ctx.ChildId };
            _db.ChildLunaSettings.Add(settings);
            await _db.SaveChangesAsync(ct);
        }
        if (!settings.Enabled) throw new InvalidOperationException(AppText.Get("Luna.Error.Disabled"));
        return (device, child, settings);
    }

    private async Task<string> ChildLanguageAsync(Guid familyId, Device device, CancellationToken ct)
    {
        var family = await _db.Families.AsNoTracking().Where(f => f.Id == familyId)
            .Select(f => f.DefaultLanguage).FirstOrDefaultAsync(ct);
        return AppCulture.Normalize(device.ClientLanguage ?? family);
    }
}
