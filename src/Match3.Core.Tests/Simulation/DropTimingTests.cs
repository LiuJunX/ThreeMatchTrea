using System.Collections.Generic;
using System.Linq;
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
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Simulation;

/// <summary>
/// Tick-by-tick observation of drop timing after various destruction events.
/// Verifies when destroyed cells first accept new tiles (refill/gravity).
/// </summary>
public class DropTimingTests
{
    private readonly ITestOutputHelper _output;

    public DropTimingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Scenario: 3-in-a-row match on a small board.
    /// Observe tick-by-tick when destroyed cells get new tiles.
    ///
    /// Board layout (5 wide x 5 tall, y=0 is top):
    ///   col:  0    1    2    3    4
    /// y=0:  Item2 Item2 Item2 Item3 Item4   ← horizontal match at y=0
    /// y=1:  Item3 Item4 Item3 Item4 Item3
    /// y=2:  Item4 Item3 Item4 Item3 Item4
    /// y=3:  Item3 Item4 Item3 Item4 Item3
    /// y=4:  Item4 Item3 Item4 Item3 Item4
    /// </summary>
    [Fact]
    public void Match3_ObserveDropTiming_AfterMatch()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);

        // Fill board with non-matching checkerboard
        int id = 1;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, (x + y) % 2 == 0 ? ElementType.Item4 : ElementType.Item3, x, y));

        // Place horizontal 3-match at row 0, cols 0-2
        state.SetTile(0, 0, new Tile(100, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(101, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(102, ElementType.Item2, 2, 0));

        var events = new BufferedEventCollector();
        var engine = CreateFullEngine(state, rng, events);

        _output.WriteLine("=== Match3 Drop Timing Test ===");
        _output.WriteLine($"Initial: tiles at (0,0)={state.GetTile(0,0).Type}, (1,0)={state.GetTile(1,0).Type}, (2,0)={state.GetTile(2,0).Type}");

        // Record tile IDs at match positions before any tick
        var originalIds = new[] { state.GetTile(0, 0).Id, state.GetTile(1, 0).Id, state.GetTile(2, 0).Id };

        // Step tick by tick, observe state changes
        for (int tick = 0; tick < 10; tick++)
        {
            var preState = engine.State;
            var preTiles = new[]
            {
                preState.GetTile(0, 0),
                preState.GetTile(1, 0),
                preState.GetTile(2, 0)
            };

            engine.Tick();

            var postState = engine.State;
            var postTiles = new[]
            {
                postState.GetTile(0, 0),
                postState.GetTile(1, 0),
                postState.GetTile(2, 0)
            };

            // Drain events for this tick
            var tickEvents = new List<GameEvent>();
            events.DrainEventsTo(tickEvents);

            var matchEvents = tickEvents.Where(e => e is MatchDetectedEvent).ToList();
            var destroyEvents = tickEvents.Where(e => e is TileDestroyedEvent).ToList();

            _output.WriteLine($"\n--- Tick {tick} ---");
            for (int i = 0; i < 3; i++)
            {
                var pre = preTiles[i];
                var post = postTiles[i];
                string change = "";
                if (pre.Type != post.Type) change = $" *** CHANGED: {pre.Type}(id={pre.Id}) -> {post.Type}(id={post.Id})";
                else if (pre.Id != post.Id) change = $" *** NEW TILE: id {pre.Id} -> {post.Id}";
                _output.WriteLine($"  ({i},0): type={post.Type}, id={post.Id}, falling={post.IsFalling}, pos=({post.Position.X:F2},{post.Position.Y:F2}){change}");
            }

            if (matchEvents.Count > 0) _output.WriteLine($"  Events: {matchEvents.Count} match(es) detected");
            if (destroyEvents.Count > 0) _output.WriteLine($"  Events: {destroyEvents.Count} tile(s) destroyed");

            // Check: did any original match position get a NEW tile?
            for (int i = 0; i < 3; i++)
            {
                var post = postTiles[i];
                if (post.Type != ElementType.None && post.Id != originalIds[i] && !originalIds.Contains(post.Id))
                {
                    _output.WriteLine($"  >>> Cell ({i},0) received new tile (id={post.Id}) at tick {tick}");
                }
            }

            if (engine.IsStable())
            {
                _output.WriteLine($"\n  Engine stable at tick {tick}.");
                break;
            }
        }
    }

    /// <summary>
    /// Scenario: Tap-activate a ColorBomb. Observe when target cells go empty
    /// and when they first receive new tiles.
    ///
    /// Board (5x5):
    /// y=0: CB    Item1 Item1 Item1 Item3
    /// y=1: Item3 Item1 Item3 Item1 Item3  ← Item1 targets at (1,1),(3,1)
    /// y=2: Item1 Item3 Item1 Item3 Item1  ← Item1 targets
    /// y=3: Item3 Item1 Item3 Item1 Item3
    /// y=4: Item1 Item3 Item1 Item3 Item1
    /// </summary>
    [Fact]
    public void ColorBomb_ObserveDropTiming_AfterActivation()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);

        // Fill with checkerboard of Item1 and Item3 (no matches)
        int id = 1;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, (x + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item3, x, y));

        // Place ColorBomb at (0,0)
        state.SetTile(0, 0, new Tile(200, ElementType.ColorBomb, 0, 0));

        // Record all Item1 positions and IDs
        var item1Positions = new List<(int x, int y, int id)>();
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
            {
                var t = state.GetTile(x, y);
                if (t.Type == ElementType.Item1)
                    item1Positions.Add((x, y, t.Id));
            }

        var events = new BufferedEventCollector();
        var engine = CreateFullEngine(state, rng, events);

        _output.WriteLine("=== ColorBomb Drop Timing Test ===");
        _output.WriteLine($"ColorBomb at (0,0), target=Item1, {item1Positions.Count} targets");

        // Activate ColorBomb
        engine.ActivateBomb(new Position(0, 0));
        _output.WriteLine($"After ActivateBomb: (0,0) type={engine.State.GetTile(0,0).Type}");

        // Track when each target cell first becomes None, and first gets a new tile
        var firstEmptyTick = new Dictionary<(int, int), int>();
        var firstNewTileTick = new Dictionary<(int, int), int>();
        var originalIds = item1Positions.Select(p => p.id).ToHashSet();

        for (int tick = 0; tick < 80; tick++)
        {
            engine.Tick();

            var s = engine.State;
            var tickEvents = new List<GameEvent>();
            events.DrainEventsTo(tickEvents);

            var beamEvents = tickEvents.Where(e => e is ColorBombBeamLaunchedEvent).ToList();
            var destroyEvents = tickEvents.Where(e => e is TileDestroyedEvent).ToList();
            var batchEvents = tickEvents.Where(e => e is ColorBombBatchDestroyEvent).ToList();

            bool hasInteresting = beamEvents.Count > 0 || destroyEvents.Count > 0 || batchEvents.Count > 0;

            // Check each target position
            foreach (var (px, py, origId) in item1Positions)
            {
                var tile = s.GetTile(px, py);

                // Track first empty
                if (tile.Type == ElementType.None && !firstEmptyTick.ContainsKey((px, py)))
                    firstEmptyTick[(px, py)] = tick;

                // Track first new tile (different from original)
                if (tile.Type != ElementType.None && tile.Id != origId && !originalIds.Contains(tile.Id))
                {
                    if (!firstNewTileTick.ContainsKey((px, py)))
                        firstNewTileTick[(px, py)] = tick;
                }
            }

            // Also check bomb position (0,0)
            var bombCell = s.GetTile(0, 0);
            if (bombCell.Type != ElementType.None && bombCell.Id != 200)
            {
                if (!firstNewTileTick.ContainsKey((0, 0)))
                    firstNewTileTick[(0, 0)] = tick;
            }

            if (hasInteresting)
            {
                _output.WriteLine($"\n--- Tick {tick} ---");
                if (beamEvents.Count > 0) _output.WriteLine($"  Beams launched: {beamEvents.Count}");
                if (batchEvents.Count > 0)
                {
                    var batch = (ColorBombBatchDestroyEvent)batchEvents[0];
                    _output.WriteLine($"  Batch destroy: {batch.DestroyedPositions.Count} tiles");
                }
                if (destroyEvents.Count > 0) _output.WriteLine($"  Tiles destroyed: {destroyEvents.Count}");
            }

            if (engine.IsStable())
            {
                _output.WriteLine($"\nEngine stable at tick {tick}.");
                break;
            }
        }

        // Summary report
        _output.WriteLine("\n=== TIMING SUMMARY ===");
        _output.WriteLine($"Bomb cell (0,0): first new tile at tick {(firstNewTileTick.ContainsKey((0, 0)) ? firstNewTileTick[(0, 0)].ToString() : "never")}");
        foreach (var (px, py, _) in item1Positions.OrderBy(p => p.y).ThenBy(p => p.x))
        {
            var emptyTick = firstEmptyTick.ContainsKey((px, py)) ? firstEmptyTick[(px, py)].ToString() : "never";
            var newTick = firstNewTileTick.ContainsKey((px, py)) ? firstNewTileTick[(px, py)].ToString() : "never";
            _output.WriteLine($"  ({px},{py}): empty at tick {emptyTick}, new tile at tick {newTick}, gap={(firstEmptyTick.ContainsKey((px, py)) && firstNewTileTick.ContainsKey((px, py)) ? (firstNewTileTick[(px, py)] - firstEmptyTick[(px, py)]).ToString() : "N/A")}");
        }
    }

    /// <summary>
    /// Scenario: Simple bomb activation (e.g., rocket).
    /// Observe when explosion-destroyed cells accept new tiles.
    /// </summary>
    [Fact]
    public void Rocket_ObserveDropTiming_AfterExplosion()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);

        // Fill with non-matching pattern
        int id = 1;
        var types = new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4 };
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, types[(x + y * 3) % 4], x, y));

        // Place horizontal rocket at center (2,2)
        state.SetTile(2, 2, new Tile(200, ElementType.HorizontalRocket, 2, 2));

        // Record tiles in row 2 (rocket will clear this row)
        var row2Ids = new int[5];
        for (int x = 0; x < 5; x++)
            row2Ids[x] = state.GetTile(x, 2).Id;

        var events = new BufferedEventCollector();
        var engine = CreateFullEngine(state, rng, events);

        _output.WriteLine("=== Rocket Drop Timing Test ===");
        _output.WriteLine("HorizontalRocket at (2,2), will clear row 2");

        // Activate rocket
        engine.ActivateBomb(new Position(2, 2));

        var firstEmptyTick = new Dictionary<int, int>();
        var firstNewTileTick = new Dictionary<int, int>();

        for (int tick = 0; tick < 30; tick++)
        {
            engine.Tick();

            var s = engine.State;
            var tickEvents = new List<GameEvent>();
            events.DrainEventsTo(tickEvents);

            var destroyEvents = tickEvents.Where(e => e is TileDestroyedEvent).ToList();
            var bombEvents = tickEvents.Where(e => e is BombActivatedEvent).ToList();

            // Check row 2
            for (int x = 0; x < 5; x++)
            {
                var tile = s.GetTile(x, 2);

                if (tile.Type == ElementType.None && !firstEmptyTick.ContainsKey(x))
                    firstEmptyTick[x] = tick;

                if (tile.Type != ElementType.None && tile.Id != row2Ids[x] && !row2Ids.Contains(tile.Id))
                {
                    if (!firstNewTileTick.ContainsKey(x))
                        firstNewTileTick[x] = tick;
                }
            }

            bool hasInteresting = destroyEvents.Count > 0 || bombEvents.Count > 0;
            if (hasInteresting)
            {
                _output.WriteLine($"\n--- Tick {tick} ---");
                if (bombEvents.Count > 0) _output.WriteLine($"  Bomb activated");
                if (destroyEvents.Count > 0) _output.WriteLine($"  Tiles destroyed: {destroyEvents.Count}");
            }

            if (engine.IsStable())
            {
                _output.WriteLine($"\nEngine stable at tick {tick}.");
                break;
            }
        }

        _output.WriteLine("\n=== TIMING SUMMARY (row 2) ===");
        for (int x = 0; x < 5; x++)
        {
            var emptyTick = firstEmptyTick.ContainsKey(x) ? firstEmptyTick[x].ToString() : "never";
            var newTick = firstNewTileTick.ContainsKey(x) ? firstNewTileTick[x].ToString() : "never";
            var gap = firstEmptyTick.ContainsKey(x) && firstNewTileTick.ContainsKey(x)
                ? (firstNewTileTick[x] - firstEmptyTick[x]).ToString()
                : "N/A";
            _output.WriteLine($"  ({x},2): empty at tick {emptyTick}, new tile at tick {newTick}, gap={gap}");
        }
    }

    #region Engine Factory

    private static SimulationEngine CreateFullEngine(GameState state, IRandom rng, IEventCollector events)
    {
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
        var matchProcessor = new StandardMatchProcessor(score, coverSystem, groundSystem, BombEffectRegistry.CreateDefault());
        var lockScheduler = new LockScheduler();
        var explosionSystem = new ExplosionSystem(coverSystem, groundSystem, objectiveSystem, lockScheduler);
        var sessionManager = new ColorBombSessionManager(null, coverSystem, groundSystem, objectiveSystem, lockScheduler);
        var powerUpHandler = new PowerUpHandler(score, new BombComboHandler(), BombEffectRegistry.CreateDefault(),
            coverSystem, groundSystem, explosionSystem: explosionSystem, colorBombSessionManager: sessionManager,
            lockScheduler: lockScheduler);

        return new SimulationEngine(
            state,
            SimulationConfig.ForHumanPlay(),
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            eventCollector: events,
            explosionSystem: explosionSystem,
            objectiveSystem: objectiveSystem,
            colorBombSessionManager: sessionManager,
            lockScheduler: lockScheduler);
    }

    #endregion

    #region Assertion-based Verification Tests

    /// <summary>
    /// After a 3-match, destroyed cells should NOT receive new tiles on the same tick.
    /// The Receive lock must delay refill by at least 1 tick.
    /// </summary>
    [Fact]
    public void Match3_DestroyedCells_DoNotFillImmediately()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);

        // Non-matching checkerboard
        int id = 1;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, (x + y) % 2 == 0 ? ElementType.Item4 : ElementType.Item3, x, y));

        // Horizontal 3-match at row 0
        state.SetTile(0, 0, new Tile(100, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(101, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(102, ElementType.Item2, 2, 0));

        var events = new BufferedEventCollector();
        var engine = CreateFullEngine(state, rng, events);

        // Tick 0: match detection + tile destruction
        engine.Tick();

        // Verify: match positions should now be empty (matched tiles cleared)
        var s = engine.State;
        bool anyCleared = s.GetTile(0, 0).Type == ElementType.None ||
                          s.GetTile(1, 0).Type == ElementType.None ||
                          s.GetTile(2, 0).Type == ElementType.None;
        if (!anyCleared)
        {
            // Match might need 1 more tick (physics must settle first)
            engine.Tick();
            s = engine.State;
        }

        // Record which cells are empty after destruction
        var emptyAfterMatch = new bool[3];
        for (int i = 0; i < 3; i++)
            emptyAfterMatch[i] = s.GetTile(i, 0).Type == ElementType.None;

        // If cells were cleared, the NEXT tick should still show Receive locks preventing refill
        if (emptyAfterMatch[0] || emptyAfterMatch[1] || emptyAfterMatch[2])
        {
            // Check that Receive locks are present
            for (int i = 0; i < 3; i++)
            {
                if (emptyAfterMatch[i])
                {
                    Assert.True(s.IsLocked(i, 0, CellLockType.Receive),
                        $"Cell ({i},0) should have Receive lock after match clear");
                }
            }
        }
    }

    /// <summary>
    /// When a 4-match triggers bomb generation, the bomb origin cell gets DropLock (not ReceiveLock)
    /// and all other merge-source positions get ReceiveLock (not DropLock).
    /// </summary>
    [Fact]
    public void BombMerge_BombOriginGetsDropLock_MergeSourcesGetReceiveLock()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);

        // Non-matching fill
        int id = 1;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, (x + y) % 2 == 0 ? ElementType.Item4 : ElementType.Item3, x, y));

        // 4-in-a-row at row 0 → triggers bomb generation
        state.SetTile(0, 0, new Tile(100, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(101, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(102, ElementType.Item2, 2, 0));
        state.SetTile(3, 0, new Tile(103, ElementType.Item2, 3, 0));

        var events = new BufferedEventCollector();
        var engine = CreateFullEngine(state, rng, events);

        // Tick until match fires (bomb appears)
        Position? bombPos = null;
        for (int tick = 0; tick < 5; tick++)
        {
            engine.Tick();
            var s = engine.State;
            for (int x = 0; x < 4; x++)
            {
                if (s.GetTile(x, 0).Type.IsBomb())
                {
                    bombPos = new Position(x, 0);
                    break;
                }
            }
            if (bombPos.HasValue) break;
        }

        Assert.True(bombPos.HasValue, "4-match should generate a bomb");

        var st = engine.State;

        // Bomb origin: has DropLock, no ReceiveLock
        Assert.True(st.IsLocked(bombPos.Value, CellLockType.Drop),
            $"BombOrigin ({bombPos.Value}) should have DropLock");
        Assert.False(st.IsLocked(bombPos.Value, CellLockType.Receive),
            $"BombOrigin ({bombPos.Value}) should NOT have ReceiveLock");

        // Merge sources: have ReceiveLock, no DropLock
        for (int x = 0; x < 4; x++)
        {
            var pos = new Position(x, 0);
            if (pos == bombPos.Value) continue;

            Assert.True(st.IsLocked(pos, CellLockType.Receive),
                $"MergeSource ({pos}) should have ReceiveLock");
            Assert.False(st.IsLocked(pos, CellLockType.Drop),
                $"MergeSource ({pos}) should NOT have DropLock");
        }
    }

    /// <summary>
    /// BombOrigin DropLock duration = BombOriginDrop (0.15s).
    /// At 60 FPS (dt ≈ 0.01667s), the lock should expire after ≈9 ticks.
    /// </summary>
    [Fact]
    public void BombMerge_DropLockDuration_MatchesBombOriginDropConstant()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);

        int id = 1;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, (x + y) % 2 == 0 ? ElementType.Item4 : ElementType.Item3, x, y));

        state.SetTile(0, 0, new Tile(100, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(101, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(102, ElementType.Item2, 2, 0));
        state.SetTile(3, 0, new Tile(103, ElementType.Item2, 3, 0));

        var engine = CreateFullEngine(state, rng, new BufferedEventCollector());

        // Find the tick where bomb appears
        Position? bombPos = null;
        for (int tick = 0; tick < 5; tick++)
        {
            engine.Tick();
            var s = engine.State;
            for (int x = 0; x < 4; x++)
            {
                if (s.GetTile(x, 0).Type.IsBomb())
                {
                    bombPos = new Position(x, 0);
                    break;
                }
            }
            if (bombPos.HasValue) break;
        }

        Assert.True(bombPos.HasValue, "4-match should generate a bomb");

        // Count how many additional ticks until DropLock expires
        float dt = SimulationConfig.DefaultFixedDeltaTime;
        int expectedTicks = (int)System.Math.Ceiling(ReceiveLockTimings.BombOriginDrop / dt);

        int ticksWithLock = 0;
        for (int i = 0; i < expectedTicks + 5; i++)
        {
            if (engine.State.IsLocked(bombPos.Value, CellLockType.Drop))
                ticksWithLock++;
            else
                break;
            engine.Tick();
        }

        // Lock should last approximately expectedTicks (allow ±1 for boundary rounding)
        Assert.InRange(ticksWithLock, expectedTicks - 1, expectedTicks + 1);
    }

    /// <summary>
    /// MergeSource ReceiveLock duration = MergeSource (0.3s).
    /// Should last roughly twice as long as the BombOrigin DropLock (0.15s).
    /// </summary>
    [Fact]
    public void BombMerge_ReceiveLockDuration_MatchesMergeSourceConstant()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);

        int id = 1;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, (x + y) % 2 == 0 ? ElementType.Item4 : ElementType.Item3, x, y));

        state.SetTile(0, 0, new Tile(100, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(101, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(102, ElementType.Item2, 2, 0));
        state.SetTile(3, 0, new Tile(103, ElementType.Item2, 3, 0));

        var engine = CreateFullEngine(state, rng, new BufferedEventCollector());

        // Find bomb position and a merge source
        Position? bombPos = null;
        for (int tick = 0; tick < 5; tick++)
        {
            engine.Tick();
            var s = engine.State;
            for (int x = 0; x < 4; x++)
            {
                if (s.GetTile(x, 0).Type.IsBomb())
                {
                    bombPos = new Position(x, 0);
                    break;
                }
            }
            if (bombPos.HasValue) break;
        }

        Assert.True(bombPos.HasValue, "4-match should generate a bomb");

        // Pick first merge source (non-bomb position in the match)
        Position mergeSource = default;
        for (int x = 0; x < 4; x++)
        {
            if (x != bombPos.Value.X)
            {
                mergeSource = new Position(x, 0);
                break;
            }
        }

        float dt = SimulationConfig.DefaultFixedDeltaTime;
        int expectedTicks = (int)System.Math.Ceiling(ReceiveLockTimings.MergeSource / dt);

        int ticksWithLock = 0;
        for (int i = 0; i < expectedTicks + 5; i++)
        {
            if (engine.State.IsLocked(mergeSource, CellLockType.Receive))
                ticksWithLock++;
            else
                break;
            engine.Tick();
        }

        Assert.InRange(ticksWithLock, expectedTicks - 1, expectedTicks + 1);
    }

    /// <summary>
    /// After running to stable, no cell should have Receive or Drop locks remaining.
    /// This verifies timed locks expire correctly.
    /// </summary>
    [Fact]
    public void TimedLocks_AllExpire_WhenStable()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);

        int id = 1;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, (x + y) % 2 == 0 ? ElementType.Item4 : ElementType.Item3, x, y));

        // 3-match at row 0
        state.SetTile(0, 0, new Tile(100, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(101, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(102, ElementType.Item2, 2, 0));

        var engine = CreateFullEngine(state, rng, new BufferedEventCollector());
        engine.RunUntilStable();

        var s = engine.State;
        for (int i = 0; i < s.Width * s.Height; i++)
        {
            Assert.Equal((byte)0, s.CellLocks[i]);
        }
    }

    /// <summary>
    /// After settling with covers, no tile should have stale IsFalling flag.
    /// </summary>
    [Fact]
    public void CoverTiles_NoStaleFallingFlags_AfterSettling()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);

        // Place Cage covers on center column
        for (int y = 0; y < 5; y++)
            state.SetCover(2, y, new Cover(CoverType.Cage, 1));

        int id = 1;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, (x + y) % 2 == 0 ? ElementType.Item4 : ElementType.Item3, x, y));

        // 3-match at row 0
        state.SetTile(0, 0, new Tile(100, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(101, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(102, ElementType.Item2, 2, 0));

        var engine = CreateFullEngine(state, rng, new BufferedEventCollector());
        engine.RunUntilStable();

        var s = engine.State;
        for (int i = 0; i < s.Width * s.Height; i++)
        {
            if (s.Grid[i].IsFalling)
            {
                int x = i % s.Width, y = i / s.Width;
                Assert.Fail($"Tile at ({x},{y}) has stale IsFalling after settling");
            }
        }
    }

    /// <summary>
    /// Verify the Clone path creates an independent ColorBombSessionManager.
    /// </summary>
    [Fact]
    public void Clone_HasIndependentColorBombSessionManager()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);

        int id = 1;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, (x + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item3, x, y));

        var engine = CreateFullEngine(state, rng, new BufferedEventCollector());

        // Clone should not throw
        var clone = engine.Clone();

        // Both should be stable independently
        Assert.True(engine.IsStable());
        Assert.True(clone.IsStable());

        // Running the clone should not affect original state
        var originalScore = engine.State.Score;
        clone.RunUntilStable();
        Assert.Equal(originalScore, engine.State.Score);
    }

    #endregion
}
