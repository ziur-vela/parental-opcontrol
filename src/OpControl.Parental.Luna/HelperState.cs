// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;

namespace OpControl.Parental.Luna;

internal sealed record HelperLockState(bool IsLocked, string? Reason);

internal static class HelperState
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "OpControlParental", "public", "state.json");

    public static HelperLockState? Read()
    {
        try
        {
            if (!File.Exists(StatePath)) return null;
            using var stream = new FileStream(StatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var locked = root.TryGetProperty("IsLocked", out var l) && l.ValueKind == JsonValueKind.True
                || root.TryGetProperty("isLocked", out var l2) && l2.ValueKind == JsonValueKind.True;
            var reason = root.TryGetProperty("Reason", out var r) ? r.GetString()
                : root.TryGetProperty("reason", out var r2) ? r2.GetString() : null;
            return new HelperLockState(locked, reason);
        }
        catch { return null; }
    }
}
