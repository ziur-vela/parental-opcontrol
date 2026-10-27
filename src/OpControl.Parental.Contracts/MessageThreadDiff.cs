// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text;

namespace OpControl.Parental.Contracts;

/// <summary>What changed between two reads of the same child/parent message thread.</summary>
public sealed record MessageThreadDelta(
    IReadOnlyList<ChildMessageDto> Final,
    IReadOnlyList<ChildMessageDto> Added,
    IReadOnlyList<ChildMessageDto> Updated,
    IReadOnlyList<Guid> Removed,
    bool OrderChanged)
{
    public bool Changed => Added.Count > 0 || Updated.Count > 0 || Removed.Count > 0 || OrderChanged;
}

/// <summary>
/// Compares message polls so the child windows can update rows in place. A poll that
/// returns the same thread must not clear or rebuild the list (Job 19i flicker).
/// </summary>
public static class MessageThreadDiff
{
    public static IReadOnlyList<ChildMessageDto> Normalize(IEnumerable<ChildMessageDto>? messages) =>
        (messages ?? [])
        .Where(message => !message.Deleted)
        .OrderBy(message => message.CreatedUtc)
        .ThenBy(message => message.Id)
        .ToList();

    public static string Signature(IReadOnlyList<ChildMessageDto> normalized)
    {
        var builder = new StringBuilder(normalized.Count * 96);
        foreach (var message in normalized)
        {
            builder.Append(message.Id).Append('|')
                .Append((int)message.Direction).Append('|')
                .Append(message.SenderName).Append('|')
                .Append(message.Text).Append('|')
                .Append(message.Preset).Append('|')
                .Append(message.SpeakAloud ? '1' : '0').Append('|')
                .Append(message.RequireAck ? '1' : '0').Append('|')
                .Append(message.CreatedUtc.Ticks).Append('|')
                .Append(message.DeliveredUtc?.Ticks).Append('|')
                .Append(message.SeenUtc?.Ticks).Append('|')
                .Append(message.AcknowledgedUtc?.Ticks).Append('|')
                .Append(message.ParentReadUtc?.Ticks).Append('|')
                .Append((int)message.Status).Append('|')
                .Append(message.DeliveredBy).Append('|')
                .Append(message.SeenBy).Append('|')
                .Append(message.AcknowledgedBy).Append('\n');
        }

        return builder.ToString();
    }

    public static MessageThreadDelta Compare(
        IEnumerable<ChildMessageDto>? previousMessages,
        IEnumerable<ChildMessageDto>? currentMessages)
    {
        var previous = previousMessages as IReadOnlyList<ChildMessageDto> ?? Normalize(previousMessages);
        var current = Normalize(currentMessages);

        var previousById = previous.ToDictionary(message => message.Id);
        var currentById = current.ToDictionary(message => message.Id);

        var added = current.Where(message => !previousById.ContainsKey(message.Id)).ToList();
        var updated = current
            .Where(message => previousById.TryGetValue(message.Id, out var old) && !SameRow(old, message))
            .ToList();
        var removed = previous
            .Where(message => !currentById.ContainsKey(message.Id))
            .Select(message => message.Id)
            .ToList();

        var commonPrevious = previous.Where(message => currentById.ContainsKey(message.Id))
            .Select(message => message.Id).ToList();
        var commonCurrent = current.Where(message => previousById.ContainsKey(message.Id))
            .Select(message => message.Id).ToList();
        var orderChanged = !commonPrevious.SequenceEqual(commonCurrent);

        return new MessageThreadDelta(current, added, updated, removed, orderChanged);
    }

    private static bool SameRow(ChildMessageDto a, ChildMessageDto b) =>
        a.Direction == b.Direction
        && string.Equals(a.SenderName, b.SenderName, StringComparison.Ordinal)
        && string.Equals(a.Text, b.Text, StringComparison.Ordinal)
        && string.Equals(a.Preset, b.Preset, StringComparison.Ordinal)
        && a.SpeakAloud == b.SpeakAloud
        && a.RequireAck == b.RequireAck
        && a.CreatedUtc == b.CreatedUtc
        && a.DeliveredUtc == b.DeliveredUtc
        && a.SeenUtc == b.SeenUtc
        && a.AcknowledgedUtc == b.AcknowledgedUtc
        && a.ParentReadUtc == b.ParentReadUtc
        && a.Status == b.Status
        && string.Equals(a.DeliveredBy, b.DeliveredBy, StringComparison.Ordinal)
        && string.Equals(a.SeenBy, b.SeenBy, StringComparison.Ordinal)
        && string.Equals(a.AcknowledgedBy, b.AcknowledgedBy, StringComparison.Ordinal);
}
