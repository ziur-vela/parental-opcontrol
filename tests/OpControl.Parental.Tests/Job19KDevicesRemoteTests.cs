// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job19KDevicesRemoteTests
{
    private static ParentalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("job19k-" + Guid.NewGuid())
            .Options);

    [Theory]
    [InlineData("US Mountain Standard Time", "America/Phoenix")]
    [InlineData("Mountain Standard Time (Mexico)", "America/Chihuahua")]
    [InlineData("Central Standard Time (Mexico)", "America/Mexico_City")]
    [InlineData("Pacific Standard Time", "America/Los_Angeles")]
    [InlineData("America/Hermosillo", "America/Hermosillo")]
    [InlineData("UTC", "UTC")]
    public void Windows_ids_map_to_the_expected_iana_ids(string windowsId, string expected)
    {
        Assert.Equal(expected, TimeZoneResolver.ToIanaId(windowsId));
    }

    [Fact]
    public void Windows_display_name_is_available_for_the_automatic_label()
    {
        var name = TimeZoneResolver.DisplayName("US Mountain Standard Time");
        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.Contains("Mountain", name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reported_device_zone_applies_until_a_parent_overrides_it()
    {
        var device = new Device();
        DeviceTimeZonePolicy.ApplyReported(device, "US Mountain Standard Time");

        Assert.Equal("US Mountain Standard Time", device.ReportedTimeZoneId);
        Assert.Equal("America/Phoenix", device.TimeZoneId);
        Assert.Null(device.TimeZoneOverrideId);

        DeviceTimeZonePolicy.ApplyOverride(device, "America/Denver");
        Assert.Equal("America/Denver", device.TimeZoneOverrideId);
        Assert.Equal("America/Denver", device.TimeZoneId);

        DeviceTimeZonePolicy.ApplyReported(device, "Pacific Standard Time");
        Assert.Equal("Pacific Standard Time", device.ReportedTimeZoneId);
        Assert.Equal("America/Denver", device.TimeZoneId);
        Assert.Equal("America/Denver", device.TimeZoneOverrideId);

        DeviceTimeZonePolicy.ApplyOverride(device, null);
        Assert.Null(device.TimeZoneOverrideId);
        Assert.Equal("America/Los_Angeles", device.TimeZoneId);
    }

    [Fact]
    public async Task Family_zone_is_explicit_when_set_otherwise_the_most_recent_device()
    {
        await using var db = NewDb();
        var family = new Family { Name = "Zones" };
        db.Families.Add(family);
        db.Devices.AddRange(
            new Device
            {
                FamilyId = family.Id,
                ChildId = Guid.NewGuid(),
                Name = "Older laptop",
                TimeZoneId = "America/Los_Angeles",
                LastSeenUtc = new DateTime(2026, 10, 26, 1, 0, 0, DateTimeKind.Utc)
            },
            new Device
            {
                FamilyId = family.Id,
                ChildId = Guid.NewGuid(),
                Name = "Current laptop",
                TimeZoneId = "America/Phoenix",
                LastSeenUtc = new DateTime(2026, 10, 27, 16, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();

        var automatic = await DashboardTime.FamilyZoneInfoAsync(db, family.Id);
        Assert.True(automatic.Automatic);
        Assert.Equal("America/Phoenix", automatic.Zone.Id);
        Assert.Equal("Current laptop", automatic.DeviceName);

        db.FamilySettings.Add(new FamilySettings { FamilyId = family.Id, TimeZoneId = "America/New_York" });
        await db.SaveChangesAsync();
        var explicitZone = await DashboardTime.FamilyZoneInfoAsync(db, family.Id);
        Assert.False(explicitZone.Automatic);
        Assert.Equal("America/New_York", explicitZone.Zone.Id);
        Assert.Equal("America/New_York", explicitZone.ExplicitId);
    }

    [Fact]
    public void Device_icon_and_offline_remote_policy_are_correct()
    {
        Assert.Equal("WINDOWS", DevicePresentation.OsLabel(DevicePlatform.Windows));
        Assert.Equal("LINUX", DevicePresentation.OsLabel(DevicePlatform.Linux));
        Assert.Equal("ANDROID", DevicePresentation.OsLabel(DevicePlatform.Android));
        Assert.True(DevicePresentation.IsPhone(DevicePlatform.Android));
        Assert.False(DevicePresentation.IsPhone(DevicePlatform.Windows));
        Assert.True(DevicePresentation.CanStartRemote(true));
        Assert.False(DevicePresentation.CanStartRemote(false));

        var root = FindRoot();
        var icon = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Shared", "DeviceIcon.razor"));
        Assert.Contains("Device.Icon.Aria", icon, StringComparison.Ordinal);
        Assert.Contains("Device.Icon.Phone", icon, StringComparison.Ordinal);
        Assert.Contains("Device.Icon.Laptop", icon, StringComparison.Ordinal);
        Assert.DoesNotContain("windows logo", icon, StringComparison.OrdinalIgnoreCase);

        var css = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "wwwroot", "css", "parental.css"));
        Assert.Contains("#3ddc97", css, StringComparison.Ordinal);
        Assert.Contains("#ff8f80", css, StringComparison.Ordinal);
        Assert.Contains("#1f3a33", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Remote_page_disables_buttons_for_offline_devices_with_a_tooltip()
    {
        var remote = File.ReadAllText(Path.Combine(FindRoot(), "src", "OpControl.Parental.Server", "Components", "Pages", "Remote.razor"));
        Assert.Contains("DevicePresentation.CanStartRemote(device.Online)", remote, StringComparison.Ordinal);
        Assert.Contains("Remote.DeviceOffline", remote, StringComparison.Ordinal);
        Assert.Contains("DeviceIcon", remote, StringComparison.Ordinal);
        Assert.DoesNotContain("status-dot", remote, StringComparison.Ordinal);
    }

    [Fact]
    public void Devices_page_uses_the_tidy_card_sections_and_automatic_zone_option()
    {
        var devices = File.ReadAllText(Path.Combine(FindRoot(), "src", "OpControl.Parental.Server", "Components", "Pages", "Devices.razor"));
        Assert.Contains("Devices.TimeZoneAutomatic", devices, StringComparison.Ordinal);
        Assert.Contains("DeviceGauge", devices, StringComparison.Ordinal);
        Assert.Contains("Devices.SystemResources", devices, StringComparison.Ordinal);
        Assert.Contains("Devices.NotifyOffline", devices, StringComparison.Ordinal);
        Assert.Contains("device-card-grid", devices, StringComparison.Ordinal);
        Assert.Contains("os-label", devices, StringComparison.Ordinal);

        var settings = File.ReadAllText(Path.Combine(FindRoot(), "src", "OpControl.Parental.Server", "Components", "Pages", "Settings.razor"));
        Assert.Contains("FamilyZoneInfoAsync", settings, StringComparison.Ordinal);
        Assert.Contains("Settings.FamilyTimeZoneAutomatic", settings, StringComparison.Ordinal);
        Assert.Contains("FamilyZoneUsedText", settings, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
