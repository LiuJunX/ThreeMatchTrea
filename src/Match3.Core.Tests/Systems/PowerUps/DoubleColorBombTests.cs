using System;
using System.Collections.Generic;
using System.Linq;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Choreography;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.PowerUps;

/// <summary>
/// 双彩球组合专项测试
///
/// 覆盖范围：
/// - P0-1: 时序同步（DoubleColorBombConstants 统一）
/// - P0-2: 链式 ColorBomb 跳过 session manager
/// - P1:   Indestructible 锁阻止消除
/// - P2-1: Void/空格过滤
/// - 复杂交互：棋盘上有其他炸弹、Cover、Unmatchable
/// </summary>
public class DoubleColorBombTests : IDisposable
{
    private readonly ExplosionSystem _explosionSystem;
    private readonly StubEventCollector _eventCollector;

    public DoubleColorBombTests()
    {
        _explosionSystem = new ExplosionSystem();
        _eventCollector = new StubEventCollector();
    }

    public void Dispose()
    {
        _explosionSystem.Reset();
    }

    #region P0-1: 时序常量同步

    [Fact]
    public void DoubleColorBombConstants_MatchChoreographyConfigDefaults()
    {
        var config = new ChoreographyConfig();
        Assert.Equal(DoubleColorBombConstants.WipeInterval, config.DoubleColorWipeInterval);
        Assert.Equal(DoubleColorBombConstants.WipeAcceleration, config.DoubleColorWipeAccel);
    }

    [Fact]
    public void DoubleColorBomb_ExplosionUsesSharedConstants()
    {
        // Arrange: 8x8 board, two adjacent ColorBombs
        var state = CreateFilledState(8, 8);
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);
        state.SetTile(p1.X, p1.Y, new Tile(100, ElementType.ColorBomb, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(101, ElementType.ColorBomb, p2.X, p2.Y));

        var handler = CreateHandlerWithExplosion();
        handler.ProcessSpecialMove(ref state, p1, p2, 1, 0f, _eventCollector, out _);

        // Assert: explosion was created (tiles are suspended)
        Assert.True(_explosionSystem.HasActiveExplosions);

        // Verify wave timing matches constants by advancing exactly one WipeInterval
        var triggeredBombs = new List<Position>();
        _explosionSystem.Update(ref state, DoubleColorBombConstants.WipeInterval, 2, 0.04f,
            _eventCollector, triggeredBombs);

        // Wave 0 should have processed (tiles at distance 0 from p2 destroyed)
        var originTile = state.GetTile(p2.X, p2.Y);
        Assert.Equal(ElementType.None, originTile.Type);
    }

    #endregion

    #region P0-2: 链式 ColorBomb 跳过 session manager

    [Fact]
    public void ActivateBomb_ColorBomb_ChainReaction_SkipsSessionManager()
    {
        // Arrange: board with a single ColorBomb, use session manager
        var state = CreateFilledState(8, 8);
        var bombPos = new Position(3, 3);
        state.SetTile(bombPos.X, bombPos.Y, new Tile(50, ElementType.ColorBomb, bombPos.X, bombPos.Y));

        var sessionManager = new ColorBombSessionManager();
        var handler = CreateHandlerWithExplosionAndSession(sessionManager);

        // Act: activate as chain reaction
        handler.ActivateBomb(ref state, bombPos, 1, 0f, _eventCollector, isChainReaction: true);

        // Assert: session manager should NOT have an active session
        Assert.False(sessionManager.HasActiveSessions);

        // But the bomb should be activated (BombActivatedEvent emitted)
        Assert.Contains(_eventCollector.Events, e => e is BombActivatedEvent bae
            && bae.Position.Equals(bombPos));
    }

    [Fact]
    public void ActivateBomb_ColorBomb_PlayerInitiated_UsesSessionManager()
    {
        // Arrange
        var state = CreateFilledState(8, 8);
        var bombPos = new Position(3, 3);
        state.SetTile(bombPos.X, bombPos.Y, new Tile(50, ElementType.ColorBomb, bombPos.X, bombPos.Y));

        var sessionManager = new ColorBombSessionManager();
        var handler = CreateHandlerWithExplosionAndSession(sessionManager);

        // Act: activate as player-initiated (NOT chain reaction)
        handler.ActivateBomb(ref state, bombPos, 1, 0f, _eventCollector, isChainReaction: false);

        // Assert: session manager SHOULD have an active session
        Assert.True(sessionManager.HasActiveSessions);
    }

