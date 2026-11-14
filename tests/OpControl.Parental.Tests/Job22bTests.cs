// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Hubs;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job22bTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 5)]
    [InlineData(60, 60)]
    [InlineData(120, 300)]
    [InlineData(300, 300)]
    public void Delay_is_normalized_to_the_offered_values(int requested, int expected) =>
        Assert.Equal(expected, DevicePowerRules.NormalizeDelay(requested));

    [Fact]
    public void Cancel_window_and_request_guards_are_explicit()
    {
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(DevicePowerRules.CanCancel(DevicePowerStatus.Pending, now.AddSeconds(10), now));
        Assert.True(DevicePowerRules.CanCancel(DevicePowerStatus.Accepted, now.AddSeconds(10), now));
        Assert.False(DevicePowerRules.CanCancel(DevicePowerStatus.Accepted, now.AddSeconds(-1), now));
        Assert.False(DevicePowerRules.CanCancel(DevicePowerStatus.Done, now.AddMinutes(1), now));
        Assert.False(DevicePowerRules.CanCancel(DevicePowerStatus.Cancelled, now.AddMinutes(1), now));
        Assert.True(DevicePowerRules.CanRequest(true, DevicePlatform.Windows));
        Assert.False(DevicePowerRules.CanRequest(false, DevicePlatform.Windows));
        Assert.False(DevicePowerRules.CanRequest(true, DevicePlatform.Linux));
    }

    [Fact]
    public async Task Offline_device_is_rejected_and_nothing_is_recorded()
    {
        var (db, provider, presence, hub, service, device, _) = await SetupAsync();
        var error = await Assert.ThrowsAsync<DevicePowerException>(() =>
            service.RequestAsync(device.FamilyId, device.Id, Guid.NewGuid(), "Parent", DevicePowerAction.Restart, 60));
        Assert.Contains("offline", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.DevicePowerCommands.ToListAsync());
        Assert.Empty(hub.FakeClients.Proxy.Sent);
        provider.Dispose();
    }

    [Fact]
    public async Task Pending_command_cannot_be_cancelled_while_the_device_is_offline()
    {
        var (db, provider, presence, _, service, device, _) = await SetupAsync();
        await presence.ConnectedAsync(device);
        var command = await service.RequestAsync(device.FamilyId, device.Id, Guid.NewGuid(), "Parent", DevicePowerAction.Restart, 300);
        await presence.DisconnectedAsync(device);
        await Assert.ThrowsAsync<DevicePowerException>(() =>
            service.CancelAsync(device.FamilyId, device.Id, command.Id, Guid.NewGuid(), "Parent"));
        var row = await db.DevicePowerCommands.AsNoTracking().SingleAsync(c => c.Id == command.Id);
        Assert.Equal(DevicePowerStatus.Pending, row.Status);
        provider.Dispose();
    }

    [Fact]
    public async Task Command_is_pending_and_sleep_uses_five_seconds()
    {
        var (db, provider, presence, hub, service, device, _) = await SetupAsync();
        await presence.ConnectedAsync(device);

        var sleep = await service.RequestAsync(device.FamilyId, device.Id, Guid.NewGuid(), "Parent", DevicePowerAction.Sleep, 300);
        Assert.Equal(DevicePowerStatus.Pending, sleep.Status);
        Assert.Equal(5, sleep.NoticeSeconds);
        Assert.InRange(sleep.RunAtUtc, DateTime.UtcNow.AddSeconds(3), DateTime.UtcNow.AddSeconds(8));
        Assert.Contains(hub.FakeClients.Proxy.Sent, m => m.Method == DeviceHubMethods.PowerCommand);

        await service.CancelAsync(device.FamilyId, device.Id, sleep.Id, Guid.NewGuid(), "Parent");
        var cancelled = await db.DevicePowerCommands.AsNoTracking().SingleAsync(c => c.Id == sleep.Id);
        Assert.Equal(DevicePowerStatus.Cancelled, cancelled.Status);
        Assert.Equal("Parent", cancelled.CancelledByName);
        Assert.Contains(hub.FakeClients.Proxy.Sent, m => m.Method == DeviceHubMethods.PowerCancel);

        var restart = await service.RequestAsync(device.FamilyId, device.Id, Guid.NewGuid(), "Parent", DevicePowerAction.Restart, 300);
        Assert.Equal(300, restart.NoticeSeconds);
        Assert.Equal(DevicePowerStatus.Pending, restart.Status);
        provider.Dispose();
    }

    [Fact]
    public async Task Accepted_then_done_is_recorded_and_cancel_after_done_is_rejected()
    {
        var (db, provider, presence, _, service, device, _) = await SetupAsync();
        await presence.ConnectedAsync(device);
        var command = await service.RequestAsync(device.FamilyId, device.Id, Guid.NewGuid(), "Parent", DevicePowerAction.Restart, 60);

        await service.ApplyResultAsync(device.Id, new DevicePowerResultDto(command.Id, DevicePowerAction.Restart,
            DevicePowerStatus.Accepted, null, DateTime.UtcNow));
        var accepted = await db.DevicePowerCommands.AsNoTracking().SingleAsync(c => c.Id == command.Id);
        Assert.Equal(DevicePowerStatus.Accepted, accepted.Status);
        Assert.NotNull(accepted.AcceptedUtc);

        await service.ApplyResultAsync(device.Id, new DevicePowerResultDto(command.Id, DevicePowerAction.Restart,
            DevicePowerStatus.Done, "restart initiated", DateTime.UtcNow));
        var done = await db.DevicePowerCommands.AsNoTracking().SingleAsync(c => c.Id == command.Id);
        Assert.Equal(DevicePowerStatus.Done, done.Status);
        Assert.NotNull(done.CompletedUtc);
        Assert.Equal("restart initiated", done.ResultDetail);

        await Assert.ThrowsAsync<DevicePowerException>(() =>
            service.CancelAsync(device.FamilyId, device.Id, command.Id, Guid.NewGuid(), "Parent"));
        provider.Dispose();
    }

    [Fact]
    public async Task Sweep_marks_unanswered_commands_failed()
    {
        var (db, provider, _, _, service, device, _) = await SetupAsync();
        var now = DateTime.UtcNow;
        var noReply = new DevicePowerCommand
        {
            FamilyId = device.FamilyId, ChildId = device.ChildId, DeviceId = device.Id,
            Action = DevicePowerAction.Restart, RunAtUtc = now.AddMinutes(-2), NoticeSeconds = 60,
            RequestedByParentId = Guid.NewGuid(), RequestedByName = "Parent",
            Status = DevicePowerStatus.Pending, CreatedUtc = now.AddMinutes(-3)
        };
        var noResult = new DevicePowerCommand
        {
            FamilyId = device.FamilyId, ChildId = device.ChildId, DeviceId = device.Id,
            Action = DevicePowerAction.PowerOff, RunAtUtc = now.AddMinutes(-3), NoticeSeconds = 60,
            RequestedByParentId = Guid.NewGuid(), RequestedByName = "Parent",
            Status = DevicePowerStatus.Accepted, CreatedUtc = now.AddMinutes(-4), AcceptedUtc = now.AddMinutes(-3)
        };
        db.DevicePowerCommands.AddRange(noReply, noResult);
        await db.SaveChangesAsync();

        Assert.Equal(2, await service.SweepAsync());
        var rows = await db.DevicePowerCommands.AsNoTracking().OrderBy(c => c.Action).ToListAsync();
        Assert.All(rows, row => Assert.Equal(DevicePowerStatus.Failed, row.Status));
        provider.Dispose();
    }

    [Fact]
    public void Power_ui_strings_and_source_are_wired()
    {
        var root = FindRoot();
        var en = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", "AppStrings.resx"));
        var es = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", "AppStrings.es-MX.resx"));
        foreach (var key in new[]
                 {
                     "Devices.Restart", "Devices.Sleep", "Devices.PowerOff", "Devices.PowerOffline",
                     "Devices.RestartConfirmTitle", "Devices.PowerNow", "Devices.PowerIn1Min", "Devices.PowerIn5Min",
                     "Devices.PowerCancelPending", "Devices.PowerStatus.Done", "Activity.Power.Restart",
                     "Error.PowerOffline"
                 })
        {
            Assert.Contains($"name=\"{key}\"", en, StringComparison.Ordinal);
            Assert.Contains($"name=\"{key}\"", es, StringComparison.Ordinal);
        }
        Assert.Contains("Apagar", es, StringComparison.Ordinal);
        Assert.Contains("This release is in beta. Report anything that looks wrong to Hendry.", en, StringComparison.Ordinal);
        Assert.Contains("Esta versión está en beta. Reporta a Hendry cualquier cosa que se vea mal.", es, StringComparison.Ordinal);

        var devices = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Pages", "Devices.razor"));
        Assert.Contains("DevicePowerAction.Restart", devices, StringComparison.Ordinal);
        Assert.Contains("DevicePowerAction.Sleep", devices, StringComparison.Ordinal);
        Assert.Contains("DevicePowerAction.PowerOff", devices, StringComparison.Ordinal);
        Assert.Contains("Devices.PowerCancelPending", devices, StringComparison.Ordinal);
        Assert.Contains("Devices.PowerOffline", devices, StringComparison.Ordinal);
        Assert.Contains("PowerService.RequestAsync", devices, StringComparison.Ordinal);
        Assert.Contains("PowerService.CancelAsync", devices, StringComparison.Ordinal);

        var client = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows", "PowerCommandService.cs"));
        Assert.Contains("InitiateSystemShutdownEx", client, StringComparison.Ordinal);
        Assert.Contains("SetSuspendState", client, StringComparison.Ordinal);
        Assert.Contains("ReportPowerResult", File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows", "Worker.cs")), StringComparison.Ordinal);

        var helper = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "HelperContext.cs"));
        Assert.Contains("UpdatePowerNotice", helper, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "PowerNoticeForm.cs")));

        var layout = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Layout", "MainLayout.razor"));
        Assert.Contains("sidebar-version-box", layout, StringComparison.Ordinal);
        Assert.Contains("sidebar-version-title", layout, StringComparison.Ordinal);
        var about = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Shared", "AboutPanel.razor"));
        Assert.Contains("about-beta-note", about, StringComparison.Ordinal);
        Assert.Contains("AboutPage.BetaBadge", about, StringComparison.Ordinal);
        var kid = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        Assert.Contains("BetaPill", kid, StringComparison.Ordinal);
        Assert.Contains("Client.Kid.About.Beta", kid, StringComparison.Ordinal);
        var css = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server", "wwwroot", "css", "parental.css"));
        Assert.Contains(".site-foot:hover { color: var(--muted); text-decoration: none; }", css, StringComparison.Ordinal);
        Assert.Contains(".site-foot:hover .site-foot-hint", css, StringComparison.Ordinal);
        Assert.Contains(".beta-badge", css, StringComparison.Ordinal);
        Assert.Contains("border-radius: 999px", css, StringComparison.Ordinal);
    }

    private static async Task<(ParentalDbContext Db, ServiceProvider Provider, DevicePresenceService Presence,
        FakeHubContext Hub, DevicePowerService Service, Device Device, DashboardEventBus Events)> SetupAsync()
    {
        var services = new ServiceCollection();
        var database = "job22b-" + Guid.NewGuid();
        services.AddDbContext<ParentalDbContext>(options => options.UseInMemoryDatabase(database));
        services.AddSingleton<DashboardEventBus>();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ParentalDbContext>();
        var events = scope.ServiceProvider.GetRequiredService<DashboardEventBus>();
        var family = new Family { Name = "Power" };
        var child = new Child { FamilyId = family.Id, Name = "Emma" };
        var device = new Device
        {
            FamilyId = family.Id, ChildId = child.Id, Name = "ZIUR-X1-LAPTOP",
            Platform = DevicePlatform.Windows, NotifyOnline = false, NotifyOffline = false
        };
        db.Families.Add(family);
        db.Children.Add(child);
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        var presence = new DevicePresenceService(scopes, events, NullLogger<DevicePresenceService>.Instance);
        var hub = new FakeHubContext();
        var service = new DevicePowerService(db, hub, presence, events, NullLogger<DevicePowerService>.Instance);
        return (db, provider, presence, hub, service, device, events);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class FakeClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> Sent { get; } = new();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Sent.Add((method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubClients : IHubClients
    {
        public FakeClientProxy Proxy { get; } = new();
        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public IClientProxy Group(string groupName) => Proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class FakeGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeHubContext : IHubContext<ParentalHub>
    {
        public FakeHubClients FakeClients { get; } = new();
        public IHubClients Clients => FakeClients;
        public IGroupManager Groups { get; } = new FakeGroupManager();
    }
}
