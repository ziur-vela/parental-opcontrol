// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts.Games;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

public sealed class Job23Tests
{
    private static MoonTowerState Empty(MoonTowerMode mode = MoonTowerMode.Make10)
    {
        var state = MoonTowerEngine.NewGame(mode, 7);
        state.Bag.Clear();
        state.ChildHand.Clear();
        state.LunaHand.Clear();
        state.Placed.Clear();
        state.ChildScore = 0;
        state.LunaScore = 0;
        state.Turn = MoonTowerPlayer.Child;
        state.Finished = false;
        state.Winner = null;
        state.BagEmptyAnnounced = false;
        return state;
    }

    private static MoonTowerBlock Block(int length, MoonTowerSymbol symbol = MoonTowerSymbol.Sun,
        MoonTowerPower power = MoonTowerPower.None, int id = 900) => new(id, length, symbol, power);

    private static MoonTowerPlacedBlock Placed(int length, int level, int start,
        MoonTowerSymbol symbol = MoonTowerSymbol.Sun, MoonTowerPlayer owner = MoonTowerPlayer.Child, int id = 901) =>
        new()
        {
            BlockId = id,
            Length = length,
            Symbol = symbol,
            Power = MoonTowerPower.None,
            Level = level,
            Start = start,
            Owner = owner
        };

    [Fact]
    public void Placement_needs_support_and_no_overhang()
    {
        var state = Empty();
        state.ChildHand.Add(Block(2));

        Assert.False(MoonTowerEngine.IsPlacementLegal(state, 1, 0, 2));
        state.Placed.Add(Placed(2, 0, 0));
        Assert.True(MoonTowerEngine.IsPlacementLegal(state, 1, 0, 2));
        Assert.False(MoonTowerEngine.IsPlacementLegal(state, 1, 0, 3));
        Assert.True(MoonTowerEngine.IsPlacementLegal(state, 1, 0, 2));
    }

    [Fact]
    public void Placement_rejects_overlap_and_out_of_bounds()
    {
        var state = Empty();
        state.Placed.Add(Placed(3, 0, 0));
        Assert.False(MoonTowerEngine.IsPlacementLegal(state, 0, 1, 2));
        Assert.False(MoonTowerEngine.IsPlacementLegal(state, 0, 9, 2));
        Assert.True(MoonTowerEngine.IsPlacementLegal(state, 0, 3, 2));
    }

    [Fact]
    public void Completing_a_level_scores_base_plus_ten()
    {
        var state = Empty();
        state.Placed.Add(Placed(5, 0, 0, MoonTowerSymbol.Sun));
        state.Placed.Add(Placed(3, 0, 5, MoonTowerSymbol.Moon, id: 902));
        state.ChildHand.Add(Block(2, MoonTowerSymbol.Star, id: 903));

        var move = new MoonTowerMove(MoonTowerMoveKind.Place, 0, 0, 8);
        MoonTowerEngine.Apply(state, MoonTowerPlayer.Child, move);

        Assert.Equal(12, state.ChildScore);
        Assert.True(MoonTowerEngine.IsLevelComplete(state, 0));
        Assert.Contains(state.LastEvents, e => e.Kind == MoonTowerEventKind.LevelComplete);
    }

    [Fact]
    public void Landing_on_the_same_symbol_adds_two()
    {
        var state = Empty();
        state.Placed.Add(Placed(5, 0, 0, MoonTowerSymbol.Moon));
        state.ChildHand.Add(Block(2, MoonTowerSymbol.Moon, id: 904));

        var score = MoonTowerEngine.ScorePlacement(state, 1, 0, state.ChildHand[0]);
        Assert.Equal(2, score.Base);
        Assert.Equal(2, score.SymbolBonus);
        Assert.Equal(4, score.Total);
    }

    [Fact]
    public void Eclipse_counts_as_any_symbol()
    {
        var state = Empty();
        state.Placed.Add(Placed(3, 0, 0, MoonTowerSymbol.Comet));
        var eclipse = Block(2, MoonTowerSymbol.Star, MoonTowerPower.Eclipse, 905);

        var score = MoonTowerEngine.ScorePlacement(state, 1, 1, eclipse);
        Assert.Equal(2, score.SymbolBonus);
    }

    [Fact]
    public void Double_star_doubles_the_move()
    {
        var state = Empty();
        state.ChildHand.Add(Block(1, MoonTowerSymbol.Star, MoonTowerPower.DoubleStar, 906));

        var score = MoonTowerEngine.ScorePlacement(state, 0, 0, state.ChildHand[0]);
        Assert.Equal(2, score.Total);
    }

