// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

public static class LunaLaunchPolicy
{
    /// <summary>Luna runs only while the server says it is enabled and the device is unlocked.</summary>
    public static bool ShouldRun(bool lunaEnabled, bool deviceLocked) => lunaEnabled && !deviceLocked;
}

public static class LunaPipePolicy
{
    public const string TokenRequest = "luna-token";
    public const string KidSummaryRequest = "kid-summary";

    private static readonly HashSet<string> LunaRequests = new(StringComparer.Ordinal)
    {
        TokenRequest,
        KidSummaryRequest,
        "messages-thread",
        "message-send",
        "message-seen",
        "message-ack",
        "set-theme",
        "state"
    };

    public static bool IsImage(string? imagePath, string expectedPath) =>
        !string.IsNullOrWhiteSpace(imagePath)
        && string.Equals(Path.GetFileName(imagePath), Path.GetFileName(expectedPath), StringComparison.OrdinalIgnoreCase)
        && string.Equals(Path.GetFullPath(imagePath), Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The Luna executable may request the read-only kid summary, messages and its theme.
    /// PIN/override and window-launch requests stay Helper-only.
    /// </summary>
    public static bool IsRequestAllowed(bool isLunaImage, string? requestType) =>
        !isLunaImage || LunaRequests.Contains(requestType ?? "");

    public static bool IsAllowed(bool isHelperImage, bool isLunaImage, string? requestType) =>
        (isHelperImage || isLunaImage) && IsRequestAllowed(isLunaImage, requestType);
}

public static class LunaDevMode
{
    public const string FakeModeKey = "Luna:FakeMode";

    public static bool AllowsTokenEndpoint(bool isDevelopment, bool fakeMode) => isDevelopment && fakeMode;
}
