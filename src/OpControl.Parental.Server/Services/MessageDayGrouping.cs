// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Server.Services;

public sealed record MessageDayGroup<T>(DateOnly Date, string Id, string Label, IReadOnlyList<T> Items);

/// <summary>Groups a message thread by local calendar day (family time zone).</summary>
public static class MessageDayGrouping
{
    public static List<MessageDayGroup<T>> Group<T>(IEnumerable<T> items, Func<T, DateTime> createdUtc,
        TimeZoneInfo zone, DateOnly today, Func<DateOnly, DateOnly, string> label)
    {
        return items
            .Select(item => (Item: item, Local: TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(createdUtc(item), DateTimeKind.Utc), zone)))
            .GroupBy(pair => DateOnly.FromDateTime(pair.Local))
            .OrderBy(group => group.Key)
            .Select(group => new MessageDayGroup<T>(group.Key,
                "msg-day-" + group.Key.ToString("yyyy-MM-dd"),
                label(group.Key, today),
                group.Select(pair => pair.Item).ToList()))
            .ToList();
    }

    public static string Label(DateOnly date, DateOnly today, string todayText, string yesterdayText,
        System.Globalization.CultureInfo culture)
    {
        if (date == today) return todayText;
        if (date == today.AddDays(-1)) return yesterdayText;
        return date.ToString("ddd d MMM", culture);
    }
}
