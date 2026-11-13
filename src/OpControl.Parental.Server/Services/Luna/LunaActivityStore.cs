// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Collections.Concurrent;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Server.Services.Luna;

/// <summary>
/// Short-lived per-chat activity for the child app. The desktop client polls this
/// while a message is in flight so it can show the web-search animation only when
/// the server has actually sent the search tool.
/// </summary>
public sealed class LunaActivityStore
{
    public const string Searching = "searching";
    public const string Thinking = "thinking";
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(90);

    private sealed record Entry(Guid ChildId, string State, DateTime UpdatedUtc);
    private readonly ConcurrentDictionary<Guid, Entry> _chats = new();

    public void Set(Guid childId, Guid chatId, string state)
    {
        _chats[chatId] = new Entry(childId, state, DateTime.UtcNow);
        Cleanup();
    }

    public string Get(Guid childId, Guid chatId)
    {
        if (!_chats.TryGetValue(chatId, out var entry)) return LunaActivityState.Idle;
        if (entry.ChildId != childId || DateTime.UtcNow - entry.UpdatedUtc > Lifetime)
        {
            _chats.TryRemove(chatId, out _);
            return LunaActivityState.Idle;
        }
        return entry.State;
    }

    public void Clear(Guid chatId) => _chats.TryRemove(chatId, out _);

    private void Cleanup()
    {
        if (_chats.Count < 64) return;
        var cutoff = DateTime.UtcNow - Lifetime;
        foreach (var pair in _chats)
            if (pair.Value.UpdatedUtc < cutoff) _chats.TryRemove(pair.Key, out _);
    }
}

public static class LunaActivityState
{
    public const string Idle = "idle";
}

public static class LunaSearchIntent
{
    private static readonly string[] English =
    [
        "latest", "newest", "news", "today", "tonight", "current", "currently", "right now",
        "this week", "this month", "this year", "price", "weather", "score", "who won",
        "how tall", "population", "released", "update", "still", "2025", "2026", "2027",
        "what happened", "how much does", "how many people"
    ];

    private static readonly string[] Spanish =
    [
        "últim", "reciente", "noticias", "hoy", "esta semana", "este mes", "este año",
        "actual", "ahora", "precio", "clima", "resultado", "quién ganó", "cuánto mide",
        "población", "2025", "2026", "2027", "qué pasó", "cuánto cuesta"
    ];

    /// <summary>
    /// Conservative hint used only for the animation. The real signal is the
    /// provider's web_search_requests count; a miss here only means the child
    /// sees the normal thinking animation for a search turn.
    /// </summary>
    public static bool LikelyNeedsSearch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.ToLowerInvariant();
        return English.Any(value.Contains) || Spanish.Any(value.Contains);
    }
}
