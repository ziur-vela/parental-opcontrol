// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Drawing.Imaging;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

public sealed record KidPreviewData(ChildSummaryDto Summary, RuntimeLockStateDto State, bool Online, IReadOnlyList<ChildMessageDto>? Messages = null);

#if DEBUG
/// <summary>Debug-only demo of every My OpControl screen. No files, service or settings are touched.</summary>
internal static class KidPreview
{
    public static void Run(string[] args)
    {
        var language = Arg(args, "--language");
        if (!string.IsNullOrWhiteSpace(language)) Loc.SetLanguage(language);
        var theme = string.Equals(Arg(args, "--theme"), "gray", StringComparison.OrdinalIgnoreCase) ? "gray" : "night";
        var tab = (Arg(args, "--tab") ?? "home") switch
        {
            "schedule" => MyOpControlTab.Schedule,
            "rules" => MyOpControlTab.Rules,
            "activity" => MyOpControlTab.Activity,
            "messages" => MyOpControlTab.Messages,
            _ => MyOpControlTab.Home
        };
        var data = Build(theme);
        ApplicationConfiguration.Initialize();
        using var form = new MyOpControlForm(false, tab, false, theme, language, data);
        var sub = Arg(args, "--sub");
        if (string.Equals(sub, "apps", StringComparison.OrdinalIgnoreCase)) form.ShowRulesApps();
        if (string.Equals(sub, "week", StringComparison.OrdinalIgnoreCase)) form.ShowActivityWeek();
        var capture = Arg(args, "--capture");
        if (!string.IsNullOrWhiteSpace(capture))
        {
            var width = int.TryParse(Arg(args, "--width"), out var w) ? w : 1040;
            var height = int.TryParse(Arg(args, "--height"), out var h) ? h : 700;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-4000, -4000);
            form.ShowInTaskbar = false;
            form.ClientSize = new Size(width, height);
            form.Show();
            form.PerformLayout();
            Application.DoEvents();
            Thread.Sleep(400);
            using var bitmap = new Bitmap(width, height);
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
            bitmap.Save(capture, ImageFormat.Png);
            form.Close();
            return;
        }
        Application.Run(form);
    }

    private static KidPreviewData Build(string theme)
    {
        var now = DateTime.UtcNow;
        var today = now.ToLocalTime().Date;
        var nextLock = today.AddHours(20).AddMinutes(30);
        if (nextLock <= DateTime.Now) nextLock = nextLock.AddDays(1);
        var child = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var device = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var schedules = new[]
        {
            new SummaryScheduleDto(Guid.Parse("33333333-3333-3333-3333-333333333331"), "Bedtime", 127, 20 * 60 + 30, 7 * 60, true),
            new SummaryScheduleDto(Guid.Parse("33333333-3333-3333-3333-333333333332"), "School time", 62, 8 * 60, 13 * 60 + 30, true)
        };
        var categories = new[]
        {
            new SummaryCategoryDto("adult", "Adult content"),
            new SummaryCategoryDto("gambling", "Gambling"),
            new SummaryCategoryDto("social", "Social media")
        };
        var websites = new[]
        {
            new SummaryWebsiteDto("khanacademy.org", true, true, null),
            new SummaryWebsiteDto("classroom.google.com", true, true, null),
            new SummaryWebsiteDto("wikipedia.org", true, true, null),
            new SummaryWebsiteDto("youtube.com", false, false, "School time"),
            new SummaryWebsiteDto("tiktok.com", false, true, null),
            new SummaryWebsiteDto("roblox.com", false, false, "School time"),
            new SummaryWebsiteDto("discord.com", false, true, null),
            new SummaryWebsiteDto("twitch.tv", false, true, null)
        };
        var apps = new[]
        {
            new SummaryAppDto("discord.exe", "Discord", false, false, null, null, 0),
            new SummaryAppDto("roblox.exe", "Roblox", false, false, "School time", 45, 0),
            new SummaryAppDto("steam.exe", "Steam", false, false, null, null, 0),
            new SummaryAppDto("minecraft.exe", "Minecraft", true, false, null, 60, 21 * 60),
            new SummaryAppDto("calc.exe", "Calculator", true, true, null, null, 6 * 60),
            new SummaryAppDto("onlyoffice.exe", "ONLYOFFICE", true, true, null, null, 12 * 60),
            new SummaryAppDto("notepad.exe", "Notepad", true, true, null, null, 0)
        };
        var appsToday = new[]
        {
            new SummaryAppUsageDto("brave.exe", "Brave", 42 * 60),
            new SummaryAppUsageDto("minecraft.exe", "Minecraft", 21 * 60),
            new SummaryAppUsageDto("onlyoffice.exe", "ONLYOFFICE", 12 * 60),
            new SummaryAppUsageDto("calc.exe", "Calculator", 6 * 60)
        };
        var appsWeek = new[]
        {
            new SummaryAppUsageDto("brave.exe", "Brave", 5 * 3600 + 10 * 60),
            new SummaryAppUsageDto("minecraft.exe", "Minecraft", 3 * 3600 + 2 * 60),
            new SummaryAppUsageDto("onlyoffice.exe", "ONLYOFFICE", 1 * 3600 + 40 * 60),
            new SummaryAppUsageDto("calc.exe", "Calculator", 22 * 60),
            new SummaryAppUsageDto("paint.exe", "Paint", 18 * 60)
        };
        var visits = new List<SummaryVisitDto>
        {
            new("khanacademy.org", today.AddHours(10).AddMinutes(38), false),
            new("youtube.com", today.AddHours(10).AddMinutes(31), true),
            new("wikipedia.org", today.AddHours(10).AddMinutes(12), false),
            new("classroom.google.com", today.AddHours(9).AddMinutes(55), false),
            new("coolmathgames.com", today.AddHours(9).AddMinutes(40), false),
            new("tiktok.com", today.AddHours(9).AddMinutes(22), true),
            new("google.com", today.AddHours(9).AddMinutes(5), false)
        };
        var history = new[]
        {
            new SummaryHistoryDto("bonus", 15, today.AddHours(11).AddMinutes(15)),
            new SummaryHistoryDto("unlocked", null, today.AddHours(13).AddMinutes(30)),
            new SummaryHistoryDto("locked", null, today.AddHours(8)),
            new SummaryHistoryDto("unlocked", null, today.AddHours(7)),
            new SummaryHistoryDto("locked", null, today.AddDays(-1).AddHours(20).AddMinutes(30))
        };
        var messages = new List<ChildMessageDto>
        {
            new(Guid.NewGuid(), child, device, MessageDirection.ParentToChild, null, "Dad",
                "hello", null, false, false, today.AddHours(10).AddMinutes(11),
                today.AddHours(10).AddMinutes(11), null, null, null, MessageStatus.Delivered, false),
            new(Guid.NewGuid(), child, device, MessageDirection.ChildToParent, null, "Ana",
                "Hi! Can I play Minecraft after homework?", null, false, false, today.AddHours(10).AddMinutes(12),
                today.AddHours(10).AddMinutes(12), null, null, today.AddHours(10).AddMinutes(14), MessageStatus.Seen, false),
            new(Guid.NewGuid(), child, device, MessageDirection.ParentToChild, null, "Dad",
                "Yes, after you finish math. I added 15 minutes of bonus time.", null, false, false,
                today.AddHours(10).AddMinutes(15), today.AddHours(10).AddMinutes(15), today.AddHours(10).AddMinutes(16),
                null, null, MessageStatus.Seen, false),
            new(Guid.NewGuid(), child, device, MessageDirection.ChildToParent, null, "Ana",
                "Thank you!!", null, false, false, today.AddHours(10).AddMinutes(20),
                today.AddHours(10).AddMinutes(20), null, null, null, MessageStatus.Delivered, false),
            new(Guid.NewGuid(), child, device, MessageDirection.ChildToParent, null, "Ana",
                "Done with math", null, false, false, today.AddHours(10).AddMinutes(22),
                null, null, null, null, MessageStatus.Sent, false),
            new(Guid.NewGuid(), child, device, MessageDirection.ChildToParent, null, "Ana",
                "Can you check it?", null, false, false, today.AddHours(10).AddMinutes(24),
                null, null, null, null, MessageStatus.Sent, false)
        };
        var weekdays = new[]
        {
            new SummaryWeekdayDto(1, 180), new SummaryWeekdayDto(2, 180), new SummaryWeekdayDto(3, 360),
            new SummaryWeekdayDto(4, 180), new SummaryWeekdayDto(5, 240), new SummaryWeekdayDto(6, 360),
            new SummaryWeekdayDto(0, 360)
        };
        var summary = new ChildSummaryDto(
            child, "Ana", "X1 laptop", now, true, 360, 81 * 60, 10 * 3600 + 32 * 60,
            weekdays, schedules, categories, websites, apps, appsToday, appsWeek, visits, history,
            messages.Take(1).ToList(), 1, theme);
        var state = new RuntimeLockStateDto(
            child, "Ana", "Dad", false, "", null, null, nextLock.ToUniversalTime(), null, null, now,
            279 * 60, 360, false, null, "en", null, true, theme);
        return new KidPreviewData(summary, state, true, messages);
    }

    private static string? Arg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}
#endif
