// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

/// <summary>Debug-only preview and screenshot renderer for the lock timer pill.</summary>
internal static class TimerPreview
{
    public static void Run(string[] args)
    {
        var state = Arg(args, "--timer-preview") ?? "show";
        var theme = (Arg(args, "--theme") ?? "night").ToLowerInvariant() is "gray" ? "gray" : "night";
        var language = Arg(args, "--language");
        Loc.SetLanguage(language);
        var outDir = Arg(args, "--out");
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
            foreach (var themeKey in new[] { "night", "gray" })
                foreach (var preview in new[] { "show", "tuck", "warn", "last" })
                    SaveScene(Path.Combine(outDir, $"pill-{preview}-{themeKey}.png"), preview, themeKey);
            return;
        }
        var framesDir = Arg(args, "--timer-frames");
        if (!string.IsNullOrWhiteSpace(framesDir))
        {
            SaveFrames(framesDir);
            return;
        }

        ApplicationConfiguration.Initialize();
        var remaining = Remaining(state);
        var settings = LockTimerSettings.Default with
        {
            Look = theme,
            Tuck = state is "show" or "tuck",
            TuckAfterSeconds = state == "tuck" ? 5 : 10
        };
        var form = new CountdownForm();
        form.Update(remaining, Loc.T("Client.Pill.LockIn"), settings, theme, Loc.Language);
        if (state == "tuck") form.ForceTuck(true);
        Application.Run(form);
    }

    private static TimeSpan Remaining(string state) => state switch
    {
        "warn" => new TimeSpan(0, 4, 12),
        "last" => new TimeSpan(0, 0, 42),
        "tuck" => new TimeSpan(1, 8, 45, 31),
        _ => new TimeSpan(1, 8, 45, 0)
    };

    private static void SaveFrames(string directory)
    {
        Directory.CreateDirectory(directory);
        var remaining = new TimeSpan(1, 8, 45, 0);
        var settings = LockTimerSettings.Default with { Look = "night", ShowSeconds = false };
        for (var i = 0; i <= 14; i++)
        {
            var slide = Ease(i / 14f);
            RenderFrame(Path.Combine(directory, $"tuck-{i:000}.png"), "night", LockTimerPhase.Normal,
                slide, i * 0.06, remaining, settings);
        }
        for (var i = 0; i <= 14; i++)
        {
            var slide = 1f - Ease(i / 14f);
            RenderFrame(Path.Combine(directory, $"untuck-{i:000}.png"), "night", LockTimerPhase.Normal,
                slide, 0.9 + i * 0.06, remaining, settings);
        }
        var lastSettings = LockTimerSettings.Default with { Look = "night", ShowSeconds = true };
        for (var i = 0; i < 12; i++)
            RenderFrame(Path.Combine(directory, $"shake-{i:000}.png"), "night", LockTimerPhase.Last,
                0f, i * (0.5 / 12), new TimeSpan(0, 0, 42), lastSettings);
    }

    private static float Ease(float t)
    {
        const float c1 = 1.70158f;
        var u = t - 1f;
        return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
    }

    private static void RenderFrame(string path, string theme, LockTimerPhase phase, float slide,
        double now, TimeSpan remaining, LockTimerSettings settings)
    {
        const int scale = 2;
        const int width = 900;
        const int height = 250;
        using var bitmap = new Bitmap(width * scale, height * scale);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(scale, scale);

        using (var backdrop = new LinearGradientBrush(
                   new Rectangle(0, 0, width, height),
                   ColorTranslator.FromHtml("#3b4a5e"),
                   ColorTranslator.FromHtml("#26303d"),
                   160f))
            graphics.FillRectangle(backdrop, 0, 0, width, height);

        using (var taskbar = new SolidBrush(ColorTranslator.FromHtml("#1c1f26")))
            graphics.FillRectangle(taskbar, 0, height - 44, width, 44);
        using (var clock = new SolidBrush(ColorTranslator.FromHtml("#d9dce4")))
        using (var font = Theme.Sans(12f, FontStyle.Bold))
            graphics.DrawString(DateTime.Now.ToString("h:mm tt") + "\n11/1/2026", font, clock,
                new RectangleF(width - 150, height - 40, 130, 36), new StringFormat
                {
                    Alignment = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center
                });

        var language = Loc.Language;
        var time = LockTimerFormat.Format(remaining, settings, language);
        var label = Loc.T("Client.Pill.LockIn");

        using var measureBitmap = new Bitmap(1, 1);
        using var measure = Graphics.FromImage(measureBitmap);
        var layout = PillArt.Measure(measure, label, time);
        var left = width - layout.Width + 28;
        var top = height - 44 - 16 - PillArt.Height;
        var saved = graphics.Save();
        graphics.TranslateTransform(left, top);
        PillArt.Draw(graphics, layout, new PillArt.Visual(theme, phase, label, time, slide, false,
            now, DateTime.Now));
        graphics.Restore(saved);

        bitmap.Save(path, ImageFormat.Png);
    }

    private static void SaveScene(string path, string state, string theme)
    {
        var settings = LockTimerSettings.Default with { Look = theme, ShowSeconds = state is "warn" or "last" };
        var phase = state switch
        {
            "warn" => LockTimerPhase.Warn,
            "last" => LockTimerPhase.Last,
            _ => LockTimerPhase.Normal
        };
        RenderFrame(path, theme, phase, state == "tuck" ? 1f : 0f,
            Environment.TickCount64 / 1000.0, Remaining(state), settings);
    }

    private static string? Arg(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return args[i][(name.Length + 1)..];
        }
        return null;
    }
}