    [Fact]
    public void DoubleColorBomb_BoardWithOtherColorBomb_ChainSkipsSession()
    {
        // Arrange: board with two ColorBombs being combo'd + a third ColorBomb on the board
        var state = CreateFilledState(8, 8);
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);
        var thirdBombPos = new Position(6, 6);

        state.SetTile(p1.X, p1.Y, new Tile(100, ElementType.ColorBomb, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(101, ElementType.ColorBomb, p2.X, p2.Y));
        state.SetTile(thirdBombPos.X, thirdBombPos.Y,
            new Tile(102, ElementType.ColorBomb, thirdBombPos.X, thirdBombPos.Y));

        var sessionManager = new ColorBombSessionManager();
        var handler = CreateHandlerWithExplosionAndSession(sessionManager);

        // Act: process the double ColorBomb combo
        handler.ProcessSpecialMove(ref state, p1, p2, 1, 0f, _eventCollector, out _);

        // Advance explosion waves until the third bomb is hit
        var triggeredBombs = new List<Position>();
        for (int i = 0; i < 20; i++)
        {
            _explosionSystem.Update(ref state, 0.05f, 2 + i, 0.05f * i,
                _eventCollector, triggeredBombs);

            // If the third bomb was triggered, activate it as chain reaction
            foreach (var pos in triggeredBombs)
            {
                handler.ActivateBomb(ref state, pos, 2 + i, 0.05f * i,
                    _eventCollector, isChainReaction: true);
            }
            triggeredBombs.Clear();

            if (!_explosionSystem.HasActiveExplosions)
                break;
        }

        // Assert: session manager should NOT have created any sessions
        Assert.False(sessionManager.HasActiveSessions);
    }

    #endregion

    #region P1: Indestructible 锁

    [Fact]
    public void Explosion_IndestructibleTile_Survives()
    {
        // Arrange
        var state = CreateFilledState(8, 8);
        var origin = new Position(4, 4);
        var protectedPos = new Position(5, 4); // Distance 1 from origin

        // Lock the tile as Indestructible
        state.Lock(protectedPos, CellLockType.Indestructible);

        // Create explosion centered at origin
        _explosionSystem.CreateExplosion(ref state, origin, 2);

        // Verify tile is suspended initially
        Assert.True(state.IsLocked(protectedPos.X, protectedPos.Y, CellLockType.Drop));

        var triggeredBombs = new List<Position>();

        // Act: advance wave 0 (origin)
        _explosionSystem.Update(ref state, 0.1f, 1, 0f, _eventCollector, triggeredBombs);

        // Act: advance wave 1 (hits the protected tile)
        _explosionSystem.Update(ref state, 0.1f, 2, 0.1f, _eventCollector, triggeredBombs);

        // Assert: protected tile survives
        var tile = state.GetTile(protectedPos.X, protectedPos.Y);
        Assert.NotEqual(ElementType.None, tile.Type);
        // Suspended flag should be cleared (gravity can resume)
        Assert.False(state.IsLocked(protectedPos.X, protectedPos.Y, CellLockType.Drop));
    }

    [Fact]
    public void Explosion_IndestructibleBomb_NotTriggered()
    {
        // Arrange: place an indestructible bomb in the explosion path
        var state = CreateFilledState(8, 8);
        var origin = new Position(4, 4);
        var bombPos = new Position(5, 4); // Distance 1

        state.SetTile(bombPos.X, bombPos.Y,
            new Tile(50, ElementType.HorizontalRocket, bombPos.X, bombPos.Y));
        state.Lock(bombPos, CellLockType.Indestructible);

        _explosionSystem.CreateExplosion(ref state, origin, 2);

        var triggeredBombs = new List<Position>();

        // Wave 0
        _explosionSystem.Update(ref state, 0.1f, 1, 0f, _eventCollector, triggeredBombs);
        // Wave 1 (hits the indestructible bomb)
        _explosionSystem.Update(ref state, 0.1f, 2, 0.1f, _eventCollector, triggeredBombs);

        // Assert: bomb should NOT be triggered
        Assert.Empty(triggeredBombs);
        // Bomb still exists
        Assert.Equal(ElementType.HorizontalRocket, state.GetTile(bombPos.X, bombPos.Y).Type);
    }

