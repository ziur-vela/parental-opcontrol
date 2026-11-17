// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public sealed class Job26UiTests
{
    private sealed class FakeApi : ILunaApiClient
    {
        public LunaStatusDto Status => new(true, "Emma", "night", 55, null, false, Loc.English, "", null, null,
            Guid.NewGuid(), true);

        public Task ConnectAsync(bool dev, CancellationToken ct = default) => Task.CompletedTask;
        public Task<LunaStatusDto> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(Status);
        public Task<List<LunaChatDto>> GetChatsAsync(bool includeHidden = false, CancellationToken ct = default) =>
            Task.FromResult(new List<LunaChatDto>());
        public Task<LunaChatDto> CreateChatAsync(string subject, CancellationToken ct = default) =>
            Task.FromResult(new LunaChatDto(Guid.NewGuid(), subject, "", false, false, DateTime.UtcNow, DateTime.UtcNow));
        public Task<LunaChatDto> UpdateChatAsync(Guid chatId, LunaUpdateChatRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<List<LunaChildMessageDto>> GetMessagesAsync(Guid chatId, CancellationToken ct = default) =>
            Task.FromResult(new List<LunaChildMessageDto>());
        public Task<LunaMessageResponse> SendMessageAsync(Guid chatId, string text, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<LunaActivityDto> GetActivityAsync(Guid chatId, CancellationToken ct = default) =>
            Task.FromResult(new LunaActivityDto("idle"));
        public Task<LunaMoreRequestDto> RequestMoreAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task SetThemeAsync(string theme, CancellationToken ct = default) => Task.CompletedTask;
        public Task<LunaGameResultDto> SendGameResultAsync(LunaGameResultRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<LunaGameAskResponse> AskGameLunaAsync(LunaGameAskRequest request, CancellationToken ct = default) =>
            Task.FromResult(new LunaGameAskResponse("Look at level 3.", false, null, request.ChatId ?? Guid.NewGuid()));
    }

    private static LunaAppView View(ILunaApiClient api)
    {
        Loc.SetLanguage(Loc.English);
        var view = new LunaAppView(api, "night");
        view.MarkInitializedForTest();
        view.InitializeForTestAsync().GetAwaiter().GetResult();
        return view;
    }

    private static Control Find(Control root, string name)
    {
        var control = root.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);
        Assert.NotNull(control);
        return control!;
    }

    private static Rect InWindow(Control control, Window window)
    {
        var topLeft = control.TranslatePoint(new Point(0, 0), window);
        var bottomRight = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), window);
        Assert.NotNull(topLeft);
        Assert.NotNull(bottomRight);
        return new Rect(topLeft!.Value, bottomRight!.Value);
    }

    private static void AssertInside(Control control, Window window, string label)
    {
        var rect = InWindow(control, window);
        Assert.True(rect.Width > 0 && rect.Height > 0, $"{label} has no size");
        Assert.True(rect.Left >= -1 && rect.Top >= -1 &&
                    rect.Right <= window.ClientSize.Width + 1 && rect.Bottom <= window.ClientSize.Height + 1,
            $"{label} is clipped: {rect} window {window.ClientSize.Width}x{window.ClientSize.Height}");
    }

    [Theory]
    [InlineData(520, 600)]
    [InlineData(630, 900)]
    public async Task Play_row_cards_are_equal_and_span_the_subject_grid(double width, double height)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var view = View(new FakeApi());
            var window = new Window { Width = width, Height = height, Content = view };
            window.Show();
            UiHarness.Pump(150);

            foreach (var theme in new[] { "night", "gray" })
            {
                view.SetThemeForTest(theme);
                UiHarness.Pump(80);
                var boards = Find(view, "PlayCard_SpaceBoards");
                var craft = Find(view, "PlayCard_LunarCraft");
                var row = Find(view, "PlayRowGrid");
                var subjects = Find(view, "SubjectGrid");

                Assert.Equal(boards.Bounds.Width, craft.Bounds.Width, 1);
                Assert.Equal(row.Bounds.Width, subjects.Bounds.Width, 1);
                Assert.Equal(70, boards.Bounds.Height, 1);
                Assert.Equal(InWindow(boards, window).Top, InWindow(craft, window).Top, 0.5);
            }

            Loc.SetLanguage(Loc.Spanish);
            view.SetThemeForTest("gray");
            UiHarness.Pump(80);
            Assert.Contains("Tableros espaciales", view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
            Assert.Contains("Juega con Luna", view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
            window.Close();
        }, CancellationToken.None);
    }

    [Theory]
    [InlineData(1354, 859)]
    [InlineData(1280, 690)]
    public async Task Moon_tower_fits_the_big_game_window(double width, double height)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var view = View(new FakeApi());
            var window = new Window { Width = width, Height = height, Content = view };
            window.Show();
            UiHarness.Pump(150);
            view.OpenMoonTowerForTest();
            UiHarness.Pump(300);

            AssertInside(Find(view, "ScoreChip_Child"), window, "child score chip");
            AssertInside(Find(view, "ScoreChip_Luna"), window, "Luna score chip");
            AssertInside(Find(view, "GameLevelSelect"), window, "Luna level select");
            AssertInside(Find(view, "GameModeSelect"), window, "mode select");
            AssertInside(Find(view, "GameFriends"), window, "friends button");
            AssertInside(Find(view, "GamePause"), window, "pause");
            AssertInside(Find(view, "GameAskBox"), window, "Ask Luna box");
            AssertInside(Find(view, "HandViewbox"), window, "hand row");
            AssertInside(Find(view, "GameBag"), window, "bag count");
            for (var i = 0; i < 5; i++)
                AssertInside(Find(view, "HandBlock_" + i), window, "hand block " + i);

            Assert.Equal(5, view.GameBoardForTest!.State.ChildHand.Count);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Control>(), c => c.Name == "ResizeGrip");
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Game_window_animation_resizes_panel_clip_and_chat_root()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            var window = new LunaWindow();
            window.Show();
            UiHarness.Pump(250, window.TickForTest);

            var chat = new LunaChatDto(Guid.NewGuid(), LunaSubjects.Math, "Fractions", false, false,
                DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
            var message = new LunaChildMessageDto(Guid.NewGuid(), chat.Id, LunaChatRoles.Child,
                "Hello Luna", DateTime.UtcNow.AddMinutes(-1), "none");
            window.ChatViewForTest.OpenChatForTest(chat, [message]);
            window.OpenChatForTest();
            UiHarness.Pump(1300, window.TickForTest);
            Assert.Equal("Chat", window.ModeForTest);
            var restoreWidth = window.Width;
            var restoreHeight = window.Height;

            window.ChatViewForTest.OpenMoonTowerForTest();
            UiHarness.Pump(1400, window.TickForTest);

            Assert.True(window.GameModeForTest);
            Assert.False(window.BoundsAnimatingForTest);
            Assert.Equal(window.Width, window.BlobWidthForTest, 0);
            Assert.Equal(window.Height, window.BlobHeightForTest, 0);
            Assert.Equal(window.BlobWidthForTest, window.BlobClipWidthForTest, 0);
            Assert.Equal(window.BlobHeightForTest, window.BlobClipHeightForTest, 0);
            Assert.Equal(window.BlobWidthForTest, window.ChatRootWidthForTest, 0);
            Assert.Equal(window.BlobHeightForTest, window.ChatRootHeightForTest, 0);

            window.ChatViewForTest.ShowHomeWithoutRefreshForTest();
            UiHarness.Pump(1400, window.TickForTest);

            Assert.False(window.GameModeForTest);
            Assert.False(window.BoundsAnimatingForTest);
            Assert.InRange(window.Width, restoreWidth - 2, restoreWidth + 2);
            Assert.InRange(window.Height, restoreHeight - 2, restoreHeight + 2);
            Assert.Equal(restoreWidth, window.BlobWidthForTest, 0);
            Assert.Equal(restoreHeight, window.BlobHeightForTest, 0);
            Assert.Equal(restoreWidth, window.BlobClipWidthForTest, 0);
            Assert.Equal(restoreHeight, window.BlobClipHeightForTest, 0);
            Assert.Equal(restoreWidth, window.ChatRootWidthForTest, 0);
            Assert.Equal(restoreHeight, window.ChatRootHeightForTest, 0);
            window.Close();
        }, CancellationToken.None);
    }
}
