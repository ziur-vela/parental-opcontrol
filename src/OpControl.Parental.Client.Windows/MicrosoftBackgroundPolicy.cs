// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.Win32;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Client;

/// <summary>
/// Parent switch "Reduce Microsoft background connections". Writes or removes
/// only Edge's ExperimentationAndConfigurationServiceControl value under the
/// machine-wide Edge policy key; no other Edge policy is touched.
/// </summary>
public static class MicrosoftBackgroundPolicy
{
    private static bool? _lastApplied;

    public static void Apply(bool reduce, ILogger? logger = null, string context = "")
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            MicrosoftBackgroundPolicyRules.Apply(reduce, new WindowsPolicyValueStore());
            if (_lastApplied != reduce)
            {
                _lastApplied = reduce;
                logger?.LogInformation(
                    "Microsoft background connections reduced={Reduce}; Edge experimentation policy {Action} ({Context})",
                    reduce, reduce ? "set to 0" : "removed", context);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not apply the Microsoft background connections policy");
        }
    }

    /// <summary>Removes our policy value on uninstall so the machine is left clean.</summary>
    public static void Remove(ILogger? logger = null)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            new WindowsPolicyValueStore().DeleteValue(
                MicrosoftBackgroundPolicyRules.EdgePolicyKey, MicrosoftBackgroundPolicyRules.ValueName);
            _lastApplied = null;
            logger?.LogInformation("Microsoft background connections policy removed for uninstall");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not remove the Microsoft background connections policy");
        }
    }

    private sealed class WindowsPolicyValueStore : IPolicyValueStore
    {
        public void SetDword(string keyPath, string valueName, int value)
        {
            using var key = Registry.LocalMachine.CreateSubKey(keyPath, writable: true);
            key?.SetValue(valueName, value, RegistryValueKind.DWord);
        }

        public void DeleteValue(string keyPath, string valueName)
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}
