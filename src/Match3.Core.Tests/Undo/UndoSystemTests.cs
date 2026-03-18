using System;
using Match3.Core.Commands;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Tests.TestFixtures;
using Match3.Core.Tests.TestHelpers;
using Match3.Core.Undo;
using Match3.Random;

namespace Match3.Core.Tests.Undo;

public class UndoSystemTests
{
    private readonly UndoSystem _undo = new();

    /// <summary>
    /// Creates an engine with real RNG via GameServiceBuilder for full integration.
    /// </summary>
    private static (SimulationEngine Engine, GameState InitialState) CreateSettledEngine(int seed = 42)
    {
        var session = SimulationTestHelper.CreateSession(seed);
        var engine = session.Engine;
        SimulationTestHelper.SettleCompletely(engine);
        var initial = engine.State.Clone(new XorShift64((ulong)seed));
        return (engine, initial);
    }

    [Fact]
    public void CanUndo_IsFalse_WhenNoCheckpoints()
    {
        Assert.False(_undo.CanUndo);
        Assert.Equal(0, _undo.CheckpointCount);
    }

    [Fact]
    public void SaveCheckpoint_IncrementsCount()
    {
        var (engine, _) = CreateSettledEngine();

        _undo.SaveCheckpoint(engine);

        Assert.Equal(1, _undo.CheckpointCount);
        Assert.True(_undo.CanUndo);
    }

    [Fact]
    public void Undo_RestoresState_AfterOneMove()
    {
        var (engine, _) = CreateSettledEngine();
        SimulationTestHelper.SettleCompletely(engine);

        // Save checkpoint before move
        var stateBefore = engine.State;
        int scoreBefore = stateBefore.Score;
        int moveCountBefore = stateBefore.MoveCount;
        _undo.SaveCheckpoint(engine);

        // Make a move and settle
        bool moved = SimulationTestHelper.TryApplyRandomMove(engine, 0);
        Assert.True(moved);
        SimulationTestHelper.SettleCompletely(engine);

        // Verify state actually changed
        var stateAfterMove = engine.State;

        // Undo
        var checkpoint = _undo.Undo(engine);
        Assert.NotNull(checkpoint);

        // Verify state restored
        Assert.Equal(scoreBefore, engine.State.Score);
        Assert.Equal(moveCountBefore, engine.State.MoveCount);
        Assert.Equal(0, _undo.CheckpointCount);
    }

    [Fact]
    public void Undo_RestoresGridLayout()
    {
        var (engine, _) = CreateSettledEngine(seed: 100);
        SimulationTestHelper.SettleCompletely(engine);

        // Snapshot tile types before move
        var state = engine.State;
        int size = state.Width * state.Height;
        var typesBefore = new ElementType[size];
        for (int i = 0; i < size; i++)
            typesBefore[i] = state.Grid[i].Type;

        _undo.SaveCheckpoint(engine);

        // Make a move
        SimulationTestHelper.TryApplyRandomMove(engine, 0);
        SimulationTestHelper.SettleCompletely(engine);

        // Undo
        _undo.Undo(engine);

        // Verify grid layout matches
        var restored = engine.State;
        for (int i = 0; i < size; i++)
        {
            Assert.Equal(typesBefore[i], restored.Grid[i].Type);
        }
    }

    [Fact]
    public void Undo_MultipleSteps_RestoresEachState()
    {
        var (engine, _) = CreateSettledEngine(seed: 200);
        SimulationTestHelper.SettleCompletely(engine);

        int[] scores = new int[4];
        scores[0] = engine.State.Score;

        // Make 3 moves, saving checkpoint before each
        for (int i = 0; i < 3; i++)
        {
            _undo.SaveCheckpoint(engine);
            SimulationTestHelper.TryApplyRandomMove(engine, i);
            SimulationTestHelper.SettleCompletely(engine);
            scores[i + 1] = engine.State.Score;
        }

        Assert.Equal(3, _undo.CheckpointCount);

        // Undo all 3 moves one by one
        for (int i = 2; i >= 0; i--)
        {
            _undo.Undo(engine);
            Assert.Equal(scores[i], engine.State.Score);
        }

        Assert.Equal(0, _undo.CheckpointCount);
    }

    [Fact]
    public void Undo_ReturnsNull_WhenEmpty()
    {
        var (engine, _) = CreateSettledEngine();

        var result = _undo.Undo(engine);

        Assert.Null(result);
    }

