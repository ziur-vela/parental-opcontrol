#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property EnableTrimAnalyzer=false
#:property EnableAotAnalyzer=false
// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
//
// Build-time generator for the About screen's third-party credits and the
// installer's third-party notices. It reads NuGet's project.assets.json (the
// real dependency graph of the built project), the package .nuspec files and
// the bundled font folders. Nothing is hand-maintained.
//
// dotnet run tools/generate-third-party-credits.cs -- <assets> <packages> <tfm> <projectDir> <repoRoot> <outputDir>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

var argsList = args.ToList();
if (argsList.Count < 6)
{
    Console.Error.WriteLine("usage: generate-third-party-credits <assets1;assets2> <packagesRoot> <tfm1;tfm2> <projectDir> <repoRoot> <outputDir>");
    return 2;
}

var assetsPaths = argsList[0].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var packagesRoot = argsList[1];
var tfms = argsList[2].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var projectDir = argsList[3];
var repoRoot = argsList[4];
var outputDir = argsList[5];

foreach (var path in assetsPaths)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"assets file not found: {path}");
        return 3;
    }
}

Directory.CreateDirectory(outputDir);

// Union every target graph for the requested TFMs (the plain TFM and the
// RID-specific graph when publishing self-contained), so runtime packs are
// included too. Several projects may feed the installer's notices.
var usedPackages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var assetsPath in assetsPaths)
{
    var assets = JsonNode.Parse(File.ReadAllText(assetsPath))!.AsObject();
    var libraries = assets["libraries"]?.AsObject() ?? new JsonObject();
    var targets = assets["targets"]?.AsObject() ?? new JsonObject();
    foreach (var (targetName, targetValue) in targets)
    {
        if (!tfms.Any(tfm => targetName.Equals(tfm, StringComparison.OrdinalIgnoreCase) ||
                             targetName.StartsWith(tfm + "/", StringComparison.OrdinalIgnoreCase))) continue;
        if (targetValue is not JsonObject target) continue;
        foreach (var (key, value) in target)
        {
            if (!key.Contains('/')) continue;
            if (value is not JsonObject assetsForPackage) continue;
            if (libraries[key] is not JsonObject library || (string?)library["type"] != "package") continue;
            var bundled = assetsForPackage.ContainsKey("runtime") ||
                          assetsForPackage.ContainsKey("compile") ||
                          assetsForPackage.ContainsKey("runtimeTargets") ||
                          assetsForPackage.ContainsKey("native") ||
                          assetsForPackage.ContainsKey("resource") ||
                          assetsForPackage.ContainsKey("frameworkAssemblies");
            if (bundled)
                usedPackages[key] = (string?)library["path"] ?? key.ToLowerInvariant();
        }
    }
}

var credits = new List<Credit>();
var licenseTexts = new Dictionary<string, string>(StringComparer.Ordinal); // hash -> text
var licenseLabels = new Dictionary<string, string>(StringComparer.Ordinal); // hash -> label

