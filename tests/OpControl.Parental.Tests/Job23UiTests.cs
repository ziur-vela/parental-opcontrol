// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using OpControl.Parental.Contracts;
using OpControl.Parental.Contracts.Games;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public sealed class Job23UiTests
{
    private sealed class FakeApi : ILunaApiClient
    {
        public bool GamesEnabled { get; set; } = true;
        public LunaStatusDto Status => new(true, "Emma", "night", 55, null, false, Loc.English, "", null, null,
            Guid.NewGuid(), GamesEnabled);

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
            Task.FromResult(new LunaGameResultDto(Guid.NewGuid(), request.Game, request.Mode, request.Level,
                request.Result, request.Score, request.DurationSeconds, DateTime.UtcNow));
        public Task<LunaGameAskResponse> AskGameLunaAsync(LunaGameAskRequest request, CancellationToken ct = default) =>
            Task.FromResult(new LunaGameAskResponse("Look at level 3.", false, null));
    }

    private static LunaAppView View(ILunaApiClient api)
    {
        Loc.SetLanguage(Loc.English);
        var view = new LunaAppView(api, "night");
        view.MarkInitializedForTest();
        view.InitializeForTestAsync().GetAwaiter().GetResult();
        return view;
    }

    private static List<string?> Texts(LunaAppView view) =>
        view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

    [Fact]
    public async Task Play_with_luna_row_is_visible_when_games_are_on()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var view = View(new FakeApi { GamesEnabled = true });
            var window = new Window { Width = 960, Height = 760, Content = view };
            window.Show();
            UiHarness.Pump(150);

            Assert.True(view.GamesRowVisibleForTest);
            var texts = Texts(view);
            Assert.Contains("Play with Luna", texts);
            Assert.Contains("Space Boards", texts);
            Assert.Contains("LunarCraft", texts);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Play_with_luna_row_is_hidden_when_games_are_off()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var view = View(new FakeApi { GamesEnabled = false });
            var window = new Window { Width = 960, Height = 760, Content = view };
            window.Show();
            UiHarness.Pump(150);

            Assert.False(view.GamesRowVisibleForTest);
            Assert.DoesNotContain("Play with Luna", Texts(view));
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Picker_lists_moon_tower_and_the_coming_soon_games()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var view = View(new FakeApi());
            var window = new Window { Width = 960, Height = 760, Content = view };
            window.Show();
            UiHarness.Pump(120);

            view.ShowGamePickerForTest(LunaGameCatalog.SpaceBoards);
            UiHarness.Pump(120);
            Assert.Equal("GamePicker", view.ScreenForTest);
            var texts = Texts(view);
            Assert.Contains("Moon Tower", texts);
            Assert.Contains("Orbit", texts);
            Assert.Contains("Crater Clash", texts);
            Assert.Contains("Coming soon", texts);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Moon_tower_opens_with_a_hand_and_a_legal_hint()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var view = View(new FakeApi());
            var window = new Window { Width = 1000, Height = 760, Content = view };
            window.Show();
            UiHarness.Pump(120);

            view.OpenMoonTowerForTest();
            UiHarness.Pump(250);
            Assert.Equal("Game", view.ScreenForTest);
            var board = view.GameBoardForTest;
            Assert.NotNull(board);
            Assert.Equal(5, board!.State.ChildHand.Count);
            Assert.Equal(MoonTowerPlayer.Child, board.State.Turn);
            var hint = board.Hint();
            Assert.NotNull(hint);
            Assert.Contains(MoonTowerEngine.LegalMoves(board.State, MoonTowerPlayer.Child),
                m => MoonTowerEngine.SameMove(m, hint!.Move));
            window.Close();
        }, CancellationToken.None);
    }
}
