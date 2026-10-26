// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services.Luna;

public sealed class LunaParentService
{
    private readonly ParentalDbContext _db;
    private readonly LunaSettingsService _settings;
    private readonly LunaUsageStore _usage;

    public LunaParentService(ParentalDbContext db, LunaSettingsService settings, LunaUsageStore usage)
    {
        _db = db;
        _settings = settings;
        _usage = usage;
    }

    public async Task<ChildLunaDto> GetChildAsync(Guid familyId, Guid childId, CancellationToken ct = default)
    {
        var child = await ChildAsync(familyId, childId, ct);
        var (settings, day) = await LoadAsync(familyId, childId, ct);
        return await BuildDtoAsync(familyId, child, settings, day, ct);
    }

    public async Task<ChildLunaDto> SaveChildAsync(Guid familyId, Guid parentId, Guid childId,
        ChildLunaRequest request, CancellationToken ct = default)
    {
        var child = await ChildAsync(familyId, childId, ct);
        var family = await _settings.EnsureAsync(familyId, ct);
        var (settings, day) = await LoadAsync(familyId, childId, ct);
        var now = DateTime.UtcNow;

        if (request.Enabled)
        {
            if (!_settings.IsReady(family))
                throw new InvalidOperationException(AppText.Get("Luna.Error.SetupNotTested"));
            var underThirteen = LunaPrompts.IsUnderThirteen(child.DateOfBirth, now);
            if (family.Setup == LunaSetup.B && underThirteen)
            {
                if (!request.UnderThirteenAcknowledged && settings.UnderThirteenAcknowledgedUtc is null)
                    throw new InvalidOperationException(AppText.Get("Luna.Error.UnderThirteenAck"));
                if (request.UnderThirteenAcknowledged)
                    settings.UnderThirteenAcknowledgedUtc = now;
            }

            if (!settings.Enabled)
            {
                settings.EnabledUtc = now;
                settings.EnabledByParentId = parentId;
            }
            settings.Enabled = true;
        }
        else
        {
            settings.Enabled = false;
        }

        settings.DailyMessageLimit = Math.Clamp(request.DailyMessageLimit ?? LunaLimits.DefaultDailyMessages,
            LunaLimits.MinDailyMessages, LunaLimits.MaxDailyMessages);
        var extra = (request.ExtraInstructions ?? "").Trim();
        if (extra.Length > LunaPrompts.MaxExtraInstructions) extra = extra[..LunaPrompts.MaxExtraInstructions];
        settings.ExtraInstructions = extra.Length == 0 ? null : extra;
        settings.UpdatedUtc = now;
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(familyId, child, settings, day, ct);
    }

    public async Task<List<LunaChatDto>> ListChatsAsync(Guid familyId, Guid childId, CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        var chats = await _db.LunaChats.AsNoTracking()
            .Where(c => c.FamilyId == familyId && c.ChildId == childId)
            .OrderByDescending(c => c.LastMessageUtc)
            .Take(200)
            .ToListAsync(ct);
        return chats.Select(ToDto).ToList();
    }