foreach (var (key, packagePath) in usedPackages.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
{
    var slash = key.IndexOf('/');
    if (slash <= 0) continue;
    var id = key[..slash];
    var version = key[(slash + 1)..];
    var packageDir = Path.Combine(packagesRoot, packagePath.Replace('/', Path.DirectorySeparatorChar));
    var nuspecPath = Path.Combine(packageDir, $"{id}.nuspec");
    if (!File.Exists(nuspecPath) && Directory.Exists(packageDir))
    {
        var candidates = Directory.GetFiles(packageDir, "*.nuspec");
        if (candidates.Length > 0) nuspecPath = candidates[0];
    }

    var license = "Unknown";
    var licenseUrl = (string?)null;
    var copyright = (string?)null;
    var authors = (string?)null;
    if (File.Exists(nuspecPath))
    {
        try
        {
            var document = XDocument.Load(nuspecPath);
            var metadata = document.Root?.Elements().FirstOrDefault(element =>
                element.Name.LocalName.Equals("metadata", StringComparison.OrdinalIgnoreCase));
            string? Value(string name) => metadata?.Elements().FirstOrDefault(element =>
                element.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
            copyright = Value("copyright");
            authors = Value("authors");
            licenseUrl = Value("licenseUrl");
            var licenseElement = metadata?.Elements().FirstOrDefault(element =>
                element.Name.LocalName.Equals("license", StringComparison.OrdinalIgnoreCase));
            var type = (string?)licenseElement?.Attribute("type");
            var licenseValue = licenseElement?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(licenseValue))
            {
                if (type is not null && type.Equals("expression", StringComparison.OrdinalIgnoreCase))
                {
                    license = NormalizeExpression(licenseValue);
                }
                else
                {
                    var licenseFile = Path.Combine(packageDir, licenseValue.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(licenseFile))
                    {
                        var text = File.ReadAllText(licenseFile);
                        AddLicenseText(text, licenseFile);
                        license = DetectLicenseName(licenseValue + "\n" + text);
                    }
                    else
                    {
                        license = DetectLicenseName(licenseValue);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"warn: {id}: {ex.Message}");
        }
    }

    // Many packages ship a LICENSE/NOTICE file even when the nuspec uses an
    // expression. Include those texts too.
    if (Directory.Exists(packageDir))
    {
        foreach (var file in Directory.GetFiles(packageDir, "*", SearchOption.AllDirectories)
                     .Where(file => IsLicenseFile(Path.GetFileName(file)))
                     .OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var text = File.ReadAllText(file);
                if (text.Length is < 40 or > 400_000) continue;
                AddLicenseText(text, file);
                if (license == "Unknown") license = DetectLicenseName(Path.GetFileName(file) + "\n" + text);
            }
            catch { }
        }
    }

    if (license == "Unknown" && !string.IsNullOrWhiteSpace(licenseUrl))
        license = LicenseFromUrl(licenseUrl);
    if (string.IsNullOrWhiteSpace(copyright)) copyright = authors;

    credits.Add(new Credit(id, version, license, copyright, licenseUrl, "NuGet"));
}

// Bundled fonts.
var fontDirs = new[]
{
    Path.Combine(projectDir, "Assets", "Fonts"),
    Path.Combine(repoRoot, "src", "OpControl.Parental.Client.Windows.Helper", "fonts"),
    Path.Combine(repoRoot, "src", "OpControl.Parental.Setup", "fonts")
};
var seenFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var directory in fontDirs)
{
    if (!Directory.Exists(directory)) continue;
    foreach (var font in Directory.GetFiles(directory, "*.ttf").OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
    {
        var family = FontFamilyName(Path.GetFileNameWithoutExtension(font));
        if (family is null || !seenFonts.Add(family)) continue;
        var ofl = Directory.GetFiles(directory, "OFL-*.txt")
            .FirstOrDefault(file => Path.GetFileName(file).Contains(
                family.Replace(" ", "-", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase));
        ofl ??= Directory.GetFiles(directory, "OFL-*.txt").FirstOrDefault(file =>
            Path.GetFileName(file).Contains(family.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
        if (ofl is not null)
        {
            try
            {
                var text = File.ReadAllText(ofl);
                AddLicenseText(text, ofl);
            }
            catch { }
        }
        credits.Add(new Credit(family + " (font)", "", "SIL OFL 1.1", null, "https://openfontlicense.org", "Font"));
    }
}

var sorted = credits
    .GroupBy(credit => (credit.Name, credit.Version), StringTupleComparer.Instance)
    .Select(group => group.First())
    .OrderBy(credit => credit.Name, StringComparer.OrdinalIgnoreCase)
    .ThenBy(credit => credit.Version, StringComparer.OrdinalIgnoreCase)
    .ToList();

var creditsModel = new
{
    generatedFrom = "project.assets.json",
    components = sorted.Select(credit => new
    {
        name = credit.Name,
        version = credit.Version,
        license = credit.License,
        copyright = credit.Copyright,
        licenseUrl = credit.LicenseUrl
    }).ToList()
};
File.WriteAllText(Path.Combine(outputDir, "third-party-credits.json"),
    JsonSerializer.Serialize(creditsModel, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(Path.Combine(outputDir, "third-party-credits.md"), BuildMarkdown(sorted, licenseTexts, licenseLabels));

Console.WriteLine($"credits: {sorted.Count} components ({sorted.Count(c => c.Kind == "NuGet")} packages, {sorted.Count(c => c.Kind == "Font")} fonts)");
return 0;

void AddLicenseText(string text, string source)
{
    var normalized = text.Replace("\r\n", "\n").Trim();
    if (normalized.Length < 40) return;
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
    licenseTexts.TryAdd(hash, normalized);
    licenseLabels.TryAdd(hash, Path.GetFileName(source));
}

static bool IsLicenseFile(string name) =>
    name.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase) ||
    name.StartsWith("LICENCE", StringComparison.OrdinalIgnoreCase) ||
    name.StartsWith("COPYING", StringComparison.OrdinalIgnoreCase) ||
    name.StartsWith("NOTICE", StringComparison.OrdinalIgnoreCase);

static string NormalizeExpression(string expression) => expression.Trim();

static string DetectLicenseName(string value)
{
    var upper = value.ToUpperInvariant();
    if (upper.Contains("SIL OPEN FONT LICENSE") || upper.Contains("OFL-FREDOKA") || upper.Contains("OFL-NUNITO"))
        return "SIL OFL 1.1";
    if (upper.Contains("APACHE LICENSE") || upper.Contains("APACHE-2.0") || upper.Contains("LICENSE-2.0"))
        return "Apache-2.0";
    if (upper.Contains("MIT LICENSE") || upper.Contains("THE MIT LICENSE") || upper.Contains("PERMISSION IS HEREBY GRANTED"))
        return "MIT";
    if (upper.Contains("MOZILLA PUBLIC LICENSE")) return "MPL-2.0";
    if (upper.Contains("POSTGRESQL")) return "PostgreSQL";
    if (upper.Contains("MICROSOFT PUBLIC LICENSE")) return "MS-PL";
    if (upper.Contains("BSD 3-CLAUSE") || upper.Contains("BSD-3-CLAUSE") ||
        (upper.Contains("REDISTRIBUTION AND USE IN SOURCE AND BINARY FORMS") && upper.Contains("NEITHER THE NAME")))
        return "BSD-3-Clause";
    if (upper.Contains("BSD 2-CLAUSE") || upper.Contains("BSD-2-CLAUSE")) return "BSD-2-Clause";
    if (upper.Contains("ISC LICENSE")) return "ISC";
    return "Unknown";
}

static string LicenseFromUrl(string url)
{
    var lower = url.ToLowerInvariant();
    if (lower.Contains("mit")) return "MIT";
    if (lower.Contains("apache")) return "Apache-2.0";
    if (lower.Contains("bsd-3")) return "BSD-3-Clause";
    if (lower.Contains("bsd-2")) return "BSD-2-Clause";
    if (lower.Contains("mpl")) return "MPL-2.0";
    if (lower.Contains("ms-pl")) return "MS-PL";
    if (lower.Contains("isc")) return "ISC";
    return "See license URL";
}

static string? FontFamilyName(string file)
{
    if (file.StartsWith("Fredoka", StringComparison.OrdinalIgnoreCase)) return "Fredoka";
    if (file.StartsWith("Nunito", StringComparison.OrdinalIgnoreCase)) return "Nunito";
    if (file.StartsWith("IBMPlexMono", StringComparison.OrdinalIgnoreCase)) return "IBM Plex Mono";
    if (file.StartsWith("Manrope", StringComparison.OrdinalIgnoreCase)) return "Manrope";
    return null;
}

static string BuildMarkdown(List<Credit> credits, Dictionary<string, string> texts, Dictionary<string, string> labels)
{
    var builder = new StringBuilder();
    builder.AppendLine("# Third-party notices");
    builder.AppendLine();
    builder.AppendLine("Parental OpControl is distributed by OpControl IT Solutions. This file is");
    builder.AppendLine("generated at build time from the actual dependency graph and bundled fonts.");
    builder.AppendLine("Each component remains under its own license; nothing here changes those");
    builder.AppendLine("licenses.");
    builder.AppendLine();
    builder.AppendLine("## Components");
    builder.AppendLine();
    builder.AppendLine("| Component | Version | License | Kind |");
    builder.AppendLine("|---|---|---|---|");
    foreach (var credit in credits)
    {
        builder.AppendLine($"| {Escape(credit.Name)} | {Escape(credit.Version)} | {Escape(credit.License)} | {credit.Kind} |");
    }
    builder.AppendLine();
    builder.AppendLine("## Full license texts");
    builder.AppendLine();
    var index = 0;
    foreach (var (hash, text) in texts.OrderBy(pair => labels[pair.Key], StringComparer.OrdinalIgnoreCase))
    {
        index++;
        builder.AppendLine($"### {index}. {Escape(labels[hash])}");
        builder.AppendLine();
        builder.AppendLine("```text");
        builder.AppendLine(text);
        builder.AppendLine("```");
        builder.AppendLine();
    }
    return builder.ToString();
}

static string Escape(string value) => value.Replace("|", "\\|").Replace("\n", " ");

internal sealed record Credit(string Name, string Version, string License, string? Copyright, string? LicenseUrl, string Kind);

internal sealed class StringTupleComparer : IEqualityComparer<(string, string)>
{
    public static readonly StringTupleComparer Instance = new();
    public bool Equals((string, string) x, (string, string) y) =>
        string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);
    public int GetHashCode((string, string) value) =>
        HashCode.Combine(value.Item1.ToLowerInvariant(), value.Item2.ToLowerInvariant());
}
