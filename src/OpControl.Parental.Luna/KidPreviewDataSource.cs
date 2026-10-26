#if LUNA_DEV
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Luna;

/// <summary>Demo data for --kid-preview screenshots. Never reads or writes the device state.</summary>
public sealed class KidPreviewDataSource : IKidDataSource
{
    private readonly List<ChildMessageDto> _messages;
    private readonly ChildSummaryDto _summary;
    private readonly RuntimeLockStateDto _state;
    private string _theme;

    public KidPreviewDataSource(string theme = "night")
    {
        _theme = theme;
        var now = DateTime.UtcNow;
        var today = now.ToLocalTime().Date;
        var child = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var device = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var nextLock = today.AddHours(20).AddMinutes(30);
        if (nextLock <= DateTime.Now) nextLock = nextLock.AddDays(1);

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
            new SummaryWebsiteDto("discord.com", false, true, null)
        };
        var apps = new[]
        {
            new SummaryAppDto("discord.exe", "Discord", false, false, null, null, 0),
            new SummaryAppDto("roblox.exe", "Roblox", false, false, "School time", 45, 12 * 60),
            new SummaryAppDto("steam.exe", "Steam", false, false, null, null, 0),
            new SummaryAppDto("minecraft.exe", "Minecraft", true, false, null, 60, 21 * 60),
            new SummaryAppDto("calc.exe", "Calculator", true, true, null, null, 6 * 60),
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
            new("tiktok.com", today.AddHours(9).AddMinutes(22), true)
        };
        var history = new[]
        {
            new SummaryHistoryDto("bonus", 15, today.AddHours(11).AddMinutes(15)),
            new SummaryHistoryDto("unlocked", null, today.AddHours(13).AddMinutes(30)),
            new SummaryHistoryDto("locked", null, today.AddHours(8)),
            new SummaryHistoryDto("unlocked", null, today.AddHours(7)),
            new SummaryHistoryDto("locked", null, today.AddDays(-1).AddHours(20).AddMinutes(30))
        };
        _messages =
        [
            new(Guid.NewGuid(), child, device, MessageDirection.ParentToChild, null, "Dad",
                "Hello! How was school?", null, false, false, today.AddHours(10).AddMinutes(11),
                today.AddHours(10).AddMinutes(11), today.AddHours(10).AddMinutes(12), null, null,
                MessageStatus.Seen, false),
            new(Guid.NewGuid(), child, device, MessageDirection.ChildToParent, null, "Ana",
                "It was good! We did fractions.", null, false, false, today.AddHours(10).AddMinutes(12),
                today.AddHours(10).AddMinutes(12), null, null, today.AddHours(10).AddMinutes(14),
                MessageStatus.Seen, false),
            new(Guid.NewGuid(), child, device, MessageDirection.ParentToChild, null, "Dad",
                "Yes, after you finish math. I added 15 minutes of bonus time.", null, false, true,
                today.AddHours(10).AddMinutes(15), today.AddHours(10).AddMinutes(15),
                today.AddHours(10).AddMinutes(16), null, null, MessageStatus.Seen, false),
            new(Guid.NewGuid(), child, device, MessageDirection.ChildToParent, null, "Ana",
                "Thank you!!", null, false, false, today.AddHours(10).AddMinutes(20),
                today.AddHours(10).AddMinutes(20), null, null, null, MessageStatus.Delivered, false)
        ];
        var weekdays = new[]
        {
            new SummaryWeekdayDto(1, 180), new SummaryWeekdayDto(2, 180), new SummaryWeekdayDto(3, 360),
            new SummaryWeekdayDto(4, 180), new SummaryWeekdayDto(5, 240), new SummaryWeekdayDto(6, 360),
            new SummaryWeekdayDto(0, 360)
        };
        _summary = new ChildSummaryDto(
            child, "Ana", "X1 laptop", now, true, 360, 81 * 60, 10 * 3600 + 32 * 60,
            weekdays, schedules, categories, websites, apps, appsToday, appsWeek, visits, history,
            _messages.Take(1).ToList(), 1, _theme);
        _state = new RuntimeLockStateDto(
            child, "Ana", "Dad", false, "", null, null, nextLock.ToUniversalTime(), null, null, now,
            279 * 60, 360, false, null, "en", null, true, _theme);
    }

    public Task<KidSnapshot> LoadAsync(CancellationToken ct) =>
        Task.FromResult(new KidSnapshot(_summary, _state, _messages.ToList(), true));

    public Task<PinPipeResponse> SendMessageAsync(string text, CancellationToken ct)
    {
        var message = new ChildMessageDto(Guid.NewGuid(), _summary.ChildId, null,
            MessageDirection.ChildToParent, null, _summary.ChildName, text, null, false, false,
            DateTime.UtcNow, null, null, null, null, MessageStatus.Sent, false);
        _messages.Add(message);
        return Task.FromResult(new PinPipeResponse(true, Message: message));
    }

    public Task MarkSeenAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
    public Task MarkAcknowledgedAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;

    public Task SetThemeAsync(string theme, CancellationToken ct)
    {
        _theme = theme;
        return Task.CompletedTask;
    }
}

#endif
