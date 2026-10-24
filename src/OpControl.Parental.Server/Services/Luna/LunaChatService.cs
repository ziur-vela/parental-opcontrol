// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services.Luna;

public sealed class LunaChatService
{
    private readonly ParentalDbContext _db;
    private readonly ILunaProviderFactory _providers;
    private readonly LunaSettingsService _settings;
    private readonly LunaUsageStore _usage;
    private readonly LockService _locks;
    private readonly AlertService _alerts;
    private readonly ILogger<LunaChatService> _logger;

    public LunaChatService(ParentalDbContext db, ILunaProviderFactory providers, LunaSettingsService settings,
        LunaUsageStore usage, LockService locks, AlertService alerts, ILogger<LunaChatService> logger)
    {
        _db = db;
        _providers = providers;
        _settings = settings;
        _usage = usage;
        _locks = locks;
        _alerts = alerts;
        _logger = logger;
    }

    public async Task<LunaStatusDto> GetStatusAsync(LunaTokenContext ctx, CancellationToken ct = default)
    {
        var (device, child, settings, day) = await LoadAsync(ctx, ct);
        var now = DateTime.UtcNow;
        var tz = LunaUsageStore.ResolveZone(device.TimeZoneId);
        var usage = await _usage.EnsureAsync(ctx.FamilyId, ctx.ChildId, LunaUsageStore.LocalDate(now, tz), ct);
        var familySettings = await _settings.EnsureAsync(ctx.FamilyId, ct);
        var language = await ChildLanguageAsync(ctx.FamilyId, device, ct);
        var left = LunaUsageStore.MessagesLeft(settings, usage);
        var lockState = await _locks.GetStateAsync(ctx.FamilyId, ctx.ChildId, device.TimeZoneId, ct);
        var (notice, warning) = StateText(child, settings, usage, left, lockState.IsLocked, language, now);
        return new LunaStatusDto(settings.Enabled, FirstName(child.Name), settings.Theme, left,
            usage.CooldownUntilUtc, left <= 0, language, HelplineFor(familySettings, language), notice, warning);
    }

    public async Task<List<LunaChatDto>> ListChatsAsync(LunaTokenContext ctx, CancellationToken ct = default)
    {
        await LoadAsync(ctx, ct);
        var chats = await _db.LunaChats.AsNoTracking()
            .Where(c => c.FamilyId == ctx.FamilyId && c.ChildId == ctx.ChildId && !c.HiddenByChild)
            .OrderByDescending(c => c.LastMessageUtc)
            .Take(100)
            .ToListAsync(ct);
        return chats.Select(ToDto).ToList();
    }

    public async Task<LunaChatDto> CreateChatAsync(LunaTokenContext ctx, string subject, CancellationToken ct = default)
    {
        await LoadAsync(ctx, ct);
        if (!LunaSubjects.IsValid(subject))
            throw new InvalidOperationException(AppText.Get("Luna.Error.InvalidSubject"));
        var now = DateTime.UtcNow;
        var chat = new LunaChat
        {
            FamilyId = ctx.FamilyId,
            ChildId = ctx.ChildId,
            DeviceId = ctx.DeviceId,
            Subject = LunaSubjects.Normalize(subject),
            Title = "",
            CreatedUtc = now,
            LastMessageUtc = now,
            UpdatedUtc = now
        };
        _db.LunaChats.Add(chat);
        await _db.SaveChangesAsync(ct);
        return ToDto(chat);
    }

