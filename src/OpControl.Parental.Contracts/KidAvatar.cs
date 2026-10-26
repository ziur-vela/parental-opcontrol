// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

/// <summary>The eight flat avatars the child can choose for their Luna chat bubbles.</summary>
public static class LunaAvatarKeys
{
    public const string Face = "face";
    public const string Cat = "cat";
    public const string Dog = "dog";
    public const string Fox = "fox";
    public const string Rocket = "rocket";
    public const string Star = "star";
    public const string Planet = "planet";
    public const string Robot = "robot";

    public static readonly string[] All =
    [
        Face, Cat, Dog, Fox, Rocket, Star, Planet, Robot
    ];

    public static bool IsValid(string? key) => key is not null && Array.IndexOf(All, key) >= 0;

    public static string Normalize(string? key) => IsValid(key) ? key! : Face;
}

/// <summary>Per-child local avatar choice, next to the theme file.</summary>
public static class KidAvatarStore
{
    public static string FilePath => Path.Combine(KidThemeStore.Directory, "kid-avatar.txt");

    public static string ReadLocal(string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            if (!File.Exists(file)) return LunaAvatarKeys.Face;
            return LunaAvatarKeys.Normalize(File.ReadAllText(file).Trim());
        }
        catch
        {
            return LunaAvatarKeys.Face;
        }
    }

    public static void WriteLocal(string? key, string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var temp = file + ".tmp";
            File.WriteAllText(temp, LunaAvatarKeys.Normalize(key));
            File.Move(temp, file, overwrite: true);
        }
        catch
        {
            // The avatar is cosmetic; never break the chat over it.
        }
    }
}
