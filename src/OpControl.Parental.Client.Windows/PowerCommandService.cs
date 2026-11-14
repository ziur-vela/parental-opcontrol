// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Client;

/// <summary>
/// Schedules restart / sleep / power-off commands received from the server,
/// writes the child-session notice, and reports accepted / done / failed.
/// Runs as SYSTEM.
/// </summary>
public sealed class PowerCommandService
{
    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint SePrivilegeEnabled = 0x00000002;
    private const uint ShutdownReason = 0x80020002; // planned operating-system maintenance
    private const uint ShutdownTimeoutSeconds = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(ClientOptions.JsonOptions);
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _pending = new();
    private readonly ILogger<PowerCommandService> _logger;

    public PowerCommandService(ILogger<PowerCommandService> logger)
    {
        _logger = logger;
    }

    public void Handle(DevicePowerCommandDto command, Func<DevicePowerResultDto, Task> report)
    {
        if (!_pending.TryAdd(command.Id, new CancellationTokenSource()))
            return;
        var cts = _pending[command.Id];
        WriteNotice(command);
        var accepted = new DevicePowerResultDto(command.Id, command.Action, DevicePowerStatus.Accepted, null, DateTime.UtcNow);
        _ = report(accepted);

        _ = Task.Run(async () =>
        {
            try
            {
                var delay = command.RunAtUtc - DateTime.UtcNow;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cts.Token);
                DeleteNotice(command.Id);
                await report(new DevicePowerResultDto(command.Id, command.Action, DevicePowerStatus.Done, null, DateTime.UtcNow));
                var (ok, detail) = Execute(command.Action);
                if (!ok)
                {
                    _logger.LogWarning("Power action {Action} failed: {Detail}", command.Action, detail ?? "");
                    await report(new DevicePowerResultDto(command.Id, command.Action, DevicePowerStatus.Failed, detail, DateTime.UtcNow));
                }
            }
            catch (OperationCanceledException)
            {
                // The server records the cancellation; no device result is required.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Power action {Action} failed", command.Action);
                try
                {
                    await report(new DevicePowerResultDto(command.Id, command.Action, DevicePowerStatus.Failed, ex.Message, DateTime.UtcNow));
                }
                catch { }
            }
            finally
            {
                if (_pending.TryRemove(command.Id, out var active)) active.Dispose();
                DeleteNotice(command.Id);
            }
        });
    }

    public void Cancel(Guid commandId)
    {
        if (_pending.TryRemove(commandId, out var cts))
        {
            try { cts.Cancel(); } catch { }
            cts.Dispose();
        }
        DeleteNotice(commandId);
        _logger.LogInformation("Power command {CommandId} cancelled by the server", commandId);
    }

    private static (bool Ok, string? Detail) Execute(DevicePowerAction action)
    {
        if (action == DevicePowerAction.Sleep)
        {
            if (SetSuspendState(false, false, false)) return (true, null);
            return (false, new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }

        if (!EnableShutdownPrivilege())
            return (false, new Win32Exception(Marshal.GetLastWin32Error()).Message);
        var restart = action == DevicePowerAction.Restart;
        return InitiateSystemShutdownEx(null, null, ShutdownTimeoutSeconds, true, restart, ShutdownReason)
            ? (true, null)
            : (false, new Win32Exception(Marshal.GetLastWin32Error()).Message);
    }

    private static bool EnableShutdownPrivilege()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
            return false;
        try
        {
            if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out var luid))
                return false;
            var privileges = new TokenPrivileges
            {
                PrivilegeCount = 1,
                Privileges = new LuidAndAttributes { Luid = luid, Attributes = SePrivilegeEnabled }
            };
            if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
                return false;
            return Marshal.GetLastWin32Error() == 0;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static void WriteNotice(DevicePowerCommandDto command)
    {
        try
        {
            Directory.CreateDirectory(ClientPaths.UserDirectory);
            var data = new
            {
                id = command.Id,
                action = command.Action.ToString(),
                runAtUtc = command.RunAtUtc,
                noticeSeconds = command.NoticeSeconds
            };
            var temp = ClientPaths.PowerNoticeFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(data, JsonOptions));
            File.Move(temp, ClientPaths.PowerNoticeFile, overwrite: true);
        }
        catch { }
    }

    private static void DeleteNotice(Guid commandId)
    {
        try
        {
            if (!File.Exists(ClientPaths.PowerNoticeFile)) return;
            using var document = JsonDocument.Parse(File.ReadAllText(ClientPaths.PowerNoticeFile));
            if (document.RootElement.TryGetProperty("id", out var id) && Guid.TryParse(id.GetString(), out var current)
                && current != commandId)
                return;
            File.Delete(ClientPaths.PowerNoticeFile);
        }
        catch
        {
            try { File.Delete(ClientPaths.PowerNoticeFile); } catch { }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LuidAndAttributes
    {
        public Luid Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public LuidAndAttributes Privileges;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges,
        ref TokenPrivileges newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool InitiateSystemShutdownEx(string? machineName, string? message, uint timeout,
        bool forceAppsClosed, bool rebootAfterShutdown, uint reason);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