    [Fact]
    public void DoubleColorBomb_WithIndestructibleTile_TileSurvives()
    {
        // Arrange
        var state = CreateFilledState(8, 8);
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);
        var protectedPos = new Position(7, 7);

        state.SetTile(p1.X, p1.Y, new Tile(100, ElementType.ColorBomb, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(101, ElementType.ColorBomb, p2.X, p2.Y));
        state.Lock(protectedPos, CellLockType.Indestructible);

        var handler = CreateHandlerWithExplosion();

        // Act
        handler.ProcessSpecialMove(ref state, p1, p2, 1, 0f, _eventCollector, out _);

        // Run all explosion waves to completion
        RunExplosionToCompletion(ref state);

        // Assert: protected tile survives, others destroyed
        Assert.NotEqual(ElementType.None, state.GetTile(protectedPos.X, protectedPos.Y).Type);

        // At least one other tile should be destroyed
        int remaining = CountNonEmptyTiles(state);
        Assert.Equal(1, remaining); // Only the protected tile remains
    }

    #endregion

    #region P2-1: Void/空格过滤

    [Fact]
    public void ApplyColorPlusColor_SkipsVoidCells()
    {
        // Arrange: 5x5 board with some Void cells
        var state = CreateFilledState(5, 5);
        state.SetCell(0, 0, CellKind.Void);
        state.SetCell(4, 4, CellKind.Void);
        // Clear tiles at void positions (they shouldn't exist there)
        state.SetTile(0, 0, new Tile(0, ElementType.None, 0, 0));
        state.SetTile(4, 4, new Tile(0, ElementType.None, 4, 4));

        var combo = new BombComboHandler();
        var p1 = new Position(2, 2);
        var p2 = new Position(3, 2);
        state.SetTile(p1.X, p1.Y, new Tile(100, ElementType.ColorBomb, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(101, ElementType.ColorBomb, p2.X, p2.Y));

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: Void positions excluded
        Assert.DoesNotContain(new Position(0, 0), affected);
        Assert.DoesNotContain(new Position(4, 4), affected);
        // Non-void tiles included (25 total - 2 void = 23 tiles)
        Assert.Equal(23, affected.Count);
    }

    [Fact]
    public void ApplyColorPlusColor_SkipsEmptyTiles()
    {
        // Arrange: 5x5 board with some empty tiles
        var state = CreateFilledState(5, 5);
        state.SetTile(0, 0, new Tile(0, ElementType.None, 0, 0));
        state.SetTile(1, 0, new Tile(0, ElementType.None, 1, 0));

        var combo = new BombComboHandler();
        var p1 = new Position(2, 2);
        var p2 = new Position(3, 2);
        state.SetTile(p1.X, p1.Y, new Tile(100, ElementType.ColorBomb, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(101, ElementType.ColorBomb, p2.X, p2.Y));

        // Act
        var affected = new HashSet<Position>();
        combo.ApplyCombo(ref state, p1, p2, affected);

        // Assert: empty positions excluded
        Assert.DoesNotContain(new Position(0, 0), affected);
        Assert.DoesNotContain(new Position(1, 0), affected);
        Assert.Equal(23, affected.Count);
    }

    [Fact]
    public void ApplyColorPlusColor_EmptyBoard_NoAffected()
    {
        // Arrange: board with only two ColorBombs, everything else empty
        var state = CreateEmptyState(5, 5);
        var p1 = new Position(2, 2);
        var p2 = new Position(3, 2);
        state.SetTile(p1.X, p1.Y, new Tile(100, ElementType.ColorBomb, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(101, ElementType.ColorBomb, p2.X, p2.Y));

        // Act
        var affected = new HashSet<Position>();
        new BombComboHandler().ApplyCombo(ref state, p1, p2, affected);

        // Assert: only the two ColorBombs themselves
        Assert.Equal(2, affected.Count);
        Assert.Contains(p1, affected);
        Assert.Contains(p2, affected);
    }

    #endregion

    #region 复杂交互：棋盘上有其他炸弹

    [Fact]
    public void DoubleColorBomb_BoardWithRocket_TriggersChainExplosion()
    {
        // Arrange
        var state = CreateFilledState(8, 8);
        var p1 = new Position(3, 4);
        var p2 = new Position(4, 4);
        var rocketPos = new Position(6, 6);

        state.SetTile(p1.X, p1.Y, new Tile(100, ElementType.ColorBomb, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(101, ElementType.ColorBomb, p2.X, p2.Y));
        state.SetTile(rocketPos.X, rocketPos.Y,
            new Tile(102, ElementType.HorizontalRocket, rocketPos.X, rocketPos.Y));

        var handler = CreateHandlerWithExplosion();
        handler.ProcessSpecialMove(ref state, p1, p2, 1, 0f, _eventCollector, out _);

        // Run waves and handle chain reactions
        var triggeredBombs = new List<Position>();
        bool rocketTriggered = false;
        for (int i = 0; i < 30; i++)
        {
            _explosionSystem.Update(ref state, 0.05f, 2 + i, 0.05f * i,
                _eventCollector, triggeredBombs);

            foreach (var pos in triggeredBombs)
            {
                if (pos.Equals(rocketPos))
                    rocketTriggered = true;
                handler.ActivateBomb(ref state, pos, 2 + i, 0.05f * i,
                    _eventCollector, isChainReaction: true);
            }
            triggeredBombs.Clear();

            if (!_explosionSystem.HasActiveExplosions) break;
        }

        // Assert: rocket was chain-triggered
        Assert.True(rocketTriggered);
    }

    [Fact]
    public void DoubleColorBomb_WithUnmatchable_DestroysUnmatchable()
    {
        // Arrange: board with Unmatchable (stone) tiles
        var state = CreateFilledState(5, 5);
        var stonePos = new Position(0, 0);
        state.SetTile(stonePos.X, stonePos.Y,
            new Tile(50, ElementType.Unmatchable, stonePos.X, stonePos.Y));

        var p1 = new Position(2, 2);
        var p2 = new Position(3, 2);
        state.SetTile(p1.X, p1.Y, new Tile(100, ElementType.ColorBomb, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(101, ElementType.ColorBomb, p2.X, p2.Y));

        // Act
        var affected = new HashSet<Position>();
        new BombComboHandler().ApplyCombo(ref state, p1, p2, affected);

        // Assert: Unmatchable position is in affected set
        Assert.Contains(stonePos, affected);
    }

    #endregion

    #region Choreographer 四阶段编排

    [Fact]
    public void Choreographer_DoubleColorBomb_EmitsFourPhases()
    {
        var choreographer = new Choreographer();
        var events = new GameEvent[]
        {
            new BombComboEvent
            {
                Tick = 1,
                SimulationTime = 0f,
                TileIdA = 100,
                TileIdB = 101,
                BombTypeA = ElementType.ColorBomb,
                BombTypeB = ElementType.ColorBomb,
                PositionA = new Position(3, 4),
                PositionB = new Position(4, 4),
                AffectedPositions = GenerateFullBoardPositions(8, 8)
            }
        };

        var commands = choreographer.Choreograph(events);
        var effects = commands.OfType<ShowEffectCommand>().ToList();

        // All 4 phases present
        Assert.Contains(effects, e => e.EffectType == "colorx2_converge");
        Assert.Contains(effects, e => e.EffectType == "colorx2_fusion");
        Assert.Contains(effects, e => e.EffectType == "colorx2_aftermath");
        // Wipe hits for each affected tile
        var wipeHits = effects.Where(e => e.EffectType == "colorx2_wipe_hit").ToList();
        Assert.Equal(64, wipeHits.Count);
    }

    [Fact]
    public void Choreographer_DoubleColorBomb_WipeTimingUsesConfig()
    {
        var choreographer = new Choreographer();
        var config = choreographer.Config;

        var events = new GameEvent[]
        {
            new BombComboEvent
            {
                Tick = 1,
                SimulationTime = 0f,
                TileIdA = 100,
                TileIdB = 101,
                BombTypeA = ElementType.ColorBomb,
                BombTypeB = ElementType.ColorBomb,
                PositionA = new Position(3, 4),
                PositionB = new Position(4, 4),
                AffectedPositions = new List<Position>
                {
                    new Position(4, 4), // distance 0
                    new Position(5, 4), // distance 1
                    new Position(6, 4), // distance 2
                }
            }
        };

        var commands = choreographer.Choreograph(events);
        var wipeHits = commands.OfType<ShowEffectCommand>()
            .Where(e => e.EffectType == "colorx2_wipe_hit")
            .OrderBy(e => e.StartTime)
            .ToList();

        // First wipe hit starts after converge + fusion
        float wipeStart = config.DoubleColorConvergeDuration + config.DoubleColorFusionDuration;
        Assert.Equal(wipeStart, wipeHits[0].StartTime, 3);

        // Second wipe hit (distance 1) has interval offset
        float expectedSecond = wipeStart + config.DoubleColorWipeInterval;
        Assert.Equal(expectedSecond, wipeHits[1].StartTime, 3);

        // Third wipe hit (distance 2) has accelerated interval
        float expectedThird = expectedSecond + config.DoubleColorWipeInterval * config.DoubleColorWipeAccel;
        Assert.Equal(expectedThird, wipeHits[2].StartTime, 3);
    }

    [Fact]
    public void Choreographer_DoubleColorBomb_PhaseOrder()
    {
        var choreographer = new Choreographer();
        var events = new GameEvent[]
        {
            new BombComboEvent
            {
                Tick = 1,
                SimulationTime = 0f,
                TileIdA = 100,
                TileIdB = 101,
                BombTypeA = ElementType.ColorBomb,
                BombTypeB = ElementType.ColorBomb,
                PositionA = new Position(3, 4),
                PositionB = new Position(4, 4),
                AffectedPositions = GenerateFullBoardPositions(8, 8)
            }
        };

        var commands = choreographer.Choreograph(events);
        var effects = commands.OfType<ShowEffectCommand>().ToList();

        var converge = effects.First(e => e.EffectType == "colorx2_converge");
        var fusion = effects.First(e => e.EffectType == "colorx2_fusion");
        var aftermath = effects.First(e => e.EffectType == "colorx2_aftermath");
        var firstWipe = effects.Where(e => e.EffectType == "colorx2_wipe_hit")
            .OrderBy(e => e.StartTime).First();

        // Phase order: converge < fusion < wipe < aftermath
        Assert.True(converge.StartTime < fusion.StartTime);
        Assert.True(fusion.StartTime < firstWipe.StartTime ||
                    Math.Abs(fusion.StartTime + fusion.Duration - firstWipe.StartTime) < 0.001f);
        Assert.True(firstWipe.StartTime < aftermath.StartTime);
    }

    #endregion

    #region 辅助方法

    private GameState CreateFilledState(int width, int height)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        var types = new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4 };
        int id = 1;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                state.SetTile(x, y, new Tile(id++, types[(x + y) % types.Length], x, y));
        return state;
    }

    private GameState CreateEmptyState(int width, int height)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                state.SetTile(x, y, new Tile(0, ElementType.None, x, y));
        return state;
    }

    private int CountNonEmptyTiles(GameState state)
    {
        int count = 0;
        for (int i = 0; i < state.Grid.Length; i++)
            if (state.Grid[i].Type != ElementType.None) count++;
        return count;
    }

    private PowerUpHandler CreateHandlerWithExplosion()
    {
        return new PowerUpHandler(
            new StubScoreSystem(),
            new BombComboHandler(),
            BombEffectRegistry.CreateDefault(),
            new CoverSystem(),
            new GroundSystem(),
            _explosionSystem);
    }

    private PowerUpHandler CreateHandlerWithExplosionAndSession(ColorBombSessionManager sessionManager)
    {
        return new PowerUpHandler(
            new StubScoreSystem(),
            new BombComboHandler(),
            BombEffectRegistry.CreateDefault(),
            new CoverSystem(),
            new GroundSystem(),
            _explosionSystem,
            colorBombSessionManager: sessionManager);
    }

    private void RunExplosionToCompletion(ref GameState state)
    {
        var triggeredBombs = new List<Position>();
        for (int i = 0; i < 100 && _explosionSystem.HasActiveExplosions; i++)
        {
            _explosionSystem.Update(ref state, 0.05f, 10 + i, 0.05f * i,
                _eventCollector, triggeredBombs);
            triggeredBombs.Clear();
        }
    }

    private static List<Position> GenerateFullBoardPositions(int width, int height)
    {
        var positions = new List<Position>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                positions.Add(new Position(x, y));
        return positions;
    }

    #endregion
}
