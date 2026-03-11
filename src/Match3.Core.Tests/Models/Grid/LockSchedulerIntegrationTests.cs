using System;
using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Models.Grid;

/// <summary>
/// Cross-system integration tests verifying LockScheduler works correctly
/// when multiple Core systems interact through it (explosions, physics, refill, clone).
/// </summary>
public class LockSchedulerIntegrationTests
{
    #region Helpers

    private static GameState CreateFilledState(int width, int height)
    {
        var rng = new StubRandom();
        var state = new GameState(width, height, 6, rng);
        var types = new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4, ElementType.Item5 };
        int id = 1;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int typeIdx = (x + y) % types.Length;
            state.SetTile(x, y, new Tile(id++, types[typeIdx], x, y));
        }
        return state;
    }

    private static ExplosionSystem CreateExplosionSystem(LockScheduler lockScheduler)
    {
        var coverSystem = new CoverSystem();
        var groundSystem = new GroundSystem();
        return new ExplosionSystem(coverSystem, groundSystem, null, lockScheduler);
    }

    private static ExplosionSystem CreateExplosionSystemNoScheduler()
    {
        var coverSystem = new CoverSystem();
        var groundSystem = new GroundSystem();
        return new ExplosionSystem(coverSystem, groundSystem);
    }

    private static RealtimeGravitySystem CreateGravitySystem(StubRandom? rng = null)
    {
        var config = new Match3Config
        {
            InitialFallSpeed = 12f,
            GravitySpeed = 20f,
            MaxFallSpeed = 25f
        };
        return new RealtimeGravitySystem(config, rng ?? new StubRandom());
    }

    /// <summary>
    /// Create a SimulationEngine with a shared LockScheduler and ExplosionSystem wired together.
    /// </summary>
    private static SimulationEngine CreateEngineWithLocks(
        GameState state,
        LockScheduler lockScheduler,
        IEventCollector? eventCollector = null)
    {
        var rng = new StubRandom();
        var config = new Match3Config();
        var physics = new RealtimeGravitySystem(config, rng);
        var spawn = new StubSpawnModel();
        var refill = new RealtimeRefillSystem(spawn);
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var score = new StubScoreSystem();
        var objectiveSystem = new LevelObjectiveSystem();
        var coverSystem = new CoverSystem(objectiveSystem);
        var groundSystem = new GroundSystem(objectiveSystem);
        var matchProcessor = new StandardMatchProcessor(
            score, coverSystem, groundSystem, BombEffectRegistry.CreateDefault());
        var explosionSystem = new ExplosionSystem(coverSystem, groundSystem, objectiveSystem, lockScheduler);
        var powerUpHandler = new PowerUpHandler(score);
        powerUpHandler = (PowerUpHandler)powerUpHandler.WithExplosionSystem(explosionSystem);

        return new SimulationEngine(
            state,
            SimulationConfig.ForHumanPlay(),
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            eventCollector: eventCollector,
            explosionSystem: explosionSystem,
            objectiveSystem: objectiveSystem,
            lockScheduler: lockScheduler);
    }

    #endregion

    #region 1. Explosion -> Physics: Drop lock blocks gravity

    [Fact]
    public void Explosion_DropLockBlocksGravity_TilesStayInPlace()
    {
        // Arrange: 5x6 grid filled with tiles, explosion at center radius 1
        var state = CreateFilledState(5, 6);
        var lockScheduler = new LockScheduler();
        var explosionSystem = CreateExplosionSystem(lockScheduler);
        var gravity = CreateGravitySystem();

        var origin = new Position(2, 3);
        explosionSystem.CreateExplosion(ref state, origin, 1);

        // Assert: tiles in blast zone (Chebyshev distance <= 1 from (2,3)) have Drop lock
        for (int y = 2; y <= 4; y++)
        for (int x = 1; x <= 3; x++)
        {
            Assert.True(state.IsLocked(x, y, CellLockType.Drop),
                $"Tile at ({x},{y}) should have Drop lock after explosion creation");
        }

        // Act: run physics multiple times — locked tiles must not move
        var originalPositions = new Dictionary<(int, int), Tile>();
        for (int y = 2; y <= 4; y++)
        for (int x = 1; x <= 3; x++)
            originalPositions[(x, y)] = state.GetTile(x, y);

        for (int i = 0; i < 20; i++)
            gravity.Update(ref state, 1f / 60f);

        // Assert: locked tiles are still at their original grid positions
        foreach (var (pos, origTile) in originalPositions)
        {
            var tile = state.GetTile(pos.Item1, pos.Item2);
            Assert.Equal(origTile.Id, tile.Id);
            Assert.Equal(origTile.Type, tile.Type);
        }
    }

    [Fact]
    public void Explosion_AfterWave0_CenterDestroyed_LockReleased()
    {
        // Arrange
        var state = CreateFilledState(5, 6);
        var lockScheduler = new LockScheduler();
        var explosionSystem = CreateExplosionSystem(lockScheduler);
        var eventCollector = new StubEventCollector();
        var triggeredBombs = new List<Position>();

        var origin = new Position(2, 3);
        explosionSystem.CreateExplosion(ref state, origin, 1);

        // Act: process one wave (deltaTime = 0.1s = WaveInterval)
        explosionSystem.Update(ref state, 0.1f, 1, 0.1f, eventCollector, triggeredBombs);

        // Assert: wave-0 (center) tile is destroyed and unlocked
        Assert.Equal(ElementType.None, state.GetTile(origin.X, origin.Y).Type);
        Assert.False(state.IsLocked(origin.X, origin.Y, CellLockType.Drop),
            "Center cell should be unlocked after wave-0 processes it");

        // Wave-1 tiles (distance 1) should still be locked
        Assert.True(state.IsLocked(1, 3, CellLockType.Drop),
            "Wave-1 tiles should still be locked");
    }

    [Fact]
    public void Explosion_AllWavesComplete_AllLocksReleased_PhysicsResumes()
    {
        // Arrange: 5x6 grid, explosion radius 1 at (2,3)
        var state = CreateFilledState(5, 6);
        var lockScheduler = new LockScheduler();
        var explosionSystem = CreateExplosionSystem(lockScheduler);
        var gravity = CreateGravitySystem();
        var eventCollector = new StubEventCollector();
        var triggeredBombs = new List<Position>();

        var origin = new Position(2, 3);
        explosionSystem.CreateExplosion(ref state, origin, 1);

        // Act: process all waves (radius 1 = waves 0 and 1, so 2 updates at WaveInterval)
        explosionSystem.Update(ref state, 0.1f, 1, 0.1f, eventCollector, triggeredBombs);
        explosionSystem.Update(ref state, 0.1f, 2, 0.2f, eventCollector, triggeredBombs);

        // Assert: explosion is finished, no Drop locks remain in the blast zone
        Assert.False(explosionSystem.HasActiveExplosions,
            "Explosion should be finished after processing all waves");

        for (int y = 2; y <= 4; y++)
        for (int x = 1; x <= 3; x++)
        {
            Assert.False(state.IsLocked(x, y, CellLockType.Drop),
                $"Cell ({x},{y}) should have no Drop lock after all waves complete");
        }

        // Act: run physics — tiles above the blast zone should now fall
        // Place a tile above the destroyed zone that can fall
        state.SetTile(2, 0, new Tile(999, ElementType.Item1, 2, 0));
        // Clear the column below to let it fall
        state.SetTile(2, 1, new Tile(0, ElementType.None, 2, 1));
        state.SetTile(2, 2, new Tile(0, ElementType.None, 2, 2));
        state.SetTile(2, 3, new Tile(0, ElementType.None, 2, 3));

        for (int i = 0; i < 60; i++)
            gravity.Update(ref state, 1f / 60f);

        // The tile should have moved downward (not stuck at row 0)
        Assert.False(gravity.IsStable(in state) && state.GetTile(2, 0).Id == 999,
            "Physics should have moved the tile after all locks released");
    }

    #endregion

    #region 2. Timed lock expires via SimulationEngine.Tick

    [Fact]
    public void TimedLock_BlocksGravity_UntilExpiry_ThenTileFalls()
    {
        // Arrange: 5x6 grid, a tile at (2,2) with an empty cell below at (2,3)
        var state = CreateFilledState(5, 6);
        // Create an empty cell below (2,3) so tile at (2,2) could fall there
        state.SetTile(2, 3, new Tile(0, ElementType.None, 2, 3));

        var lockScheduler = new LockScheduler();
        var tileBeforeLock = state.GetTile(2, 2);
        Assert.NotEqual(ElementType.None, tileBeforeLock.Type);

        // Acquire a timed Drop lock on (2,2) for 0.5 seconds
        lockScheduler.Acquire(ref state, new Position(2, 2), CellLockType.Drop, 0.5f);

        var engine = CreateEngineWithLocks(state, lockScheduler);

        // Act: tick for 0.3 seconds (below expiry) — tile should not fall
        float dt = 1f / 60f;
        int ticksBefore = (int)(0.3f / dt);
        for (int i = 0; i < ticksBefore; i++)
            engine.Tick(dt);

        // Assert: tile is still at (2,2), cell is still locked
        var stateAfterPartial = engine.State;
        Assert.True(stateAfterPartial.IsLocked(2, 2, CellLockType.Drop),
            "Drop lock should still be active before expiry");
        Assert.NotEqual(ElementType.None, stateAfterPartial.GetTile(2, 2).Type);

        // Act: tick past expiry (0.3 more seconds, total ~0.6s > 0.5s)
        int ticksAfter = (int)(0.3f / dt);
        for (int i = 0; i < ticksAfter; i++)
            engine.Tick(dt);

        // Assert: lock expired, tile should have fallen (or be falling)
        var stateAfterExpiry = engine.State;
        Assert.False(stateAfterExpiry.IsLocked(2, 2, CellLockType.Drop),
            "Drop lock should have expired after sufficient time");

        // Strengthen: verify tile actually moved — run additional physics ticks
        for (int i = 0; i < 60; i++)
            engine.Tick(dt);

        var finalState = engine.State;
        // The tile that was at (2,2) should no longer be there (fell to (2,3) or beyond)
        bool tileMoved = finalState.GetTile(2, 2).Id != tileBeforeLock.Id
                      || finalState.GetTile(2, 3).Type != ElementType.None;
        Assert.True(tileMoved,
            $"Tile (id={tileBeforeLock.Id}) should have moved from (2,2) after lock expired. " +
            $"(2,2)={finalState.GetTile(2, 2).Type}, (2,3)={finalState.GetTile(2, 3).Type}");
    }

    #endregion

    #region 3. Overlapping explosions — ref-count prevents premature unlock

    [Fact]
    public void OverlappingExplosions_RefCountPreventsEarlyUnlock()
    {
        // Arrange: 7x7 grid, two explosions with overlapping blast zones
        // Explosion A at (2,3) radius 1, Explosion B at (4,3) radius 1
        // Overlap at (3,3) and potentially (3,2), (3,4)
        var state = CreateFilledState(7, 7);
        var lockScheduler = new LockScheduler();
        var explosionSystem = CreateExplosionSystem(lockScheduler);
        var eventCollector = new StubEventCollector();
        var triggeredBombs = new List<Position>();

        var originA = new Position(2, 3);
        var originB = new Position(4, 3);

        explosionSystem.CreateExplosion(ref state, originA, 1);
        explosionSystem.CreateExplosion(ref state, originB, 1);

        // Assert: overlapping cell (3,3) has ref-count 2
        var overlapIdx = state.Index(3, 3);
        Assert.Equal(2, CellLockOps.GetCount(state.CellLocks[overlapIdx], CellLockType.Drop));

        // Act: process ALL waves for both explosions simultaneously
        // Wave 0: centers (2,3) and (4,3)
        explosionSystem.Update(ref state, 0.1f, 1, 0.1f, eventCollector, triggeredBombs);

        // The overlap cell (3,3) is at distance 1 from both — it's in wave 1 for both.
        // After wave 0, it should still have ref-count 2 (wave 1 hasn't fired yet).
        Assert.Equal(2, CellLockOps.GetCount(state.CellLocks[overlapIdx], CellLockType.Drop));

        // Wave 1: distance-1 cells for both explosions
        explosionSystem.Update(ref state, 0.1f, 2, 0.2f, eventCollector, triggeredBombs);

        // After wave 1, the overlap cell was processed by BOTH explosions in the same wave.
        // The first one destroys the tile and releases; the second finds None and releases.
        // Both release, so ref-count should be 0.
        Assert.Equal(0, CellLockOps.GetCount(state.CellLocks[overlapIdx], CellLockType.Drop));
        Assert.False(state.IsLocked(3, 3, CellLockType.Drop),
            "Overlapping cell should be fully unlocked after both explosions release");
    }

    [Fact]
    public void OverlappingExplosions_StaggeredWaves_OverlapStaysLocked()
    {
        // Arrange: two overlapping radius-2 explosions where waves are staggered
        // Explosion A at (3,3) radius 2 — overlap cells at distance 1 from A
        // Explosion B at (5,3) radius 2 — overlap cells at distance 1 from B, distance 3 from A is out of range
        // Focus on cell (4,3): distance 1 from A, distance 1 from B
        var state = CreateFilledState(8, 7);
        var lockScheduler = new LockScheduler();
        var explosionSystem = CreateExplosionSystem(lockScheduler);
        var eventCollector = new StubEventCollector();
        var triggeredBombs = new List<Position>();

        explosionSystem.CreateExplosion(ref state, new Position(3, 3), 2);
        explosionSystem.CreateExplosion(ref state, new Position(5, 3), 2);

        // Cell (4,3): distance 1 from both origins — ref-count should be 2
        var cellIdx = state.Index(4, 3);
        Assert.Equal(2, CellLockOps.GetCount(state.CellLocks[cellIdx], CellLockType.Drop));

        // Process wave 0 (centers only — (3,3) and (5,3))
        explosionSystem.Update(ref state, 0.1f, 1, 0.1f, eventCollector, triggeredBombs);

        // (4,3) is wave 1 for both, so still locked with count 2
        Assert.Equal(2, CellLockOps.GetCount(state.CellLocks[cellIdx], CellLockType.Drop));
        Assert.True(state.IsLocked(4, 3, CellLockType.Drop));

        // Process wave 1 — both explosions release their lock on (4,3)
        explosionSystem.Update(ref state, 0.1f, 2, 0.2f, eventCollector, triggeredBombs);

        Assert.Equal(0, CellLockOps.GetCount(state.CellLocks[cellIdx], CellLockType.Drop));
        Assert.False(state.IsLocked(4, 3, CellLockType.Drop));
    }

    #endregion

    #region 4. Explosion + Refill coordination

    [Fact]
    public void Explosion_TilesSpawnAtTop_DontFallIntoBlastZone_UntilComplete()
    {
        // Arrange: 5x6 grid filled, explosion at (2,3) radius 1
        // Blast zone: rows 2-4, columns 1-3 (Chebyshev distance <= 1 from (2,3))
        var state = CreateFilledState(5, 6);
        var lockScheduler = new LockScheduler();
        var explosionSystem = CreateExplosionSystem(lockScheduler);
        var gravity = CreateGravitySystem();
        var refill = new RealtimeRefillSystem(new StubSpawnModel());

        var origin = new Position(2, 3);
        explosionSystem.CreateExplosion(ref state, origin, 1);

        // Verify blast zone is locked
        Assert.True(state.IsLocked(2, 2, CellLockType.Drop));
        Assert.True(state.IsLocked(2, 3, CellLockType.Drop));
        Assert.True(state.IsLocked(2, 4, CellLockType.Drop));

        // Clear a tile ABOVE the blast zone to create a gap for refill
        state.SetTile(2, 0, new Tile(0, ElementType.None, 2, 0));

        // Refill spawns a new tile at (2,0)
        refill.Update(ref state);
        var spawnedTile = state.GetTile(2, 0);
        Assert.NotEqual(ElementType.None, spawnedTile.Type);
        int spawnedId = spawnedTile.Id;

        // Run gravity multiple times WHILE the explosion is still active (locks held).
        // The spawned tile should NOT enter the blast zone cells — they are occupied + Drop-locked.
        for (int i = 0; i < 30; i++)
            gravity.Update(ref state, 1f / 60f);

        // Assert: blast zone cells still have their original tiles (locked, not displaced)
        Assert.True(state.IsLocked(2, 2, CellLockType.Drop),
            "Blast zone cell (2,2) should still be Drop-locked during explosion");
        Assert.True(state.IsLocked(2, 3, CellLockType.Drop),
            "Blast zone cell (2,3) should still be Drop-locked during explosion");

        // The spawned tile must not have entered any blast zone cell
        for (int y = 2; y <= 4; y++)
        {
            Assert.True(state.GetTile(2, y).Id != spawnedId,
                $"Spawned tile should NOT have fallen into blast zone cell (2,{y}) while locks are held");
        }

        // Now process all explosion waves (destroy blast zone tiles, release locks)
        var eventCollector = new StubEventCollector();
        var triggeredBombs = new List<Position>();
        explosionSystem.Update(ref state, 0.1f, 1, 0.1f, eventCollector, triggeredBombs);
        explosionSystem.Update(ref state, 0.1f, 2, 0.2f, eventCollector, triggeredBombs);

        // All locks should be released now
        Assert.False(state.IsLocked(2, 2, CellLockType.Drop));
        Assert.False(state.IsLocked(2, 3, CellLockType.Drop));
        Assert.False(state.IsLocked(2, 4, CellLockType.Drop));

        // Run gravity again — tile should now fall through the cleared cells
        // Also tick the lock scheduler to expire timed Receive locks from destruction
        for (int i = 0; i < 120; i++)
        {
            lockScheduler.Tick(ref state, 1f / 60f);
            gravity.Update(ref state, 1f / 60f);
        }

        // The spawned tile should have moved from (2,0) or (2,1) into the now-cleared zone
        bool tileMovedDown = state.GetTile(2, 0).Id != spawnedId;
        Assert.True(tileMovedDown,
            "Tile should have fallen through the cleared blast zone after locks released");
    }

    #endregion

    #region 5. AI Clone isolation

    [Fact]
    public void Clone_ReleaseInClone_DoesNotAffectOriginal()
    {
        // Arrange
        var state = CreateFilledState(5, 6);
        var lockScheduler = new LockScheduler();

        var tokenA = lockScheduler.Acquire(ref state, new Position(2, 2), CellLockType.Drop);
        var tokenB = lockScheduler.Acquire(ref state, new Position(3, 3), CellLockType.Swap);
        lockScheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, 1.0f);

        // Clone state and scheduler
        var clonedState = state.Clone();
        var clonedScheduler = lockScheduler.Clone();

        // Act: release locks in clone
        clonedScheduler.Release(ref clonedState, tokenA);
        clonedScheduler.Release(ref clonedState, tokenB);

        // Assert: clone is unlocked
        Assert.False(clonedState.IsLocked(2, 2, CellLockType.Drop));
        Assert.False(clonedState.IsLocked(3, 3, CellLockType.Swap));

        // Assert: original is still locked
        Assert.True(state.IsLocked(2, 2, CellLockType.Drop),
            "Original should still have Drop lock after clone releases it");
        Assert.True(state.IsLocked(3, 3, CellLockType.Swap),
            "Original should still have Swap lock after clone releases it");
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop),
            "Original timed lock should still be active");
    }

    [Fact]
    public void Clone_TickTimedLockToExpiry_DoesNotAffectOriginal()
    {
        // Arrange
        var state = CreateFilledState(5, 6);
        var lockScheduler = new LockScheduler();

        lockScheduler.Acquire(ref state, new Position(2, 2), CellLockType.Drop, 0.5f);
        lockScheduler.Acquire(ref state, new Position(3, 3), CellLockType.Swap, 1.0f);

        var clonedState = state.Clone();
        var clonedScheduler = lockScheduler.Clone();

        // Act: tick clone past all timer durations
        clonedScheduler.Tick(ref clonedState, 2.0f);

        // Assert: clone locks expired
        Assert.False(clonedState.IsLocked(2, 2, CellLockType.Drop));
        Assert.False(clonedState.IsLocked(3, 3, CellLockType.Swap));

        // Assert: original locks are intact
        Assert.True(state.IsLocked(2, 2, CellLockType.Drop),
            "Original timed Drop lock should remain after clone ticks");
        Assert.True(state.IsLocked(3, 3, CellLockType.Swap),
            "Original timed Swap lock should remain after clone ticks");
    }

    [Fact]
    public void Clone_FullConsumption_OriginalIntact()
    {
        // Arrange: create state with multiple lock types
        var state = CreateFilledState(5, 6);
        var lockScheduler = new LockScheduler();

        var manualToken = lockScheduler.Acquire(ref state, new Position(0, 0), CellLockType.Drop);
        lockScheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, 0.3f);
        lockScheduler.Acquire(ref state, new Position(2, 2), CellLockType.Receive, 0.6f);

        var clonedState = state.Clone();
        var clonedScheduler = lockScheduler.Clone();

        // Act: fully consume clone — release manual, tick past all timers
        clonedScheduler.Release(ref clonedState, manualToken);
        clonedScheduler.Tick(ref clonedState, 1.0f);

        // Assert: clone is fully unlocked
        Assert.False(clonedState.IsLocked(0, 0, CellLockType.Drop));
        Assert.False(clonedState.IsLocked(1, 1, CellLockType.Drop));
        Assert.False(clonedState.IsLocked(2, 2, CellLockType.Receive));

        // Assert: original still has all locks
        Assert.True(state.IsLocked(0, 0, CellLockType.Drop));
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));
        Assert.True(state.IsLocked(2, 2, CellLockType.Receive));
    }

    #endregion

    #region 6. Manual early release of timed lock — physics resumes immediately

    [Fact]
    public void TimedLock_ManualEarlyRelease_PhysicsResumesImmediately()
    {
        // Arrange: tile at (2,1) with empty cell below at (2,2)
        var state = CreateFilledState(5, 6);
        state.SetTile(2, 2, new Tile(0, ElementType.None, 2, 2));

        var lockScheduler = new LockScheduler();
        var gravity = CreateGravitySystem();

        // Acquire a timed Drop lock with long duration (5s)
        var token = lockScheduler.Acquire(ref state, new Position(2, 1), CellLockType.Drop, 5.0f);

        // Verify tile can't move
        Assert.True(state.IsLocked(2, 1, CellLockType.Drop));
        Assert.False(state.CanMove(2, 1));

        // Act: run gravity — tile should not move
        for (int i = 0; i < 10; i++)
            gravity.Update(ref state, 1f / 60f);

        var tileAfterLocked = state.GetTile(2, 1);
        Assert.NotEqual(ElementType.None, tileAfterLocked.Type);

        // Act: manually release the lock early
        lockScheduler.Release(ref state, token);

        // Assert: physics should resume immediately
        Assert.False(state.IsLocked(2, 1, CellLockType.Drop));
        Assert.True(state.CanMove(2, 1));

        // Run gravity — tile should now fall
        for (int i = 0; i < 60; i++)
            gravity.Update(ref state, 1f / 60f);

        // The tile should have moved from (2,1) to (2,2) — or at least be falling
        var tileAtOrig = state.GetTile(2, 1);
        var tileAtDest = state.GetTile(2, 2);
        Assert.True(
            tileAtOrig.Type == ElementType.None || tileAtDest.Type != ElementType.None,
            "Tile should have fallen after early release of Drop lock");
    }

    [Fact]
    public void TimedLock_ManualEarlyRelease_NoDoubleRelease_RefCountCorrect()
    {
        // Arrange
        var state = CreateFilledState(5, 6);
        var lockScheduler = new LockScheduler();

        // Acquire a timed Drop lock with 0.5s duration
        var timedToken = lockScheduler.Acquire(ref state, new Position(2, 2), CellLockType.Drop, 0.5f);

        // Also acquire a manual lock on the same cell (another system holding it)
        var manualToken = lockScheduler.Acquire(ref state, new Position(2, 2), CellLockType.Drop);

        Assert.Equal(2, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));

        // Act: manually release the timed lock early
        lockScheduler.Release(ref state, timedToken);
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));

        // Tick past the timer expiry — the idempotent release should not decrement further
        lockScheduler.Tick(ref state, 1.0f);

        // Assert: ref-count is still 1 (only the manual lock remains)
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));
        Assert.True(state.IsLocked(2, 2, CellLockType.Drop),
            "Manual lock should still be active after timed lock's timer expires");

        // Release the manual lock — now fully unlocked
        lockScheduler.Release(ref state, manualToken);
        Assert.Equal(0, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));
        Assert.False(state.IsLocked(2, 2, CellLockType.Drop));
    }

    #endregion

    #region 7. Empty cell in blast zone — no spurious unlock

    [Fact]
    public void Explosion_EmptyCellInBlastZone_NoSpuriousUnlock_WithLockScheduler()
    {
        // Arrange: 5x6 grid with one empty cell inside the explosion radius
        var state = CreateFilledState(5, 6);
        var lockScheduler = new LockScheduler();
        var explosionSystem = CreateExplosionSystem(lockScheduler);

        // Create an empty cell at (2,2) — inside the blast zone of explosion at (2,3) radius 1
        state.SetTile(2, 2, new Tile(0, ElementType.None, 2, 2));

        // Manually lock (2,2) with Drop (simulating another system, ref-count = 1)
        var externalToken = lockScheduler.Acquire(ref state, new Position(2, 2), CellLockType.Drop);
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));

        // Create an explosion that covers the empty cell
        var origin = new Position(2, 3);
        explosionSystem.CreateExplosion(ref state, origin, 1);

        // The empty cell should still have ref-count 1 (explosion skips empty cells)
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));

        // Process all waves
        var eventCollector = new StubEventCollector();
        var triggeredBombs = new List<Position>();
        explosionSystem.Update(ref state, 0.1f, 1, 0.1f, eventCollector, triggeredBombs);
        explosionSystem.Update(ref state, 0.1f, 2, 0.2f, eventCollector, triggeredBombs);

        // Assert: the manually-acquired Drop lock is STILL active (ref-count = 1, not decremented)
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));
        Assert.True(state.IsLocked(2, 2, CellLockType.Drop),
            "External Drop lock on empty cell should NOT be decremented by explosion (LockScheduler path)");

        // Cleanup: release external lock
        lockScheduler.Release(ref state, externalToken);
        Assert.Equal(0, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));
    }

    [Fact]
    public void Explosion_EmptyCellInBlastZone_NoSpuriousUnlock_NullScheduler()
    {
        // Arrange: 5x6 grid with one empty cell inside the explosion radius.
        // This tests the null-scheduler path (uses state.Lock/Unlock directly).
        // Before the fix, ReleaseLockForCell would blindly call state.Unlock on a cell
        // it never locked, decrementing another system's ref-count.
        var state = CreateFilledState(5, 6);
        var explosionSystem = CreateExplosionSystemNoScheduler();

        // Create an empty cell at (2,2) — inside the blast zone
        state.SetTile(2, 2, new Tile(0, ElementType.None, 2, 2));

        // Manually lock (2,2) with Drop (simulating another system, ref-count = 1)
        state.Lock(2, 2, CellLockType.Drop);
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));

        // Create an explosion that covers the empty cell
        var origin = new Position(2, 3);
        explosionSystem.CreateExplosion(ref state, origin, 1);

        // The empty cell should still have ref-count 1 (explosion skips empty cells during lock)
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));

        // Process all waves
        var eventCollector = new StubEventCollector();
        var triggeredBombs = new List<Position>();
        explosionSystem.Update(ref state, 0.1f, 1, 0.1f, eventCollector, triggeredBombs);
        explosionSystem.Update(ref state, 0.1f, 2, 0.2f, eventCollector, triggeredBombs);

        // Assert: the manually-acquired Drop lock is STILL active (ref-count = 1, not decremented)
        // Before the fix, the null-scheduler path would call state.Unlock on the empty cell,
        // spuriously decrementing the ref-count from 1 to 0.
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));
        Assert.True(state.IsLocked(2, 2, CellLockType.Drop),
            "External Drop lock on empty cell should NOT be decremented by explosion (null-scheduler path)");

        // Cleanup: manually unlock
        state.Unlock(2, 2, CellLockType.Drop);
        Assert.Equal(0, CellLockOps.GetCount(state.CellLocks[state.Index(2, 2)], CellLockType.Drop));
    }

    #endregion
}
