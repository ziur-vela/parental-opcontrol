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
        var uiScale = ArgDouble(args, "--ui-scale");
        Loc.SetLanguage(language);
        ApplicationConfiguration.Initialize();
        Console.WriteLine(Theme.FontCheckReport());

        using var form = new LockForm(new Rectangle(0, 0, width, height), true)
        {
            PreviewMode = true,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(0, 0),
            UiScaleOverride = uiScale
        };
        var es = language.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var state = new RuntimeLockStateDto(
            Guid.Empty, "Ana", es ? "Mamá" : "Mom", true, MapReason(reason),
            reason is "bedtime" or "schedule" ? (es ? "Hora de dormir" : "Bedtime") : null,
            reason is "bedtime" or "schedule" ? DateTime.Today.AddDays(1).AddHours(7).ToUniversalTime() : null,
            null, null, null, DateTime.UtcNow, Language: language);
        form.UpdateState(state);

        var capture = Arg(args, "--capture");
        if (args.Any(a => string.Equals(a, "--layout-check", StringComparison.OrdinalIgnoreCase)))
        {
            form.Show();
            Application.DoEvents();
            var issues = form.ValidateLayout();
            Console.WriteLine($"layout-check {form.ClientSize.Width}x{form.ClientSize.Height} " +
                              $"ui={form.UiScaleOverride?.ToString("0.##") ?? "dpi"} lang={language} issues={issues.Count}");
            foreach (var issue in issues) Console.WriteLine("  " + issue);
            Environment.ExitCode = issues.Count == 0 ? 0 : 1;
            form.Close();
            return;
        }
        if (args.Any(a => string.Equals(a, "--messages-preview", StringComparison.OrdinalIgnoreCase)))
        {
            var messages = DemoMessages(es);
            using var panel = new LockMessagesForm(messages)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(0, 0)
            };
            if (!string.IsNullOrWhiteSpace(capture))
            {
                panel.Show();
                Application.DoEvents();
                Thread.Sleep(250);
                using var bitmap = new Bitmap(panel.ClientSize.Width, panel.ClientSize.Height);
                panel.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                bitmap.Save(capture, ImageFormat.Png);
                panel.Close();
                return;
            }
            panel.Show();
            Application.Run(panel);
            return;
        }
        if (!string.IsNullOrWhiteSpace(capture))
        {
            Capture(form, capture);
            return;
        }

        form.Show();
        Application.Run(form);
    }

    /// <summary>Captures the real LockForm paint path (the same one the overlay uses).</summary>
    private static void Capture(LockForm form, string path)
    {
        form.Show();
        Application.DoEvents();
        Thread.Sleep(250);
        var issues = form.ValidateLayout();
        Console.WriteLine($"capture {form.ClientSize.Width}x{form.ClientSize.Height} " +
                          $"ui={form.UiScaleOverride?.ToString("0.##") ?? "dpi"} issues={issues.Count}");
        foreach (var issue in issues) Console.WriteLine("  " + issue);
        if (issues.Count > 0) Environment.ExitCode = 1;
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Save(path, ImageFormat.Png);
        form.Close();
    }

    private static List<ChildMessageDto> DemoMessages(bool es)
    {
        var child = Guid.NewGuid();
        var now = DateTime.UtcNow;
        return
        [
            new(child, child, null, MessageDirection.ParentToChild, null, es ? "Mamá" : "Mom",
                es ? "¡Hola! ¿Cómo estuvo la escuela?" : "Hello! How was school?", null, false, false,
                now.AddMinutes(-35), now.AddMinutes(-35), now.AddMinutes(-30), null, null,
                MessageStatus.Seen, false),
            new(child, child, null, MessageDirection.ChildToParent, null, string.Empty,
                es ? "¡Bien! Hicimos fracciones." : "It was good! We did fractions.", null, false, false,
                now.AddMinutes(-30), now.AddMinutes(-29), null, null, now.AddMinutes(-28),
                MessageStatus.Seen, false),
            new(child, child, null, MessageDirection.ParentToChild, null, es ? "Mamá" : "Mom",
                es ? "Sí, después de terminar la tarea. Te agregué 15 minutos extra." : "Yes, after you finish homework. I added 15 minutes of bonus time.",
                null, false, true, now.AddMinutes(-10), now.AddMinutes(-10), null, null, null,
                MessageStatus.Delivered, false)
        ];
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

    private static double? ArgDouble(string[] args, string name) =>
        double.TryParse(Arg(args, name), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;
}
#endif
