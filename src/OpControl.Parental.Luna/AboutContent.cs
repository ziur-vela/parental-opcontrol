// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Reflection;
using System.Text.Json;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Luna;

/// <summary>
/// Embedded legal content for the About screen. The license is the repository
/// LICENSE file; the credits are generated at build time from the real package
/// graph (see tools/generate-third-party-credits.cs).
/// </summary>
public static class AboutContent
{
    private const string LicenseResource = "OpControl.Parental.Legal.LICENSE.txt";
    private const string CreditsResource = "OpControl.Parental.ThirdPartyCredits.json";
    private const string LogoResource = "OpControl.Parental.Brand.icon-color-128.png";

    public const string LicenseName = "PolyForm Noncommercial License 1.0.0";
    public const string CopyrightLine = "© 2026 Ziur / OpControl IT Solutions";
    public const string CreatedByLine = "Created by Ziur · OpControl IT";

    private static readonly Lazy<string> LazyLicense = new(ReadLicense);
    private static readonly Lazy<ThirdPartyCreditsDto> LazyCredits = new(ReadCredits);

    public static string LicenseText => LazyLicense.Value;
    public static ThirdPartyCreditsDto Credits => LazyCredits.Value;

    public static Stream? OpenLogo() => Assembly.GetExecutingAssembly().GetManifestResourceStream(LogoResource);

    private static string ReadLicense()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LicenseResource);
        if (stream is null) return string.Empty;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static ThirdPartyCreditsDto ReadCredits()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(CreditsResource);
            if (stream is null) return new ThirdPartyCreditsDto("project.assets.json", []);
            using var reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<ThirdPartyCreditsDto>(reader.ReadToEnd(),
                       new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true })
                   ?? new ThirdPartyCreditsDto("project.assets.json", []);
        }
        catch
        {
            return new ThirdPartyCreditsDto("project.assets.json", []);
        }
    }
}
