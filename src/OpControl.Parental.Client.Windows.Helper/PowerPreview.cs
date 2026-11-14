// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

#if DEBUG
using System.Drawing.Imaging;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

/// <summary>Development-only renderer used to capture the child power notice for docs.</summary>
internal static class PowerPreview
{
    public static void Run(string[] args)
    {
        var output = Get(args, "--output") ?? "power-notice.png";
        var action = Enum.TryParse<DevicePowerAction>(Get(args, "--action"), true, out var parsed)
            ? parsed
            : DevicePowerAction.Restart;
        var seconds = int.TryParse(Get(args, "--seconds"), out var value) ? value : 60;
        var language = Get(args, "--language") ?? "en";
        var theme = Get(args, "--theme") ?? "night";
        Loc.SetLanguage(language);
        KidThemeStore.WriteLocal(theme);

        ApplicationConfiguration.Initialize();
        using var form = new PowerNoticeForm();
        form.UpdateNotice(new PowerNotice(Guid.NewGuid(), action, DateTime.UtcNow.AddSeconds(seconds), seconds), DateTime.UtcNow);
        try
        {
            form.Show();
            Application.DoEvents();
        }
        catch
        {
            form.CreateControl();
        }
        Thread.Sleep(600);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
        bitmap.Save(output, ImageFormat.Png);
        form.HideNotice();
    }

    private static string? Get(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}
#endif
