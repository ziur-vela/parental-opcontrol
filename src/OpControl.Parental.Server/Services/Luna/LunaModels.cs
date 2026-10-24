// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Server.Services.Luna;

/// <summary>
/// The curated Setup A model list. Edit this one file when the ZDR catalog changes.
/// Prices are OpenRouter list prices per million input/output tokens and the monthly
/// estimate assumes 60 messages a day at about 1,500 input and 300 output tokens each.
/// </summary>
public sealed record LunaModelOption(
    string Id,
    string NameKey,
    string TierKey,
    string EstimateKey,
    decimal InputUsdPerMillion,
    decimal OutputUsdPerMillion);

public static class LunaModels
{
    public const string Other = "other";

    public static readonly IReadOnlyList<LunaModelOption> All =
    [
        new("anthropic/claude-haiku-5.5", "Luna.Model.Name.Haiku55", "Luna.Model.Tier.Recommended",
            "Luna.Model.Estimate.Haiku55", 0.10m, 0.50m),
        new("openai/gpt-5.6-luna", "Luna.Model.Name.Gpt56Luna", "Luna.Model.Tier.Budget",
            "Luna.Model.Estimate.Gpt56Luna", 0.20m, 1.20m),
        new("google/gemini-3.5-flash-lite", "Luna.Model.Name.Gemini35FlashLite", "Luna.Model.Tier.Budget",
            "Luna.Model.Estimate.Gemini35FlashLite", 0.30m, 2.50m),
        new("anthropic/claude-sonnet-5.5", "Luna.Model.Name.Sonnet55", "Luna.Model.Tier.Best",
            "Luna.Model.Estimate.Sonnet55", 2.00m, 10.00m)
    ];

    public static LunaModelOption? Find(string? id) =>
        All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
}
