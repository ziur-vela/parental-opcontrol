// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Runtime.InteropServices;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Client;

/// <summary>
/// Cheap system-resource sampler for the Devices page. No WMI polling: one
/// GetSystemTimes / GlobalMemoryStatusEx / DriveInfo / GetSystemPowerStatus
/// call per sample, and no process or network data.
/// </summary>
public sealed class SystemMetricsCollector
{
    private long _previousIdle;
    private long _previousKernel;
    private long _previousUser;
    private bool _hasPreviousCpu;

    public DeviceMetricsDto Collect()
    {
        var (ramUsed, ramTotal) = Memory();
        var (diskFree, diskTotal) = SystemDrive();
        var (batteryPercent, batteryState) = Battery();
        return new DeviceMetricsDto(
            CpuPercent(),
            ramUsed,
            ramTotal,
            diskFree,
            diskTotal,
            batteryPercent,
            batteryState,
            Math.Max(0, Environment.TickCount64 / 1000),
            DateTime.UtcNow);
    }

    private int CpuPercent()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
            return 0;
        var idle = (long)idleTime.ToUInt64();
        var kernel = (long)kernelTime.ToUInt64();
        var user = (long)userTime.ToUInt64();
        if (!_hasPreviousCpu)
        {
            _previousIdle = idle;
            _previousKernel = kernel;
            _previousUser = user;
            _hasPreviousCpu = true;
            return 0;
        }
        var percent = DeviceMetricsMath.CpuPercent(_previousIdle, _previousKernel, _previousUser, idle, kernel, user);
        _previousIdle = idle;
        _previousKernel = kernel;
        _previousUser = user;
        return percent;
    }

    private static (long Used, long Total) Memory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status)) return (0, 0);
        var total = (long)Math.Min(status.TotalPhys, long.MaxValue);
        var available = (long)Math.Min(status.AvailPhys, long.MaxValue);
        return (Math.Max(0, total - available), total);
    }

    private static (long Free, long Total) SystemDrive()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(root);
            return drive.IsReady ? (drive.AvailableFreeSpace, drive.TotalSize) : (0, 0);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static (int? Percent, BatteryPowerState State) Battery()
    {
        if (!GetSystemPowerStatus(out var status))
            return (null, BatteryPowerState.NoBattery);
        var state = DeviceMetricsMath.BatteryState(status.AcLineStatus, status.BatteryFlag, status.BatteryLifePercent);
        return state == BatteryPowerState.NoBattery ? (null, state) : (status.BatteryLifePercent, state);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
