// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Server.Services;

/// <summary>
/// Third-party components shown on the dashboard About page. The package list is
/// generated at build time from the server and kid view dependency graphs (see
/// tools/generate-third-party-credits.cs); the PostgreSQL server is deployed
/// with the stack but is not a NuGet package.
/// </summary>
public static class AboutCredits
{
    private const string ResourceName = "OpControl.Parental.Server.ThirdPartyCredits.json";

    private static readonly Lazy<ThirdPartyCreditsDto> LazyCredits = new(Read);

    public static ThirdPartyCreditsDto Credits => LazyCredits.Value;

    public static string EmbeddedResourceName => ResourceName;

    private static ThirdPartyCreditsDto Read()
    {
        ThirdPartyCreditsDto? credits = null;
        try
        {
            using var stream = typeof(AboutCredits).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                credits = JsonSerializer.Deserialize<ThirdPartyCreditsDto>(reader.ReadToEnd(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
            }
        }
        catch
        {
            credits = null;
        }

        credits ??= new ThirdPartyCreditsDto("project.assets.json", []);
        var components = credits.Components.ToList();
        if (!components.Any(c => string.Equals(c.Name, "PostgreSQL (server)", StringComparison.OrdinalIgnoreCase)))
        {
            components.Add(new ThirdPartyCreditDto("PostgreSQL (server)", "17.6", "PostgreSQL",
                "Copyright (c) 1996-2025 The PostgreSQL Global Development Group",
                "https://www.postgresql.org/about/licence/"));
        }

        return credits with { Components = components };
    }
}
