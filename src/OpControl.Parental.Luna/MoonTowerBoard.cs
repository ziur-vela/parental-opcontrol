// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OpControl.Parental.Contracts.Games;
using OpControl.Parental.Localization;
using Path = Avalonia.Controls.Shapes.Path;

namespace OpControl.Parental.Luna;

public sealed record MoonTowerOutcome(MoonTowerResult Result, int Score, int DurationSeconds,
    MoonTowerMode Mode, MoonTowerDifficulty Difficulty);

/// <summary>
/// The Moon Tower board. Pure drawing plus input; every rule decision goes through
/// <see cref="MoonTowerEngine"/> so the UI can never invent a move the engine rejects.
/// </summary>
public sealed class MoonTowerBoard : UserControl
{
    private const double CellW = 52;
    private const double CellH = 44;
    private const double LabelW = 76;
    private const double PalettePad = 6;

    private static readonly Color SunColor = Color.Parse("#ffb547");
    private static readonly Color MoonColor = Color.Parse("#b18cff");
    private static readonly Color StarColor = Color.Parse("#5bd3ff");
    private static readonly Color CometColor = Color.Parse("#3ddc97");
    private static readonly Color Ink = Color.Parse("#15103a");

    private readonly LunaPalette _palette;
    private readonly string _childId;
    private readonly string _childName;
    private readonly Canvas _towerCanvas = new();
    private readonly Grid _overlay = new();
    private readonly StackPanel _handPanel = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly StackPanel _status = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
    private readonly Viewbox _towerBox;
    private readonly TextBlock _bagLabel;
    private readonly MoonTowerLinePicker _lines;
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _aiTimer;

    private MoonTowerState _state;
    private int _elapsed;
    private bool _paused;
    private int? _selected;
    private bool _meteorArmed;
    private bool _swapArmed;
    private MoonTowerMove? _highlight;
    private bool _reportedOver;
    private Point _pressPoint;
    private int _pressHand = -1;
    private bool _dragging;
    private Control? _ghost;
    private long _lastTapAt;
    private int _lastTapIndex = -1;
    private int _bounceBlockId;
    private double _bounceStart;
    private readonly DispatcherTimer _bounceTimer;
    private readonly Dictionary<int, TranslateTransform> _bounceTransforms = new();
    private readonly List<long> _renderTimes = new();
    private long _lastRenderRateLog = -1000;
    private int _renderCount;
    private long _lastPointerReleaseAt = -1;

