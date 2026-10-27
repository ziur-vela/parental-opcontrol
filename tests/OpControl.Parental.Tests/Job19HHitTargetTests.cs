// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public class Job19HHitTargetTests
{
    private static readonly double[] Scales = [1.0, 1.25, 1.5, 2.0];

    private static IEnumerable<Point> Points(Control control)
    {
        var w = control.Bounds.Width;
        var h = control.Bounds.Height;
        yield return new Point(8, 8);
        yield return new Point(w - 8, 8);
        yield return new Point(8, h - 8);
        yield return new Point(w - 8, h - 8);
        yield return new Point(4, h / 2);
        yield return new Point(w - 4, h / 2);
        yield return new Point(w / 2, 4);
        yield return new Point(w / 2, h - 4);
        yield return new Point(w / 2, h / 2);
    }

    private static void AssertHit(Window window, Control control, string label)
    {
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0, $"{label} has no bounds");
        foreach (var point in Points(control))
        {
            var at = control.TranslatePoint(point, window);
            Assert.NotNull(at);
            var hit = window.InputHitTest(at.Value) as Visual;
            var ok = ReferenceEquals(hit, control) ||
                     (hit is not null && hit.GetVisualAncestors().Contains(control));
            var hitName = hit is Control c ? c.Name : null;
            var chain = hit is null ? "(null)" : string.Join(" < ",
                new[] { hit }.Concat(hit.GetVisualAncestors()).Take(8)
                    .Select(v => v.GetType().Name + ":" + (v as Control)?.Name));
            Assert.True(ok,
                $"{label} point {point} (window {at.Value}) hit {hit?.GetType().Name} name={hitName} chain={chain}");
        }
    }

    private static Control Find(Window window, string name)
    {
        var control = window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(c => c.Name == name);
        Assert.NotNull(control);
        return control!;
    }

    private static void PrepareChatWindow(LunaWindow window, out LunaChatDto chat, out LunaChildMessageDto message)
    {
        chat = new LunaChatDto(Guid.NewGuid(), LunaSubjects.Math, "Fractions", false, false,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        message = new LunaChildMessageDto(Guid.NewGuid(), chat.Id, LunaChatRoles.Child,
            "Hello Luna", DateTime.UtcNow.AddMinutes(-1), "none");
        window.ChatViewForTest.OpenChatForTest(chat, [message]);
        window.OpenChatForTest();
        UiHarness.Pump(1300, window.TickForTest);
        Assert.Equal("Chat", window.ModeForTest);
    }

    [Fact]
    public async Task Every_title_bar_button_grip_avatar_and_home_tile_hits_across_scales_and_themes()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new LunaWindow();
            window.Show();
            UiHarness.Pump(250, window.TickForTest);
            PrepareChatWindow(window, out var chat, out var message);

            foreach (var theme in new[] { "night", "gray" })
            {
                window.ChatViewForTest.SetThemeForTest(theme);
                UiHarness.Pump(80, window.TickForTest);

                foreach (var scale in Scales)
                {
                    window.SetRenderScaling(scale);
                    UiHarness.Pump(80, window.TickForTest);

                    foreach (var name in new[] { "ChatBack", "ChatTheme", "ChatMenu", "ChatClose" })
                    {
                        var button = Find(window, name);
                        Assert.True(button.Bounds.Width >= 44 && button.Bounds.Height >= 44,
                            $"{theme} {scale}x {name} hit area is {button.Bounds.Width}x{button.Bounds.Height}");
                        AssertHit(window, button, $"{theme} {scale}x {name}");
                    }
                    AssertHit(window, Find(window, "ResizeGrip"), $"{theme} {scale}x ResizeGrip");
                    AssertHit(window, Find(window, "ChildAvatar"), $"{theme} {scale}x ChildAvatar");

                    window.ChatViewForTest.ShowHomeWithoutRefreshForTest();
                    UiHarness.Pump(80, window.TickForTest);
                    foreach (var subject in LunaSubjects.All)
                        AssertHit(window, Find(window, "SubjectTile_" + subject), $"{theme} {scale}x tile {subject}");

                    window.ChatViewForTest.ShowMyChats();
                    UiHarness.Pump(80, window.TickForTest);
                    foreach (var name in new[] { "ChatsBack", "ChatsTheme", "ChatsClose" })
                    {
                        var button = Find(window, name);
                        Assert.True(button.Bounds.Width >= 44 && button.Bounds.Height >= 44,
                            $"{theme} {scale}x {name} hit area is {button.Bounds.Width}x{button.Bounds.Height}");
                        AssertHit(window, button, $"{theme} {scale}x {name}");
                    }

                    window.ChatViewForTest.OpenChatForTest(chat, [message]);
                    UiHarness.Pump(80, window.TickForTest);
                }
            }

            Assert.False(window.SleepLayerHitTestForTest,
                "the hidden moon layer must not be hit-testable over the open chat");
            window.Close();
        }, CancellationToken.None);
    }
}
