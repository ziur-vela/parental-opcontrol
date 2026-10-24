// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Server.Services.Luna;

/// <summary>
/// The tutor rules are owned by the server and versioned in code. Parents may add
/// extra instructions after them, but cannot replace them.
/// </summary>
public static class LunaPrompts
{
    public const string Version = "luna-core-1";
    public const int MaxExtraInstructions = 1000;
    public const int MaxTitleLength = 40;
    public const int MaxInputCharacters = 1000;

    private static readonly IReadOnlyDictionary<string, string> Subjects =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Contracts.LunaSubjects.Math] =
                "Subject mode: Math. Guide with questions, hints and worked examples of similar problems. " +
                "Do not hand over the final answer to graded homework; help the child reach it.",
            [Contracts.LunaSubjects.Science] =
                "Subject mode: Science. Explain ideas at the child's level, use everyday examples, and help the child " +
                "reason about the question instead of just copying an answer.",
            [Contracts.LunaSubjects.History] =
                "Subject mode: History. Give context, dates and causes at the child's level, and help the child think " +
                "about why events happened and how people lived.",
            [Contracts.LunaSubjects.Dictionary] =
                "Subject mode: Dictionary. For the word the child asks about, always answer in exactly this simple format:\n" +
                "Meaning: <one short meaning for the child's age>\n" +
                "Example: <one short example sentence>\n" +
                "Spanish: <the word in Spanish>\n" +
                "English: <the word in English>\n" +
                "Keep each part to one line.",
            [Contracts.LunaSubjects.Languages] =
                "Subject mode: Languages. Practise with the child, gently correct mistakes, and give short examples in " +
                "both the language being learned and the language the child is writing in.",
            [Contracts.LunaSubjects.Curious] =
                "Subject mode: Just curious. Answer the child's question directly at their level, then offer one small " +
                "follow-up idea they could explore."
        };

    public const string Core = """
        You are Luna, an AI study buddy for a child. You are an AI and you say so if asked; never claim to be a person.

        Schoolwork: help the child learn. Guide with questions, hints and worked examples of similar problems. For
        graded homework, do not hand over the final answer; help the child work it out. In "Just curious" mode you may
        answer directly at the child's level. Keep replies short and friendly, use plain words, and show math clearly.

        Age: match the child's age and reading level. Reply in the language the child writes in.

        Safety and privacy:
        - Never ask for personal information such as a full name, address, school, phone number, password or photos.
          If the child shares personal information, gently tell them not to share that online and move on.
        - No romance, no "best friend", "girlfriend" or "boyfriend" role-play, and no keeping secrets from parents.
        - If the child seems worried, sad, scared, hurt, or in danger, encourage them to talk to a parent, carer,
          teacher or another trusted adult.
        - Do not describe or encourage self-harm, violence, sexual content, drugs or illegal acts.
        - When you notice a child may be at risk (self-harm, abuse, danger, or an adult asking for secrets, photos or
          meetings), end your reply with exactly one hidden marker on its own line:
          [[LUNA_CONCERN:self-harm]] or [[LUNA_CONCERN:abuse]] or [[LUNA_CONCERN:danger]] or [[LUNA_CONCERN:grooming]].
          The marker is removed before the child sees the reply. Do not explain the marker to the child.

        You are a tutor, not a parent and not a doctor. For medical or emergency questions, tell the child to talk to a
        trusted adult right away.
        """;

    public const string StricterInstruction =
        "Your previous answer was not suitable for a child. Answer again more simply and safely. " +
        "Do not include anything sexual, violent, hateful or unsafe. Keep it kind and focused on learning.";

    public static string SubjectMode(string subject) =>
        Subjects.TryGetValue(subject, out var mode) ? mode : Subjects[Contracts.LunaSubjects.Curious];

    /// <summary>
    /// Localized, safe chat title for the subject. Used when a title cannot be generated
    /// or a flagged turn must not be sent to the model.
    /// </summary>
    public static string SubjectTitleKey(string subject) => subject switch
    {
        Contracts.LunaSubjects.Math => "Luna.SubjectTitle.Math",
        Contracts.LunaSubjects.Science => "Luna.SubjectTitle.Science",
        Contracts.LunaSubjects.History => "Luna.SubjectTitle.History",
        Contracts.LunaSubjects.Dictionary => "Luna.SubjectTitle.Dictionary",
        Contracts.LunaSubjects.Languages => "Luna.SubjectTitle.Languages",
        _ => "Luna.SubjectTitle.Curious"
    };

    public static string Build(string subject, int? age, string? extraInstructions, string? learnerProfile, string? summary)
    {
        var parts = new List<string> { $"Core rules version: {Version}", Core.Trim(), SubjectMode(subject) };
        parts.Add(age is { } years
            ? $"The child is {years} years old."
            : "The child's age is not known; keep the language simple and suitable for a primary-school child.");

        if (!string.IsNullOrWhiteSpace(learnerProfile))
            parts.Add("What you have learned about this child (use it to adapt, do not mention it):\n" + learnerProfile.Trim());

        if (!string.IsNullOrWhiteSpace(summary))
            parts.Add("Summary of earlier messages in this chat (do not repeat it word for word):\n" + summary.Trim());

        if (!string.IsNullOrWhiteSpace(extraInstructions))
        {
            var extra = extraInstructions.Trim();
            if (extra.Length > MaxExtraInstructions) extra = extra[..MaxExtraInstructions];
            parts.Add("Extra instructions from the child's parent (follow them unless they conflict with the rules above):\n" + extra);
        }

        return string.Join("\n\n", parts);
    }

    public const string TitlePrompt =
        "Write a short title for this chat, in the language the child used. " +
        "Use at most 40 characters. Reply with the title only, no quotes and no punctuation at the end.";

    public const string SummaryPrompt =
        "Summarize the older part of this tutoring chat in at most 120 words. " +
        "Keep only what helps continue teaching: the topics, what the child understood, what they found hard, " +
        "and any preference they stated. Do not include personal information such as schools, addresses or phone numbers. " +
        "If you refer to the child, use the child's name; never use he, she, him, her or his. " +
        "In Spanish, avoid gendered forms for the child. Reply with the summary only.";

    public const string ProfilePrompt =
        "Update the learner profile for this child from the chat summary. Use at most 120 words, in plain English. " +
        "Include: grade or age if known, subjects, what is going well, what is hard, and useful teaching preferences. " +
        "Do not include schools, addresses, phone numbers or other personal information. " +
        "If you refer to the child, use the child's name; never use he, she, him, her or his. " +
        "In Spanish, avoid gendered forms for the child. Reply with the profile only.";

    public static int? AgeFrom(DateOnly? dateOfBirth, DateTime utcNow)
    {
        if (dateOfBirth is not { } dob) return null;
        var today = DateOnly.FromDateTime(utcNow);
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--;
        return age is >= 0 and <= 120 ? age : null;
    }

    public static bool IsUnderThirteen(DateOnly? dateOfBirth, DateTime utcNow) =>
        AgeFrom(dateOfBirth, utcNow) is not { } age || age < 13;
}