    public MoonTowerBoard(LunaPalette palette, string childId, string childName)
    {
        _palette = palette;
        _childId = childId;
        _childName = string.IsNullOrWhiteSpace(childName) ? "You" : childName;
        _lines = new MoonTowerLinePicker(Environment.TickCount);
        _state = MoonTowerEngine.NewGame(MoonTowerMode.Make10, Environment.TickCount);

        _towerBox = new Viewbox { Stretch = Stretch.Uniform, Margin = new Thickness(4) };
        _towerCanvas.Name = "TowerCanvas";
        _towerCanvas.Width = LabelW + _state.Width * CellW + 32;
        _towerCanvas.Height = _state.Levels * CellH + 40;
        _towerBox.Child = _towerCanvas;

        _bagLabel = Text("", 13, _palette.Muted, FontWeight.Bold);
        _bagLabel.Name = "GameBag";

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 6) };
        top.Children.Add(_status);
        Grid.SetColumn(_bagLabel, 2);
        top.Children.Add(_bagLabel);
        root.Children.Add(top);

        var boardGrid = new Grid();
        boardGrid.Children.Add(_towerBox);
        boardGrid.Children.Add(_overlay);
        // The overlay is only a drawing surface for the drag ghost and the
        // pause/end cards. A transparent background made it swallow every click
        // meant for the legal-spot buttons under it, so it is not hit-testable
        // unless a modal card is visible.
        _overlay.Background = null;
        _overlay.IsHitTestVisible = false;
        Grid.SetRow(boardGrid, 1);
        root.Children.Add(boardGrid);

        var handHead = Text(Loc.T("Games.YourBlocks"), 12, _palette.Muted, FontWeight.Bold);
        var handWrap = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 6, 0, 0) };
        handWrap.Children.Add(handHead);
        // Scale the whole hand to the available width so all five blocks stay
        // visible without a horizontal scrollbar, at 100% and at the big game size.
        var handBox = new Viewbox
        {
            Name = "HandViewbox",
            Child = _handPanel,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(handBox, 1);
        handWrap.Children.Add(handBox);
        Grid.SetRow(handWrap, 2);
        root.Children.Add(handWrap);

        Content = root;
        _towerCanvas.PointerReleased += OnTowerPointerReleased;
        _towerCanvas.PointerMoved += OnTowerPointerMoved;
        _towerCanvas.PointerPressed += (_, e) => _pressPoint = e.GetPosition(_towerCanvas);

        _clock = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            if (_paused || _state.Finished) return;
            _elapsed++;
        });
        _aiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
        _aiTimer.Tick += (_, _) =>
        {
            _aiTimer.Stop();
            PlayLunaTurn();
        };
        _clock.Start();
        _bounceTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            if (_bounceBlockId == 0) { _bounceTimer.Stop(); return; }
            var t = Math.Clamp((Environment.TickCount64 / 1000.0 - _bounceStart) / 0.45, 0, 1);
            var offset = -24 * (1 - Bounce(t));
            if (_bounceTransforms.TryGetValue(_bounceBlockId, out var transform))
                transform.Y = offset;
            if (t >= 1)
            {
                if (_bounceTransforms.TryGetValue(_bounceBlockId, out var done)) done.Y = 0;
                _bounceBlockId = 0;
                _bounceTimer.Stop();
            }
        });
        Render();
    }

    public event Action<string>? Tip;
    public event Action<MoonTowerOutcome>? GameOver;
    public event Action? SaveUpdated;

    public MoonTowerState State => _state;
    internal int RenderCountForTest => _renderCount;
    internal int? SelectedIndexForTest => _selected;
    internal bool DraggingForTest => _dragging;
    internal bool SwapArmedForTest => _swapArmed;
    internal void RefreshForTest() => Render();
    internal int LegalSpotCountForTest => _towerCanvas.Children.OfType<Button>()
        .Count(b => b.Tag is MoonTowerMove { Kind: MoonTowerMoveKind.Place });
    internal Button? FirstLegalSpotForTest => _towerCanvas.Children.OfType<Button>()
        .FirstOrDefault(b => b.Tag is MoonTowerMove { Kind: MoonTowerMoveKind.Place });
    public MoonTowerDifficulty Difficulty { get; private set; } = MoonTowerDifficulty.Easy;
    public MoonTowerMode Mode => _state.Mode;
    public int ElapsedSeconds => _elapsed;
    public bool IsPaused => _paused;

    public void NewGame(MoonTowerMode mode, MoonTowerDifficulty difficulty, int? seed = null)
    {
        Difficulty = difficulty;
        _state = MoonTowerEngine.NewGame(mode, seed ?? Environment.TickCount);
        _elapsed = 0;
        _paused = false;
        _reportedOver = false;
        ResetSelection();
        _clock.Start();
        Render();
        Tip?.Invoke(Loc.T("Games.YourTurn"));
    }

    public void Resume(MoonTowerSave save)
    {
        _state = save.State;
        _elapsed = save.ElapsedSeconds;
        _paused = false;
        _reportedOver = false;
        MoonTowerEngine.CheckEnd(_state);
        ResetSelection();
        Render();
        if (!_state.Finished && _state.Turn == MoonTowerPlayer.Luna) ScheduleLuna();
    }

    public void SetDifficulty(MoonTowerDifficulty difficulty) => Difficulty = difficulty;

    public void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused) _aiTimer.Stop();
        Render();
        if (!paused && !_state.Finished && _state.Turn == MoonTowerPlayer.Luna) ScheduleLuna();
    }

    public void Stop()
    {
        _clock.Stop();
        _aiTimer.Stop();
        _bounceTimer.Stop();
    }

    private static double Bounce(double t) =>
        t < 0.45 ? Math.Sin(t / 0.45 * Math.PI / 2) : 1 - Math.Sin((t - 0.45) / 0.55 * Math.PI) * 0.18 * (1 - t);

    public MoonTowerHint? Hint() => MoonTowerEngine.Hint(_state, MoonTowerPlayer.Child);

    public void Highlight(MoonTowerHint hint)
    {
        _highlight = hint.Move;
        Render();
    }

    public string StateSummary() => MoonTowerEngine.DescribeState(_state);

    public MoonTowerSave Snapshot() => new()
    {
        ChildId = _childId,
        ElapsedSeconds = _elapsed,
        SavedUtc = DateTime.UtcNow,
        State = _state
    };

    public void Save()
    {
        if (_state.Finished)
        {
            SaveUpdated?.Invoke();
            return;
        }
        MoonTowerSaveStore.Save(_childId, Snapshot());
        SaveUpdated?.Invoke();
    }

    public static MoonTowerSave? LoadSave(string childId) => MoonTowerSaveStore.Load(childId);

    public static void ClearSave(string childId) => MoonTowerSaveStore.Clear(childId);

    private void ResetSelection()
    {
        _selected = null;
        _meteorArmed = false;
        _swapArmed = false;
        _highlight = null;
        _dragging = false;
        _pressHand = -1;
        RemoveGhost();
    }

    private void Render()
    {
        _renderCount++;
        var nowMs = Environment.TickCount64;
        _renderTimes.Add(nowMs);
        _renderTimes.RemoveAll(t => t < nowMs - 1000);
        if (_renderTimes.Count > 5 && nowMs - _lastRenderRateLog >= 1000)
        {
            _lastRenderRateLog = nowMs;
            LunaLog.Write($"game_render_rate renders={_renderTimes.Count} window_ms=1000");
        }
        RenderStatus();
        RenderTower();
        RenderHand();
        if (_state.Finished && !_reportedOver)
        {
            ReportGameOver();
            return;
        }
        if (_paused) ShowPauseOverlay();
        else if (!_state.Finished) HideOverlayCards();
    }

    private void RenderStatus()
    {
        _status.Children.Clear();
        var childTurn = _state.Turn == MoonTowerPlayer.Child && !_state.Finished;
        _status.Children.Add(ScoreChip("ScoreChip_Child", Loc.T("Games.MoonTower.Name") == "" ? "" : ChildName(),
            _state.ChildScore, "#5bd3ff", childTurn));
        _status.Children.Add(ScoreChip("ScoreChip_Luna", "Luna", _state.LunaScore, "#ff9ad5",
            _state.Turn == MoonTowerPlayer.Luna && !_state.Finished));
        _bagLabel.Text = Loc.T("Games.Bag", _state.Bag.Count);
    }

    private string ChildName() => _childName;

    private Control ScoreChip(string key, string name, int score, string accent, bool active)
    {
        var color = Color.Parse(accent);
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        stack.Children.Add(Text(name, 13, _palette.Text, FontWeight.Bold));
        stack.Children.Add(Text(score.ToString(CultureInfo.InvariantCulture), 20, color,
            FontWeight.SemiBold, display: true));
        if (active) stack.Children.Add(Text(Loc.T("Games.YourTurn"), 10, _palette.Accent, FontWeight.Bold));
        return new Border
        {
            Name = key,
            Background = LunaBrushes.B(_palette.Surface),
            BorderBrush = LunaBrushes.B(active ? color : _palette.Surface2),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 5),
            Child = stack
        };
    }

    private void RenderTower()
    {
        _towerCanvas.Children.Clear();
        _towerCanvas.Width = LabelW + _state.Width * CellW + 32;
        _towerCanvas.Height = _state.Levels * CellH + 40;

        for (var level = 0; level < _state.Levels; level++)
        {
            var y = Y(level);
            var complete = MoonTowerEngine.IsLevelComplete(_state, level);
            var label = Text($"{(complete ? "✓ " : "")}{Loc.T("Games.LevelLabel", level + 1)}", 10,
                complete ? _palette.Success : _palette.Soft, FontWeight.Bold);
            label.Width = LabelW - 8;
            label.TextAlignment = TextAlignment.Right;
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, y + 12);
            _towerCanvas.Children.Add(label);

            for (var col = 0; col < _state.Width; col++)
            {
                var cell = new Border
                {
                    Width = CellW - 2,
                    Height = CellH - 2,
                    CornerRadius = new CornerRadius(6),
                    Background = LunaBrushes.B(_palette.Sunken),
                    BorderBrush = LunaBrushes.B(_palette.Surface2),
                    BorderThickness = new Thickness(1),
                    Opacity = 0.55,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(cell, X(col) + 1);
                Canvas.SetTop(cell, y + 1);
                _towerCanvas.Children.Add(cell);
            }
        }

        foreach (var placed in _state.Placed)
        {
            var block = new Border
            {
                Width = placed.Length * CellW - 4,
                Height = CellH - 6,
                CornerRadius = new CornerRadius(10),
                Background = LunaBrushes.B(SymbolColor(placed.Symbol)),
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    OffsetY = 4, Blur = 0, Spread = -1, Color = Color.FromArgb(80, 0, 0, 0)
                }),
                Child = BlockFace(placed.Symbol, placed.Length, placed.Power),
                IsHitTestVisible = false,
                Tag = placed.BlockId
            };
            Canvas.SetLeft(block, X(placed.Start) + 2);
            Canvas.SetTop(block, Y(placed.Level) + 3);
            if (placed.BlockId == _bounceBlockId)
            {
                var transform = new TranslateTransform();
                block.RenderTransform = transform;
                _bounceTransforms[placed.BlockId] = transform;
            }
            _towerCanvas.Children.Add(block);
        }

        RenderDropSpots();
        RenderMeteorTargets();

        var baseBar = new Border
        {
            Width = _state.Width * CellW,
            Height = 13,
            CornerRadius = new CornerRadius(0, 0, 14, 14),
            Background = LunaBrushes.B(Color.Parse("#cfd3dc")),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(baseBar, X(0));
        Canvas.SetTop(baseBar, 6 + _state.Levels * CellH);
        _towerCanvas.Children.Add(baseBar);

        for (var col = 0; col < _state.Width; col++)
        {
            var number = Text((col + 1).ToString(CultureInfo.InvariantCulture), 9, _palette.Soft, FontWeight.Bold);
            number.Width = CellW;
            number.TextAlignment = TextAlignment.Center;
            Canvas.SetLeft(number, X(col));
            Canvas.SetTop(number, 6 + _state.Levels * CellH + 14);
            _towerCanvas.Children.Add(number);
        }

        var flag = Text("⚑", 20, Color.Parse("#f6dc7e"), FontWeight.Bold);
        Canvas.SetLeft(flag, X(_state.Width) + 1);
        Canvas.SetTop(flag, Y(_state.Levels - 1) - 2);
        _towerCanvas.Children.Add(flag);
    }

    private void RenderDropSpots()
    {
        if (_state.Finished || _paused || _state.Turn != MoonTowerPlayer.Child) return;
        if (_selected is not { } index || _swapArmed || _meteorArmed) return;
        var hand = _state.ChildHand;
        if (index < 0 || index >= hand.Count) return;
        var block = hand[index];
        if (!block.IsPlaceable) return;

        for (var level = 0; level < _state.Levels; level++)
        {
            for (var start = 0; start + block.Length <= _state.Width; start++)
            {
                if (!MoonTowerEngine.IsPlacementLegal(_state, level, start, block.Length)) continue;
                var spot = new Button
                {
                    Width = block.Length * CellW - 4,
                    Height = CellH - 6,
                    Background = LunaBrushes.B(Color.FromArgb(40, 255, 246, 200)),
                    BorderBrush = LunaBrushes.B(_palette.Accent),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(10),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Focusable = true,
                    Tag = new MoonTowerMove(MoonTowerMoveKind.Place, index, level, start)
                };
                AutomationProperties.SetName(spot,
                    Loc.T("Games.LevelLabel", level + 1) + " " + (start + 1));
                spot.Click += (_, _) =>
                {
                    var move = (MoonTowerMove)spot.Tag!;
                    LunaLog.Write($"game_input spot_click level={move.Level} start={move.Start} hand={move.HandIndex}");
                    PlayChildMove(move);
                };
                Canvas.SetLeft(spot, X(start) + 2);
                Canvas.SetTop(spot, Y(level) + 3);
                _towerCanvas.Children.Add(spot);
            }
        }

        if (_highlight is { } hint && hint.Kind == MoonTowerMoveKind.Place)
        {
            var pulse = new Border
            {
                Width = block.Length * CellW - 4,
                Height = CellH - 6,
                CornerRadius = new CornerRadius(10),
                BorderBrush = LunaBrushes.B(_palette.Accent),
                BorderThickness = new Thickness(3),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(pulse, X(hint.Start) + 2);
            Canvas.SetTop(pulse, Y(hint.Level) + 3);
            _towerCanvas.Children.Add(pulse);
        }
    }

    private void RenderMeteorTargets()
    {
        if (!_meteorArmed || _paused || _state.Finished) return;
        foreach (var top in MoonTowerEngine.TopBlocks(_state))
        {
            var button = new Button
            {
                Width = top.Length * CellW - 4,
                Height = CellH - 6,
                Background = LunaBrushes.B(Color.FromArgb(70, 255, 143, 177)),
                BorderBrush = LunaBrushes.B(Color.Parse("#ff8fb1")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Tag = new MoonTowerMove(MoonTowerMoveKind.Meteor, _selected ?? 0, top.Level, top.Start, top.BlockId),
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            AutomationProperties.SetName(button, Loc.T("Games.Meteor"));
            button.Click += (_, _) => PlayChildMove((MoonTowerMove)button.Tag!);
            Canvas.SetLeft(button, X(top.Start) + 2);
            Canvas.SetTop(button, Y(top.Level) + 3);
            _towerCanvas.Children.Add(button);
        }
    }

    private void RenderHand()
    {
        _handPanel.Children.Clear();
        var hand = _state.ChildHand;
        var myTurn = _state.Turn == MoonTowerPlayer.Child && !_state.Finished && !_paused;

        for (var i = 0; i < hand.Count; i++)
        {
            var block = hand[i];
            // Capture the index in a local: a lambda over the loop variable would
            // see the final value after the loop (5), so every click and drag was
            // delivered to an out-of-range hand index and silently did nothing.
            var handIndex = i;
            var selected = _selected == handIndex || (_swapArmed && _selected is null);
            var border = HandBlock(block, selected);
            border.Name = "HandBlock_" + handIndex;
            border.Opacity = myTurn ? 1 : 0.65;
            border.Tag = handIndex;
            border.Focusable = true;
            AutomationProperties.SetName(border, BlockName(block));
            // Button's class handler marks PointerPressed/Released handled, so a
            // plain += handler never sees the press (no selection, no drag).
            border.AddHandler(InputElement.PointerPressedEvent,
                (sender, e) => OnHandPressed(e, handIndex, (Control)sender!),
                RoutingStrategies.Tunnel, handledEventsToo: true);
            border.AddHandler(InputElement.PointerMovedEvent,
                (_, e) => OnHandMoved(e, handIndex), RoutingStrategies.Tunnel,
                handledEventsToo: true);
            border.AddHandler(InputElement.PointerReleasedEvent,
                (sender, e) => OnHandReleased(e, handIndex, (Control)sender!),
                RoutingStrategies.Tunnel, handledEventsToo: true);
            // Button.Click is the reliable tap path (it also serves keyboard and
            // automation). A real drag sets _lastPointerReleaseAt so its trailing
            // Click does not toggle the block after the drop.
            border.Click += (_, _) =>
            {
                if (_lastPointerReleaseAt >= 0 && Environment.TickCount64 - _lastPointerReleaseAt < 500) return;
                LunaLog.Write($"game_input click_fallback index={handIndex}");
                _pressHand = -1;
                _dragging = false;
                TapHand(handIndex);
            };
            if (myTurn) border.Cursor = new Cursor(StandardCursorType.Hand);
            _handPanel.Children.Add(border);
        }

        if (myTurn)
        {
            var swap = ChromeButton(Loc.T("Games.Swap"), _swapArmed);
            swap.Name = "GameSwap";
            swap.Click += (_, _) => ArmSwap();
            _handPanel.Children.Add(swap);
        }
    }

    private Button HandBlock(MoonTowerBlock block, bool selected)
    {
        var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        face.Children.Add(SymbolIcon(block.Symbol, Ink, 14));
        if (block.Power == MoonTowerPower.None || block.Power is MoonTowerPower.Eclipse or MoonTowerPower.DoubleStar)
            face.Children.Add(Text(block.Length.ToString(CultureInfo.InvariantCulture), 16, Ink, FontWeight.SemiBold, display: true));
        if (block.Power != MoonTowerPower.None)
            face.Children.Add(Text(PowerWord(block.Power), 10, Ink, FontWeight.Bold, wrap: true));

        return new Button
        {
            Width = block.IsPlaceable ? Math.Max(44, block.Length * 34) : 92,
            Height = 46,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(10),
            Background = LunaBrushes.B(block.Power is MoonTowerPower.Rocket or MoonTowerPower.Meteor
                ? _palette.Surface
                : SymbolColor(block.Symbol)),
            BorderBrush = LunaBrushes.B(selected ? _palette.Accent : (block.Power is MoonTowerPower.Rocket or MoonTowerPower.Meteor
                ? _palette.Accent : Color.FromArgb(40, 0, 0, 0))),
            BorderThickness = new Thickness(selected ? 3 : (block.Power is MoonTowerPower.Rocket or MoonTowerPower.Meteor ? 2 : 1)),
            Content = block.Power is MoonTowerPower.Rocket or MoonTowerPower.Meteor
                ? new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        PowerIcon(block.Power, _palette.Accent),
                        Text(PowerWord(block.Power), 12, _palette.Accent, FontWeight.SemiBold, display: true)
                    }
                }
                : face
        };
    }

    private void OnHandPressed(PointerPressedEventArgs e, int index, Control source)
    {
        LunaLog.Write($"game_input hand_press index={index} turn={_state.Turn} paused={_paused} finished={_state.Finished}");
        if (_paused || _state.Finished || _state.Turn != MoonTowerPlayer.Child) return;
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed && e.Pointer.Type is not (PointerType.Touch or PointerType.Pen)) return;
        _pressPoint = e.GetPosition(this);
        _pressHand = index;
        _dragging = false;
        _lastPointerReleaseAt = -1;
        // Capture the block itself, not the overlay: the overlay is not hit-testable
        // while the board is live, and replacing the captured control on Render()
        // would lose the drag.
        e.Pointer.Capture(source);
    }

    private void OnHandMoved(PointerEventArgs e, int index)
    {
        if (_pressHand != index) return;
        var now = e.GetPosition(this);
        var dx = now.X - _pressPoint.X;
        var dy = now.Y - _pressPoint.Y;
        if (!_dragging && Math.Sqrt(dx * dx + dy * dy) > 7)
        {
            _dragging = true;
            _selected = index;
            _meteorArmed = false;
            _swapArmed = false;
            LunaLog.Write($"game_input drag_start index={index}");
            // Render the tower only. Rebuilding the hand here would destroy the
            // Button that currently owns the pointer capture.
            RenderTower();
        }
        if (!_dragging) return;
        ShowGhost(index, e.GetPosition(_overlay));
    }

    private void OnHandReleased(PointerReleasedEventArgs e, int index, Control source)
    {
        if (_pressHand != index) return;
        var wasDragging = _dragging;
        _pressHand = -1;
        _dragging = false;
        RemoveGhost();
        e.Pointer.Capture(null);
        _lastPointerReleaseAt = Environment.TickCount64;
        if (wasDragging)
        {
            LunaLog.Write($"game_input drop_release index={index}");
            DropAt(index, e.GetPosition(_towerCanvas));
        }
        else if (e.InitialPressMouseButton is MouseButton.Left or MouseButton.None)
        {
            LunaLog.Write($"game_input hand_click index={index}");
            TapHand(index);
        }
    }

    private void DropAt(int index, Point point)
    {
        if (index < 0 || index >= _state.ChildHand.Count) return;
        var block = _state.ChildHand[index];
        if (!block.IsPlaceable) { TapHand(index); return; }
        var column = (int)Math.Floor((point.X - X(0)) / CellW);
        // Y(0) is the bottom level and Y(Levels-1) the top; the old formula
        // measured from the bottom and mirrored every drop to the wrong level.
        var levelFromTop = (int)Math.Floor((point.Y - Y(_state.Levels - 1)) / CellH);
        var level = _state.Levels - 1 - levelFromTop;
        var length = block.Length;
        var start = column - length / 2;
        start = Math.Clamp(start, 0, Math.Max(0, _state.Width - length));
        if (levelFromTop < 0 || levelFromTop >= _state.Levels)
        {
            LunaLog.Write($"game_input drop_outside level_from_top={levelFromTop}");
            Tip?.Invoke(Loc.T("Games.Invalid"));
            Render();
            return;
        }
        var legal = level >= 0 && level < _state.Levels &&
                    MoonTowerEngine.IsPlacementLegal(_state, level, start, length);
        LunaLog.Write($"game_input drop index={index} level={level} start={start} legal={legal}");
        if (!legal)
        {
            Tip?.Invoke(Loc.T("Games.Invalid"));
            Render();
            return;
        }
        PlayChildMove(new MoonTowerMove(MoonTowerMoveKind.Place, index, level, start));
    }

    private void TapHand(int index)
    {
        if (index < 0 || index >= _state.ChildHand.Count) return;
        var now = Environment.TickCount64;
        LunaLog.Write($"game_input tap_enter index={index} last={_lastTapIndex} since_ms={now - _lastTapAt}");
        if (index == _lastTapIndex && now - _lastTapAt < 300) return;
        var block = _state.ChildHand[index];
        LunaLog.Write($"game_input tap index={index} swap={_swapArmed} power={block.Power}");
        _lastTapIndex = index;
        _lastTapAt = now;
        if (_swapArmed)
        {
            PlayChildMove(new MoonTowerMove(MoonTowerMoveKind.Swap, index));
            return;
        }
        if (block.Power == MoonTowerPower.Rocket)
        {
            PlayChildMove(new MoonTowerMove(MoonTowerMoveKind.Rocket, index));
            return;
        }
        if (block.Power == MoonTowerPower.Meteor)
        {
            _selected = index;
            _meteorArmed = true;
            _swapArmed = false;
            Render();
            Tip?.Invoke(Loc.T("Games.Meteor"));
            return;
        }
        _selected = _selected == index ? null : index;
        _meteorArmed = false;
        _swapArmed = false;
        LunaLog.Write($"game_input select index={index} selected={_selected}");
        Render();
    }

    private void ArmSwap()
    {
        _swapArmed = !_swapArmed;
        LunaLog.Write($"game_input swap_arm active={_swapArmed}");
        _selected = null;
        _meteorArmed = false;
        Render();
        Tip?.Invoke(Loc.T("Games.SwapHint"));
    }

    private void PlayChildMove(MoonTowerMove move)
    {
        if (_paused || _state.Finished || _state.Turn != MoonTowerPlayer.Child) return;
        try
        {
            LunaLog.Write($"game_input move kind={move.Kind} hand={move.HandIndex} level={move.Level} start={move.Start}");
            var events = MoonTowerEngine.Apply(_state, MoonTowerPlayer.Child, move);
            ResetSelection();
            StartBounce();
            HandleEvents(events, MoonTowerPlayer.Child);
            Save();
            Render();
            if (!_state.Finished && _state.Turn == MoonTowerPlayer.Luna) ScheduleLuna();
        }
        catch (InvalidOperationException)
        {
            Tip?.Invoke(Loc.T("Games.Invalid"));
        }
    }

    private void StartBounce()
    {
        if (_state.LastEvents.Any(e => e.Kind == MoonTowerEventKind.Placed))
        {
            var placed = _state.Placed.LastOrDefault();
            if (placed is null) return;
            _bounceBlockId = placed.BlockId;
            _bounceStart = Environment.TickCount64 / 1000.0;
            _bounceTimer.Start();
        }
    }

    private void ScheduleLuna()
    {
        if (_paused || _state.Finished) return;
        _aiTimer.Stop();
        _aiTimer.Start();
    }

    private void PlayLunaTurn()
    {
        if (_paused || _state.Finished || _state.Turn != MoonTowerPlayer.Luna) return;
        var move = MoonTowerAi.Choose(_state, Difficulty);
        if (move is null)
        {
            MoonTowerEngine.CheckEnd(_state);
            Render();
            return;
        }
        try
        {
            var events = MoonTowerEngine.Apply(_state, MoonTowerPlayer.Luna, move);
            StartBounce();
            HandleEvents(events, MoonTowerPlayer.Luna);
            Save();
            Render();
            if (!_state.Finished && _state.Turn == MoonTowerPlayer.Luna) ScheduleLuna();
        }
        catch (InvalidOperationException)
        {
            MoonTowerEngine.CheckEnd(_state);
            Render();
        }
    }

    private void HandleEvents(List<MoonTowerEvent> events, MoonTowerPlayer player)
    {
        if (events.Any(e => e.Kind == MoonTowerEventKind.LevelComplete))
        {
            var level = events.First(e => e.Kind == MoonTowerEventKind.LevelComplete).Level;
            Tip?.Invoke(_lines.Next(LunaLineEvent.LevelComplete, Loc.Language, level + 1, _state.Width));
            Sparkle(level);
            return;
        }
        if (events.Any(e => e.Kind == MoonTowerEventKind.PowerUsed))
        {
            Tip?.Invoke(_lines.Next(LunaLineEvent.PowerUsed, Loc.Language));
            return;
        }
        if (events.Any(e => e.Kind == MoonTowerEventKind.BagEmpty))
        {
            Tip?.Invoke(_lines.Next(LunaLineEvent.BagEmpty, Loc.Language));
            return;
        }
        if (_state.Finished)
        {
            Tip?.Invoke(_lines.Next(player == MoonTowerPlayer.Child
                ? (MoonTowerEngine.ResultFor(_state, MoonTowerPlayer.Child) == MoonTowerResult.Win ? LunaLineEvent.Win : LunaLineEvent.Loss)
                : LunaLineEvent.Idle, Loc.Language));
            return;
        }
        if (player == MoonTowerPlayer.Luna)
            Tip?.Invoke(_lines.Next(LunaLineEvent.Idle, Loc.Language));
        else if (events.Any(e => e.Kind == MoonTowerEventKind.Placed && e.Value > 0))
        {
            var close = Enumerable.Range(0, _state.Levels)
                .Where(l => MoonTowerEngine.RowSum(_state, l) == _state.Width - 1)
                .Select(l => (int?)l).FirstOrDefault();
            Tip?.Invoke(close is { } level
                ? _lines.Next(LunaLineEvent.CloseCall, Loc.Language, level + 1, 1)
                : _lines.Next(LunaLineEvent.GoodMove, Loc.Language));
        }
    }

    private void Sparkle(int level)
    {
        for (var i = 0; i < 6; i++)
        {
            var star = Text("✦", 16 + i % 3 * 4, Color.Parse("#fff6c8"), FontWeight.Bold);
            Canvas.SetLeft(star, X(i * 2) + 10);
            Canvas.SetTop(star, Y(level) + 4);
            star.IsHitTestVisible = false;
            _towerCanvas.Children.Add(star);
        }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            foreach (var child in _towerCanvas.Children.OfType<TextBlock>()
                         .Where(t => t.Text == "✦").ToList())
                _towerCanvas.Children.Remove(child);
        };
        timer.Start();
    }

    private void ReportGameOver()
    {
        _reportedOver = true;
        _clock.Stop();
        MoonTowerSaveStore.Clear(_childId);
        var result = MoonTowerEngine.ResultFor(_state, MoonTowerPlayer.Child);
        ShowEndCard(result);
        GameOver?.Invoke(new MoonTowerOutcome(result, _state.ChildScore, Math.Max(1, _elapsed),
            _state.Mode, Difficulty));
    }

    private void ShowEndCard(MoonTowerResult result)
    {
        HideOverlayCards();
        _overlay.Background = Brushes.Transparent;
        _overlay.IsHitTestVisible = true;
        var title = result switch
        {
            MoonTowerResult.Win => Loc.T("Games.Win"),
            MoonTowerResult.Loss => Loc.T("Games.Loss"),
            _ => Loc.T("Games.Draw")
        };
        var stack = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(Text(title, 26, _palette.Text, FontWeight.SemiBold, display: true, center: true));
        stack.Children.Add(Text($"{ChildName()} {_state.ChildScore} · Luna {_state.LunaScore}", 15,
            _palette.Muted, FontWeight.Bold, center: true));
        var again = new Button
        {
            Content = Loc.T("Games.NewGame"),
            Height = 46,
            CornerRadius = new CornerRadius(14),
            Background = LunaBrushes.B(_palette.Accent),
            Foreground = LunaBrushes.B(_palette.AccentInk),
            Padding = new Thickness(22, 0),
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        again.Click += (_, _) =>
        {
            HideOverlayCards();
            NewGame(_state.Mode, Difficulty);
        };
        stack.Children.Add(again);

        var card = new Border
        {
            Background = LunaBrushes.B(_palette.Surface),
            BorderBrush = LunaBrushes.B(_palette.Border),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(28),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = stack
        };
        _overlay.Children.Add(new Border
        {
            Background = LunaBrushes.B(Color.FromArgb(150, 10, 6, 35)),
            Child = card,
            Tag = "card"
        });
    }

    private void ShowPauseOverlay()
    {
        HideOverlayCards();
        _overlay.Background = Brushes.Transparent;
        _overlay.IsHitTestVisible = true;
        var stack = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(Text(Loc.T("Games.Paused"), 24, _palette.Text, FontWeight.SemiBold, display: true));
        var resume = new Button
        {
            Content = Loc.T("Games.Resume"),
            Height = 44,
            CornerRadius = new CornerRadius(14),
            Background = LunaBrushes.B(_palette.Accent),
            Foreground = LunaBrushes.B(_palette.AccentInk),
            Padding = new Thickness(20, 0),
            FontWeight = FontWeight.Bold
        };
        resume.Click += (_, _) =>
        {
            _paused = false;
            Render();
        };
        stack.Children.Add(resume);
        _overlay.Children.Add(new Border
        {
            Background = LunaBrushes.B(Color.FromArgb(150, 10, 6, 35)),
            Child = new Border
            {
                Background = LunaBrushes.B(_palette.Surface),
                BorderBrush = LunaBrushes.B(_palette.Border),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(28),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = stack
            },
            Tag = "card"
        });
    }

    private void HideOverlayCards()
    {
        foreach (var card in _overlay.Children.Where(c => c is Control { Tag: "card" }).ToList())
            _overlay.Children.Remove(card);
        if (_ghost is null)
        {
            _overlay.Background = null;
            _overlay.IsHitTestVisible = false;
        }
    }

    private void ShowGhost(int index, Point point)
    {
        RemoveGhost();
        if (index < 0 || index >= _state.ChildHand.Count) return;
        var block = _state.ChildHand[index];
        var ghost = HandBlock(block, true);
        ghost.Opacity = 0.8;
        ghost.IsHitTestVisible = false;
        ghost.HorizontalAlignment = HorizontalAlignment.Left;
        ghost.VerticalAlignment = VerticalAlignment.Top;
        ghost.Margin = new Thickness(point.X - ghost.Width / 2, point.Y - ghost.Height / 2, 0, 0);
        _overlay.Children.Add(ghost);
        _ghost = ghost;
    }

    private void RemoveGhost()
    {
        if (_ghost is not null) _overlay.Children.Remove(_ghost);
        _ghost = null;
    }

    private void OnTowerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging || _pressHand < 0) return;
        var index = _pressHand;
        _pressHand = -1;
        _dragging = false;
        RemoveGhost();
        e.Pointer.Capture(null);
        DropAt(index, e.GetPosition(_towerCanvas));
    }

    private void OnTowerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging && _pressHand >= 0) ShowGhost(_pressHand, e.GetPosition(_overlay));
    }

    private static double X(int column) => LabelW + column * CellW;
    private static double Y(int level) => 6 + (MoonTowerConfig.Levels - 1 - level) * CellH;

    private static Color SymbolColor(MoonTowerSymbol symbol) => symbol switch
    {
        MoonTowerSymbol.Sun => SunColor,
        MoonTowerSymbol.Moon => MoonColor,
        MoonTowerSymbol.Star => StarColor,
        _ => CometColor
    };

    private static string PowerWord(MoonTowerPower power) => power switch
    {
        MoonTowerPower.Rocket => Loc.T("Games.Power.Rocket"),
        MoonTowerPower.Meteor => Loc.T("Games.Power.Meteor"),
        MoonTowerPower.Eclipse => Loc.T("Games.Power.Eclipse"),
        MoonTowerPower.DoubleStar => Loc.T("Games.Power.DoubleStar"),
        _ => ""
    };

    private static string BlockName(MoonTowerBlock block) =>
        block.Power != MoonTowerPower.None ? PowerWord(block.Power) : $"{block.Symbol} {block.Length}";

    private Control BlockFace(MoonTowerSymbol symbol, int length, MoonTowerPower power)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(SymbolIcon(symbol, Ink, 13));
        panel.Children.Add(new Border
        {
            Background = LunaBrushes.B(Color.FromArgb(120, 255, 255, 255)),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(5, 0),
            Child = Text(power == MoonTowerPower.Eclipse ? "2" : length.ToString(CultureInfo.InvariantCulture),
                15, Ink, FontWeight.SemiBold, display: true)
        });
        return panel;
    }

    private static Control SymbolIcon(MoonTowerSymbol symbol, Color color, double size)
    {
        var canvas = new Canvas { Width = size, Height = size };
        switch (symbol)
        {
            case MoonTowerSymbol.Sun:
                canvas.Children.Add(new Ellipse
                {
                    Width = size * 0.5, Height = size * 0.5,
                    Fill = LunaBrushes.B(color)
                });
                for (var i = 0; i < 8; i++)
                {
                    var angle = Math.PI * 2 * i / 8;
                    var ray = new Rectangle { Width = 1.6, Height = size * 0.22, Fill = LunaBrushes.B(color),
                        RenderTransform = new RotateTransform(i * 45, 0.8, 0) };
                    Canvas.SetLeft(ray, size / 2 - 0.8 + Math.Cos(angle) * size * 0.34);
                    Canvas.SetTop(ray, size / 2 - 0.8 + Math.Sin(angle) * size * 0.34);
                    canvas.Children.Add(ray);
                }
                Canvas.SetLeft(canvas.Children[0], size * 0.25);
                Canvas.SetTop(canvas.Children[0], size * 0.25);
                break;
            case MoonTowerSymbol.Moon:
                canvas.Children.Add(new Ellipse
                {
                    Width = size * 0.72, Height = size * 0.72,
                    Fill = LunaBrushes.B(color)
                });
                Canvas.SetLeft(canvas.Children[0], size * 0.16);
                Canvas.SetTop(canvas.Children[0], size * 0.14);
                canvas.Children.Add(new Ellipse
                {
                    Width = size * 0.58, Height = size * 0.58,
                    Fill = LunaBrushes.B(SymbolColor(MoonTowerSymbol.Moon))
                });
                Canvas.SetLeft(canvas.Children[1], size * 0.34);
                Canvas.SetTop(canvas.Children[1], size * 0.10);
                break;
            case MoonTowerSymbol.Star:
                canvas.Children.Add(new Path
                {
                    Data = Geometry.Parse("M 8,0 L 10,5.4 L 16,6 L 11.5,9.8 L 13,16 L 8,12.6 L 3,16 L 4.5,9.8 L 0,6 L 6,5.4 Z"),
                    Fill = LunaBrushes.B(color),
                    Stretch = Stretch.Uniform,
                    Width = size,
                    Height = size
                });
                break;
            default:
                canvas.Children.Add(new Ellipse
                {
                    Width = size * 0.45, Height = size * 0.45,
                    Fill = LunaBrushes.B(color)
                });
                Canvas.SetLeft(canvas.Children[0], size * 0.5);
                Canvas.SetTop(canvas.Children[0], 0);
                canvas.Children.Add(new Path
                {
                    Data = Geometry.Parse("M 10,3 L 0,14 M 11,6 L 3,15 M 8,1 L 0,8"),
                    Stroke = LunaBrushes.B(color),
                    StrokeThickness = 1.6,
                    Stretch = Stretch.Uniform,
                    Width = size,
                    Height = size
                });
                break;
        }
        return canvas;
    }

    private Control PowerIcon(MoonTowerPower power, Color color)
    {
        if (power == MoonTowerPower.Rocket)
        {
            return new Path
            {
                Data = Geometry.Parse("M 9,0 C 13,3 14,8 12.5,13 L 5.5,13 C 4,8 5,3 9,0 Z M 5.5,13 L 2,17 L 6,16 Z M 12.5,13 L 16,17 L 12,16 Z M 7,16 L 9,20 L 11,16 Z"),
                Fill = LunaBrushes.B(color),
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform
            };
        }
        if (power == MoonTowerPower.DoubleStar)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
            panel.Children.Add(SymbolIcon(MoonTowerSymbol.Star, color, 12));
            panel.Children.Add(SymbolIcon(MoonTowerSymbol.Star, color, 12));
            return panel;
        }
        return SymbolIcon(MoonTowerSymbol.Moon, color, 14);
    }

    private Button ChromeButton(string label, bool active) => new()
    {
        Content = label,
        Height = 46,
        CornerRadius = new CornerRadius(12),
        Background = LunaBrushes.B(active ? _palette.Accent : _palette.Surface),
        Foreground = LunaBrushes.B(active ? _palette.AccentInk : _palette.Text),
        BorderBrush = LunaBrushes.B(_palette.Border),
        BorderThickness = new Thickness(2),
        Padding = new Thickness(12, 0),
        FontWeight = FontWeight.Bold,
        VerticalAlignment = VerticalAlignment.Center
    };

    private TextBlock Text(string text, double size, Color color, FontWeight weight,
        bool display = false, bool center = false, bool wrap = false) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = LunaBrushes.B(color),
        FontWeight = weight,
        FontFamily = display ? LunaThemes.Display : LunaThemes.Body,
        TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center
    };
}

public static class MoonTowerSaveStore
{
    public static string PathFor(string childId) =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpControlParental", $"moontower-{Safe(childId)}.json");

    public static MoonTowerSave? Load(string childId, string? path = null)
    {
        try
        {
            var file = path ?? PathFor(childId);
            if (!System.IO.File.Exists(file)) return null;
            var save = System.Text.Json.JsonSerializer.Deserialize<MoonTowerSave>(
                System.IO.File.ReadAllText(file),
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });
            if (save?.State is null || save.State.Finished || save.State.Width <= 0) return null;
            return save;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string childId, MoonTowerSave save, string? path = null)
    {
        try
        {
            var file = path ?? PathFor(childId);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            System.IO.File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(save,
                new System.Text.Json.JsonSerializerOptions
                {
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }));
        }
        catch
        {
        }
    }

    public static void Clear(string childId, string? path = null)
    {
        try
        {
            var file = path ?? PathFor(childId);
            if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
        }
        catch
        {
        }
    }

    private static string Safe(string value)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "child" : value;
    }
}
