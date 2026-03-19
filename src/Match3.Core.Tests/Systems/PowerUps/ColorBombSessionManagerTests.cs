using System.Linq;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.PowerUps;

public class ColorBombSessionManagerTests
{
    private readonly StubEventCollector _events = new();
    private readonly ColorBombConfig _config = new()
    {
        BeamInterval = 0.12f,
        BeamSpeed = 24f,
        MinFlightDuration = 0.08f,
        MaxReScans = 3
    };

    #region Session Creation

    [Fact]
    public void CreateSession_PicksMostFrequentColor()
    {
        var manager = CreateManager();
        var state = CreateState(4, 4);

        // Fill with: 7x Item1 (most), 5x Item2, 3x Item3
        // Bomb at (0,0) = None, doesn't count
        int id = 1;
        var layout = new[]
        {
            ElementType.None,  ElementType.Item1, ElementType.Item1, ElementType.Item1,
            ElementType.Item1, ElementType.Item1, ElementType.Item1, ElementType.Item1,
            ElementType.Item2, ElementType.Item2, ElementType.Item2, ElementType.Item2,
            ElementType.Item2, ElementType.Item3, ElementType.Item3, ElementType.Item3,
        };
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                state.SetTile(x, y, new Tile(id++, layout[y * 4 + x], x, y));

        manager.CreateSession(ref state, new Position(0, 0), 1, 1, 0f, _events);

        Assert.True(manager.HasActiveSessions);
        var startEvt = _events.Events.OfType<ColorBombSessionStartEvent>().Single();
        Assert.Equal(ElementType.Item1, startEvt.TargetColor);
    }

    [Fact]
    public void CreateSession_AllColorsReserved_DoesNothing()
    {
        var manager = CreateManager();
        var state = CreateState(4, 4);

        // Fill all tiles with Item1
        int id = 1;
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                state.SetTile(x, y, new Tile(id++, ElementType.Item1, x, y));

        // First session reserves Item1
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        Assert.True(manager.HasActiveSessions);

        // Second session: only color is Item1, which is reserved
        _events.Clear();
        state.SetTile(3, 3, new Tile(200, ElementType.None, 3, 3));
        manager.CreateSession(ref state, new Position(3, 3), 200, 2, 0f, _events);

        // No new session created
        Assert.Empty(_events.Events.OfType<ColorBombSessionStartEvent>());
    }

    [Fact]
    public void CreateSession_ReservesColor_SecondSessionPicksDifferentColor()
    {
        var manager = CreateManager();
        var state = CreateState(6, 6);

        // Fill with mixed colors
        int id = 1;
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
            {
                var type = (x + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item2;
                state.SetTile(x, y, new Tile(id++, type, x, y));
            }

        // First session at 0,0
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        var first = _events.Events.OfType<ColorBombSessionStartEvent>().Single();
        Assert.True(manager.IsColorReserved(first.TargetColor));

        // Second session at 5,5
        _events.Clear();
        state.SetTile(5, 5, new Tile(200, ElementType.None, 5, 5));
        manager.CreateSession(ref state, new Position(5, 5), 200, 2, 0f, _events);

        var second = _events.Events.OfType<ColorBombSessionStartEvent>().Single();
        Assert.NotEqual(first.TargetColor, second.TargetColor);
    }

    [Fact]
    public void CreateSession_EmitsSessionStartEvent()
    {
        var manager = CreateManager();
        var state = CreateState(4, 4);
        FillState(ref state, ElementType.Item1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 5, 1.5f, _events);

        var evt = _events.Events.OfType<ColorBombSessionStartEvent>().Single();
        Assert.Equal(100, evt.TileId);
        Assert.Equal(new Position(0, 0), evt.Position);
        Assert.Equal(5, evt.Tick);
        Assert.Equal(1.5f, evt.SimulationTime);
    }

    #endregion

    #region Beam Firing

    [Fact]
    public void Update_FiresBeamsWithInterval()
    {
        var manager = CreateManager();
        var state = CreateState(4, 4);
        FillState(ref state, ElementType.Item1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        _events.Clear();

        // First tick: should fire first beam immediately (ShootTimer starts at 0)
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);
        int beamsAfterFirst = _events.Events.OfType<ColorBombBeamLaunchedEvent>().Count();
        Assert.True(beamsAfterFirst >= 1, $"Expected at least 1 beam, got {beamsAfterFirst}");

        _events.Clear();

        // Advance by beam interval → should fire another
        manager.Update(ref state, _config.BeamInterval, 3, 0.13f, _events);
        int beamsAfterSecond = _events.Events.OfType<ColorBombBeamLaunchedEvent>().Count();
        Assert.True(beamsAfterSecond >= 1, $"Expected at least 1 beam after interval, got {beamsAfterSecond}");
    }

    [Fact]
    public void Update_BeamLaunch_LocksTargetCell()
    {
        var manager = CreateManager();
        var state = CreateState(4, 4);
        FillState(ref state, ElementType.Item1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        _events.Clear();

        // Fire beams
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);

        var beamEvt = _events.Events.OfType<ColorBombBeamLaunchedEvent>().First();
        var tp = beamEvt.TargetPosition;

        // Target cell should be locked (Drop, Swap, Matching, Targeting)
        Assert.True(state.IsLocked(tp.X, tp.Y, CellLockType.Drop));
        Assert.True(state.IsLocked(tp.X, tp.Y, CellLockType.Swap));
        Assert.True(state.IsLocked(tp.X, tp.Y, CellLockType.Matching));
        Assert.True(state.IsLocked(tp.X, tp.Y, CellLockType.Targeting));
    }

    [Fact]
    public void Update_SkipsDestroyedTargetOnFire()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);

