// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services.Luna;

/// <summary>
/// The single mapping from a stored message to the text a chat provider may see.
/// Blocked and urgent child messages become the removal placeholder; Luna messages
/// use the text the child actually saw. Every provider request that replays stored
/// history must use this helper so the rules cannot drift apart.
/// </summary>
public static class LunaTranscript
{
    public static string ProviderText(LunaMessage message) =>
        message.Role == LunaChatRoles.Child
            ? IsFlaggedChild(message.Flag) ? LunaRemovePlaceholder.Text : message.Text
            : message.ShownText;

    public static bool IsFlaggedChild(LunaMessageFlag flag) =>
        flag is LunaMessageFlag.Blocked or LunaMessageFlag.Urgent;
}
