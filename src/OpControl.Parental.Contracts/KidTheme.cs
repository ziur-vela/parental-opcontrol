// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;

namespace OpControl.Parental.Contracts;

/// <summary>
/// The child's single theme choice, shared by Luna and My OpControl.
/// The server copy is authoritative across devices; the local file makes a change
/// visible to both running apps immediately and works offline.
/// </summary>
public static class KidThemeStore
{
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpControlParental");

    public static string FilePath => Path.Combine(Directory, "kid-theme.txt");

    public static string Normalize(string? theme) => LunaThemes.Normalize(theme);

    public static string Toggle(string? theme) => LunaThemes.Toggle(theme);

    public static string? ReadLocal(string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            if (!File.Exists(file)) return null;
            var text = File.ReadAllText(file).Trim();
            return LunaThemes.IsValid(text) ? LunaThemes.Normalize(text) : null;
        }
        catch
        {
            return null;
        }
    }

    public static void WriteLocal(string? theme, string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var temp = file + ".tmp";
            File.WriteAllText(temp, LunaThemes.Normalize(theme));
            File.Move(temp, file, overwrite: true);
        }
        catch
        {
            // Theme persistence is cosmetic; never break the UI over it.
        }
    }
}

/// <summary>The tray "Open Luna" item is present only when Luna is enabled for the child.</summary>
public static class KidTrayPolicy
{
    public static bool ShowOpenLuna(bool lunaEnabled) => lunaEnabled;
}
