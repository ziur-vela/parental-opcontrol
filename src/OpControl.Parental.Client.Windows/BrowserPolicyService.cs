// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Client;

/// <summary>
/// Writes the child-only browser policy while that Windows account is signed in,
/// re-applies it if it is removed and removes it cleanly on uninstall.
/// </summary>
public sealed class BrowserPolicyService
{
    private readonly LockRuntime _runtime;
    private readonly ClientSettings _settings;
    private readonly DataSyncService _sync;
    private readonly ILogger<BrowserPolicyService> _logger;
    private readonly BrowserPolicyManager _manager;
    private readonly object _gate = new();
    private string _lastSignature = "";
    private DateTime _lastVerify = DateTime.MinValue;
    private bool _wasActive;

    public BrowserPolicyService(LockRuntime runtime, ClientSettings settings, DataSyncService sync, ILogger<BrowserPolicyService> logger)
        : this(runtime, settings, sync, logger,
            new BrowserPolicyManager(new WindowsPolicyRegistry(), new BrowserPolicyBaselineFile()))
    {
    }

    public BrowserPolicyService(LockRuntime runtime, ClientSettings settings, DataSyncService sync,
        ILogger<BrowserPolicyService> logger, BrowserPolicyManager manager)
    {
        _runtime = runtime;
        _settings = settings;
        _sync = sync;
        _logger = logger;
        _manager = manager;
    }

    public void Sync()
    {
        if (!OperatingSystem.IsWindows()) return;
        lock (_gate)
        {
            var account = _settings.Value.MonitoredAccount;
            if (string.IsNullOrWhiteSpace(account)) return;

            var session = WindowsSession.GetMonitoredSessionId(account);
            if (session == 0)
            {
                _wasActive = false;
                return;
            }

            var sid = ResolveSid(account);
            if (sid is null) return;

            var state = _runtime.DeviceState;
            var desired = BrowserPolicyRules.BuildPatterns(state?.BrowserPolicy);
            var signature = sid + "|" + string.Join('\n', desired);
            var now = DateTime.UtcNow;

            if (signature != _lastSignature || !_wasActive)
            {
                _manager.Apply(sid, desired);
                _lastSignature = signature;
                _lastVerify = now;
                _wasActive = true;
                _logger.LogInformation("Browser policy applied for {Account}: {Count} pattern(s)", account, desired.Count);
                return;
            }

            if (now - _lastVerify < TimeSpan.FromSeconds(15)) return;
            _lastVerify = now;
            if (desired.Count == 0) return;
            if (!_manager.VerifyAndRepair(sid, desired)) return;
            _logger.LogWarning("Browser policy removed or edited for {Account}; re-applied", account);
            _sync.QueueTamper(new TamperEventDto("browser-policy-removed", "", now));
        }
    }

    /// <summary>Restores the pre-existing policy values before the client is uninstalled.</summary>
    public void RemoveAll()
    {
        if (!OperatingSystem.IsWindows()) return;
        lock (_gate)
        {
            var account = _settings.Value.MonitoredAccount;
            if (string.IsNullOrWhiteSpace(account)) return;
            var sid = ResolveSid(account);
            if (sid is null) return;
            // The child may be signed out during uninstall; load their hive briefly
            // so our entries are removed from their NTUSER.DAT too.
            using var hive = WindowsUserHive.Open(sid);
            _manager.Apply(sid, [], removeAll: true);
            _lastSignature = "";
            _wasActive = false;
            _logger.LogInformation("Browser policy removed for {Account}", account);
        }
    }

    private static string? ResolveSid(string account)
    {
        try
        {
            var name = account.Contains('\\') ? account : $"{Environment.MachineName}\\{account}";
            return new NTAccount(name).Translate(typeof(SecurityIdentifier)).Value;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// Loads a signed-out user's registry hive only for uninstall cleanup. When the
/// account is signed in the hive is already loaded and nothing is done.
/// </summary>
public sealed class WindowsUserHive : IDisposable
{
    private readonly string _sid;
    private readonly bool _loaded;

    private WindowsUserHive(string sid, bool loaded)
    {
        _sid = sid;
        _loaded = loaded;
    }

    public static WindowsUserHive Open(string sid)
    {
        try
        {
            using var existing = Registry.Users.OpenSubKey(sid);
            if (existing is not null) return new WindowsUserHive(sid, false);
        }
        catch
        {
        }

        try
        {
            using var profileKey = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid}");
            var profile = profileKey?.GetValue("ProfileImagePath") as string;
            if (string.IsNullOrWhiteSpace(profile)) return new WindowsUserHive(sid, false);
            var file = Path.Combine(profile, "NTUSER.DAT");
            if (!File.Exists(file)) return new WindowsUserHive(sid, false);
            if (RunReg($@"load HKU\{sid} ""{file}""")) return new WindowsUserHive(sid, true);
        }
        catch
        {
        }
        return new WindowsUserHive(sid, false);
    }

    public void Dispose()
    {
        if (_loaded) RunReg($@"unload HKU\{_sid}");
    }

    private static bool RunReg(string arguments)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null) return false;
            if (!process.WaitForExit(15000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>HKEY_USERS implementation: never HKLM, and never another user's hive.</summary>
public sealed class WindowsPolicyRegistry : IPolicyRegistry
{
    public IReadOnlyList<string>? GetMultiString(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.Users.OpenSubKey(keyPath);
            var value = key?.GetValue(valueName);
            return value switch
            {
                string[] list => list,
                string single => [single],
                _ => null
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    public void SetMultiString(string keyPath, string valueName, IReadOnlyList<string> values)
    {
        try
        {
            using var key = Registry.Users.CreateSubKey(keyPath, writable: true);
            key?.SetValue(valueName, values.ToArray(), RegistryValueKind.MultiString);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
    }

    public void DeleteValue(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.Users.OpenSubKey(keyPath, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
    }
}

/// <summary>Remembers the URLBlocklist entries that existed before our first write.</summary>
public sealed class BrowserPolicyBaselineFile : IBrowserPolicyBaselineStore
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Load()
    {
        try
        {
            if (!File.Exists(ClientPaths.BrowserPolicyStateFile)) return new Dictionary<string, IReadOnlyList<string>>();
            var data = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
                File.ReadAllText(ClientPaths.BrowserPolicyStateFile), ClientOptions.JsonOptions);
            return data is null
                ? new Dictionary<string, IReadOnlyList<string>>()
                : data.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, IReadOnlyList<string>>();
        }
    }

    public void Save(IReadOnlyDictionary<string, IReadOnlyList<string>> baselines)
    {
        try
        {
            Directory.CreateDirectory(ClientPaths.PrivateDirectory);
            SecureFile.SystemOnlyDirectory(ClientPaths.PrivateDirectory);
            var temp = ClientPaths.BrowserPolicyStateFile + ".tmp";
            var data = baselines.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
            File.WriteAllText(temp, JsonSerializer.Serialize(data, ClientOptions.JsonOptions));
            SecureFile.SystemOnly(temp);
            File.Move(temp, ClientPaths.BrowserPolicyStateFile, overwrite: true);
            SecureFile.SystemOnly(ClientPaths.BrowserPolicyStateFile);
        }
        catch
        {
        }
    }
}
