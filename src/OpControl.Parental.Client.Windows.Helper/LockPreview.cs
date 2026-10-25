// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

#if DEBUG
using System.Drawing.Imaging;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

/// <summary>Development-only standalone preview of the lock screen.</summary>
internal static class LockPreview
{
    public static void Run(string[] args)
    {
        var width = ArgInt(args, "--width", 1600);
        var height = ArgInt(args, "--height", 900);
        var language = Arg(args, "--language") ?? "en";
        var reason = Arg(args, "--reason") ?? "parent";
        Loc.SetLanguage(language);
        ApplicationConfiguration.Initialize();

        using var form = new LockForm(new Rectangle(0, 0, width, height), true)
        {
            PreviewMode = true,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(0, 0)
        };
        var es = language.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var state = new RuntimeLockStateDto(
            Guid.Empty, "Ana", es ? "Mamá" : "Mom", true, MapReason(reason),
            reason is "bedtime" or "schedule" ? (es ? "Hora de dormir" : "Bedtime") : null,
            reason is "bedtime" or "schedule" ? DateTime.Today.AddDays(1).AddHours(7).ToUniversalTime() : null,
            null, null, null, DateTime.UtcNow, Language: language);
        form.UpdateState(state);

        var capture = Arg(args, "--capture");
        if (!string.IsNullOrWhiteSpace(capture))
        {
            LockPreviewRenderer.Render(capture, width, height, state);
            return;
        }

        var captureForm = Arg(args, "--capture-form");
        if (!string.IsNullOrWhiteSpace(captureForm))
        {
            form.Show();
            Application.DoEvents();
            Thread.Sleep(250);
            using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(captureForm, ImageFormat.Png);
            form.Close();
            return;
        }

        form.Show();
        Application.Run(form);
    }

    private static string MapReason(string reason) => reason switch
    {
        "parent" or "manual" => "manual",
        "bedtime" or "schedule" => "schedule",
        "daily-limit" => "daily-limit",
        "paused" => "paused",
        _ => "manual"
    };

    private static string? Arg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static int ArgInt(string[] args, string name, int fallback) =>
        int.TryParse(Arg(args, name), out var value) && value > 0 ? value : fallback;
}
#endif
