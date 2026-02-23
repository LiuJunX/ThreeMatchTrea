using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestHelpers;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Integration;

/// <summary>
/// 集成测试：验证 CellLock 与重力、补充、匹配、交换系统的交互。
/// </summary>
public class CellLockIntegrationTests
{
    private readonly ITestOutputHelper _output;

    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private class FixedSpawnModel : ISpawnModel
    {
        public TileType TypeToSpawn { get; set; } = TileType.Blue;
        public TileType Predict(ref GameState state, int spawnX, in SpawnContext context) => TypeToSpawn;
    }

    public CellLockIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region ReceiveLock + Gravity

    /// <summary>
    /// ReceiveLock 阻止重力掉入：空格被锁后，上方 tile 不掉落到该位置。
    /// </summary>
    [Fact]
    public void ReceiveLock_BlocksGravityDropInto()
    {
        var rng = new StubRandom();
        var state = new GameState(1, 3, 6, rng);

        // Column layout:
        // 0: Red (should fall)
        // 1: empty + ReceiveLock
        // 2: empty
        var tile = new Tile(1, TileType.Red, 0, 0);
        tile.Position = new Vector2(0, 0);
        state.SetTile(0, 0, tile);
        // rows 1 and 2 are empty (TileType.None by default)

        // Lock row 1 to refuse incoming
        state.Lock(0, 1, CellLockType.Receive);

        var config = new Match3Config { GravitySpeed = 20f, MaxFallSpeed = 25f };
        var gravity = new RealtimeGravitySystem(config, rng);
        var animation = new AnimationSystem(config);
        var helper = new AnimationTestHelper(_output);

        helper.UpdateUntilStable(ref state, gravity, animation, maxFrames: 120);

        // Row 1 should remain empty (locked)
        Assert.Equal(TileType.None, state.GetTile(0, 1).Type);
        // Red cannot reach row 2 because row 1 blocks the path
        // It stays at row 0
        Assert.Equal(TileType.Red, state.GetTile(0, 0).Type);
    }

    #endregion

    #region ReceiveLock + Refill

