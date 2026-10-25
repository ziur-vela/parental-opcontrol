// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Diagnostics;

namespace OpControl.Parental.Luna;

public static class LunaLog
{
    private static readonly object Gate = new();

    public static string LogPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "OpControlParental", "user");
            try
            {
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "luna.log");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "luna.log");
            }
        }
    }

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(LogPath,
                    $"{DateTime.UtcNow:o} pid={Environment.ProcessId} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }

    public static void Exception(string context, Exception ex)
    {
        Write($"{context} {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        Debug.WriteLine(ex);
    }
}
