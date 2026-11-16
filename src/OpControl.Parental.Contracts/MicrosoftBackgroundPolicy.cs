// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Contracts;

/// <summary>
/// Minimal registry surface used by the "Reduce Microsoft background
/// connections" switch. Kept platform-neutral so the policy decision can be
/// unit-tested on Linux while the Windows client supplies the real registry.
/// </summary>
public interface IPolicyValueStore
{
    void SetDword(string keyPath, string valueName, int value);
    void DeleteValue(string keyPath, string valueName);
}

/// <summary>
/// Microsoft Edge Experimentation and Configuration Service policy. The parent
/// switch turns it off (DWORD 0) and, when switched off, the value is deleted
/// rather than set to 1 or 2 so Edge returns to its own default.
/// </summary>
public static class MicrosoftBackgroundPolicyRules
{
    public const string EdgePolicyKey = @"SOFTWARE\Policies\Microsoft\Edge";
    public const string ValueName = "ExperimentationAndConfigurationServiceControl";

    /// <summary>0 when reduced, null when the value must be removed.</summary>
    public static int? DesiredValue(bool reduce) => reduce ? 0 : null;

    public static void Apply(bool reduce, IPolicyValueStore store)
    {
        if (DesiredValue(reduce) is { } value)
            store.SetDword(EdgePolicyKey, ValueName, value);
        else
            store.DeleteValue(EdgePolicyKey, ValueName);
    }
}