    public async Task<LunaParentChatDto> GetChatAsync(Guid familyId, Guid childId, Guid chatId, CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        var chat = await _db.LunaChats.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chatId && c.FamilyId == familyId && c.ChildId == childId, ct)
            ?? throw new InvalidOperationException(AppText.Get("Luna.Error.ChatNotFound"));
        var messages = await _db.LunaMessages.AsNoTracking()
            .Where(m => m.ChatId == chatId && m.FamilyId == familyId && m.ChildId == childId)
            .OrderBy(m => m.CreatedUtc)
            .Take(1000)
            .ToListAsync(ct);
        return new LunaParentChatDto(ToDto(chat), messages.Select(ToParentDto).ToList());
    }

    public async Task<(int Chats, int Messages)> DeleteChatAsync(Guid familyId, Guid childId, Guid chatId, CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        var chat = await _db.LunaChats.FirstOrDefaultAsync(c => c.Id == chatId && c.FamilyId == familyId && c.ChildId == childId, ct)
            ?? throw new InvalidOperationException(AppText.Get("Luna.Error.ChatNotFound"));
        var messages = await _db.LunaMessages.Where(m => m.ChatId == chatId && m.FamilyId == familyId).ToListAsync(ct);
        _db.LunaMessages.RemoveRange(messages);
        _db.LunaChats.Remove(chat);
        await _db.SaveChangesAsync(ct);
        return (1, messages.Count);
    }

    public async Task<(int Chats, int Messages)> ClearAsync(Guid familyId, Guid childId, CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        var messages = await _db.LunaMessages.Where(m => m.FamilyId == familyId && m.ChildId == childId).ToListAsync(ct);
        var chats = await _db.LunaChats.Where(c => c.FamilyId == familyId && c.ChildId == childId).ToListAsync(ct);
        _db.LunaMessages.RemoveRange(messages);
        _db.LunaChats.RemoveRange(chats);
        await _db.SaveChangesAsync(ct);
        return (chats.Count, messages.Count);
    }

    public async Task<LunaParentMessageDto> ReviewAsync(Guid familyId, Guid childId, Guid messageId, Guid parentId,
        bool markedFine, CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        var message = await _db.LunaMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.FamilyId == familyId && m.ChildId == childId, ct)
            ?? throw new InvalidOperationException(AppText.Get("Luna.Error.MessageNotFound"));
        message.ReviewedUtc = DateTime.UtcNow;
        message.ReviewedByParentId = parentId;
        message.MarkedFine = markedFine;
        await _db.SaveChangesAsync(ct);
        return ToParentDto(message);
    }

    public async Task<int> GrantExtraAsync(Guid familyId, Guid childId, Guid parentId, int amount,
        CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        var tz = LunaUsageStore.ResolveZone(await _usage.FamilyTimeZoneIdAsync(familyId, childId, ct));
        var today = LunaUsageStore.LocalDate(DateTime.UtcNow, tz);
        var usage = await _usage.EnsureAsync(familyId, childId, today, ct);
        var settings = await SettingsAsync(familyId, childId, ct);
        var grant = Math.Clamp(amount, 1, LunaLimits.MaxExtraGrant);
        usage.ExtraAllowed = Math.Min(LunaLimits.MaxExtraGrant, usage.ExtraAllowed + grant);
        usage.UpdatedUtc = DateTime.UtcNow;

        var request = await _db.LunaMoreRequests
            .Where(r => r.FamilyId == familyId && r.ChildId == childId && r.Status == LunaMoreRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedUtc)
            .FirstOrDefaultAsync(ct);
        if (request is not null)
        {
            request.Status = LunaMoreRequestStatus.Granted;
            request.GrantedAmount = grant;
            request.HandledBy = parentId;
            request.HandledUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return LunaUsageStore.MessagesLeft(settings, usage);
    }

    public async Task DismissRequestAsync(Guid familyId, Guid childId, Guid requestId, Guid parentId,
        CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        var request = await _db.LunaMoreRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.FamilyId == familyId && r.ChildId == childId, ct)
            ?? throw new InvalidOperationException(AppText.Get("Luna.Error.RequestNotFound"));
        if (request.Status == LunaMoreRequestStatus.Pending)
        {
            request.Status = LunaMoreRequestStatus.Dismissed;
            request.HandledBy = parentId;
            request.HandledUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<LunaMoreRequestDto>> ListRequestsAsync(Guid familyId, Guid childId, CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        return await _db.LunaMoreRequests.AsNoTracking()
            .Where(r => r.FamilyId == familyId && r.ChildId == childId)
            .OrderByDescending(r => r.CreatedUtc)
            .Take(50)
            .Select(r => new LunaMoreRequestDto(r.Id, r.ChildId, r.CreatedUtc, r.Status.ToString(), r.GrantedAmount, r.HandledBy))
            .ToListAsync(ct);
    }

    public async Task<LunaProfileDto> GetProfileAsync(Guid familyId, Guid childId, CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        var profile = await _db.LunaLearnerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.ChildId == childId, ct);
        return profile is null
            ? new LunaProfileDto(childId, "", DateTime.UtcNow, null)
            : new LunaProfileDto(childId, profile.Text, profile.UpdatedUtc, profile.UpdatedBy);
    }

    public async Task<LunaProfileDto> SaveProfileAsync(Guid familyId, Guid childId, Guid parentId, string? text,
        CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        var profile = await _db.LunaLearnerProfiles.FirstOrDefaultAsync(p => p.ChildId == childId, ct);
        if (profile is null)
        {
            profile = new LunaLearnerProfile { FamilyId = familyId, ChildId = childId };
            _db.LunaLearnerProfiles.Add(profile);
        }
        var value = (text ?? "").Trim();
        profile.Text = value.Length > LunaLimits.MaxProfileCharacters ? value[..LunaLimits.MaxProfileCharacters] : value;
        profile.UpdatedUtc = DateTime.UtcNow;
        profile.UpdatedBy = parentId.ToString();
        await _db.SaveChangesAsync(ct);
        return new LunaProfileDto(childId, profile.Text, profile.UpdatedUtc, profile.UpdatedBy);
    }

    public async Task<LunaUsageDto> GetUsageAsync(Guid familyId, Guid childId, CancellationToken ct = default)
    {
        await ChildAsync(familyId, childId, ct);
        var tz = LunaUsageStore.ResolveZone(await _usage.FamilyTimeZoneIdAsync(familyId, childId, ct));
        var today = LunaUsageStore.LocalDate(DateTime.UtcNow, tz);
        var (prompt, completion) = await _usage.MonthlyTokensAsync(familyId, today, ct);
        var family = await _settings.EnsureAsync(familyId, ct);
        var total = prompt + completion;
        return new LunaUsageDto(prompt, completion, total,
            LunaUsageStore.ApproxCost(prompt, completion), family.MonthlyTokenCap,
            family.MonthlyTokenCap is int cap && cap > 0 && total >= cap);
    }

    private async Task<ChildLunaDto> BuildDtoAsync(Guid familyId, Child child, ChildLunaSettings settings,
        LunaUsageDay day, CancellationToken ct)
    {
        var family = await _settings.EnsureAsync(familyId, ct);
        var setupValid = _settings.IsReady(family);
        var underThirteen = LunaPrompts.IsUnderThirteen(child.DateOfBirth, DateTime.UtcNow);
        var requiresAck = family.Setup == LunaSetup.B && underThirteen && settings.UnderThirteenAcknowledgedUtc is null;
        var tz = LunaUsageStore.ResolveZone(await _usage.FamilyTimeZoneIdAsync(familyId, child.Id, ct));
        var today = LunaUsageStore.LocalDate(DateTime.UtcNow, tz);
        var (prompt, completion) = await _usage.MonthlyTokensAsync(familyId, today, ct);
        return new ChildLunaDto(child.Id, settings.Enabled, settings.EnabledByParentId, settings.EnabledUtc,
            settings.UnderThirteenAcknowledgedUtc, settings.DailyMessageLimit, settings.ExtraInstructions, settings.Theme,
            setupValid, requiresAck, LunaUsageStore.MessagesLeft(settings, day), day.ExtraAllowed,
            prompt, completion, family.MonthlyTokenCap, LunaUsageStore.ApproxCost(prompt, completion));
    }

    private async Task<(ChildLunaSettings Settings, LunaUsageDay Day)> LoadAsync(Guid familyId, Guid childId,
        CancellationToken ct)
    {
        var settings = await SettingsAsync(familyId, childId, ct);
        var tz = LunaUsageStore.ResolveZone(await _usage.FamilyTimeZoneIdAsync(familyId, childId, ct));
        var day = await _usage.EnsureAsync(familyId, childId, LunaUsageStore.LocalDate(DateTime.UtcNow, tz), ct);
        return (settings, day);
    }

    private async Task<ChildLunaSettings> SettingsAsync(Guid familyId, Guid childId, CancellationToken ct)
    {
        var settings = await _db.ChildLunaSettings.FirstOrDefaultAsync(s => s.ChildId == childId, ct);
        if (settings is not null) return settings;
        settings = new ChildLunaSettings { FamilyId = familyId, ChildId = childId };
        _db.ChildLunaSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private async Task<Child> ChildAsync(Guid familyId, Guid childId, CancellationToken ct) =>
        await _db.Children.AsNoTracking().FirstOrDefaultAsync(c => c.Id == childId && c.FamilyId == familyId, ct)
        ?? throw new InvalidOperationException(AppText.Get("Error.ChildNotFound"));

    private static LunaChatDto ToDto(LunaChat c) =>
        new(c.Id, c.Subject, c.Title, c.TitleSetByChild, c.HiddenByChild, c.CreatedUtc, c.LastMessageUtc);

    private static LunaParentMessageDto ToParentDto(LunaMessage m) =>
        new(m.Id, m.ChatId, m.Role, m.Text, m.ShownText, m.CreatedUtc, m.Flag.ToString(),
            m.FlagCategories, m.FlagScores, m.ReviewedUtc, m.ReviewedByParentId, m.MarkedFine,
            m.PromptTokens, m.CompletionTokens);
}