    [Fact]
    public void Rocket_gives_another_turn()
    {
        var state = Empty();
        state.Bag.Add(Block(1, MoonTowerSymbol.Sun, id: 907));
        state.ChildHand.Add(Block(0, MoonTowerSymbol.Star, MoonTowerPower.Rocket, 908));

        MoonTowerEngine.Apply(state, MoonTowerPlayer.Child, new MoonTowerMove(MoonTowerMoveKind.Rocket, 0));

        Assert.Equal(MoonTowerPlayer.Child, state.Turn);
        Assert.Single(state.ChildHand);
    }

    [Fact]
    public void Meteor_removes_a_top_block_without_points()
    {
        var state = Empty();
        state.Bag.Add(Block(1, MoonTowerSymbol.Comet, id: 909));
        state.Placed.Add(Placed(3, 0, 0, id: 910));
        state.ChildHand.Add(Block(0, MoonTowerSymbol.Comet, MoonTowerPower.Meteor, 911));
        state.LunaHand.Add(Block(1, MoonTowerSymbol.Moon, id: 912));

        MoonTowerEngine.Apply(state, MoonTowerPlayer.Child,
            new MoonTowerMove(MoonTowerMoveKind.Meteor, 0, 0, 0, 910));

        Assert.Empty(state.Placed);
        Assert.Equal(0, state.ChildScore);
        Assert.Equal(MoonTowerPlayer.Luna, state.Turn);
    }

    [Fact]
    public void Swap_uses_the_turn_and_draws_one()
    {
        var state = Empty();
        state.Bag.Add(Block(4, MoonTowerSymbol.Star, id: 912));
        state.ChildHand.Add(Block(2, MoonTowerSymbol.Sun, id: 913));
        state.LunaHand.Add(Block(1, MoonTowerSymbol.Moon, id: 914));

        MoonTowerEngine.Apply(state, MoonTowerPlayer.Child, new MoonTowerMove(MoonTowerMoveKind.Swap, 0));

        Assert.Equal(MoonTowerPlayer.Luna, state.Turn);
        Assert.Single(state.ChildHand);
        Assert.Equal(4, state.ChildHand[0].Length);
        Assert.Empty(state.Bag);
    }

    [Fact]
    public void Completing_the_tower_ends_the_game()
    {
        var state = Empty();
        for (var level = 1; level < state.Levels; level++)
        {
            state.Placed.Add(Placed(5, level, 0, owner: MoonTowerPlayer.Luna, id: 920 + level));
            state.Placed.Add(Placed(5, level, 5, owner: MoonTowerPlayer.Luna, id: 930 + level));
        }
        state.Placed.Add(Placed(9, 0, 1, id: 940));
        state.ChildHand.Add(Block(1, MoonTowerSymbol.Comet, id: 941));

        MoonTowerEngine.Apply(state, MoonTowerPlayer.Child, new MoonTowerMove(MoonTowerMoveKind.Place, 0, 0, 0));

        Assert.True(state.Finished);
        Assert.True(MoonTowerEngine.IsTowerComplete(state));
        Assert.Equal(MoonTowerPlayer.Child, state.Winner);
    }

    [Fact]
    public void Empty_bag_and_no_legal_move_ends_the_game()
    {
        var state = Empty();
        state.Placed.Add(Placed(5, 0, 0, id: 950));
        state.Placed.Add(Placed(5, 0, 5, id: 951));
        state.Placed.Add(Placed(5, 1, 0, id: 952));
        state.Placed.Add(Placed(4, 1, 5, id: 953));
        Assert.True(MoonTowerEngine.CheckEnd(state));
        Assert.True(state.Finished);
    }

    [Theory]
    [InlineData(MoonTowerDifficulty.Easy)]
    [InlineData(MoonTowerDifficulty.Medium)]
    [InlineData(MoonTowerDifficulty.Hard)]
    public void Ai_never_makes_an_illegal_move(MoonTowerDifficulty difficulty)
    {
        for (var game = 0; game < 12; game++)
        {
            var state = MoonTowerEngine.NewGame(MoonTowerMode.Make10, 1000 + game);
            var guard = 0;
            while (!state.Finished && guard++ < 400)
            {
                var legal = MoonTowerEngine.LegalMoves(state, state.Turn);
                var move = MoonTowerAi.Choose(state, difficulty, new Random(game * 7919 + guard));
                if (state.Bag.Count == 0 && legal.Count == 0)
                {
                    MoonTowerEngine.CheckEnd(state);
                    break;
                }
                Assert.NotNull(move);
                Assert.Contains(legal, m => MoonTowerEngine.SameMove(m, move!));
                MoonTowerEngine.Apply(state, state.Turn, move!);
            }
            Assert.True(state.Finished, "the game should end");
        }
    }

