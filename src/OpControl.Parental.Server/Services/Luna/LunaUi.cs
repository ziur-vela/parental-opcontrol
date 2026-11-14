// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services.Luna;

public enum LunaMoonState
{
    Awake,
    Asleep,
    Paused,
    Happy,
    Looking
}

/// <summary>Shared subject colours, names and flag labels for the parent dashboard.</summary>
public static class LunaUi
{
    public sealed record SubjectInfo(string Id, string NameKey, string Color, string Icon);

    public static readonly IReadOnlyList<SubjectInfo> Subjects =
    [
        new(LunaSubjects.Math, "Luna.Subject.Math", "#5b8cff", "∑"),
        new(LunaSubjects.Science, "Luna.Subject.Science", "#3ddc97", "⚗"),
        new(LunaSubjects.History, "Luna.Subject.History", "#ffb547", "⌛"),
        new(LunaSubjects.Dictionary, "Luna.Subject.Dictionary", "#ff8fb1", "Aa"),
        new(LunaSubjects.Languages, "Luna.Subject.Languages", "#b388ff", "文"),
        new(LunaSubjects.Curious, "Luna.Subject.Curious", "#ff9f6b", "?")
    ];

    public static SubjectInfo Subject(string? id) =>
        Subjects.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? Subjects[^1];

    public static string FlagLabelKey(LunaMessageFlag flag) => flag switch
    {
        LunaMessageFlag.Blocked => "Luna.Flag.Blocked",
        LunaMessageFlag.ReplyBlocked => "Luna.Flag.ReplyBlocked",
        LunaMessageFlag.Urgent => "Luna.Flag.Urgent",
        LunaMessageFlag.Declined => "Luna.Flag.Declined",
        LunaMessageFlag.TopicFlagged => "Luna.Flag.Topic",
        _ => "Luna.Flag.Reviewed"
    };

    public static string FlagClass(LunaMessageFlag flag) => flag switch
    {
        LunaMessageFlag.Blocked => "blocked",
        LunaMessageFlag.ReplyBlocked => "answer-blocked",
        LunaMessageFlag.Urgent => "urgent",
        LunaMessageFlag.Declined => "declined",
        LunaMessageFlag.TopicFlagged => "blocked",
        _ => "none"
    };

    public static bool NeedsReview(LunaMessage message) =>
        NeedsReviewFlag(message.Flag) && message.ReviewedUtc is null;

    /// <summary>Declined is a low-priority marker; it is visible but not a review task.</summary>
    public static bool NeedsReviewFlag(LunaMessageFlag flag) =>
        flag is LunaMessageFlag.Blocked or LunaMessageFlag.ReplyBlocked
            or LunaMessageFlag.Urgent or LunaMessageFlag.TopicFlagged;

    /// <summary>The header moon for a child page, the review page or the setup page.</summary>
    public static LunaMoonState HeaderMoon(bool lastTestOk, string? lastTestError, bool childOn) =>
        !lastTestOk && !string.IsNullOrWhiteSpace(lastTestError)
            ? LunaMoonState.Paused
            : lastTestOk && childOn ? LunaMoonState.Awake : LunaMoonState.Asleep;

    /// <summary>One place for the public-AI-block tip condition.</summary>
    public static bool ShowPublicAiBlockTip(bool lunaOn, bool? publicAiChatsBlocked) =>
        lunaOn && publicAiChatsBlocked != true;
}
