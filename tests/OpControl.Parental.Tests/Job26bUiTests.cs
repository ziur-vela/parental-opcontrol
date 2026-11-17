// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using OpControl.Parental.Contracts;
using OpControl.Parental.Contracts.Games;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public sealed class Job26bUiTests
{
    private const double CellW = 52;
    private const double CellH = 44;
    private const double LabelW = 76;

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
            throw new NotSupportedException();
    }

    private static LunaAppView View()
    {
        Loc.SetLanguage(Loc.English);
        var view = new LunaAppView(new FakeApi(), "night");
        view.MarkInitializedForTest();
        view.InitializeForTestAsync().GetAwaiter().GetResult();
        return view;
    }

    private static Window OpenGame(out LunaAppView view)
    {
        view = View();
        var window = new Window { Width = 1354, Height = 859, Content = view };
        window.Show();
        UiHarness.Pump(200);
        view.OpenMoonTowerForTest();
        UiHarness.Pump(300);
        return window;
    }

    private static Control Find(Control root, string name)
    {
        var control = root.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);
        Assert.NotNull(control);
        return control!;
    }

    private static Point Center(Control control, Window window)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window);
        Assert.NotNull(point);
        return point!.Value;
    }

    private static int SelectableHandIndex(MoonTowerBoard board) =>
        Enumerable.Range(0, board.State.ChildHand.Count)
            .First(i => board.State.ChildHand[i].Power is MoonTowerPower.None
                or MoonTowerPower.Eclipse or MoonTowerPower.DoubleStar);

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        UiHarness.Pump(80);
    }

    [Fact]
    public async Task Real_pointer_click_on_a_hand_block_selects_it_and_shows_legal_spots()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var window = OpenGame(out var view);
            var board = view.GameBoardForTest!;
            var index = SelectableHandIndex(board);
            Assert.Equal(5, board.State.ChildHand.Count);
            Assert.InRange(index, 0, board.State.ChildHand.Count - 1);
            var block = Find(view, "HandBlock_" + index);

            Click(window, Center(block, window));

            Assert.Equal(index, board.SelectedIndexForTest);
            Assert.True(board.LegalSpotCountForTest > 0);
            Assert.NotNull(board.FirstLegalSpotForTest);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Real_pointer_click_on_a_legal_spot_places_the_selected_block()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var window = OpenGame(out var view);
            var board = view.GameBoardForTest!;
            var index = SelectableHandIndex(board);
            Click(window, Center(Find(view, "HandBlock_" + index), window));

            var spot = board.FirstLegalSpotForTest;
            Assert.NotNull(spot);
            Click(window, Center(spot!, window));

            Assert.NotEmpty(board.State.Placed);
            Assert.Null(board.SelectedIndexForTest);
            Assert.Equal(MoonTowerPlayer.Luna, board.State.Turn);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Real_pointer_drag_and_drop_places_a_block_at_the_dropped_level()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var window = OpenGame(out var view);
            var board = view.GameBoardForTest!;
            var move = MoonTowerEngine.LegalMoves(board.State, MoonTowerPlayer.Child)
                .First(m => m.Kind == MoonTowerMoveKind.Place);
            var block = board.State.ChildHand[move.HandIndex];
            var start = Center(Find(view, "HandBlock_" + move.HandIndex), window);

            var tower = Find(view, "TowerCanvas");
            var targetTower = new Point(
                LabelW + move.Start * CellW + block.Length * CellW / 2,
                6 + (board.State.Levels - 1 - move.Level) * CellH + CellH / 2);
            var target = tower.TranslatePoint(targetTower, window);
            Assert.NotNull(target);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point(start.X + 20, start.Y), RawInputModifiers.LeftMouseButton);
            UiHarness.Pump(80);
            Assert.True(board.DraggingForTest);
            window.MouseMove(target!.Value, RawInputModifiers.LeftMouseButton);
            UiHarness.Pump(80);
            window.MouseUp(target.Value, MouseButton.Left);
            UiHarness.Pump(120);

            var placed = Assert.Single(board.State.Placed);
            Assert.Equal(move.Level, placed.Level);
            Assert.Equal(move.Start, placed.Start);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Real_pointer_swap_arms_and_uses_the_selected_block()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var window = OpenGame(out var view);
            var board = view.GameBoardForTest!;
            Click(window, Center(Find(view, "GameSwap"), window));
            Assert.True(board.SwapArmedForTest);

            var index = SelectableHandIndex(board);
            var before = board.State.ChildHand[index].Id;
            Click(window, Center(Find(view, "HandBlock_" + index), window));
            Assert.Equal(MoonTowerPlayer.Luna, board.State.Turn);
            Assert.DoesNotContain(board.State.ChildHand, b => b.Id == before);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Real_pointer_rocket_power_block_plays_immediately()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var window = OpenGame(out var view);
            var board = view.GameBoardForTest!;
            var rocket = new MoonTowerBlock(99999, 1, MoonTowerSymbol.Sun, MoonTowerPower.Rocket);
            board.State.ChildHand[0] = rocket;
            board.RefreshForTest();
            UiHarness.Pump(80);

            Click(window, Center(Find(view, "HandBlock_0"), window));

            Assert.DoesNotContain(board.State.ChildHand, b => b.Id == rocket.Id);
            Assert.Equal(MoonTowerPlayer.Child, board.State.Turn);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Idle_game_does_not_rebuild_the_board_for_two_seconds()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var window = OpenGame(out var view);
            var board = view.GameBoardForTest!;
            var before = board.RenderCountForTest;

            UiHarness.Pump(2200);

            Assert.Equal(before, board.RenderCountForTest);
            window.Close();
        }, CancellationToken.None);
    }
}