    [Fact]
    public void Undo_EngineIsStable_AfterRestore()
    {
        var (engine, _) = CreateSettledEngine();
        SimulationTestHelper.SettleCompletely(engine);

        _undo.SaveCheckpoint(engine);
        SimulationTestHelper.TryApplyRandomMove(engine, 0);
        SimulationTestHelper.SettleCompletely(engine);

        _undo.Undo(engine);

        Assert.True(engine.IsStable());
    }

    [Fact]
    public void Undo_EngineCanContinue_AfterRestore()
    {
        var (engine, _) = CreateSettledEngine(seed: 300);
        SimulationTestHelper.SettleCompletely(engine);

        _undo.SaveCheckpoint(engine);
        SimulationTestHelper.TryApplyRandomMove(engine, 0);
        SimulationTestHelper.SettleCompletely(engine);

        _undo.Undo(engine);

        // Engine should be able to process a new move after undo
        bool moved = SimulationTestHelper.TryApplyRandomMove(engine, 1);
        if (moved)
        {
            SimulationTestHelper.SettleCompletely(engine);
            Assert.True(engine.IsStable());
        }
    }

    [Fact]
    public void Undo_RestoresRngState()
    {
        var (engine, _) = CreateSettledEngine(seed: 500);
        SimulationTestHelper.SettleCompletely(engine);

        // Capture RNG state before move
        var rngBefore = (XorShift64)engine.State.Random;
        ulong rngStateBefore = rngBefore.GetState();

        _undo.SaveCheckpoint(engine);

        // Manually consume some RNG to simulate state advancement
        engine.State.Random.Next(0, 100);
        engine.State.Random.Next(0, 100);

        var rngAfterConsume = (XorShift64)engine.State.Random;
        Assert.NotEqual(rngStateBefore, rngAfterConsume.GetState());

        // Undo — should restore a fresh RNG with the saved state
        _undo.Undo(engine);

        var rngAfterUndo = (XorShift64)engine.State.Random;
        Assert.Equal(rngStateBefore, rngAfterUndo.GetState());

        // Verify it's a different instance (not shared with original)
        Assert.NotSame(rngBefore, rngAfterUndo);
    }

    [Fact]
    public void Clear_RemovesAllCheckpoints()
    {
        var (engine, _) = CreateSettledEngine();

        _undo.SaveCheckpoint(engine);
        _undo.SaveCheckpoint(engine);
        Assert.Equal(2, _undo.CheckpointCount);

        _undo.Clear();

        Assert.Equal(0, _undo.CheckpointCount);
        Assert.False(_undo.CanUndo);
    }

    [Fact]
    public void Checkpoint_WasBoardStable_IsTrue_WhenStable()
    {
        var (engine, _) = CreateSettledEngine();
        SimulationTestHelper.SettleCompletely(engine);

        _undo.SaveCheckpoint(engine);

        var checkpoint = _undo.Peek();
        Assert.NotNull(checkpoint);
        Assert.True(checkpoint!.WasBoardStable);
    }

    [Fact]
    public void Undo_SkipsRushMoves_ToLastStableCheckpoint()
    {
        var (engine, _) = CreateSettledEngine(seed: 600);
        SimulationTestHelper.SettleCompletely(engine);

        int scoreAtStable = engine.State.Score;

        // Save stable checkpoint
        _undo.SaveCheckpoint(engine);

        // Make a move and partially settle (simulate rush-move scenario)
        SimulationTestHelper.TryApplyRandomMove(engine, 0);
        // Tick a few times but don't fully settle — board is unstable
        for (int i = 0; i < 3; i++)
            engine.Tick();

        // Save checkpoint while board is NOT stable (rush-move)
        _undo.SaveCheckpoint(engine);

        // Make another move (rush move)
        SimulationTestHelper.TryApplyRandomMove(engine, 1);
        SimulationTestHelper.SettleCompletely(engine);

        Assert.Equal(2, _undo.CheckpointCount);

        // Undo should skip the unstable checkpoint and go back to the stable one
        _undo.Undo(engine);

        Assert.Equal(scoreAtStable, engine.State.Score);
        Assert.Equal(0, _undo.CheckpointCount);
    }

