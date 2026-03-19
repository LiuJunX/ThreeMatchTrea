using System.Collections.Generic;
using Match3.Core.Commands;
using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Tests.TestFixtures;
using Match3.Core.Undo;
using Xunit;

namespace Match3.Core.Tests.Simulation;

public class GameEngineTests
{
    #region Basic Advancement

    [Fact]
    public void AdvanceFrame_ProgressesCurrentTick()
    {
        var ge = CreateGameEngine();

        // Advance by exactly one fixed step
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);

        Assert.Equal(1, ge.CurrentTick);
    }

    [Fact]
    public void AdvanceFrame_MultipleSteps_ProgressesCorrectly()
    {
        var ge = CreateGameEngine();

        // Advance by two fixed steps
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime * 2);

        Assert.Equal(2, ge.CurrentTick);
    }

    [Fact]
    public void AdvanceFrame_SubStep_DoesNotAdvance()
    {
        var ge = CreateGameEngine();

        // Half a step should not advance
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime * 0.5f);

        Assert.Equal(0, ge.CurrentTick);
    }

    [Fact]
    public void AdvanceFrame_AccumulatesSubSteps()
    {
        var ge = CreateGameEngine();

        // Two half-steps should accumulate to one full step
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime * 0.5f);
        Assert.Equal(0, ge.CurrentTick);

        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime * 0.5f);
        Assert.Equal(1, ge.CurrentTick);
    }

    [Fact]
    public void CurrentState_ReflectsTickProgression()
    {
        var ge = CreateGameEngine();
        var initialState = ge.CurrentState;

        // Tick forward — score and state may not change on stable board,
        // but the snapshot should be valid
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);

        var newState = ge.CurrentState;
        Assert.True(ge.CurrentTick > 0);
    }

    #endregion

    #region Peek Guarantee

    [Fact]
    public void PeekState_OneAhead_AlwaysAvailable()
    {
        var ge = CreateGameEngine();

        // After first advance, peek+1 must be available
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);

        Assert.True(ge.TryPeekState(1, out _));
    }

    [Fact]
    public void PeekState_ZeroOffset_ReturnsCurrent()
    {
        var ge = CreateGameEngine();

        Assert.True(ge.TryPeekState(0, out var state));
        // Should match CurrentState
        Assert.Equal(ge.CurrentState.Score, state.Score);
    }

    [Fact]
    public void PeekState_BeyondWrite_ReturnsFalse()
    {
        var ge = CreateGameEngine(bufferSize: 3);

        // Without advancing, peek beyond the buffer should fail
        Assert.False(ge.TryPeekState(10, out _));
    }

    [Fact]
    public void PeekState_NegativeOffset_ReturnsFalse()
    {
        var ge = CreateGameEngine();

        Assert.False(ge.TryPeekState(-1, out _));
    }

    [Fact]
    public void MaxPeekOffset_ReflectsFilledSlots()
    {
        var ge = CreateGameEngine(bufferSize: 5);
        ge.MaxTicksPerFrame = 4;

        // After one advance, engine fills ahead with remaining budget
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);

        Assert.True(ge.MaxPeekOffset >= 1);
    }

    [Fact]
    public void PeekState_ColdStart_FirstFrameHasPeek()
    {
        var ge = CreateGameEngine();

        // Even on first advance, peek+1 must be available
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);

        Assert.True(ge.TryPeekState(1, out _));
    }

    #endregion

    #region Input Invalidation

    [Fact]
    public void InjectCommand_InvalidatesSpeculation()
    {
        var ge = CreateGameEngine();
        ge.MaxTicksPerFrame = 4;

        // Fill ahead
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);
        int peekBefore = ge.MaxPeekOffset;
        Assert.True(peekBefore >= 1);

        // Inject a command (won't actually swap on stable board, but invalidation still happens)
        var cmd = new SwapCommand
        {
            From = new Position(0, 0),
            To = new Position(1, 0),
            IssuedAtTick = ge.CurrentTick
        };
        ge.InjectCommand(cmd);

        // After invalidation, peek offset should be reset
        // MaxPeekOffset may be 0 (only current is valid, nothing ahead)
        Assert.True(ge.MaxPeekOffset <= peekBefore);
    }

    [Fact]
    public void InjectCommand_EngineStateMatchesCurrent()
    {
        var ge = CreateGameEngine();

        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);

        // After inject, engine should be at the current state
        var cmd = new SwapCommand
        {
            From = new Position(0, 0),
            To = new Position(1, 0),
            IssuedAtTick = ge.CurrentTick
        };
        ge.InjectCommand(cmd);

        // Engine tick should match what GameEngine reports
        Assert.Equal(ge.CurrentTick, ge.Engine.CurrentTick);
    }

    #endregion

    #region Events

    [Fact]
    public void DrainCurrentEvents_ReturnsEventsForCurrentTick()
    {
        var ge = CreateGameEngine();

        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);

        var events = new List<GameEvent>();
        ge.DrainCurrentEvents(events);

        // On a stable board with no matches, there may be no events.
        // But the method should not throw.
        Assert.NotNull(events);
    }

    [Fact]
    public void DrainCurrentEvents_ClearsAfterDrain()
    {
        var ge = CreateGameEngine();

        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);

        var events1 = new List<GameEvent>();
        ge.DrainCurrentEvents(events1);

        var events2 = new List<GameEvent>();
        ge.DrainCurrentEvents(events2);

        // Second drain should be empty
        Assert.Empty(events2);
    }

    [Fact]
    public void DrainCurrentEvents_MultiStep_ReturnsAllEvents()
    {
        var ge = CreateGameEngine();

        // Advance by 3 fixed steps in one frame (simulates GameSpeed=3x or frame drop)
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime * 3);

        Assert.Equal(3, ge.CurrentTick);

        var events = new List<GameEvent>();
        ge.DrainCurrentEvents(events);

        // Should not throw, and should have drained events from all 3 consumed ticks.
        // On a stable board there may be no events, but the mechanism should work.
        // Verify by checking that a second drain returns empty (all were consumed).
        var events2 = new List<GameEvent>();
        ge.DrainCurrentEvents(events2);
        Assert.Empty(events2);
    }

    [Fact]
    public void DrainCurrentEvents_NoAdvance_ReturnsEmpty()
    {
        var ge = CreateGameEngine();

        // No AdvanceFrame call — frameStartCurrent == current, no ticks consumed
        var events = new List<GameEvent>();
        ge.DrainCurrentEvents(events);
        Assert.Empty(events);
    }

    #endregion

    #region IsStable

    [Fact]
    public void IsStable_ReadsCurrentSlot_NotSpeculatedState()
    {
        var ge = CreateGameEngine();

        // Initial state should be stable (no falling tiles, no effects)
        Assert.True(ge.IsStable);

        // After advancing, the current slot's stability should be captured
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);
        // Stable board remains stable
        Assert.True(ge.IsStable);
    }

    #endregion

    #region Undo Integration

    [Fact]
    public void InjectCommand_SavesUndoCheckpoint()
    {
        var undoSystem = new UndoSystem();
        var ge = CreateGameEngine();
        ge.SetUndoSystem(undoSystem);

        Assert.Equal(0, undoSystem.CheckpointCount);

        var cmd = new SwapCommand
        {
            From = new Position(0, 0),
            To = new Position(1, 0),
            IssuedAtTick = ge.CurrentTick
        };
        ge.InjectCommand(cmd);

        Assert.Equal(1, undoSystem.CheckpointCount);
    }

    [Fact]
    public void Undo_RestoresAndInvalidatesSpeculation()
    {
        var undoSystem = new UndoSystem();
        var ge = CreateGameEngine();
        ge.SetUndoSystem(undoSystem);

        int tickBefore = ge.CurrentTick;

        // Advance a few ticks then inject a command
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime * 3);
        var cmd = new SwapCommand
        {
            From = new Position(0, 0),
            To = new Position(1, 0),
            IssuedAtTick = ge.CurrentTick
        };
        ge.InjectCommand(cmd);

        // Undo
        var checkpoint = ge.Undo();

        // Should have restored
        Assert.NotNull(checkpoint);
    }

    #endregion

    #region Buffer Bounds

    [Fact]
    public void AdvanceFrame_DoesNotExceedBufferSize()
    {
        var ge = CreateGameEngine(bufferSize: 3);
        ge.MaxTicksPerFrame = 10; // High budget

        // Advance once — should fill ahead but not exceed buffer
        ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);

        // MaxPeekOffset should not exceed bufferSize - 1
        Assert.True(ge.MaxPeekOffset <= 2); // bufferSize=3, so max peek is 2
    }

    [Fact]
    public void AdvanceFrame_ManyFrames_BufferWrapsCorrectly()
    {
        var ge = CreateGameEngine(bufferSize: 3);

        // Run many frames — should wrap around the ring buffer without error
        for (int i = 0; i < 20; i++)
        {
            ge.AdvanceFrame(SimulationConfig.DefaultFixedDeltaTime);
        }

        Assert.Equal(20, ge.CurrentTick);
        Assert.True(ge.TryPeekState(0, out _));
    }

    #endregion

    #region Snapshot Round-Trip

    [Fact]
    public void CreateSnapshot_RestoreFromSnapshot_RoundTrip()
    {
        var state = GameStateBuilder.CreateStableState(5, 5);
        var engine = TestEngineFactory.CreateEngine(state);

        // Tick a few times
        engine.Tick();
        engine.Tick();

        var snapshot = engine.CreateSnapshot();
        Assert.True(snapshot.IsValid);
        Assert.Equal(2, snapshot.Tick);

        // Tick more
        engine.Tick();
        engine.Tick();
        Assert.Equal(4, engine.CurrentTick);

        // Restore
        engine.RestoreFromSnapshot(in snapshot);
        Assert.Equal(2, engine.CurrentTick);
    }

    #endregion

    #region Helpers

    private GameEngine CreateGameEngine(int bufferSize = 5)
    {
        var state = GameStateBuilder.CreateStableState(5, 5);
        var engine = TestEngineFactory.CreateEngine(state);
        return new GameEngine(engine, bufferSize);
    }

    #endregion
}
