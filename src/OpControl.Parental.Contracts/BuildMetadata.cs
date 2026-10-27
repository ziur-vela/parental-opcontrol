// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Reflection;

namespace OpControl.Parental.Contracts;

/// <summary>
/// Reads the version, commit and build date baked into an assembly. The build
/// date comes from the BuildDate assembly metadata (set by the release build);
/// development builds fall back to the assembly file timestamp.
/// </summary>
public static class BuildMetadata
{
    public static AboutInfoDto Read(Assembly assembly, string? serverVersion = null,
        string? serverBuildDate = null, string? serverDisplayVersion = null)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        var plus = informational.IndexOf('+');
        var version = plus >= 0 ? informational[..plus] : informational;
        var metadata = plus >= 0 ? informational[(plus + 1)..] : "";
        var commit = metadata.Split('.')[0];
        if (string.IsNullOrWhiteSpace(commit)) commit = null;
        var buildDate = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "BuildDate", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        if (string.IsNullOrWhiteSpace(buildDate) && !string.IsNullOrWhiteSpace(assembly.Location))
        {
            try { buildDate = File.GetLastWriteTimeUtc(assembly.Location).ToString("yyyy-MM-dd"); }
            catch { buildDate = null; }
        }
        return new AboutInfoDto(version.TrimStart('v'), buildDate ?? "unknown",
            commit is null ? null : (commit.Length > 12 ? commit[..12] : commit),
            serverVersion, serverBuildDate, serverDisplayVersion);
    }
}
