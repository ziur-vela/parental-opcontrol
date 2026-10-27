// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia.Headless;
using Avalonia.Input;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public class Job19IMessageFlickerTests
{
    private static readonly Guid Child = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Unchanged_poll_reports_no_change()
    {
        var messages = new List<ChildMessageDto>
        {
            Message("hello", DateTime.UtcNow.AddMinutes(-5)),
            Message("hi", DateTime.UtcNow.AddMinutes(-4), own: true)
        };

        var delta = MessageThreadDiff.Compare(messages, messages.ToList());

        Assert.False(delta.Changed);
        Assert.Empty(delta.Added);
        Assert.Empty(delta.Updated);
        Assert.Empty(delta.Removed);
    }

    [Fact]
    public void New_message_is_appended_once()
    {
        var first = Message("hello", DateTime.UtcNow.AddMinutes(-5));
        var second = Message("new", DateTime.UtcNow.AddMinutes(-1));
        var delta = MessageThreadDiff.Compare([first], [first, second]);

        Assert.True(delta.Changed);
        Assert.Single(delta.Added);
        Assert.Equal(second.Id, delta.Added[0].Id);
        Assert.Empty(delta.Updated);
        Assert.Equal(new[] { first.Id, second.Id }, delta.Final.Select(message => message.Id));
    }

    [Fact]
    public void Status_change_is_an_in_place_update_not_a_append()
    {
        var pending = Message("hello", DateTime.UtcNow.AddMinutes(-1), own: true) with
        {
            Status = MessageStatus.Sent
        };
        var sent = pending with { Status = MessageStatus.Delivered, DeliveredUtc = DateTime.UtcNow };

        var delta = MessageThreadDiff.Compare([pending], [sent]);

        Assert.True(delta.Changed);
        Assert.Empty(delta.Added);
        Assert.Single(delta.Updated);
        Assert.Equal(MessageStatus.Delivered, delta.Updated[0].Status);
        Assert.False(delta.OrderChanged);
    }

    [Fact]
    public void Deleted_message_is_removed()
    {
        var message = Message("bye", DateTime.UtcNow.AddMinutes(-2));
        var delta = MessageThreadDiff.Compare([message], [message with { Deleted = true }]);

        Assert.True(delta.Changed);
        Assert.Single(delta.Removed);
        Assert.Empty(delta.Final);
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.0, 2.0)]
    [InlineData(1.6, 1.0)]
    [InlineData(1.6, 1.5)]
    [InlineData(2.2, 1.25)]
    [InlineData(3.0, 2.0)]
    public void Only_the_yellow_sliver_is_hit_testable(double k, double scaling)
    {
        // Centre of the visible yellow quarter.
        Assert.True(LunaSleepGeometry.IsInsideSliver(8 * k, 8 * k, k));
        Assert.True(LunaSleepGeometry.IsInsideSliver(1 * k, 1 * k, k));

        // On the curved edge and just outside it.
        var edge = (-20 + 48 / Math.Sqrt(2)) * k;
        Assert.True(LunaSleepGeometry.IsInsideSliver(edge - 0.4, edge - 0.4, k));
        Assert.False(LunaSleepGeometry.IsInsideSliver(edge + 0.6, edge + 0.6, k));

        // End of the Z float and the old square corner, over the browser Back
        // button: neither may wake Luna.
        var zEnd = (4 + 11.3) * k;
        Assert.False(LunaSleepGeometry.IsInsideSliver(zEnd, zEnd, k));
        Assert.False(LunaSleepGeometry.IsInsideSliver(40 * k, 40 * k, k));
        Assert.False(LunaSleepGeometry.IsInsideSliver(44 * k, 4 * k, k));

        var (left, top, right, bottom) = LunaSleepGeometry.EllipseBounds(k, scaling);
        Assert.Equal((int)Math.Floor(-68 * k * scaling), left);
        Assert.Equal((int)Math.Floor(-68 * k * scaling), top);
        Assert.Equal((int)Math.Ceiling(28 * k * scaling), right);
        Assert.Equal((int)Math.Ceiling(28 * k * scaling), bottom);
    }

    [Fact]
    public void Real_window_sleep_hit_follows_the_sliver_at_every_tuning_value()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new LunaWindow();
            window.Show();
            UiHarness.Pump(250);
            foreach (var k in new[] { 1.0, 1.6, 2.2, 3.0 })
            {
                window.SetSleepScaleForTest(k);
                Assert.True(window.SleepHitForTest(6 * k, 6 * k));
                Assert.False(window.SleepHitForTest(40 * k, 40 * k));
                Assert.False(window.SleepHitForTest(15.3 * k, 15.3 * k));
            }
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void Kid_messages_do_not_rebuild_on_an_unchanged_poll()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var data = new TestKidData();
            var window = new MyOpControlWindow(data, new KidWindowOptions(Tab: "messages"));
            window.Show();
            UiHarness.Pump(300);
            var builds = window.MessageListBuildCountForTest;
            var adds = window.MessageRowAddCountForTest;
            var rows = window.MessageRowCountForTest;
            Assert.True(rows >= 2);

            window.ComposerForTest!.Text = "I am still typing";
            window.ComposerForTest.Focus();
            UiHarness.Pump(60);

            window.RefreshForTestAsync().GetAwaiter().GetResult();
            UiHarness.Pump(80);

            Assert.Equal(builds, window.MessageListBuildCountForTest);
            Assert.Equal(adds, window.MessageRowAddCountForTest);
            Assert.Equal(rows, window.MessageRowCountForTest);
            Assert.Equal("I am still typing", window.ComposerForTest.Text);
            Assert.True(window.ComposerForTest.IsFocused);
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void New_message_appends_once_and_status_updates_in_place()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var data = new TestKidData();
            var window = new MyOpControlWindow(data, new KidWindowOptions(Tab: "messages"));
            window.Show();
            UiHarness.Pump(300);
            var builds = window.MessageListBuildCountForTest;
            var rows = window.MessageRowCountForTest;

            var added = Message("a new message", DateTime.UtcNow);
            data.Messages.Add(added);
            window.RefreshForTestAsync().GetAwaiter().GetResult();
            UiHarness.Pump(80);
            Assert.Equal(builds, window.MessageListBuildCountForTest);
            Assert.Equal(rows + 1, window.MessageRowCountForTest);
            Assert.Equal("a new message", window.MessageBodyForTest(added.Id));

            var again = window.MessageRowAddCountForTest;
            window.RefreshForTestAsync().GetAwaiter().GetResult();
            UiHarness.Pump(80);
            Assert.Equal(again, window.MessageRowAddCountForTest);
            Assert.Equal(rows + 1, window.MessageRowCountForTest);

            var own = data.Messages.First(message => message.Direction == MessageDirection.ChildToParent);
            var index = data.Messages.IndexOf(own);
            var before = window.MessageStatusForTest(own.Id);
            data.Messages[index] = own with { Status = MessageStatus.Delivered, DeliveredUtc = DateTime.UtcNow };
            window.RefreshForTestAsync().GetAwaiter().GetResult();
            UiHarness.Pump(80);
            Assert.Equal(builds, window.MessageListBuildCountForTest);
            Assert.Equal(rows + 1, window.MessageRowCountForTest);
            Assert.NotEqual(before, window.MessageStatusForTest(own.Id));
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void Enter_sends_and_shift_enter_adds_a_line()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var data = new TestKidData();
            var window = new MyOpControlWindow(data, new KidWindowOptions(Tab: "messages"));
            window.Show();
            UiHarness.Pump(300);
            var input = window.ComposerForTest!;
            input.Text = "Hola";
            input.Focus();
            UiHarness.Pump(60);

            window.KeyPress(Key.Enter, RawInputModifiers.Shift, PhysicalKey.Enter, null);
            UiHarness.Pump(80);
            Assert.Empty(data.Sent);
            Assert.Contains("\n", input.Text);

            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            UiHarness.Pump(120);
            Assert.Single(data.Sent);
            Assert.Equal("Hola", data.Sent[0]);
            Assert.Equal(string.Empty, input.Text);
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void Ime_and_accent_input_never_send_and_empty_or_long_text_is_rejected()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var data = new TestKidData();
            var window = new MyOpControlWindow(data, new KidWindowOptions(Tab: "messages"));
            window.Show();
            UiHarness.Pump(300);
            var input = window.ComposerForTest!;
            input.Focus();
            UiHarness.Pump(60);

            window.KeyTextInput("á");
            window.KeyTextInput("ñ");
            UiHarness.Pump(60);
            Assert.Empty(data.Sent);

            window.KeyPress(Key.ImeProcessed, RawInputModifiers.None, PhysicalKey.None, null);
            UiHarness.Pump(60);
            Assert.Empty(data.Sent);

            input.Text = "   ";
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            UiHarness.Pump(80);
            Assert.Empty(data.Sent);

            input.Text = new string('x', 501);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            UiHarness.Pump(80);
            Assert.Empty(data.Sent);
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void Lock_panel_uses_the_shared_incremental_diff_and_ignores_ime_enter()
    {
        var root = FindRoot();
        var lockForm = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Client.Windows.Helper", "LockMessagesForm.cs"));
        Assert.Contains("MessageThreadDiff.Compare", lockForm, StringComparison.Ordinal);
        Assert.Contains("UpdateThreadRow", lockForm, StringComparison.Ordinal);
        Assert.Contains("Keys.ProcessKey", lockForm, StringComparison.Ordinal);
        Assert.Contains("SuppressKeyPress = true", lockForm, StringComparison.Ordinal);
        Assert.DoesNotContain("RebuildThread(scrollToEnd: quiet || _firstLoad)", lockForm, StringComparison.Ordinal);

        var kid = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        Assert.Contains("MessageThreadDiff.Compare", kid, StringComparison.Ordinal);
        Assert.Contains("UpdateMessagesInPlace", kid, StringComparison.Ordinal);
        Assert.Contains("RoutingStrategies.Tunnel", kid, StringComparison.Ordinal);
        Assert.Contains("Key.ImeProcessed", kid, StringComparison.Ordinal);

        var window = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaWindow.axaml.cs"));
        Assert.Contains("LunaSleepGeometry.EllipseBounds", window, StringComparison.Ordinal);
        Assert.Contains("LunaSleepGeometry.IsInsideSliver", window, StringComparison.Ordinal);
        Assert.Contains("hover_ms=400", window, StringComparison.Ordinal);
    }

    private static ChildMessageDto Message(string text, DateTime createdUtc, bool own = false) =>
        new(Guid.NewGuid(), Child, null,
            own ? MessageDirection.ChildToParent : MessageDirection.ParentToChild,
            null, own ? "Ana" : "Mom", text, null, false, false,
            createdUtc, null, null, null, null,
            own ? MessageStatus.Sent : MessageStatus.Delivered, false);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class TestKidData : IKidDataSource
    {
        private readonly DateTime _created = DateTime.UtcNow.AddMinutes(-30);
        private readonly Guid _ownId = Guid.NewGuid();

        public TestKidData()
        {
            Messages =
            [
                Message("Hello!", _created),
                new ChildMessageDto(_ownId, Child, null, MessageDirection.ChildToParent, null, "Ana",
                    "I am fine", null, false, false, _created.AddMinutes(1), null,
                    null, null, null, MessageStatus.Sent, false)
            ];
        }

        public List<ChildMessageDto> Messages { get; }
        public List<string> Sent { get; } = [];

        public Task<KidSnapshot> LoadAsync(CancellationToken ct)
        {
            var summary = new ChildSummaryDto(Child, "Ana", "X1", _created, true, 360, 4800, 30000,
                Weekdays: [new SummaryWeekdayDto(1, 360)],
                Schedules: [], Categories: [], Websites: [], Apps: [], AppsToday: [], AppsWeek: [],
                Visits: [], History: [], RecentMessages: [], UnreadMessages: 0);
            var state = new RuntimeLockStateDto(Child, "Ana", "Mom", false, "none", null, null,
                _created.AddHours(2), null, null, _created, DailyLimitRemainingSeconds: 16800);
            return Task.FromResult(new KidSnapshot(summary, state, Messages.ToList(), true));
        }

        public Task<PinPipeResponse> SendMessageAsync(string text, CancellationToken ct)
        {
            Sent.Add(text);
            var message = new ChildMessageDto(Guid.NewGuid(), Child, null, MessageDirection.ChildToParent,
                null, "Ana", text, null, false, false, DateTime.UtcNow, null, null, null, null,
                MessageStatus.Sent, false);
            Messages.Add(message);
            return Task.FromResult(new PinPipeResponse(true, Message: message));
        }

        public Task MarkSeenAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public Task MarkAcknowledgedAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public Task SetThemeAsync(string theme, CancellationToken ct) => Task.CompletedTask;

        public Task<AboutInfoDto> GetAboutAsync(CancellationToken ct) =>
            Task.FromResult(new AboutInfoDto("0.17.17", "2026-10-27", "test", "0.17.17", "2026-10-27",
                "v0.17.17"));
    }
}