    public async Task<LunaChatDto> UpdateChatAsync(LunaTokenContext ctx, Guid chatId, LunaUpdateChatRequest request,
        CancellationToken ct = default)
    {
        await LoadAsync(ctx, ct);
        var chat = await ChatAsync(ctx, chatId, ct);
        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (title.Length > 80) title = title[..80];
            if (title.Length > 0)
            {
                chat.Title = title;
                chat.TitleSetByChild = true;
            }
        }
        if (request.Hidden is { } hidden) chat.HiddenByChild = hidden;
        chat.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(chat);
    }

    public async Task<List<LunaChildMessageDto>> GetMessagesAsync(LunaTokenContext ctx, Guid chatId,
        CancellationToken ct = default)
    {
        await LoadAsync(ctx, ct);
        await ChatAsync(ctx, chatId, ct);
        var rows = await _db.LunaMessages.AsNoTracking()
            .Where(m => m.ChatId == chatId && m.FamilyId == ctx.FamilyId && m.ChildId == ctx.ChildId)
            .OrderBy(m => m.CreatedUtc)
            .Take(500)
            .ToListAsync(ct);
        return rows.Select(m => new LunaChildMessageDto(m.Id, m.ChatId, m.Role,
            m.Role == LunaChatRoles.Child ? m.Text : m.ShownText, m.CreatedUtc, m.Flag.ToString())).ToList();
    }

    public async Task<LunaMessageResponse> SendAsync(LunaTokenContext ctx, Guid chatId, string? rawText,
        CancellationToken ct = default)
    {
        var (device, child, childSettings, day) = await LoadAsync(ctx, ct);
        var chat = await ChatAsync(ctx, chatId, ct);
        var now = DateTime.UtcNow;
        var tz = LunaUsageStore.ResolveZone(device.TimeZoneId);
        var usage = await _usage.EnsureAsync(ctx.FamilyId, ctx.ChildId, LunaUsageStore.LocalDate(now, tz), ct);
        var familySettings = await _settings.EnsureAsync(ctx.FamilyId, ct);
        var language = await ChildLanguageAsync(ctx.FamilyId, device, ct);

        var text = (rawText ?? "").Trim();
        if (text.Length == 0)
            throw new InvalidOperationException(AppText.Get("Luna.Error.EmptyMessage"));
        if (text.Length > LunaPrompts.MaxInputCharacters)
            throw new InvalidOperationException(AppText.Get("Luna.Error.MessageTooLong", LunaPrompts.MaxInputCharacters));

        var state = await _locks.GetStateAsync(ctx.FamilyId, ctx.ChildId, device.TimeZoneId, ct);
        if (state.IsLocked)
            return Resting(chat, childSettings, usage, language, "Luna.Child.Resting");

        if (usage.CooldownUntilUtc is { } until && until > now)
            return Resting(chat, childSettings, usage, language, "Luna.Child.Cooldown", until: until);

        var limit = LunaUsageStore.MessageLimit(childSettings, usage);
        var leftBefore = Math.Max(0, limit - usage.Messages);
        if (leftBefore <= 0)
        {
            return new LunaMessageResponse(chatId, nullMessage(), null, 0, true, false, false,
                AppText.Get("Luna.Child.Limit", language, FirstName(child.Name)), null);
        }

        var month = await _usage.MonthlyTokensAsync(ctx.FamilyId, usage.Date, ct);
        var monthUsed = month.PromptTokens + month.CompletionTokens;
        if (familySettings.MonthlyTokenCap is int cap && cap > 0 && monthUsed >= cap)
        {
            await MonthlyCapAlertAsync(ctx.FamilyId, ctx.ChildId, child.Name, ct);
            return new LunaMessageResponse(chatId, nullMessage(), null,
                LunaUsageStore.MessagesLeft(childSettings, usage), false, false, false,
                AppText.Get("Luna.Child.MonthlyCap", language), null);
        }

        if (!_settings.IsReady(familySettings))
        {
            return await RefusalAsync(chat, usage, child, childSettings, language, text,
                AppText.Get("Luna.Child.Refusal", language), now, ct);
        }

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
            return await RefusalAsync(chat, usage, child, childSettings, language, text,
                AppText.Get(ex.MessageKey, language), now, ct);
        }

        // 3. Input moderation. Any error or timeout fails closed: the chat model is never called.
        LunaModerationResult inputModeration;
        try
        {
            inputModeration = await moderator.ModerateAsync(text, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Luna input moderation failed for child {ChildId}", ctx.ChildId);
            await MarkSetupBrokenAsync(familySettings.FamilyId, ex, ct);
            return await RefusalAsync(chat, usage, child, childSettings, language, text,
                AppText.Get("Luna.Child.Refusal", language), now, ct);
        }

        var input = Classify(familySettings, inputModeration);
        if (input.Urgent)
        {
            var urgentText = UrgentText(familySettings, language);
            var childMessage = await SaveExchangeAsync(chat, usage, child, text, null,
                LunaMessageFlag.Urgent, LunaSafety.Describe(input.Categories), ScoresJson(input.Scores),
                0, 0, now, ct);
            var lunaMessage = await AddLunaAsync(chat, urgentText, urgentText, LunaMessageFlag.None, null, null, 0, 0, now, ct);
            usage.Messages++;
            usage.UpdatedUtc = now;
            await _db.SaveChangesAsync(ct);
            await UrgentAlertAsync(ctx.FamilyId, ctx.ChildId, child.Name, chat.Id, ct);
            await MaybeTitleAsync(chat, child, text, urgentText, familySettings, moderator, chatProvider, config,
                language, true, ct);
            return new LunaMessageResponse(chatId, ToChild(childMessage), ToChild(lunaMessage),
                LunaUsageStore.MessagesLeft(childSettings, usage), false, false, true, null, WarningText(childSettings, usage, language));
        }

        if (input.Blocked)
        {
            var blockedText = AppText.Get("Luna.Child.Blocked", language);
            var childMessage = await SaveExchangeAsync(chat, usage, child, text, null,
                LunaMessageFlag.Blocked, LunaSafety.Describe(input.Categories), ScoresJson(input.Scores),
                0, 0, now, ct);
            var lunaMessage = await AddLunaAsync(chat, blockedText, blockedText, LunaMessageFlag.None, null, null, 0, 0, now, ct);
            usage.Messages++;
            usage.UpdatedUtc = now;
            var blockedCount = await _db.LunaMessages.AsNoTracking()
                .CountAsync(m => m.ChatId == chatId && m.Role == LunaChatRoles.Child && m.Flag == LunaMessageFlag.Blocked
                    && m.CreatedUtc >= now - LunaLimits.CooldownWindow, ct) + 1;
            if (blockedCount >= LunaLimits.CooldownBlockedCount)
            {
                usage.CooldownUntilUtc = now + LunaLimits.CooldownDuration;
                await _alerts.AddAsync(ctx.FamilyId, ctx.ChildId, ctx.DeviceId, "luna-flagged",
                    new AlertContent("Alert.Luna.Cooldown.Title", "Alert.Luna.Cooldown.Detail", FirstName(child.Name), chatId.ToString(), "cooldown"),
                    TimeSpan.FromHours(1), ct);
            }
            await _db.SaveChangesAsync(ct);
            await FlaggedAlertAsync(ctx.FamilyId, ctx.ChildId, ctx.DeviceId, child.Name, chatId, false, ct);
            await MaybeTitleAsync(chat, child, text, blockedText, familySettings, moderator, chatProvider, config,
                language, true, ct);
            return new LunaMessageResponse(chatId, ToChild(childMessage), ToChild(lunaMessage),
                LunaUsageStore.MessagesLeft(childSettings, usage), false,
                usage.CooldownUntilUtc > now, false, blockedText, WarningText(childSettings, usage, language));
        }

        // 5-6. Build the prompt and call the chat model, non-streaming.
        var learner = await _db.LunaLearnerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ChildId == ctx.ChildId, ct);
        var history = await BuildTurnsAsync(chat.Id, text, ct);
        var system = LunaPrompts.Build(chat.Subject,
            LunaPrompts.AgeFrom(child.DateOfBirth, now), childSettings.ExtraInstructions, learner?.Text, chat.Summary);
        var request = new LunaChatRequest(system, history, LunaLimits.MaxOutputTokens);

        LunaChatResult first;
        try
        {
            first = await chatProvider.CompleteAsync(config, request, ct);
        }
        catch (LunaProviderException ex)
        {
            await MarkSetupBrokenAsync(familySettings.FamilyId, ex, ct);
            var notice = IsSetupBreaking(ex) ? "Luna.Child.Refusal" : ex.MessageKey;
            return await RefusalAsync(chat, usage, child, childSettings, language, text,
                AppText.Get(notice, language), now, ct);
        }

        var (clean, markerKinds) = LunaSafety.StripConcernMarkers(first.Text);
        if (clean.Length == 0) clean = AppText.Get("Luna.Child.ReplyFallback", language);
        usage.PromptTokens += first.PromptTokens;
        usage.CompletionTokens += first.CompletionTokens;

        // 7. Output moderation on the full reply plus the model's concern marker.
        LunaModerationResult outputModeration;
        try
        {
            outputModeration = await moderator.ModerateAsync(clean, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Luna output moderation failed for child {ChildId}", ctx.ChildId);
            await MarkSetupBrokenAsync(familySettings.FamilyId, ex, ct);
            return await RefusalAsync(chat, usage, child, childSettings, language, text,
                AppText.Get("Luna.Child.Refusal", language), now, ct);
        }

        var output = Classify(familySettings, outputModeration);
        if (markerKinds.Count > 0 || output.Urgent)
        {
            var urgentText = UrgentText(familySettings, language);
            var categories = markerKinds.Count > 0
                ? markerKinds.Select(k => "marker:" + k).ToList()
                : output.Categories;
            var childMessage = await SaveExchangeAsync(chat, usage, child, text, null, LunaMessageFlag.None, null, null, 0, 0, now, ct);
            var lunaMessage = await AddLunaAsync(chat, clean, urgentText, LunaMessageFlag.Urgent,
                LunaSafety.Describe(categories), ScoresJson(output.Scores), first.PromptTokens, first.CompletionTokens, now, ct);
            usage.Messages++;
            usage.UpdatedUtc = now;
            await _db.SaveChangesAsync(ct);
            await UrgentAlertAsync(ctx.FamilyId, ctx.ChildId, child.Name, chat.Id, ct);
            await MaybeTitleAsync(chat, child, text, clean, familySettings, moderator, chatProvider, config,
                language, true, ct);
            await MaybeSummarizeAsync(chat, child, chatProvider, config, ct);
            return new LunaMessageResponse(chatId, ToChild(childMessage), ToChild(lunaMessage),
                LunaUsageStore.MessagesLeft(childSettings, usage), false, false, true, null,
                WarningText(childSettings, usage, language));
        }

        if (output.Blocked)
        {
            // 7b. One retry with a stricter instruction.
            LunaChatResult? retry = null;
            var retryClean = "";
            IReadOnlyList<string> retryCategories = output.Categories;
            var retryUrgent = false;
            try
            {
                var stricter = new LunaChatRequest(system + "\n\n" + LunaPrompts.StricterInstruction,
                    history, LunaLimits.MaxOutputTokens, true);
                retry = await chatProvider.CompleteAsync(config, stricter, ct);
                usage.PromptTokens += retry.PromptTokens;
                usage.CompletionTokens += retry.CompletionTokens;
                var stripped = LunaSafety.StripConcernMarkers(retry.Text);
                retryClean = stripped.Text;
                if (stripped.Kinds.Count > 0) retryUrgent = true;
                var retryModeration = await moderator.ModerateAsync(retryClean, ct);
                var retryClassification = Classify(familySettings, retryModeration);
                retryCategories = retryClassification.Categories;
                retryUrgent |= retryClassification.Urgent;
                if (retryClassification.Blocked && !retryUrgent)
                    retryClean = "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Luna reply retry failed for child {ChildId}", ctx.ChildId);
                await MarkSetupBrokenAsync(chat.FamilyId, ex, ct);
                retryClean = "";
            }

            var childMessage = await SaveExchangeAsync(chat, usage, child, text, null, LunaMessageFlag.None, null, null, 0, 0, now, ct);
            usage.Messages++;
            usage.UpdatedUtc = now;
            LunaMessage lunaMessage;
            var urgent = false;
            if (retryUrgent)
            {
                urgent = true;
                var urgentText = UrgentText(familySettings, language);
                lunaMessage = await AddLunaAsync(chat, retryClean, urgentText, LunaMessageFlag.Urgent,
                    LunaSafety.Describe(retryCategories), null,
                    first.PromptTokens + (retry?.PromptTokens ?? 0), first.CompletionTokens + (retry?.CompletionTokens ?? 0), now, ct);
                await _db.SaveChangesAsync(ct);
                await UrgentAlertAsync(ctx.FamilyId, ctx.ChildId, child.Name, chat.Id, ct);
            }
            else if (retryClean.Length > 0)
            {
                lunaMessage = await AddLunaAsync(chat, retryClean, retryClean, LunaMessageFlag.None, null, null,
                    first.PromptTokens, first.CompletionTokens, now, ct);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                var fallback = AppText.Get("Luna.Child.ReplyFallback", language);
                lunaMessage = await AddLunaAsync(chat, clean, fallback, LunaMessageFlag.ReplyBlocked,
                    LunaSafety.Describe(output.Categories), ScoresJson(output.Scores),
                    first.PromptTokens, first.CompletionTokens, now, ct);
                await _db.SaveChangesAsync(ct);
                await FlaggedAlertAsync(ctx.FamilyId, ctx.ChildId, ctx.DeviceId, child.Name, chat.Id, true, ct);
            }

            var turnFlagged = urgent || lunaMessage.Flag == LunaMessageFlag.ReplyBlocked;
            await MaybeTitleAsync(chat, child, text, retryClean.Length > 0 ? retryClean : lunaMessage.ShownText,
                familySettings, moderator, chatProvider, config, language, turnFlagged, ct);
            await MaybeSummarizeAsync(chat, child, chatProvider, config, ct);
            return new LunaMessageResponse(chatId, ToChild(childMessage), ToChild(lunaMessage),
                LunaUsageStore.MessagesLeft(childSettings, usage), false,
                usage.CooldownUntilUtc > now, urgent, null,
                WarningText(childSettings, usage, language));
        }

        // Safe reply.
        var safeChild = await SaveExchangeAsync(chat, usage, child, text, null, LunaMessageFlag.None, null, null, 0, 0, now, ct);
        var safeLuna = await AddLunaAsync(chat, clean, clean, LunaMessageFlag.None, null, null,
            first.PromptTokens, first.CompletionTokens, now, ct);
        usage.Messages++;
        usage.UpdatedUtc = now;
        await _db.SaveChangesAsync(ct);
        await MaybeTitleAsync(chat, child, text, clean, familySettings, moderator, chatProvider, config,
            language, false, ct);
        await MaybeSummarizeAsync(chat, child, chatProvider, config, ct);
        return new LunaMessageResponse(chatId, ToChild(safeChild), ToChild(safeLuna),
            LunaUsageStore.MessagesLeft(childSettings, usage), false,
            usage.CooldownUntilUtc > now, false, null, WarningText(childSettings, usage, language));
    }

    public async Task<LunaMoreRequestDto> CreateMoreRequestAsync(LunaTokenContext ctx, CancellationToken ct = default)
    {
        var (device, _, _, day) = await LoadAsync(ctx, ct);
        var now = DateTime.UtcNow;
        var existing = await _db.LunaMoreRequests.AsNoTracking()
            .Where(r => r.ChildId == ctx.ChildId && r.Status == LunaMoreRequestStatus.Pending && r.CreatedUtc >= now.AddHours(-12))
            .OrderByDescending(r => r.CreatedUtc)
            .FirstOrDefaultAsync(ct);
        if (existing is not null) return ToDto(existing);

        var child = await _db.Children.AsNoTracking().Where(c => c.Id == ctx.ChildId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "";
        var request = new LunaMoreRequest { FamilyId = ctx.FamilyId, ChildId = ctx.ChildId, CreatedUtc = now };
        _db.LunaMoreRequests.Add(request);
        await _db.SaveChangesAsync(ct);
        await _alerts.AddAsync(ctx.FamilyId, ctx.ChildId, device.Id, "luna-more-request",
            new AlertContent("Alert.Luna.MoreRequest.Title", "Alert.Luna.MoreRequest.Detail", FirstName(child), request.Id.ToString()),
            TimeSpan.FromHours(1), ct);
        return ToDto(request);
    }

    public async Task SetThemeAsync(LunaTokenContext ctx, string theme, CancellationToken ct = default)
    {
        var (_, _, settings, _) = await LoadAsync(ctx, ct);
        if (!LunaThemes.IsValid(theme))
            throw new InvalidOperationException(AppText.Get("Luna.Error.InvalidTheme"));
        settings.Theme = LunaThemes.Normalize(theme);
        settings.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ---- helpers ----

    private async Task<(Device Device, Child Child, ChildLunaSettings Settings, LunaUsageDay Usage)> LoadAsync(
        LunaTokenContext ctx, CancellationToken ct)
    {
        var device = await _db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == ctx.DeviceId && d.FamilyId == ctx.FamilyId && !d.IsRevoked, ct)
            ?? throw new InvalidOperationException(AppText.Get("Error.DeviceNotFound"));
        var child = await _db.Children.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == ctx.ChildId && c.FamilyId == ctx.FamilyId, ct)
            ?? throw new InvalidOperationException(AppText.Get("Error.ChildNotFound"));
        var settings = await EnsureChildAsync(ctx.FamilyId, ctx.ChildId, ct);
        if (!settings.Enabled) throw new InvalidOperationException(AppText.Get("Luna.Error.Disabled"));
        var tz = LunaUsageStore.ResolveZone(device.TimeZoneId);
        var day = await _usage.EnsureAsync(ctx.FamilyId, ctx.ChildId, LunaUsageStore.LocalDate(DateTime.UtcNow, tz), ct);
        return (device, child, settings, day);
    }

    private async Task<ChildLunaSettings> EnsureChildAsync(Guid familyId, Guid childId, CancellationToken ct)
    {
        var settings = await _db.ChildLunaSettings.FirstOrDefaultAsync(s => s.ChildId == childId, ct);
        if (settings is not null) return settings;
        settings = new ChildLunaSettings { FamilyId = familyId, ChildId = childId };
        _db.ChildLunaSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private async Task<LunaChat> ChatAsync(LunaTokenContext ctx, Guid chatId, CancellationToken ct) =>
        await _db.LunaChats.FirstOrDefaultAsync(c => c.Id == chatId && c.FamilyId == ctx.FamilyId && c.ChildId == ctx.ChildId, ct)
        ?? throw new InvalidOperationException(AppText.Get("Luna.Error.ChatNotFound"));

    private async Task<string> ChildLanguageAsync(Guid familyId, Device device, CancellationToken ct)
    {
        var family = await _db.Families.AsNoTracking().Where(f => f.Id == familyId).Select(f => f.DefaultLanguage).FirstOrDefaultAsync(ct);
        return AppCulture.Normalize(device.ClientLanguage ?? family);
    }

    private async Task<LunaMessage> SaveExchangeAsync(LunaChat chat, LunaUsageDay usage, Child child,
        string childText, string? lunaText, LunaMessageFlag flag, string? categories, string? scores,
        int promptTokens, int completionTokens, DateTime now, CancellationToken ct)
    {
        var message = new LunaMessage
        {
            FamilyId = chat.FamilyId,
            ChildId = chat.ChildId,
            ChatId = chat.Id,
            Role = LunaChatRoles.Child,
            Text = childText,
            ShownText = childText,
            CreatedUtc = now,
            Flag = flag,
            FlagCategories = categories,
            FlagScores = scores
        };
        _db.LunaMessages.Add(message);
        if (lunaText is not null)
            await AddLunaAsync(chat, lunaText, lunaText, LunaMessageFlag.None, null, null, promptTokens, completionTokens, now, ct);
        chat.LastMessageUtc = now;
        chat.UpdatedUtc = now;
        return message;
    }

    private async Task<LunaMessage> AddLunaAsync(LunaChat chat, string text, string shown, LunaMessageFlag flag,
        string? categories, string? scores, int promptTokens, int completionTokens, DateTime now, CancellationToken ct)
    {
        var message = new LunaMessage
        {
            FamilyId = chat.FamilyId,
            ChildId = chat.ChildId,
            ChatId = chat.Id,
            Role = LunaChatRoles.Luna,
            Text = text,
            ShownText = shown,
            CreatedUtc = now,
            Flag = flag,
            FlagCategories = categories,
            FlagScores = scores,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens
        };
        _db.LunaMessages.Add(message);
        chat.LastMessageUtc = now;
        chat.UpdatedUtc = now;
        return message;
    }

    private static LunaClassification Classify(LunaFamilySettings settings, LunaModerationResult result) =>
        settings.Setup == LunaSetup.A || settings.ChatProvider == LunaChatProviders.OpenRouter
            ? LunaSafety.ClassifyCategories(result.Categories, result.Flagged)
            : LunaSafety.ClassifyOpenAi(result.Scores);

    private static string ScoresJson(IReadOnlyDictionary<string, double> scores) =>
        JsonSerializer.Serialize(scores);

    private static LunaChildMessageDto ToChild(LunaMessage m) =>
        new(m.Id, m.ChatId, m.Role, m.Role == LunaChatRoles.Child ? m.Text : m.ShownText, m.CreatedUtc, m.Flag.ToString());

    private static LunaChatDto ToDto(LunaChat c) =>
        new(c.Id, c.Subject, c.Title, c.TitleSetByChild, c.HiddenByChild, c.CreatedUtc, c.LastMessageUtc);

    private static LunaMoreRequestDto ToDto(LunaMoreRequest r) =>
        new(r.Id, r.ChildId, r.CreatedUtc, r.Status.ToString(), r.GrantedAmount, r.HandledBy);

    private static LunaChildMessageDto nullMessage() =>
        new(Guid.Empty, Guid.Empty, "", "", DateTime.UtcNow, LunaMessageFlag.None.ToString());

    private static string FirstName(string name)
    {
        var trimmed = (name ?? "").Trim();
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
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

    private static string? WarningText(ChildLunaSettings settings, LunaUsageDay usage, string language)
    {
        var left = LunaUsageStore.MessagesLeft(settings, usage);
        return left is > 0 and <= LunaLimits.FiveLeft
            ? AppText.Get("Luna.Child.FiveLeft", language, left)
            : null;
    }

    private static LunaMessageResponse MakeResponse(Guid chatId, LunaUsageDay usage, ChildLunaSettings settings,
        string language, LunaMessage? child, LunaMessage? luna, bool urgent, string? notice)
    {
        var left = LunaUsageStore.MessagesLeft(settings, usage);
        return new LunaMessageResponse(chatId,
            child is null ? nullMessage() : ToChild(child),
            luna is null ? null : ToChild(luna),
            left, left <= 0, usage.CooldownUntilUtc > DateTime.UtcNow, urgent, notice,
            WarningText(settings, usage, language));
    }

    private static LunaMessageResponse Resting(LunaChat chat, ChildLunaSettings settings, LunaUsageDay usage,
        string language, string noticeKey, DateTime? until = null)
    {
        var notice = until is { } end
            ? AppText.Get(noticeKey, language, end.ToString("t", AppCulture.Culture(language)))
            : AppText.Get(noticeKey, language);
        return MakeResponse(chat.Id, usage, settings, language, null, null, false, notice);
    }

    private async Task<LunaMessageResponse> RefusalAsync(LunaChat chat, LunaUsageDay usage, Child child,
        ChildLunaSettings settings, string language, string childText, string notice, DateTime now,
        CancellationToken ct, int promptTokens = 0, int completionTokens = 0)
    {
        var childMessage = new LunaMessage
        {
            FamilyId = chat.FamilyId,
            ChildId = chat.ChildId,
            ChatId = chat.Id,
            Role = LunaChatRoles.Child,
            Text = childText,
            ShownText = childText,
            CreatedUtc = now
        };
        var lunaMessage = new LunaMessage
        {
            FamilyId = chat.FamilyId,
            ChildId = chat.ChildId,
            ChatId = chat.Id,
            Role = LunaChatRoles.Luna,
            Text = notice,
            ShownText = notice,
            CreatedUtc = now,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens
        };
        _db.LunaMessages.AddRange(childMessage, lunaMessage);
        chat.LastMessageUtc = now;
        chat.UpdatedUtc = now;
        usage.Messages++;
        usage.PromptTokens += promptTokens;
        usage.CompletionTokens += completionTokens;
        usage.UpdatedUtc = now;
        await _db.SaveChangesAsync(ct);
        return MakeResponse(chat.Id, usage, settings, language, childMessage, lunaMessage, false, notice);
    }

    private static (string? Notice, string? Warning) StateText(Child child, ChildLunaSettings settings,
        LunaUsageDay usage, int left, bool locked, string language, DateTime now)
    {
        string? notice = null;
        if (locked) notice = AppText.Get("Luna.Child.Resting", language);
        else if (usage.CooldownUntilUtc is { } until && until > now)
            notice = AppText.Get("Luna.Child.Cooldown", language,
                TimeZoneInfo.ConvertTimeFromUtc(until, TimeZoneInfo.Utc).ToString("t", AppCulture.Culture(language)));
        else if (left <= 0) notice = AppText.Get("Luna.Child.Limit", language, FirstName(child.Name));
        return (notice, WarningText(settings, usage, language));
    }

    private async Task<List<LunaChatTurn>> BuildTurnsAsync(Guid chatId, string currentText, CancellationToken ct)
    {
        var rows = await _db.LunaMessages.AsNoTracking()
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(60)
            .ToListAsync(ct);
        rows.Reverse();

        var turns = new List<LunaChatTurn>();
        var budget = LunaLimits.RecentTokenBudget;
        var used = Estimate(currentText);
        turns.Add(new LunaChatTurn("user", currentText));
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            var row = rows[i];
            var content = LunaTranscript.ProviderText(row);
            var cost = Estimate(content);
            if (used + cost > budget && turns.Count > 1) break;
            used += cost;
            turns.Insert(0, new LunaChatTurn(row.Role == LunaChatRoles.Child ? "user" : "assistant", content));
        }
        return turns;
    }

    private static int Estimate(string text) => Math.Max(1, (text.Length + 3) / 4);

    private async Task MaybeTitleAsync(LunaChat chat, Child child, string childText, string reply,
        LunaFamilySettings familySettings, ILunaModerator moderator, ILunaChatProvider provider,
        LunaProviderConfig config, string language, bool turnFlagged, CancellationToken ct)
    {
        var fallback = DefaultTitle(chat.Subject, language);

        if (chat.TitleSetByChild) return;

        // A flagged turn never reaches the model. The subject's localized default name is
        // used instead, and a later clean turn may still replace it if the child has not renamed it.
        if (turnFlagged)
        {
            if (string.IsNullOrWhiteSpace(chat.Title) || IsDefaultTitle(chat))
                await SaveTitleAsync(chat, fallback, ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(chat.Title) && !IsDefaultTitle(chat)) return;

        try
        {
            var snippet = $"Child: {Truncate(childText, 400)}\nLuna: {Truncate(reply, 600)}";
            var result = await provider.CompleteAsync(config,
                new LunaChatRequest(LunaPrompts.TitlePrompt, [new LunaChatTurn("user", snippet)], 40), ct);
            var (clean, kinds) = LunaSafety.StripConcernMarkers(result.Text);
            var title = CleanTitle(clean);
            if (kinds.Count > 0 || title.Length == 0)
            {
                await SaveTitleAsync(chat, fallback, ct);
                return;
            }

            // The title is shown to the child in the chat list, so it is moderated like a reply.
            LunaModerationResult moderation;
            try
            {
                moderation = await moderator.ModerateAsync(title, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Luna title moderation failed for chat {ChatId}", chat.Id);
                await MarkSetupBrokenAsync(chat.FamilyId, ex, ct);
                await SaveTitleAsync(chat, fallback, ct);
                return;
            }

            var classification = Classify(familySettings, moderation);
            await SaveTitleAsync(chat, classification.Blocked || classification.Urgent ? fallback : title, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Luna title generation failed for chat {ChatId}", chat.Id);
            await MarkSetupBrokenAsync(chat.FamilyId, ex, ct);
            await SaveTitleAsync(chat, fallback, ct);
        }
    }

    private async Task SaveTitleAsync(LunaChat chat, string title, CancellationToken ct)
    {
        chat.Title = title;
        chat.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static string DefaultTitle(string subject, string language) =>
        AppText.Get(LunaPrompts.SubjectTitleKey(subject), language);

    private static bool IsDefaultTitle(LunaChat chat) =>
        AppCulture.Supported.Any(l => string.Equals(chat.Title, DefaultTitle(chat.Subject, l.Code),
            StringComparison.Ordinal));

    private static string CleanTitle(string text)
    {
        var value = (text ?? "").Trim().Trim('"', '\'', '“', '”', '.', '!', '\r', '\n');
        var line = value.Split('\n')[0].Trim();
        return line.Length > LunaPrompts.MaxTitleLength ? line[..LunaPrompts.MaxTitleLength].Trim() : line;
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + "…";

    private async Task MaybeSummarizeAsync(LunaChat chat, Child child, ILunaChatProvider provider,
        LunaProviderConfig config, CancellationToken ct)
    {
        try
        {
            var total = await _db.LunaMessages.CountAsync(m => m.ChatId == chat.Id, ct);
            if (total < LunaLimits.SummaryAfterMessages) return;

            var query = _db.LunaMessages.AsNoTracking().Where(m => m.ChatId == chat.Id);
            if (chat.SummaryUpToMessageId is { } lastId)
            {
                var last = await _db.LunaMessages.AsNoTracking().Where(m => m.Id == lastId).Select(m => m.CreatedUtc).FirstOrDefaultAsync(ct);
                query = query.Where(m => m.CreatedUtc > last);
            }
            var rows = await query.OrderBy(m => m.CreatedUtc).ToListAsync(ct);
            if (rows.Count <= LunaLimits.SummaryKeepRecent) return;
            var older = rows.Take(rows.Count - LunaLimits.SummaryKeepRecent).ToList();
            if (older.Count == 0) return;

            var transcript = string.Join("\n", older.Select(m =>
                (m.Role == LunaChatRoles.Child ? "Child: " : "Luna: ") + Truncate(LunaTranscript.ProviderText(m), 400)));
            var result = await provider.CompleteAsync(config,
                new LunaChatRequest(LunaPrompts.SummaryPrompt,
                    [new LunaChatTurn("user", Truncate(transcript, 12000))], 300), ct);
            chat.Summary = Truncate(result.Text.Trim(), 4000);
            chat.SummaryUpToMessageId = older[^1].Id;
            chat.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            var summaries = await _db.LunaChats.AsNoTracking()
                .CountAsync(c => c.ChildId == chat.ChildId && c.Summary != null && c.Summary != "", ct);
            if (summaries % LunaLimits.ProfileEverySummaries == 0)
                await UpdateProfileAsync(chat, provider, config, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Luna summary generation failed for chat {ChatId}", chat.Id);
            await MarkSetupBrokenAsync(chat.FamilyId, ex, ct);
        }
    }

    private async Task UpdateProfileAsync(LunaChat chat, ILunaChatProvider provider, LunaProviderConfig config, CancellationToken ct)
    {
        try
        {
            var profile = await _db.LunaLearnerProfiles.FirstOrDefaultAsync(p => p.ChildId == chat.ChildId, ct);
            var result = await provider.CompleteAsync(config,
                new LunaChatRequest(LunaPrompts.ProfilePrompt,
                    [new LunaChatTurn("user", $"Existing profile:\n{profile?.Text ?? ""}\n\nNew chat summary:\n{chat.Summary ?? ""}")], 300), ct);
            if (profile is null)
            {
                profile = new LunaLearnerProfile { FamilyId = chat.FamilyId, ChildId = chat.ChildId };
                _db.LunaLearnerProfiles.Add(profile);
            }
            profile.Text = Truncate(result.Text.Trim(), LunaLimits.MaxProfileCharacters);
            profile.UpdatedUtc = DateTime.UtcNow;
            profile.UpdatedBy = "luna";
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Luna learner profile update failed for child {ChildId}", chat.ChildId);
            await MarkSetupBrokenAsync(chat.FamilyId, ex, ct);
        }
    }

    private static bool IsSetupBreaking(Exception ex) =>
        ex is LunaProviderException provider
        && provider.MessageKey is "Luna.Error.BadKey" or "Luna.Error.NoCredit";

    private async Task MarkSetupBrokenAsync(Guid familyId, Exception ex, CancellationToken ct)
    {
        if (!IsSetupBreaking(ex)) return;
        try
        {
            await _settings.MarkBrokenAsync(familyId, ((LunaProviderException)ex).MessageKey, ct);
        }
        catch (Exception alertEx)
        {
            _logger.LogWarning(alertEx, "Could not pause Luna after a provider auth failure for family {FamilyId}", familyId);
        }
    }

    private async Task FlaggedAlertAsync(Guid familyId, Guid childId, Guid deviceId, string childName, Guid chatId,
        bool replyBlocked, CancellationToken ct)
    {
        await _alerts.AddAsync(familyId, childId, deviceId, "luna-flagged",
            replyBlocked
                ? new AlertContent("Alert.Luna.ReplyBlocked.Title", "Alert.Luna.ReplyBlocked.Detail", FirstName(childName), chatId.ToString())
                : new AlertContent("Alert.Luna.Flagged.Title", "Alert.Luna.Flagged.Detail", FirstName(childName), chatId.ToString()),
            TimeSpan.FromMinutes(30), ct);
    }

    private async Task UrgentAlertAsync(Guid familyId, Guid childId, string childName, Guid chatId, CancellationToken ct)
    {
        await _alerts.AddAsync(familyId, childId, null, "luna-urgent",
            new AlertContent("Alert.Luna.Urgent.Title", "Alert.Luna.Urgent.Detail", FirstName(childName), chatId.ToString()),
            null, ct, immediateEmail: true);
    }

    private async Task MonthlyCapAlertAsync(Guid familyId, Guid childId, string childName, CancellationToken ct)
    {
        await _alerts.AddAsync(familyId, childId, null, "luna-flagged",
            new AlertContent("Alert.Luna.MonthlyCap.Title", "Alert.Luna.MonthlyCap.Detail", FirstName(childName)),
            TimeSpan.FromHours(24), ct);
    }
}