    [Fact]
    public void Undo_ReturnsNull_WhenAllCheckpointsAreUnstable()
    {
        var (engine, _) = CreateSettledEngine(seed: 700);
        SimulationTestHelper.SettleCompletely(engine);

        // Make a move, tick partially (board unstable), save checkpoint
        SimulationTestHelper.TryApplyRandomMove(engine, 0);
        for (int i = 0; i < 3; i++)
            engine.Tick();

        _undo.SaveCheckpoint(engine);

        // Make another move while still unstable
        SimulationTestHelper.TryApplyRandomMove(engine, 1);
        for (int i = 0; i < 3; i++)
            engine.Tick();

        _undo.SaveCheckpoint(engine);

        // Both checkpoints are unstable — undo should return null
        var result = _undo.Undo(engine);
        Assert.Null(result);
        Assert.Equal(0, _undo.CheckpointCount);
    }

    [Fact]
    public void Undo_ContinuousToInitialState()
    {
        var (engine, _) = CreateSettledEngine(seed: 800);
        SimulationTestHelper.SettleCompletely(engine);

        // Deep copy initial state (struct assignment shares array refs, so use Clone)
        var initialState = engine.State.Clone(new XorShift64(800));

        // Play 5 moves, saving checkpoint before each
        int movesPlayed = 0;
        for (int i = 0; i < 5; i++)
        {
            _undo.SaveCheckpoint(engine);
            if (!SimulationTestHelper.TryApplyRandomMove(engine, i))
                break;
            SimulationTestHelper.SettleCompletely(engine);
            movesPlayed++;
        }

        Assert.True(movesPlayed >= 2, "Need at least 2 moves for meaningful test");

        // Undo ALL moves back to initial state
        for (int i = 0; i < movesPlayed; i++)
        {
            var checkpoint = _undo.Undo(engine);
            Assert.NotNull(checkpoint);
        }

        // Should be back to the initial state
        Assert.Equal(0, _undo.CheckpointCount);
        SimulationTestHelper.AssertStateEqual(initialState, engine.State, "after full undo to initial");
    }

    [Fact]
    public void Undo_RestoresObstacleLayer()
    {
        var (levelConfig, tileTypesCount) = SimulationTestHelper.GetLevelPreset("WithObstacles");
        var session = SimulationTestHelper.CreateSession(seed: 900, levelConfig: levelConfig, tileTypesCount: tileTypesCount);
        var engine = session.Engine;
        SimulationTestHelper.SettleCompletely(engine);

        // Capture obstacle state before move
        var state = engine.State;
        int size = state.Width * state.Height;
        var obstaclesBefore = new Obstacle[size];
        Array.Copy(state.ObstacleLayer, obstaclesBefore, size);

        _undo.SaveCheckpoint(engine);

        // Play a move
        SimulationTestHelper.TryApplyRandomMove(engine, 0);
        SimulationTestHelper.SettleCompletely(engine);

        // Undo
        _undo.Undo(engine);

        // Verify obstacle layer restored
        var restored = engine.State;
        for (int i = 0; i < size; i++)
        {
            Assert.Equal(obstaclesBefore[i].Type, restored.ObstacleLayer[i].Type);
            Assert.Equal(obstaclesBefore[i].Stage, restored.ObstacleLayer[i].Stage);
        }
    }

    [Fact]
    public void UndoCommand_Execute_DelegatesToUndoSystem()
    {
        var (engine, _) = CreateSettledEngine(seed: 1000);
        SimulationTestHelper.SettleCompletely(engine);

        var cmd = new UndoCommand(_undo) { IssuedAtTick = engine.CurrentTick };

        // No checkpoints — CanExecute false, Execute returns false
        var emptyState = engine.State;
        Assert.False(cmd.CanExecute(in emptyState));
        Assert.False(cmd.Execute(engine));

        // Save checkpoint and make a move
        _undo.SaveCheckpoint(engine);
        SimulationTestHelper.TryApplyRandomMove(engine, 0);
        SimulationTestHelper.SettleCompletely(engine);

        // Now CanExecute and Execute should work
        var stateRef = engine.State;
        Assert.True(cmd.CanExecute(in stateRef));
        Assert.True(cmd.Execute(engine));
        Assert.Equal(0, _undo.CheckpointCount);
    }

    [Fact]
    public void SaveCheckpoint_ThrowsOnNullEngine()
    {
        Assert.Throws<ArgumentNullException>(() => _undo.SaveCheckpoint(null!));
    }

    [Fact]
    public void Undo_ThrowsOnNullEngine()
    {
        Assert.Throws<ArgumentNullException>(() => _undo.Undo(null!));
    }
}