    /// <summary>
    /// ReceiveLock 阻止 Refill：锁住 row 0 的格子后，不生成新 tile。
    /// </summary>
    [Fact]
    public void ReceiveLock_BlocksRefillSpawn()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 1, 6, rng);
        // All cells empty

        // Lock column 1 to refuse refill
        state.Lock(1, 0, CellLockType.Receive);

        var spawnModel = new FixedSpawnModel { TypeToSpawn = TileType.Green };
        var refill = new RealtimeRefillSystem(spawnModel);

        refill.Update(ref state);

        // Column 0 and 2 should get tiles, column 1 should remain empty
        Assert.NotEqual(TileType.None, state.GetTile(0, 0).Type);
        Assert.Equal(TileType.None, state.GetTile(1, 0).Type);
        Assert.NotEqual(TileType.None, state.GetTile(2, 0).Type);
    }

    #endregion

    #region DropLock + Gravity

    /// <summary>
    /// DropLock 阻止棋子掉落（同 Cover 阻止移动行为）。
    /// </summary>
    [Fact]
    public void DropLock_PreventsTileFromFalling()
    {
        var rng = new StubRandom();
        var state = new GameState(1, 3, 6, rng);

        // Row 0: empty, Row 1: Red with DropLock, Row 2: empty
        var tile = new Tile(1, TileType.Red, 0, 1);
        tile.Position = new Vector2(0, 1);
        state.SetTile(0, 1, tile);

        state.Lock(0, 1, CellLockType.Drop);

        var config = new Match3Config { GravitySpeed = 20f, MaxFallSpeed = 25f };
        var gravity = new RealtimeGravitySystem(config, rng);
        var animation = new AnimationSystem(config);
        var helper = new AnimationTestHelper(_output);

        helper.UpdateUntilStable(ref state, gravity, animation, maxFrames: 120);

        // Red should stay at row 1, not fall to row 2
        Assert.Equal(TileType.Red, state.GetTile(0, 1).Type);
        Assert.False(state.GetTile(0, 1).IsFalling);
    }

    #endregion

    #region MatchingLock

    /// <summary>
    /// MatchingLock 阻断匹配检测。
    /// </summary>
    [Fact]
    public void MatchingLock_BlocksMatchDetection()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 1, 6, rng);

        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Red, 2, 0));

        // Lock the middle tile for matching
        state.Lock(1, 0, CellLockType.Matching);

        var matchFinder = new ClassicMatchFinder(new BombGenerator());
        var matches = matchFinder.FindMatchGroups(in state);

        Assert.Empty(matches);
    }

    #endregion

    #region SwapLock

    /// <summary>
    /// SwapLock 阻止交换（CanInteract 返回 false）。
    /// </summary>
    [Fact]
    public void SwapLock_BlocksInteraction()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);
        state.SetTile(1, 1, new Tile(1, TileType.Red, 1, 1));

        Assert.True(state.CanInteract(1, 1));

        state.Lock(1, 1, CellLockType.Swap);

        Assert.False(state.CanInteract(1, 1));
    }

    #endregion

    #region Frozen Preset

    /// <summary>
    /// LockPreset.Frozen 完全冻结格子。
    /// </summary>
    [Fact]
    public void FrozenPreset_BlocksAllMovement()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);
        state.SetTile(1, 1, new Tile(1, TileType.Red, 1, 1));

        state.Lock(1, 1, LockPreset.Frozen);

        Assert.False(state.CanInteract(1, 1)); // Swap blocked
        Assert.False(state.CanMatch(1, 1));     // Matching blocked
        Assert.False(state.CanMove(1, 1));      // Drop blocked
        Assert.False(state.CanReceive(1, 1));   // Receive blocked
    }

    #endregion

    #region Multi-source Lock + Partial Unlock

    /// <summary>
    /// 多来源加锁 → 部分解锁 → 仍锁定；全部解锁 → 恢复正常。
    /// </summary>
    [Fact]
    public void MultiSourceLock_PartialUnlock_StillLocked()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);
        state.SetTile(1, 1, new Tile(1, TileType.Red, 1, 1));

        // Two sources lock the same cell
        var tokenA = state.AcquireLock(1, 1, CellLockType.Swap);
        var tokenB = state.AcquireLock(1, 1, CellLockType.Swap);

        Assert.False(state.CanInteract(1, 1));

        // Release one — still locked
        state.ReleaseLock(tokenA);
        Assert.False(state.CanInteract(1, 1));

        // Release the other — now unlocked
        state.ReleaseLock(tokenB);
        Assert.True(state.CanInteract(1, 1));
    }

    #endregion

    #region Lock + Cover Coexistence

    /// <summary>
    /// Lock 和 Cover 共存：两者独立工作，都能阻止操作。
    /// </summary>
    [Fact]
    public void Lock_And_Cover_Coexist()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);
        state.SetTile(1, 1, new Tile(1, TileType.Red, 1, 1));

        // Cover alone blocks
        state.SetCover(new Position(1, 1), new Cover(CoverType.Cage, health: 1));
        Assert.False(state.CanInteract(1, 1));
        Assert.False(state.CanMatch(1, 1));

        // Remove cover, add lock
        state.SetCover(new Position(1, 1), new Cover(CoverType.None, health: 0));
        state.Lock(1, 1, CellLockType.Swap);

        // Still blocked by lock
        Assert.False(state.CanInteract(1, 1));
        // But match is not blocked (only Swap is locked, not Matching)
        Assert.True(state.CanMatch(1, 1));
    }

    #endregion

    #region Clone Preserves Locks

    /// <summary>
    /// Clone 后锁状态正确复制。
    /// </summary>
    [Fact]
    public void Clone_PreservesCellLocks()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);
        state.Lock(0, 0, CellLockType.Drop | CellLockType.Receive);
        state.Lock(2, 2, CellLockType.Matching);

        var clone = state.Clone();

        Assert.True(clone.IsLocked(0, 0, CellLockType.Drop));
        Assert.True(clone.IsLocked(0, 0, CellLockType.Receive));
        Assert.True(clone.IsLocked(2, 2, CellLockType.Matching));
        Assert.False(clone.IsLocked(1, 1, CellLockType.Drop));
    }

    /// <summary>
    /// Clone 是深拷贝：修改原始不影响克隆。
    /// </summary>
    [Fact]
    public void Clone_IsDeepCopy_MutationsIndependent()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);
        state.Lock(1, 1, CellLockType.Swap);

        var clone = state.Clone();
        state.Unlock(1, 1, CellLockType.Swap);

        // Original unlocked, clone still locked
        Assert.False(state.IsLocked(1, 1, CellLockType.Swap));
        Assert.True(clone.IsLocked(1, 1, CellLockType.Swap));
    }

    #endregion

    #region ReceiveLock — Merge Column Gravity Blocking

    /// <summary>
    /// Receive lock on merge-affected cells prevents gravity from filling them,
    /// simulating how Bridge locks cells during merge-to-bomb animation.
    /// </summary>
    [Fact]
    public void ReceiveLock_BlocksMergeColumnGravity()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 5, 6, rng);

        // Column 1: simulate merge — rows 3,4 cleared, rows 0-2 have tiles above
        state.SetTile(1, 0, new Tile(10, TileType.Green, 1, 0));
        state.SetTile(1, 1, new Tile(11, TileType.Blue, 1, 1));
        // rows 2,3,4 empty (merge cleared them)

        // Lock rows 2,3,4 with Receive (Bridge would do this during merge)
        var token2 = state.AcquireLock(1, 2, CellLockType.Receive);
        var token3 = state.AcquireLock(1, 3, CellLockType.Receive);
        var token4 = state.AcquireLock(1, 4, CellLockType.Receive);

        var config = new Match3Config { GravitySpeed = 20f, MaxFallSpeed = 25f };
        var gravity = new RealtimeGravitySystem(config, rng);
        var animation = new AnimationSystem(config);
        var helper = new AnimationTestHelper(_output);

        helper.UpdateUntilStable(ref state, gravity, animation, maxFrames: 120);

        // Tiles should NOT have fallen into locked rows
        Assert.Equal(TileType.None, state.GetTile(1, 2).Type);
        Assert.Equal(TileType.None, state.GetTile(1, 3).Type);
        Assert.Equal(TileType.None, state.GetTile(1, 4).Type);
        // Original tiles stay at top (can't fall past locked cells)
        Assert.Equal(TileType.Green, state.GetTile(1, 0).Type);
        Assert.Equal(TileType.Blue, state.GetTile(1, 1).Type);
    }

    /// <summary>
    /// After releasing Receive locks, gravity resumes and tiles fill the cells.
    /// </summary>
    [Fact]
    public void ReceiveLock_Released_GravityResumes()
    {
        var rng = new StubRandom();
        var state = new GameState(1, 3, 6, rng);

        // Single column: tile at row 0, rows 1-2 empty and locked
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        var token1 = state.AcquireLock(0, 1, CellLockType.Receive);
        var token2 = state.AcquireLock(0, 2, CellLockType.Receive);

        var config = new Match3Config { GravitySpeed = 20f, MaxFallSpeed = 25f };
        var gravity = new RealtimeGravitySystem(config, rng);
        var animation = new AnimationSystem(config);
        var helper = new AnimationTestHelper(_output);

        // Run gravity while locked — tile stays at top
        helper.UpdateUntilStable(ref state, gravity, animation, maxFrames: 120);
        Assert.Equal(TileType.Red, state.GetTile(0, 0).Type);

        // Release locks
        state.ReleaseLock(token1);
        state.ReleaseLock(token2);

        // Run gravity again — tile should fall to bottom
        helper.UpdateUntilStable(ref state, gravity, animation, maxFrames: 120);
        Assert.Equal(TileType.Red, state.GetTile(0, 2).Type);
        Assert.Equal(TileType.None, state.GetTile(0, 0).Type);
    }

    /// <summary>
    /// Receive lock on one column does not affect other columns — gravity still works.
    /// </summary>
    [Fact]
    public void ReceiveLock_OtherColumnsUnaffected()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);

        // Tiles at row 0 in all 3 columns
        state.SetTile(0, 0, new Tile(1, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(2, TileType.Green, 1, 0));
        state.SetTile(2, 0, new Tile(3, TileType.Blue, 2, 0));

        // Lock only column 1 (merge column)
        state.AcquireLock(1, 1, CellLockType.Receive);
        state.AcquireLock(1, 2, CellLockType.Receive);

        var config = new Match3Config { GravitySpeed = 20f, MaxFallSpeed = 25f };
        var gravity = new RealtimeGravitySystem(config, rng);
        var animation = new AnimationSystem(config);
        var helper = new AnimationTestHelper(_output);

        helper.UpdateUntilStable(ref state, gravity, animation, maxFrames: 120);

        // Column 0 and 2: tiles should have fallen to bottom
        Assert.Equal(TileType.Red, state.GetTile(0, 2).Type);
        Assert.Equal(TileType.Blue, state.GetTile(2, 2).Type);

        // Column 1: tile blocked — can't fall past locked cells
        Assert.Equal(TileType.Green, state.GetTile(1, 0).Type);
    }

    #endregion

    #region CanDestroy

    [Fact]
    public void IndestructibleLock_BlocksDestroy()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 3, 6, rng);

        Assert.True(state.CanDestroy(1, 1));

        state.Lock(1, 1, CellLockType.Indestructible);

        Assert.False(state.CanDestroy(1, 1));
        Assert.False(state.CanDestroy(new Position(1, 1)));
    }

    #endregion
}
