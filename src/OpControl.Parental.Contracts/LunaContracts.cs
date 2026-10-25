// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public enum LunaSetup
{
    A = 0,
    B = 1
}

public enum LunaMessageFlag
{
    None = 0,
    Blocked = 1,
    ReplyBlocked = 2,
    Urgent = 3
}

public enum LunaMoreRequestStatus
{
    Pending = 0,
    Granted = 1,
    Dismissed = 2
}

public static class LunaSubjects
{
    public const string Math = "math";
    public const string Science = "science";
    public const string History = "history";
    public const string Dictionary = "dictionary";
    public const string Languages = "languages";
    public const string Curious = "curious";

    public static readonly string[] All = [Math, Science, History, Dictionary, Languages, Curious];

    public static bool IsValid(string? subject) =>
        !string.IsNullOrWhiteSpace(subject) && All.Contains(subject.Trim().ToLowerInvariant());

    public static string Normalize(string subject) => subject.Trim().ToLowerInvariant();
}

public static class LunaThemes
{
    public const string Night = "night";
    public const string Gray = "gray";

    public static bool IsValid(string? theme) =>
        string.Equals(theme, Night, StringComparison.OrdinalIgnoreCase)
        || string.Equals(theme, Gray, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? theme) =>
        string.Equals(theme, Gray, StringComparison.OrdinalIgnoreCase) ? Gray : Night;

    public static string Toggle(string? theme) => Normalize(theme) == Gray ? Night : Gray;
}

public static class LunaChatRoles
{
    public const string Child = "child";
    public const string Luna = "luna";
}

public static class LunaChatProviders
{
    public const string OpenAi = "openai";
    public const string Anthropic = "anthropic";
    public const string Gemini = "gemini";
    public const string DeepSeek = "deepseek";
    public const string OpenRouter = "openrouter";

    public static readonly string[] All = [OpenAi, Anthropic, Gemini, DeepSeek, OpenRouter];

    public static bool IsValid(string? provider) =>
        !string.IsNullOrWhiteSpace(provider) && All.Contains(provider.Trim().ToLowerInvariant());

    public static string Normalize(string provider) => provider.Trim().ToLowerInvariant();
}

public static class LunaRemovePlaceholder
{
    public const string Text = "[a message was removed by the safety filter]";
}

public sealed record LunaSettingsDto(
    string Setup,
    string ChatProvider,
    string ChatModel,
    bool HasChatKey,
    string? ChatKeyLast4,
    bool HasModerationKey,
    string? ModerationKeyLast4,
    DateTime? LastTestUtc,
    bool? LastTestOk,
    string? LastTestError,
    int? MonthlyTokenCap,
    string HelplineText);

public sealed record LunaSettingsRequest(
    string Setup,
    string ChatProvider,
    string ChatModel,
    string? ChatKey,
    string? ModerationKey,
    int? MonthlyTokenCap,
    string? HelplineText);

public sealed record LunaTestResult(bool Ok, string? Error, bool ChatOk = false, bool SafetyOk = false, bool ZdrOk = false);

public sealed record ChildLunaDto(
    Guid ChildId,
    bool Enabled,
    Guid? EnabledByParentId,
    DateTime? EnabledUtc,
    DateTime? UnderThirteenAcknowledgedUtc,
    int DailyMessageLimit,
    string? ExtraInstructions,
    string Theme,
    bool SetupValid,
    bool RequiresUnderThirteenAcknowledgement,
    int MessagesLeftToday,
    int ExtraAllowedToday,
    int MonthlyPromptTokens,
    int MonthlyCompletionTokens,
    int? MonthlyTokenCap,
    decimal ApproxCostUsd);

public sealed record ChildLunaRequest(
    bool Enabled,
    int? DailyMessageLimit,
    string? ExtraInstructions,
    bool UnderThirteenAcknowledged);

public sealed record LunaStatusDto(
    bool Enabled,
    string ChildFirstName,
    string Theme,
    int MessagesLeftToday,
    DateTime? CooldownUntilUtc,
    bool DailyLimitReached,
    string Language,
    string HelplineText,
    string? Notice,
    string? Warning);

public sealed record LunaChatDto(
    Guid Id,
    string Subject,
    string Title,
    bool TitleSetByChild,
    bool HiddenByChild,
    DateTime CreatedUtc,
    DateTime LastMessageUtc);

public sealed record LunaChildMessageDto(
    Guid Id,
    Guid ChatId,
    string Role,
    string Text,
    DateTime CreatedUtc,
    string Flag);

public sealed record LunaParentMessageDto(
    Guid Id,
    Guid ChatId,
    string Role,
    string Text,
    string ShownText,
    DateTime CreatedUtc,
    string Flag,
    string? FlagCategories,
    string? FlagScores,
    DateTime? ReviewedUtc,
    Guid? ReviewedByParentId,
    bool MarkedFine,
    int PromptTokens,
    int CompletionTokens);

public sealed record LunaParentChatDto(
    LunaChatDto Chat,
    IReadOnlyList<LunaParentMessageDto> Messages);

public sealed record LunaMessageResponse(
    Guid ChatId,
    LunaChildMessageDto ChildMessage,
    LunaChildMessageDto? LunaMessage,
    int MessagesLeftToday,
    bool LimitReached,
    bool Cooldown,
    bool Urgent,
    string? Notice,
    string? Warning);

public sealed record LunaCreateChatRequest(string Subject);

public sealed record LunaUpdateChatRequest(string? Title, bool? Hidden);

public sealed record LunaMessageRequest(string Text);

public sealed record LunaThemeRequest(string Theme);

public sealed record LunaGrantExtraRequest(int Amount);

public sealed record LunaReviewRequest(bool MarkedFine);

public sealed record LunaProfileDto(Guid ChildId, string Text, DateTime UpdatedUtc, string? UpdatedBy);

public sealed record LunaProfileRequest(string Text);

public sealed record LunaMoreRequestDto(
    Guid Id,
    Guid ChildId,
    DateTime CreatedUtc,
    string Status,
    int? GrantedAmount,
    Guid? HandledBy);

public sealed record LunaUsageDto(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal ApproxCostUsd,
    int? MonthlyTokenCap,
    bool CapReached);
