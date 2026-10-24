// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.RegularExpressions;

namespace OpControl.Parental.Server.Services.Luna;

public sealed record LunaClassification(
    bool Blocked,
    bool Urgent,
    IReadOnlyList<string> Categories,
    IReadOnlyDictionary<string, double> Scores);

/// <summary>
/// The single place for Luna moderation thresholds and category mappings.
/// We do not trust the providers' plain "flagged" boolean: school topics such as
/// wars or the human body can be flagged with a low score, so each category has
/// its own tuned score threshold.
/// </summary>
public static class LunaSafety
{
    // OpenAI omni-moderation-latest, normal block thresholds.
    public const double OpenAiSexual = 0.55;
    public const double OpenAiViolence = 0.90;
    public const double OpenAiViolenceGraphic = 0.85;
    public const double OpenAiHate = 0.75;
    public const double OpenAiHateThreatening = 0.60;
    public const double OpenAiHarassment = 0.80;
    public const double OpenAiHarassmentThreatening = 0.65;
    public const double OpenAiIllicit = 0.80;
    public const double OpenAiIllicitViolent = 0.70;

    // OpenAI omni-moderation-latest, urgent thresholds.
    public const double OpenAiSelfHarm = 0.35;
    public const double OpenAiSelfHarmIntent = 0.30;
    public const double OpenAiSelfHarmInstructions = 0.30;
    public const double OpenAiSexualMinors = 0.25;

    public static readonly IReadOnlyDictionary<string, double> OpenAiBlockThresholds =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["sexual"] = OpenAiSexual,
            ["violence"] = OpenAiViolence,
            ["violence/graphic"] = OpenAiViolenceGraphic,
            ["hate"] = OpenAiHate,
            ["hate/threatening"] = OpenAiHateThreatening,
            ["harassment"] = OpenAiHarassment,
            ["harassment/threatening"] = OpenAiHarassmentThreatening,
            ["illicit"] = OpenAiIllicit,
            ["illicit/violent"] = OpenAiIllicitViolent
        };

    public static readonly IReadOnlyDictionary<string, double> OpenAiUrgentThresholds =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["self-harm"] = OpenAiSelfHarm,
            ["self-harm/intent"] = OpenAiSelfHarmIntent,
            ["self-harm/instructions"] = OpenAiSelfHarmInstructions,
            ["sexual/minors"] = OpenAiSexualMinors
        };

    // Llama Guard 4 (MLCommons categories), via OpenRouter.
    public static readonly IReadOnlySet<string> LlamaGuardUrgent =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "S3", "S4", "S11" };

    public static readonly IReadOnlySet<string> LlamaGuardBlock =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "S1", "S2", "S3", "S4", "S9", "S10", "S11", "S12", "S14" };

    // Not blocked on their own: S5 defamation, S6 specialized advice, S7 privacy,
    // S8 intellectual property, S13 elections. School topics frequently touch these.

    private static readonly Regex Marker = new(
        @"\[\[\s*LUNA_CONCERN\s*:\s*([A-Za-z0-9_-]+)\s*\]\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LlamaCategory = new(@"\bS\d{1,2}\b", RegexOptions.Compiled);
    public const string ConcernMarkerKind = "model-concern";

    public static LunaClassification ClassifyOpenAi(IReadOnlyDictionary<string, double> scores)
    {
        var blocked = new List<string>();
        var urgent = new List<string>();
        foreach (var (category, score) in scores)
        {
            if (OpenAiUrgentThresholds.TryGetValue(category, out var urgentThreshold) && score >= urgentThreshold)
                urgent.Add(category);
            else if (OpenAiBlockThresholds.TryGetValue(category, out var blockThreshold) && score >= blockThreshold)
                blocked.Add(category);
        }

        return new LunaClassification(blocked.Count > 0 || urgent.Count > 0, urgent.Count > 0,
            blocked.Concat(urgent).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), scores);
    }

    public static LunaClassification ClassifyLlamaGuard(string response)
    {
        var text = (response ?? "").Trim();
        var categories = LlamaCategory.Matches(text).Select(m => m.Value.ToUpperInvariant()).Distinct().ToList();
        var unsafeText = text.StartsWith("unsafe", StringComparison.OrdinalIgnoreCase)
            || (categories.Count > 0 && !text.StartsWith("safe", StringComparison.OrdinalIgnoreCase));
        return ClassifyCategories(categories, unsafeText);
    }

    public static LunaClassification ClassifyCategories(IEnumerable<string> categories, bool flagged)
    {
        var list = categories.Select(c => c.ToUpperInvariant()).Distinct().ToList();
        if (!flagged && list.Count == 0)
            return new LunaClassification(false, false, [], new Dictionary<string, double>());

        var scores = list.ToDictionary(c => c, _ => 1.0, StringComparer.OrdinalIgnoreCase);
        var urgent = list.Where(CategoryUrgent).ToList();
        // An "unsafe" answer with no parsable category is still blocked (fail closed).
        var blocked = urgent.Count > 0 || list.Count == 0 || list.Any(c => LlamaGuardBlock.Contains(c));
        return new LunaClassification(blocked, urgent.Count > 0, list, scores);
    }

    public static bool CategoryUrgent(string category) => LlamaGuardUrgent.Contains(category);

    /// <summary>Removes every hidden concern marker and returns the clean text plus the kinds found.</summary>
    public static (string Text, IReadOnlyList<string> Kinds) StripConcernMarkers(string text)
    {
        var kinds = Marker.Matches(text).Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToList();
        return (Marker.Replace(text, "").Trim(), kinds);
    }

    public static string Describe(IReadOnlyList<string> categories) =>
        categories.Count == 0 ? "" : string.Join(",", categories);
}
