// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job22Tests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private static DeviceMetricsDto Metrics(
        int cpu = 10,
        long ramUsed = 4L * 1024 * 1024 * 1024,
        long ramTotal = 8L * 1024 * 1024 * 1024,
        long diskFree = 100L * 1024 * 1024 * 1024,
        long diskTotal = 200L * 1024 * 1024 * 1024,
        int? battery = 80,
        BatteryPowerState batteryState = BatteryPowerState.Charging,
        long uptime = 3600) =>
        new(cpu, ramUsed, ramTotal, diskFree, diskTotal, battery, batteryState, uptime, Now);

    [Theory]
    [InlineData(0, 8, 50, BatteryPowerState.OnBattery)]
    [InlineData(1, 8, 50, BatteryPowerState.Charging)]
    [InlineData(1, 2, 100, BatteryPowerState.PluggedInFull)]
    [InlineData(1, 128, 255, BatteryPowerState.NoBattery)]
    [InlineData(1, 255, 255, BatteryPowerState.NoBattery)]
    [InlineData(0, 128, 255, BatteryPowerState.NoBattery)]
    public void Battery_fallbacks_cover_desktops_and_charging(byte acLine, byte flags, byte percent, BatteryPowerState expected) =>
        Assert.Equal(expected, DeviceMetricsMath.BatteryState(acLine, flags, percent));

    [Fact]
    public void Cpu_and_capacity_math_handle_first_sample_and_zero_totals()
    {
        Assert.Equal(67, DeviceMetricsMath.CpuPercent(100, 200, 100, 200, 400, 200));
        Assert.Equal(0, DeviceMetricsMath.CpuPercent(100, 200, 100, 100, 200, 100));
        Assert.Equal(0, DeviceMetricsMath.CpuPercent(0, 0, 0, 0, 0, 0));
        Assert.Equal(75, DeviceMetricsMath.RamPercent(6, 8));
        Assert.Equal(0, DeviceMetricsMath.RamPercent(0, 0));
        Assert.Equal(8, DeviceMetricsMath.DiskFreePercent(8, 100));
        Assert.Equal(92, DeviceMetricsMath.DiskUsedPercent(8, 100));
    }

    [Fact]
    public void Sustained_cpu_creates_one_episode_and_ends_once()
    {
        var evaluator = new DeviceWarningEvaluator();
        var device = Guid.NewGuid();

        Assert.Empty(evaluator.Evaluate(device, Metrics(cpu: 95), Now));
        Assert.Empty(evaluator.Evaluate(device, Metrics(cpu: 96), Now.AddMinutes(1)));

        var started = Assert.Single(evaluator.Evaluate(device, Metrics(cpu: 97), Now.AddMinutes(2)));
        Assert.Equal(DeviceWarningEventType.Started, started.Type);
        Assert.Equal(DeviceWarningKind.Cpu, started.Kind);
        Assert.Equal(Now, started.StartedUtc);
        Assert.Equal(97, started.ExtremeValue);

        var updated = Assert.Single(evaluator.Evaluate(device, Metrics(cpu: 99), Now.AddMinutes(3)));
        Assert.Equal(DeviceWarningEventType.Updated, updated.Type);
        Assert.Equal(99, updated.ExtremeValue);

        var ended = Assert.Single(evaluator.Evaluate(device, Metrics(cpu: 10), Now.AddMinutes(4)));
        Assert.Equal(DeviceWarningEventType.Ended, ended.Type);
        Assert.Equal(99, ended.ExtremeValue);
        Assert.Equal(240, ended.DurationSeconds);

        Assert.Empty(evaluator.Evaluate(device, Metrics(cpu: 10), Now.AddMinutes(5)));
        Assert.False(evaluator.HasEpisode(device, DeviceWarningKind.Cpu));
    }

    [Fact]
    public void A_short_cpu_spike_is_not_a_warning()
    {
        var evaluator = new DeviceWarningEvaluator();
        var device = Guid.NewGuid();
        Assert.Empty(evaluator.Evaluate(device, Metrics(cpu: 95), Now));
        Assert.Empty(evaluator.Evaluate(device, Metrics(cpu: 95), Now.AddMinutes(1)));
        Assert.Empty(evaluator.Evaluate(device, Metrics(cpu: 5), Now.AddMinutes(2)));
        Assert.False(evaluator.HasEpisode(device, DeviceWarningKind.Cpu));
    }

    [Fact]
    public void Battery_and_disk_warn_immediately_and_only_once_per_episode()
    {
        var evaluator = new DeviceWarningEvaluator();
        var device = Guid.NewGuid();

        var first = evaluator.Evaluate(device,
            Metrics(battery: 8, batteryState: BatteryPowerState.OnBattery, diskFree: 8, diskTotal: 100), Now);
        Assert.Equal(DeviceWarningEventType.Started, Assert.Single(first, e => e.Kind == DeviceWarningKind.Battery).Type);
        Assert.Equal(DeviceWarningEventType.Started, Assert.Single(first, e => e.Kind == DeviceWarningKind.Disk).Type);

        var after = evaluator.Evaluate(device,
            Metrics(battery: 8, batteryState: BatteryPowerState.OnBattery, diskFree: 8, diskTotal: 100), Now.AddMinutes(1));
        Assert.DoesNotContain(after, e => e.Type == DeviceWarningEventType.Started);

        var ended = evaluator.Evaluate(device,
            Metrics(battery: 50, batteryState: BatteryPowerState.Charging, diskFree: 50, diskTotal: 100), Now.AddMinutes(2));
        Assert.Contains(ended, e => e.Type == DeviceWarningEventType.Ended && e.Kind == DeviceWarningKind.Battery);
        Assert.Contains(ended, e => e.Type == DeviceWarningEventType.Ended && e.Kind == DeviceWarningKind.Disk);
        Assert.Equal(2, ended.Count);
    }

    [Fact]
    public void Battery_plugged_in_full_does_not_warn()
    {
        var evaluator = new DeviceWarningEvaluator();
        Assert.Empty(evaluator.Evaluate(Guid.NewGuid(),
            Metrics(battery: 5, batteryState: BatteryPowerState.PluggedInFull), Now));
    }

    [Fact]
    public void Restarting_the_server_does_not_duplicate_an_active_episode()
    {
        var device = Guid.NewGuid();
        var first = new DeviceWarningEvaluator();
        Assert.Empty(first.Evaluate(device, Metrics(cpu: 95), Now));
        var started = Assert.Single(first.Evaluate(device, Metrics(cpu: 95), Now.AddMinutes(2)));
        Assert.Equal(DeviceWarningEventType.Started, started.Type);
        // A seeded evaluator (fresh process) knows the episode already exists.
        Assert.False(first.HasEpisode(device, DeviceWarningKind.Ram));
        var seeded = new DeviceWarningEvaluator();
        seeded.Seed(device, DeviceWarningKind.Cpu, Now, 95, DeviceMetricsLimits.CpuPercent);
        Assert.Empty(seeded.Evaluate(device, Metrics(cpu: 90), Now.AddMinutes(3)));
    }

    [Fact]
    public void Warnings_are_retained_for_thirty_days()
    {
        Assert.True(DeviceWarningRetention.IsExpired(Now.AddDays(-31), Now));
        Assert.False(DeviceWarningRetention.IsExpired(Now.AddDays(-29), Now));
        Assert.Equal(Now.AddDays(-30), DeviceWarningRetention.Cutoff(Now));
    }

    [Fact]
    public void Offline_state_wins_over_waiting_and_ready()
    {
        Assert.Equal(DeviceMetricsState.Offline, DeviceMetricsMath.State(online: false, hasMetrics: true));
        Assert.Equal(DeviceMetricsState.Waiting, DeviceMetricsMath.State(online: true, hasMetrics: false));
        Assert.Equal(DeviceMetricsState.Ready, DeviceMetricsMath.State(online: true, hasMetrics: true));
    }

    [Fact]
    public void Footer_sidebar_and_about_strings_exist_in_both_languages()
    {
        var root = FindRoot();
        var en = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", "AppStrings.resx"));
        var es = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", "AppStrings.es-MX.resx"));

        Assert.Contains("Click for app info and credits", en, StringComparison.Ordinal);
        Assert.Contains("Haz clic para ver la información y los créditos", es, StringComparison.Ordinal);
        Assert.Contains(">VERSION<", en, StringComparison.Ordinal);
        Assert.Contains(">VERSIÓN<", es, StringComparison.Ordinal);
        Assert.Contains("<value>BETA</value>", en, StringComparison.Ordinal);
        Assert.Contains("<value>BETA</value>", es, StringComparison.Ordinal);
        Assert.Contains("© 2026 OpControl · License", en, StringComparison.Ordinal);
        Assert.Contains("© 2026 OpControl · Licencia", es, StringComparison.Ordinal);
        Assert.Contains("Parental OpControl is in beta: features may change.", en, StringComparison.Ordinal);
        Assert.Contains("Parental OpControl está en beta: las funciones pueden cambiar.", es, StringComparison.Ordinal);
    }

    [Fact]
    public void Device_page_strings_exist_in_both_languages()
    {
        var root = FindRoot();
        var en = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", "AppStrings.resx"));
        var es = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", "AppStrings.es-MX.resx"));
        foreach (var key in new[]
                 {
                     "Devices.Battery", "Devices.Uptime", "Devices.Temperature", "Devices.OfflineLastSeen",
                     "Devices.WaitingForMetrics", "Devices.UpdatedJustNow", "Devices.UpdatedSecondsAgo",
                     "Devices.Warnings", "Devices.WarningsNone", "Devices.WarningCpu", "Devices.WarningBattery",
                     "Alert.DeviceWarning.Title", "Settings.AlertOnDeviceWarnings"
                 })
        {
            Assert.Contains($"name=\"{key}\"", en, StringComparison.Ordinal);
            Assert.Contains($"name=\"{key}\"", es, StringComparison.Ordinal);
        }
        Assert.Contains("Sin conexión · visto por última vez", es, StringComparison.Ordinal);
        Assert.Contains("Próximamente", es, StringComparison.Ordinal);
    }

    [Fact]
    public void Layout_and_about_drop_the_old_footer_and_version_link()
    {
        var root = FindRoot();
        var layout = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Layout", "MainLayout.razor"));
        var about = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Shared", "AboutPanel.razor"));

        Assert.DoesNotContain("Layout.BuiltWithAi", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("ai-credit", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Layout.VersionDetails", layout, StringComparison.Ordinal);
        Assert.Contains("class=\"site-foot\" href=\"/about\"", layout, StringComparison.Ordinal);
        Assert.Contains("Layout.FooterAboutHint", layout, StringComparison.Ordinal);
        Assert.Contains("Layout.VersionLabel", layout, StringComparison.Ordinal);
        Assert.Contains("beta-badge", layout, StringComparison.Ordinal);
        Assert.Contains("Layout.Beta", layout, StringComparison.Ordinal);
        Assert.Contains("AboutPage.BetaBadge", about, StringComparison.Ordinal);
        Assert.Contains("AboutPage.BetaNotice", about, StringComparison.Ordinal);
        Assert.Contains("Layout.BuiltWithAi", about, StringComparison.Ordinal);
    }

    [Fact]
    public void Device_page_uses_real_metrics_and_warnings()
    {
        var root = FindRoot();
        var devices = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Pages", "Devices.razor"));
        Assert.DoesNotContain("device-gauges-faded", devices, StringComparison.Ordinal);
        Assert.DoesNotContain("Percent=\"42\"", devices, StringComparison.Ordinal);
        Assert.Contains("metrics.CpuPercent", devices, StringComparison.Ordinal);
        Assert.Contains("DeviceBattery", devices, StringComparison.Ordinal);
        Assert.Contains("Devices.Temperature", devices, StringComparison.Ordinal);
        Assert.Contains("Devices.WaitingForMetrics", devices, StringComparison.Ordinal);
        Assert.Contains("Devices.OfflineLastSeen", devices, StringComparison.Ordinal);
        Assert.Contains("Devices.Warnings", devices, StringComparison.Ordinal);
        Assert.Contains("DeviceMetricsFocusService", devices, StringComparison.Ordinal);
        Assert.Contains("MetricsFocus.EnterAsync", devices, StringComparison.Ordinal);
        Assert.Contains("MetricsFocus.LeaveAsync", devices, StringComparison.Ordinal);
    }

    [Fact]
    public void Client_reports_metrics_only_on_the_configured_cadence()
    {
        var root = FindRoot();
        var collector = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows", "SystemMetricsCollector.cs"));
        var worker = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows", "Worker.cs"));
        var hub = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Hubs", "ParentalHub.cs"));
        var focus = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Services", "DeviceMetricsFocusService.cs"));

        Assert.Contains("DeviceMetricsMath.BatteryState", collector, StringComparison.Ordinal);
        Assert.Contains("GetSystemTimes", collector, StringComparison.Ordinal);
        Assert.Contains("GetSystemPowerStatus", collector, StringComparison.Ordinal);
        Assert.Contains("DeviceHubMethods.MetricsModeChanged", worker, StringComparison.Ordinal);
        Assert.Contains("DeviceHubMethods.ReportMetrics", worker, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMinutes(5)", worker, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromSeconds(30)", worker, StringComparison.Ordinal);
        Assert.Contains("ReportMetrics", hub, StringComparison.Ordinal);
        Assert.Contains("MetricsModeChanged", focus, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