    [Fact]
    public void Save_and_resume_round_trips()
    {
        var state = MoonTowerEngine.NewGame(MoonTowerMode.Make10, 4242);
        MoonTowerEngine.Apply(state, MoonTowerPlayer.Child,
            MoonTowerEngine.LegalMoves(state, MoonTowerPlayer.Child).First(m => m.Kind == MoonTowerMoveKind.Place));
        MoonTowerEngine.Apply(state, MoonTowerPlayer.Luna,
            MoonTowerEngine.LegalMoves(state, MoonTowerPlayer.Luna).First(m => m.Kind == MoonTowerMoveKind.Place));

        var json = MoonTowerEngine.Serialize(state);
        var copy = MoonTowerEngine.Deserialize(json);

        Assert.NotNull(copy);
        Assert.Equal(state.ChildScore, copy!.ChildScore);
        Assert.Equal(state.LunaScore, copy.LunaScore);
        Assert.Equal(state.Turn, copy.Turn);
        Assert.Equal(state.Bag.Count, copy.Bag.Count);
        Assert.Equal(state.Placed.Count, copy.Placed.Count);
        Assert.Equal(state.ChildHand.Select(b => b.Id), copy.ChildHand.Select(b => b.Id));
        Assert.Equal(state.LunaHand.Select(b => b.Id), copy.LunaHand.Select(b => b.Id));
        Assert.True(MoonTowerEngine.SameMove(MoonTowerEngine.LegalMoves(state, state.Turn)[0],
            MoonTowerEngine.LegalMoves(copy, copy.Turn)[0]));
    }

    [Fact]
    public void Local_save_file_round_trips_and_clears()
    {
        var path = Path.Combine(Path.GetTempPath(), "moontower-job23-" + Guid.NewGuid() + ".json");
        var state = MoonTowerEngine.NewGame(MoonTowerMode.Make10, 99);
        var save = new MoonTowerSave { ChildId = "c1", ElapsedSeconds = 42, State = state };
        MoonTowerSaveStore.Save("c1", save, path);
        var loaded = MoonTowerSaveStore.Load("c1", path);
        Assert.NotNull(loaded);
        Assert.Equal(42, loaded!.ElapsedSeconds);
        Assert.Equal(state.ChildScore, loaded.State.ChildScore);
        Assert.Equal(state.Turn, loaded.State.Turn);
        MoonTowerSaveStore.Clear("c1", path);
        Assert.Null(MoonTowerSaveStore.Load("c1", path));
    }

    [Fact]
    public void Hint_returns_a_legal_placement()
    {
        var state = MoonTowerEngine.NewGame(MoonTowerMode.Make10, 12);
        var hint = MoonTowerEngine.Hint(state, state.Turn);
        Assert.NotNull(hint);
        Assert.Equal(MoonTowerMoveKind.Place, hint!.Move.Kind);
        Assert.Contains(MoonTowerEngine.LegalMoves(state, state.Turn),
            m => MoonTowerEngine.SameMove(m, hint.Move));
        Assert.InRange(hint.Needed, 0, state.Width);
    }

    [Fact]
    public void Game_window_is_ninety_percent_and_centred()
    {
        var big = GameWindowLayout.Large(0, 0, 1920, 1040);
        Assert.Equal(1728, big.Width, 3);
        Assert.Equal(936, big.Height, 3);
        Assert.Equal(96, big.X, 3);
        Assert.Equal(52, big.Y, 3);

        var scaled = GameWindowLayout.Large(0, 0, 1280, 693.333);
        Assert.Equal(1152, scaled.Width, 1);
        Assert.Equal(624, scaled.Height, 1);

        var secondMonitor = GameWindowLayout.Large(1920, 0, 1920, 1040);
        Assert.Equal(2016, secondMonitor.X, 3);

        var clamped = GameWindowLayout.Clamp(new GameWindowRect(-50, -20, 3000, 2000), 0, 0, 1920, 1040);
        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
        Assert.Equal(1920, clamped.Width);
        Assert.Equal(1040, clamped.Height);
    }

    [Fact]
    public void Catalogue_marks_only_moon_tower_playable()
    {
        Assert.True(LunaGameCatalog.Find(LunaGameCatalog.MoonTower)!.Playable);
        Assert.False(LunaGameCatalog.Find(LunaGameCatalog.Orbit)!.Playable);
        Assert.False(LunaGameCatalog.Find(LunaGameCatalog.CraterClash)!.Playable);
        Assert.Equal(3, LunaGameCatalog.InCategory(LunaGameCatalog.SpaceBoards).Count);
        Assert.Equal(3, LunaGameCatalog.InCategory(LunaGameCatalog.LunarCraft).Count);
    }
}