        // Only 2 targets (positions 1,0 and 2,0)
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0)); // bomb
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        _events.Clear();

        // Destroy first target externally before firing
        state.SetTile(1, 0, new Tile(0, ElementType.None, 1, 0));

        // Fire — should skip the destroyed target
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        // Should only fire at remaining target(s)
        foreach (var b in beams)
        {
            Assert.NotEqual(new Position(1, 0), b.TargetPosition);
        }
    }

    #endregion

    #region Beam Arrival & Batch Destroy

    [Fact]
    public void Update_BeamArrives_TargetAddedToArrivedList()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Advance enough to fire all beams and let them arrive
        float totalTime = 0f;
        for (int i = 0; i < 100; i++)
        {
            manager.Update(ref state, 0.05f, i + 2, totalTime += 0.05f, _events);
            if (!manager.HasActiveSessions) break;
        }

        // Session should complete and emit batch destroy
        Assert.False(manager.HasActiveSessions);
        var batchEvt = _events.Events.OfType<ColorBombBatchDestroyEvent>().Single();
        Assert.NotEmpty(batchEvt.DestroyedPositions);
    }

    [Fact]
    public void BatchDestroy_ClearsTilesAndEmitsEvents()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        RunUntilDone(ref state, manager);

        // Tiles should be cleared
        Assert.Equal(ElementType.None, state.GetTile(1, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 0).Type);

        // TileDestroyedEvents should be emitted
        var destroyEvents = _events.Events.OfType<TileDestroyedEvent>().ToList();
        Assert.True(destroyEvents.Count >= 2);
    }

    [Fact]
    public void BatchDestroy_ReleasesAllLocks()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        RunUntilDone(ref state, manager);

        // All locks should be released
        Assert.False(state.IsLocked(1, 0, CellLockType.Drop));
        Assert.False(state.IsLocked(1, 0, CellLockType.Swap));
        Assert.False(state.IsLocked(2, 0, CellLockType.Drop));
        Assert.False(state.IsLocked(2, 0, CellLockType.Swap));
    }

    [Fact]
    public void BatchDestroy_ReleasesColor()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        Assert.True(manager.IsColorReserved(ElementType.Item1));

        RunUntilDone(ref state, manager);

        Assert.False(manager.IsColorReserved(ElementType.Item1));
    }

    [Fact]
    public void BatchDestroy_EmitsBatchEventBeforeTileDestroyedEvents()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        RunUntilDone(ref state, manager);

        // Find the batch event and the tile destroyed events in the final tick
        var batchEvt = _events.Events.OfType<ColorBombBatchDestroyEvent>().Single();
        int batchIndex = _events.Events.ToList().IndexOf(batchEvt);

        // All TileDestroyedEvents should come AFTER the batch event
        var destroyEvents = _events.Events.OfType<TileDestroyedEvent>()
            .Where(e => e.Reason == ElimSource.ColorBomb);
        foreach (var de in destroyEvents)
        {
            int deIndex = _events.Events.ToList().IndexOf(de);
            Assert.True(deIndex > batchIndex,
                $"TileDestroyedEvent at index {deIndex} should come after ColorBombBatchDestroyEvent at {batchIndex}");
        }
    }

    #endregion

    #region External Destruction

    [Fact]
    public void ExternalDestruction_InFlight_ReleasesLock()
    {
        var manager = CreateManager();
        var state = CreateState(5, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(3, ElementType.Item1, 3, 0));
        state.SetTile(4, 0, new Tile(4, ElementType.Item1, 4, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Fire some beams
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);

        // Find a target that was fired at
        var beam = _events.Events.OfType<ColorBombBeamLaunchedEvent>().First();
        var tp = beam.TargetPosition;

        // Externally destroy the target while beam is in flight
        state.SetTile(tp.X, tp.Y, new Tile(0, ElementType.None, tp.X, tp.Y));

        // Next update should detect destruction and release lock
        manager.Update(ref state, 0.01f, 3, 0.02f, _events);

        Assert.False(state.IsLocked(tp.X, tp.Y, CellLockType.Drop));
    }

    [Fact]
    public void ExternalDestruction_Arrived_RemovesFromBatch()
    {
        // Use a wider board so beams take longer (more time to externally destroy)
        var manager = CreateManager();
        var state = CreateState(10, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(5, 0, new Tile(1, ElementType.Item1, 5, 0));
        state.SetTile(9, 0, new Tile(2, ElementType.Item1, 9, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Advance: let beams fire and first beam arrive
        float t = 0f;
        bool destroyed = false;
        for (int i = 0; i < 300; i++)
        {
            manager.Update(ref state, 0.01f, i + 2, t += 0.01f, _events);

            // After some time, externally destroy target at (5,0)
            if (!destroyed && t > 0.5f)
            {
                state.SetTile(5, 0, new Tile(0, ElementType.None, 5, 0));
                destroyed = true;
            }

            if (!manager.HasActiveSessions) break;
        }

        Assert.False(manager.HasActiveSessions);
        var batchEvt = _events.Events.OfType<ColorBombBatchDestroyEvent>().Single();
        // Externally destroyed target should NOT be in the batch
        Assert.DoesNotContain(new Position(5, 0), batchEvt.DestroyedPositions);
    }

    #endregion

    #region Re-scan

    [Fact]
    public void ReScan_FindsNewTargetsAfterDrop()
    {
        // Use normal config with slower beams so there's time between fire and arrive
        var manager = CreateManager();
        var state = CreateState(4, 2);

        // Row 0: bomb + 2 targets, row 1: empty
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(3, ElementType.Item2, 3, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // A new same-color tile "drops in" before any updates
        // This simulates gravity dropping a new tile during the session
        state.SetTile(3, 1, new Tile(10, ElementType.Item1, 3, 1));

        // Run until session completes — re-scan should discover the new tile
        RunUntilDone(ref state, manager);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        Assert.Contains(beams, b => b.TargetPosition == new Position(3, 1));
    }

    [Fact]
    public void ReScan_LimitedToMaxReScans()
    {
        var config = new ColorBombConfig { MaxReScans = 1, BeamInterval = 0.01f, BeamSpeed = 1000f };
        var manager = new ColorBombSessionManager(config);
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(0, ElementType.None, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Fire all beams, trigger re-scan
        float t = 0f;
        for (int i = 0; i < 30; i++)
        {
            manager.Update(ref state, 0.01f, i + 2, t += 0.01f, _events);
        }

        // Add new target after re-scan limit
        state.SetTile(2, 0, new Tile(10, ElementType.Item1, 2, 0));

        // Continue — should NOT re-scan again
        for (int i = 30; i < 200; i++)
        {
            manager.Update(ref state, 0.01f, i + 2, t += 0.01f, _events);
            if (!manager.HasActiveSessions) break;
        }

        // The new tile at (2,0) should NOT have been targeted (max re-scans reached)
        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        Assert.DoesNotContain(beams, b => b.TargetPosition == new Position(2, 0));
    }

    #endregion

    #region Cell Locking Integration

    [Fact]
    public void LockedCell_NotTargetedByUfo()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Fire beams to lock targets
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        foreach (var b in beams)
        {
            Assert.True(state.IsLocked(b.TargetPosition.X, b.TargetPosition.Y, CellLockType.Targeting),
                $"Cell ({b.TargetPosition.X},{b.TargetPosition.Y}) should be Targeting-locked");
        }
    }

    [Fact]
    public void LockedCell_ExcludedFromMatching()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        foreach (var b in beams)
        {
            Assert.True(state.IsLocked(b.TargetPosition.X, b.TargetPosition.Y, CellLockType.Matching));
        }
    }

    [Fact]
    public void LockedCell_CannotBeSwapped()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        foreach (var b in beams)
        {
            Assert.False(state.CanInteract(b.TargetPosition.X, b.TargetPosition.Y),
                $"Locked cell ({b.TargetPosition.X},{b.TargetPosition.Y}) should not be interactable");
        }
    }

    #endregion

    #region Cover Interaction

    [Fact]
    public void BatchDestroy_CoverAbsorbsHit()
    {
        var coverSystem = new CoverSystem();
        var manager = new ColorBombSessionManager(_config, new CellEliminator(coverSystem, new GroundSystem()));
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        // Add cover to position (1,0)
        state.SetCover(1, 0, new Cover(CoverType.Cage, 1));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        RunUntilDone(ref state, manager);

        // Position (1,0) should have cover damaged but tile preserved
        var batchEvt = _events.Events.OfType<ColorBombBatchDestroyEvent>().Single();
        Assert.DoesNotContain(new Position(1, 0), batchEvt.DestroyedPositions);

        // Position (2,0) should be destroyed
        Assert.Contains(new Position(2, 0), batchEvt.DestroyedPositions);
    }

    #endregion

    #region Two Concurrent Sessions

    [Fact]
    public void TwoSessions_DifferentColors_RunConcurrently()
    {
        var manager = CreateManager();
        var state = CreateState(6, 6);

        // Fill with Item1 and Item2 alternating
        int id = 1;
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
            {
                var type = (x + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item2;
                state.SetTile(x, y, new Tile(id++, type, x, y));
            }

        // Two bombs
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(5, 5, new Tile(200, ElementType.None, 5, 5));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        manager.CreateSession(ref state, new Position(5, 5), 200, 1, 0f, _events);

        var starts = _events.Events.OfType<ColorBombSessionStartEvent>().ToList();
        Assert.Equal(2, starts.Count);
        Assert.NotEqual(starts[0].TargetColor, starts[1].TargetColor);

        // Both should complete
        RunUntilDone(ref state, manager);
        Assert.False(manager.HasActiveSessions);
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_ClearsAllState()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        Assert.True(manager.HasActiveSessions);
        Assert.True(manager.IsColorReserved(ElementType.Item1));

        manager.Reset();

        Assert.False(manager.HasActiveSessions);
        Assert.False(manager.IsColorReserved(ElementType.Item1));
    }

    #endregion

    #region Falling Tiles

    [Fact]
    public void CollectTargets_SkipsFallingTiles()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));

        // Set tile as falling
        var fallingTile = new Tile(1, ElementType.Item1, 1, 0) { State = TileState.Falling };
        state.SetTile(1, 0, fallingTile);
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0)); // settled

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Fire beams
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        // Should only target the settled tile, not the falling one
        Assert.DoesNotContain(beams, b => b.TargetPosition == new Position(1, 0));
    }

    #endregion

    #region Max Beam Count

    [Fact]
    public void SingleSession_TargetsAllSameColorTiles()
    {
        // Fill 8x8 board with Item1 (63 targets, bomb at origin)
        var manager = CreateManager();
        var state = CreateState(8, 8);
        FillState(ref state, ElementType.Item1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        RunUntilDone(ref state, manager);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        Assert.Equal(63, beams.Count); // 8*8 - 1 bomb = 63 targets

        // All tiles should be cleared
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                if (x == 0 && y == 0) continue;
                Assert.Equal(ElementType.None, state.GetTile(x, y).Type);
            }
    }

    [Fact]
    public void BeamIndex_Monotonic_AcrossReScans()
    {
        var manager = CreateManager();
        var state = CreateState(4, 2);

        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Add new tile before first update (will be found by re-scan)
        state.SetTile(3, 1, new Tile(10, ElementType.Item1, 3, 1));

        RunUntilDone(ref state, manager);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        // BeamIndex should be monotonically increasing
        for (int i = 0; i < beams.Count; i++)
        {
            Assert.Equal(i, beams[i].BeamIndex);
        }
    }

    #endregion

    #region External Bomb Destroying Targets Mid-Session

    [Fact]
    public void ExternalBombDestroysTargets_SessionContinues()
    {
        // Simulate: ColorBomb fires, then an explosion destroys some targets
        var manager = CreateManager();
        var state = CreateState(8, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0)); // bomb
        for (int x = 1; x < 8; x++)
            state.SetTile(x, 0, new Tile(x, ElementType.Item1, x, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Let a couple beams fire
        float t = 0f;
        for (int i = 0; i < 5; i++)
        {
            manager.Update(ref state, 0.02f, i + 2, t += 0.02f, _events);
        }

        // External explosion destroys targets at positions 4,5,6
        for (int x = 4; x <= 6; x++)
            state.SetTile(x, 0, new Tile(0, ElementType.None, x, 0));

        // Session should continue and complete without error
        RunUntilDone(ref state, manager);
        Assert.False(manager.HasActiveSessions);

        // Externally destroyed targets should NOT appear in batch event
        var batchEvt = _events.Events.OfType<ColorBombBatchDestroyEvent>().Single();
        for (int x = 4; x <= 6; x++)
            Assert.DoesNotContain(new Position(x, 0), batchEvt.DestroyedPositions);
    }

    [Fact]
    public void AllTargetsDestroyedExternally_SessionCompletes_EmptyBatch()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Fire beams
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);

        // Destroy all targets externally
        state.SetTile(1, 0, new Tile(0, ElementType.None, 1, 0));
        state.SetTile(2, 0, new Tile(0, ElementType.None, 2, 0));

        RunUntilDone(ref state, manager);
        Assert.False(manager.HasActiveSessions);

        var batchEvt = _events.Events.OfType<ColorBombBatchDestroyEvent>().Single();
        Assert.Empty(batchEvt.DestroyedPositions);
    }

    #endregion

    #region ReScan Targets Destroyed In-Flight

    [Fact]
    public void ReScanTarget_DestroyedInFlight_NotInBatch()
    {
        // Slow beams: re-scan target's beam takes time to arrive → can be destroyed in flight
        var config = new ColorBombConfig { BeamInterval = 0.01f, BeamSpeed = 2f, MinFlightDuration = 0.5f, MaxReScans = 3 };
        var manager = new ColorBombSessionManager(config);
        var state = CreateState(4, 2);

        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Add re-scan target AFTER CreateSession but BEFORE first update
        state.SetTile(3, 1, new Tile(50, ElementType.Item1, 3, 1));

        // Advance until re-scan beam fires (re-scan adds target, then BeamInterval delay before fire)
        float t = 0f;
        bool reScanBeamFired = false;
        for (int i = 0; i < 50; i++)
        {
            manager.Update(ref state, 0.01f, i + 2, t += 0.01f, _events);
            if (!reScanBeamFired)
            {
                var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>()
                    .Where(b => b.TargetPosition == new Position(3, 1)).ToList();
                if (beams.Count > 0)
                {
                    reScanBeamFired = true;
                    // Destroy re-scan target while beam is in flight (MinFlightDuration=0.5s)
                    state.SetTile(3, 1, new Tile(0, ElementType.None, 3, 1));
                }
            }
            if (!manager.HasActiveSessions) break;
        }

        Assert.True(reScanBeamFired, "Re-scan target should have been fired at");

        // Let session complete if not already
        if (manager.HasActiveSessions)
            RunUntilDone(ref state, manager);

        var batchEvt = _events.Events.OfType<ColorBombBatchDestroyEvent>().Single();
        Assert.DoesNotContain(new Position(3, 1), batchEvt.DestroyedPositions);
    }

    #endregion

    #region UFO + ColorBomb Interaction

    [Fact]
    public void UFO_PickRemoteTarget_SkipsColorBombLockedCells()
    {
        var manager = CreateManager();
        var state = CreateState(4, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0)); // bomb position
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(3, ElementType.Item2, 3, 0)); // different color, potential UFO target

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Fire beams → locks Item1 cells
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();

        // Verify locked cells are not pickable by UFO
        foreach (var b in beams)
        {
            var tp = b.TargetPosition;
            var ufoTarget = Match3.Core.Systems.PowerUps.Effects.UfoEffect.PickRemoteTarget(in state, new Position(0, 0));
            // If UFO picks a target, it must not be a locked position
            if (ufoTarget.HasValue)
            {
                Assert.False(state.IsLocked(ufoTarget.Value.X, ufoTarget.Value.Y, CellLockType.Targeting),
                    $"UFO picked locked position ({ufoTarget.Value.X},{ufoTarget.Value.Y})");
            }
        }
    }

    [Fact]
    public void PickTargetColor_SkipsTargetingLockedTiles()
    {
        var manager = CreateManager();
        var state = CreateState(4, 1);

        // 2x Item1 + 1x Item2 → Item1 wins normally
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(3, ElementType.Item2, 3, 0));

        // First session reserves Item1 and locks those cells
        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        manager.Update(ref state, 0.01f, 2, 0.01f, _events);

        // Second session: Item1 is reserved, so it should pick Item2
        _events.Clear();
        manager.CreateSession(ref state, new Position(0, 0), 200, 2, 0f, _events);
        var evt = _events.Events.OfType<ColorBombSessionStartEvent>().FirstOrDefault();
        if (evt != null)
        {
            Assert.Equal(ElementType.Item2, evt.TargetColor);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CreateSession_EmptyBoard_DoesNothing()
    {
        var manager = CreateManager();
        var state = CreateState(4, 4);
        // All tiles are None (empty)

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        Assert.False(manager.HasActiveSessions);
        Assert.Empty(_events.Events.OfType<ColorBombSessionStartEvent>());
    }

    [Fact]
    public void CreateSession_OnlyBombs_NoColorTiles_DoesNothing()
    {
        var manager = CreateManager();
        var state = CreateState(3, 1);
        state.SetTile(0, 0, new Tile(1, ElementType.ColorBomb, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.HorizontalRocket, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.VerticalRocket, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        Assert.False(manager.HasActiveSessions);
    }

    [Fact]
    public void CreateSession_SingleTargetTile_SessionCompletes()
    {
        var manager = CreateManager();
        var state = CreateState(2, 1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        Assert.True(manager.HasActiveSessions);

        RunUntilDone(ref state, manager);

        Assert.Equal(ElementType.None, state.GetTile(1, 0).Type);
        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        Assert.Single(beams);
    }

    [Fact]
    public void CreateSession_BombAtCorner_TargetsEntireBoard()
    {
        var manager = CreateManager();
        var state = CreateState(3, 3);
        FillState(ref state, ElementType.Item1);
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        RunUntilDone(ref state, manager);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        Assert.Equal(8, beams.Count); // 3*3 - 1 bomb = 8

        // All corner and edge tiles targeted
        var positions = beams.Select(b => b.TargetPosition).ToHashSet();
        Assert.Contains(new Position(2, 2), positions); // far corner
        Assert.Contains(new Position(2, 0), positions);
        Assert.Contains(new Position(0, 2), positions);
    }

    [Fact]
    public void CreateSession_BombAtCenter_TargetsEntireBoard()
    {
        var manager = CreateManager();
        var state = CreateState(3, 3);
        FillState(ref state, ElementType.Item1);
        state.SetTile(1, 1, new Tile(100, ElementType.None, 1, 1));

        manager.CreateSession(ref state, new Position(1, 1), 100, 1, 0f, _events);

        RunUntilDone(ref state, manager);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        Assert.Equal(8, beams.Count); // 3*3 - 1 bomb = 8
    }

    [Fact]
    public void MultipleReScans_FindsTargetsEachTime()
    {
        // Fast beams: fire + arrive in same tick. Add one tile before each tick for re-scan.
        var config = new ColorBombConfig { BeamInterval = 0.01f, BeamSpeed = 1000f, MinFlightDuration = 0.001f, MaxReScans = 3 };
        var manager = new ColorBombSessionManager(config);
        var state = CreateState(6, 1);

        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        float t = 0f;

        // Tick 1: fires initial beam, re-scan 1 runs. Add tile before update for re-scan to find.
        state.SetTile(2, 0, new Tile(50, ElementType.Item1, 2, 0));
        manager.Update(ref state, 0.01f, 2, t += 0.01f, _events);

        // Tick 2: fires re-scan 1 beam, re-scan 2 runs. Add tile before update.
        state.SetTile(3, 0, new Tile(51, ElementType.Item1, 3, 0));
        manager.Update(ref state, 0.01f, 3, t += 0.01f, _events);

        // Tick 3: fires re-scan 2 beam, re-scan 3 runs. Add tile before update.
        state.SetTile(4, 0, new Tile(52, ElementType.Item1, 4, 0));
        manager.Update(ref state, 0.01f, 4, t += 0.01f, _events);

        // Let session complete
        RunUntilDone(ref state, manager);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        var beamPositions = beams.Select(b => b.TargetPosition).ToHashSet();

        // Should have targeted: (1,0) initial + (2,0) re-scan1 + (3,0) re-scan2 + (4,0) re-scan3
        Assert.Equal(4, beams.Count);
        Assert.Contains(new Position(1, 0), beamPositions);
        Assert.Contains(new Position(2, 0), beamPositions);
        Assert.Contains(new Position(3, 0), beamPositions);
        Assert.Contains(new Position(4, 0), beamPositions);
    }

    [Fact]
    public void ConcurrentSessions_ExternalDestroy_OnlyAffectsCorrectSession()
    {
        var manager = CreateManager();
        var state = CreateState(8, 2);

        // Row 0: bomb1 + Item1 tiles | Row 1: bomb2 + Item2 tiles
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0)); // bomb1
        for (int x = 1; x < 8; x++)
            state.SetTile(x, 0, new Tile(x, ElementType.Item1, x, 0));

        state.SetTile(0, 1, new Tile(200, ElementType.None, 0, 1)); // bomb2
        for (int x = 1; x < 8; x++)
            state.SetTile(x, 1, new Tile(100 + x, ElementType.Item2, x, 1));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);
        manager.CreateSession(ref state, new Position(0, 1), 200, 1, 0f, _events);

        // Let beams fire
        float t = 0f;
        for (int i = 0; i < 5; i++)
            manager.Update(ref state, 0.02f, i + 2, t += 0.02f, _events);

        // Externally destroy some Item1 targets (session 1 targets)
        state.SetTile(3, 0, new Tile(0, ElementType.None, 3, 0));
        state.SetTile(4, 0, new Tile(0, ElementType.None, 4, 0));

        // Both sessions should complete
        RunUntilDone(ref state, manager);
        Assert.False(manager.HasActiveSessions);

        var batches = _events.Events.OfType<ColorBombBatchDestroyEvent>().ToList();
        Assert.Equal(2, batches.Count);

        // Session 1 batch should NOT contain externally destroyed positions
        var batch1 = batches.First(b => b.BombTileId == 100);
        Assert.DoesNotContain(new Position(3, 0), batch1.DestroyedPositions);
        Assert.DoesNotContain(new Position(4, 0), batch1.DestroyedPositions);

        // Session 2 batch should be unaffected (all Item2 tiles intact)
        var batch2 = batches.First(b => b.BombTileId == 200);
        Assert.True(batch2.DestroyedPositions.Count >= 5,
            $"Session 2 should have most targets intact, got {batch2.DestroyedPositions.Count}");
    }

    [Fact]
    public void CoverOnTarget_MultiLayer_FirstHitAbsorbedSecondDestroys()
    {
        var coverSystem = new CoverSystem();
        var manager = new ColorBombSessionManager(_config, new CellEliminator(coverSystem, new GroundSystem()));
        var state = CreateState(3, 1);

        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        // Multi-layer cover on position (1,0): HP=2
        state.SetCover(1, 0, new Cover(CoverType.Cage, health: 2));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        RunUntilDone(ref state, manager);

        // Position (1,0): cover absorbs the hit, tile survives
        var batchEvt = _events.Events.OfType<ColorBombBatchDestroyEvent>().Single();
        Assert.DoesNotContain(new Position(1, 0), batchEvt.DestroyedPositions);

        // Position (2,0): no cover, destroyed normally
        Assert.Contains(new Position(2, 0), batchEvt.DestroyedPositions);

        // Cover should have lost 1 HP (damaged from 2 to 1)
        var cover = state.GetCover(1, 0);
        Assert.Equal(1, cover.Health);
    }

    [Fact]
    public void GravityDrop_DuringSession_ReScanFindsDroppedTiles()
    {
        // Fast beams: re-scan runs on same tick as last beam fire.
        // Add "dropped" tiles after CreateSession so re-scan finds them.
        var config = new ColorBombConfig { BeamInterval = 0.01f, BeamSpeed = 1000f, MinFlightDuration = 0.001f, MaxReScans = 3 };
        var manager = new ColorBombSessionManager(config);
        var state = CreateState(4, 3);

        // Row 0: bomb + 2 targets
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));

        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        // Simulate gravity dropping new same-color tiles BEFORE first update
        // (re-scan on tick 1 will discover them)
        state.SetTile(0, 2, new Tile(50, ElementType.Item1, 0, 2));
        state.SetTile(3, 2, new Tile(51, ElementType.Item1, 3, 2));

        RunUntilDone(ref state, manager);

        var beams = _events.Events.OfType<ColorBombBeamLaunchedEvent>().ToList();
        var beamPositions = beams.Select(b => b.TargetPosition).ToHashSet();

        // Initial targets
        Assert.Contains(new Position(1, 0), beamPositions);
        Assert.Contains(new Position(2, 0), beamPositions);
        // Dropped tiles found by re-scan
        Assert.Contains(new Position(0, 2), beamPositions);
        Assert.Contains(new Position(3, 2), beamPositions);
    }

    #endregion

    #region Color Reservation Across Phases

    [Fact]
    public void SecondSession_DuringWaitingForBeams_PicksDifferentColor()
    {
        // Reproduces: click rainbow ball, first session enters WaitingForBeams,
        // then click second rainbow ball — must pick a different color.
        var manager = CreateManager();
        var state = CreateState(6, 6);

        // Fill with Item1 and Item2 alternating
        int id = 1;
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
            {
                var type = (x + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item2;
                state.SetTile(x, y, new Tile(id++, type, x, y));
            }

        // First rainbow ball
        state.SetTile(0, 0, new Tile(100, ElementType.None, 0, 0));
        manager.CreateSession(ref state, new Position(0, 0), 100, 1, 0f, _events);

        var firstStart = _events.Events.OfType<ColorBombSessionStartEvent>().Single();
        var firstColor = firstStart.TargetColor;
        Assert.True(manager.IsColorReserved(firstColor));

        // Advance until shooting phase ends (all beams fired, some still in flight)
        float t = 0f;
        for (int i = 0; i < 200; i++)
        {
            manager.Update(ref state, 0.02f, i + 10, t += 0.02f, _events);
            if (!manager.HasActiveSessions) break;

            // Check: during the entire session, color must remain reserved
            Assert.True(manager.IsColorReserved(firstColor),
                $"Color {firstColor} was released prematurely at iteration {i}");
        }

        // If session still active (beams in flight), create second session now
        if (manager.HasActiveSessions)
        {
            _events.Clear();
            state.SetTile(5, 5, new Tile(200, ElementType.None, 5, 5));
            manager.CreateSession(ref state, new Position(5, 5), 200, 300, t, _events);

            var secondStarts = _events.Events.OfType<ColorBombSessionStartEvent>().ToList();
            if (secondStarts.Count > 0)
            {
                Assert.NotEqual(firstColor, secondStarts[0].TargetColor);
            }
        }
    }

    #endregion

    #region PowerUpHandler Integration

    [Fact]
    public void PowerUpHandler_ActivateBomb_ColorBomb_RoutesToSession()
    {
        var coverSystem = new CoverSystem();
        var groundSystem = new GroundSystem();
        var sessionManager = new ColorBombSessionManager(_config, new CellEliminator(coverSystem, groundSystem));

        var handler = new BombResolution(
            new StubScoreSystem(),
            new BombComboHandler(),
            BombEffectRegistry.CreateDefault(),
            coverSystem,
            groundSystem,
            colorBombSessionManager: sessionManager);

        var state = CreateState(4, 4);
        FillState(ref state, ElementType.Item1);
        state.SetTile(2, 2, new Tile(100, ElementType.ColorBomb, 2, 2));

        handler.ActivateBomb(ref state, new Position(2, 2), 1, 0f, _events);

        // Should route to session, not instant destruction
        Assert.True(sessionManager.HasActiveSessions);

        // Bomb tile should be cleared (ConsumeBomb sets to None)
        Assert.Equal(ElementType.None, state.GetTile(2, 2).Type);

        // Session should target Item1
        var startEvt = _events.Events.OfType<ColorBombSessionStartEvent>().Single();
        Assert.Equal(ElementType.Item1, startEvt.TargetColor);
    }

    [Fact]
    public void PowerUpHandler_ActivateBomb_ColorBomb_NoSessionManager_FallsBackToLegacy()
    {
        // Without session manager, ColorBomb should use legacy instant path
        var handler = new BombResolution(
            new StubScoreSystem());

        var state = CreateState(4, 4);
        FillState(ref state, ElementType.Item1);
        state.SetTile(2, 2, new Tile(100, ElementType.ColorBomb, 2, 2));

        handler.ActivateBomb(ref state, new Position(2, 2), 1, 0f, _events);

        // Should NOT create a session (no session manager)
        Assert.Empty(_events.Events.OfType<ColorBombSessionStartEvent>());

        // Legacy path emits BombActivatedEvent
        Assert.NotEmpty(_events.Events.OfType<BombActivatedEvent>());
    }

    #endregion

    #region Helpers

    private ColorBombSessionManager CreateManager()
    {
        return new ColorBombSessionManager(_config, lockScheduler: new LockScheduler());
    }

    private static GameState CreateState(int width, int height)
    {
        return new GameState(width, height, 6, new StubRandom());
    }

    private static void FillState(ref GameState state, ElementType type)
    {
        int id = 1;
        for (int y = 0; y < state.Height; y++)
            for (int x = 0; x < state.Width; x++)
                state.SetTile(x, y, new Tile(id++, type, x, y));
    }

    private void RunUntilDone(ref GameState state, ColorBombSessionManager manager, int maxIterations = 500)
    {
        float t = 0f;
        for (int i = 0; i < maxIterations; i++)
        {
            manager.Update(ref state, 0.02f, i + 10, t += 0.02f, _events);
            if (!manager.HasActiveSessions) return;
        }
        Assert.Fail("Session did not complete within max iterations");
    }

    #endregion
}
