using System.Linq;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Integration;

/// <summary>
/// 验证炸弹激活后，被消除位置在若干 tick 内持有 ReceiveLock，
/// 阻止重力/补充过早填充。
/// </summary>
public class BombReceiveLockIntegrationTests
{
    /// <summary>
    /// 构建一个隔离的单列棋盘：上方放一个待掉落的 tile，下方放炸弹。
    /// 炸弹激活后，炸弹位置应持有 ReceiveLock，上方 tile 不会立即掉入。
    /// </summary>
    [Fact]
    public void BombActivation_OriginHasReceiveLock_BlocksGravity()
    {
        // 1×4 棋盘，避免匹配
        //  y0: Item1 (待掉落)
        //  y1: empty
        //  y2: Square5x5 (炸弹)
        //  y3: Item2
        var state = CreateIsolatedState(1, 4);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        // y1 empty
        state.SetTile(0, 2, new Tile(3, ElementType.Square5x5, 0, 2));
        state.SetTile(0, 3, new Tile(4, ElementType.Item2, 0, 3));

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        // 激活炸弹
        engine.ActivateBomb(new Position(0, 2));

        // 跑 1 tick — 物理已运行，但 ReceiveLock 应阻止掉入
        engine.Tick();

        var s = engine.State;

        // 炸弹位置 (0,2) 应被 ReceiveLock 锁住
        Assert.True(s.IsLocked(0, 2, CellLockType.Receive),
            "炸弹激活后，原位置应持有 ReceiveLock");

        // 上方 tile (Item1) 不应掉到 y2
        Assert.Equal(ElementType.None, s.GetTile(0, 2).Type);
    }

    /// <summary>
    /// 彩球点击激活：origin 持有不定期 ReceiveLock，
    /// 在 session 结束前不释放。
    /// </summary>
    [Fact]
    public void ColorBombTap_OriginHasReceiveLock()
    {
        // 3×3 棋盘
        //  (1,0): ColorBomb
        //  其余: Item1 (彩球的目标色)
        var state = CreateIsolatedState(3, 3);
        FillBoard(state, ElementType.Item1);
        state.SetTile(1, 0, new Tile(100, ElementType.ColorBomb, 1, 0));

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        // 点击激活彩球
        engine.ActivateBomb(new Position(1, 0));

        // 跑 1 tick
        engine.Tick();

        var s = engine.State;

        // 彩球原位置应持有 ReceiveLock（session 不定期锁）
        Assert.True(s.IsLocked(1, 0, CellLockType.Receive),
            "彩球激活后，原位置应持有 ReceiveLock（session 不定期锁）");
    }

    /// <summary>
    /// 彩球与普通元素交换（走 ColorBombSessionManager 路径）：
    /// 彩球位置持有不定期 ReceiveLock（session 级别），非目标色不受影响。
    /// </summary>
    [Fact]
    public void ColorBombSwapNormal_OriginHasSessionReceiveLock()
    {
        // 3×3 棋盘
        //  (0,0): ColorBomb
        //  (1,0): Item1 — 交换目标
        //  其余: Item1（都会被消除）+ (2,2) Item2 避免全空
        var state = CreateIsolatedState(3, 3);
        FillBoard(state, ElementType.Item1);
        state.SetTile(0, 0, new Tile(100, ElementType.ColorBomb, 0, 0));
        state.SetTile(2, 2, new Tile(99, ElementType.Item2, 2, 2)); // 非目标色保留

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        // 交换彩球与普通元素
        engine.ApplyMove(new Position(0, 0), new Position(1, 0));

        // 跑几 tick 让交换动画完成，触发 session
        for (int i = 0; i < 20; i++)
            engine.Tick();

        var s = engine.State;

        // 彩球交换后位置 (1,0) 应持有 ReceiveLock（session 不定期锁，同 tap 路径）
        Assert.True(s.IsLocked(1, 0, CellLockType.Receive),
            "彩球交换后，彩球位置应持有 session 级别的 ReceiveLock");

        // 非目标色 (2,2) 的 Item2 应该存活
        Assert.Equal(ElementType.Item2, s.GetTile(2, 2).Type);
    }

    /// <summary>
    /// 彩球与普通元素交换：目标颜色是被交换 tile 的颜色（非最多颜色）。
    /// </summary>
    [Fact]
    public void ColorBombSwapNormal_UsesSwappedTileColor()
    {
        // 5×3 棋盘
        //  (0,0): ColorBomb
        //  (1,0): Item2 — 交换目标（少数颜色）
        //  其余大量 Item1（多数颜色）+ 少量 Item2
        var state = CreateIsolatedState(5, 3);
        int id = 1;
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, ElementType.Item1, x, y));

        state.SetTile(0, 0, new Tile(100, ElementType.ColorBomb, 0, 0));
        state.SetTile(1, 0, new Tile(101, ElementType.Item2, 1, 0)); // 少数颜色
        state.SetTile(2, 0, new Tile(102, ElementType.Item2, 2, 0));
        state.SetTile(3, 0, new Tile(103, ElementType.Item2, 3, 0));

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        engine.ApplyMove(new Position(0, 0), new Position(1, 0));

        // 跑足够 tick 让 session 完成
        for (int i = 0; i < 300; i++)
        {
            engine.Tick(1f / 60f);
            if (i > 60 && engine.IsStable()) break;
        }

        var allEvents = collector.GetEvents().ToList();

        // session 应选择 Item2（被交换 tile 的颜色），而不是 Item1（最多颜色）
        var sessionStart = allEvents.OfType<ColorBombSessionStartEvent>().FirstOrDefault();
        Assert.NotNull(sessionStart);
        Assert.Equal(ElementType.Item2, sessionStart.TargetColor);

        // Item2 应被消除
        var destroyed = allEvents.OfType<TileDestroyedEvent>().Where(e => e.Type == ElementType.Item2).ToList();
        Assert.True(destroyed.Count >= 2, $"应消除 Item2，实际消除 {destroyed.Count} 个");
    }

    /// <summary>
    /// 爆炸波（ExplosionSystem）消除的位置也应持有 ReceiveLock。
    /// </summary>
    [Fact]
    public void ExplosionWave_ClearedPositionsHaveReceiveLock()
    {
        // 5×3 棋盘，炸弹在中间
        //  y0: Item1 * 5 (上方 tile，检验是否被 ReceiveLock 挡住)
        //  y1: Item2, Item3, HRocket, Item3, Item2
        //  y2: Item4 * 5
        var state = CreateIsolatedState(5, 3);
        for (int x = 0; x < 5; x++)
        {
            state.SetTile(x, 0, new Tile(x + 1, ElementType.Item1, x, 0));
            state.SetTile(x, 2, new Tile(x + 11, ElementType.Item4, x, 2));
        }
        state.SetTile(0, 1, new Tile(6, ElementType.Item2, 0, 1));
        state.SetTile(1, 1, new Tile(7, ElementType.Item3, 1, 1));
        state.SetTile(2, 1, new Tile(8, ElementType.HorizontalRocket, 2, 1));
        state.SetTile(3, 1, new Tile(9, ElementType.Item3, 3, 1));
        state.SetTile(4, 1, new Tile(10, ElementType.Item2, 4, 1));

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        // 激活横向火箭
        engine.ActivateBomb(new Position(2, 1));

        // 跑 1 tick — 爆炸波开始传播
        engine.Tick();

        var s = engine.State;

        // 火箭原位置 (2,1) 应持有 ReceiveLock
        Assert.True(s.IsLocked(2, 1, CellLockType.Receive),
            "火箭激活后，原位置应持有 ReceiveLock");
    }

    /// <summary>
    /// 通过 GameServiceFactory 创建的引擎，炸弹激活也应有 ReceiveLock。
    /// 验证生产环境接线正确。
    /// </summary>
    [Fact]
    public void GameServiceFactory_BombActivation_HasReceiveLock()
    {
        var factory = new Match3.Core.DependencyInjection.GameServiceBuilder()
            .UseDefaultServices()
            .Build();

        var rng = new StubRandom();
        var state = new GameState(5, 5, 5, rng);
        // 填充非匹配模式
        var types = new[] { ElementType.Item1, ElementType.Item3, ElementType.Item2, ElementType.Item4, ElementType.Item5 };
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1, types[(x + y) % 5], x, y));

        // 放一个炸弹
        state.SetTile(2, 2, new Tile(50, ElementType.Square5x5, 2, 2));

        var collector = new BufferedEventCollector();
        var engine = factory.CreateSimulationEngine(state, SimulationConfig.ForHumanPlay(), collector);

        engine.ActivateBomb(new Position(2, 2));
        engine.Tick();

        var s = engine.State;
        Assert.True(s.IsLocked(2, 2, CellLockType.Receive),
            "GameServiceFactory 创建的引擎，炸弹激活后原位置应持有 ReceiveLock");
    }

    /// <summary>
    /// 高列场景：炸弹在底部附近，上方全是 tile。
    /// 验证爆炸波向上扩散时，ReceiveLock 阻止重力把待消除 tile 拉走，
    /// 确保所有 AffectedArea 内的 tile 都被消除（没有漏掉）。
    /// 这是 WaveInterval &lt; ReceiveLockDuration 不变式的端到端守护测试。
    /// </summary>
    [Fact]
    public void TallColumn_ExplosionWave_AllTargetsEliminated()
    {
        // 3×8 棋盘，中间列全填 tile，两侧留 void 隔离重力影响
        // 炸弹放在 (1,6)（靠近底部），爆炸范围覆盖整列
        var state = CreateIsolatedState(3, 8);

        // 左右两列设为 void（隔离，只保留中间列参与重力）
        for (int y = 0; y < 8; y++)
        {
            state.SetCell(0, y, CellKind.Void);
            state.SetCell(2, y, CellKind.Void);
        }

        // 中间列填充 tile，交替颜色避免意外 match
        int id = 1;
        for (int y = 0; y < 8; y++)
        {
            var type = y % 2 == 0 ? ElementType.Item1 : ElementType.Item2;
            state.SetTile(1, y, new Tile(id++, type, 1, y));
        }

        // 放置 AreaBomb（5x5），覆盖中间列 y=4..7
        state.SetTile(1, 6, new Tile(100, ElementType.Square5x5, 1, 6));

        // 记录爆炸范围内（y=4..7）所有 tile 的 id（含炸弹自身）
        var targetTileIds = new System.Collections.Generic.HashSet<int>();
        for (int y = 4; y <= 7; y++)
        {
            var tile = state.GetTile(1, y);
            if (tile.Type != ElementType.None)
                targetTileIds.Add(tile.Id);
        }

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        // 激活炸弹
        engine.ActivateBomb(new Position(1, 6));

        // 运行足够多的 tick 直到稳定
        for (int i = 0; i < 300; i++)
        {
            engine.Tick(1f / 60f);
            if (i > 30 && engine.IsStable()) break;
        }

        // 统计被消除的目标 tile
        var destroyedIds = new System.Collections.Generic.HashSet<int>();
        foreach (var evt in collector.GetEvents())
        {
            if (evt is TileDestroyedEvent tde && targetTileIds.Contains(tde.TileId))
                destroyedIds.Add(tde.TileId);
        }

        // 所有目标 tile 都应被消除（炸弹自身 + 范围内的 y=4,5,7）
        var missing = new System.Collections.Generic.HashSet<int>(targetTileIds);
        missing.ExceptWith(destroyedIds);
        Assert.True(missing.Count == 0,
            $"有 {missing.Count} 个目标 tile 未被消除（ids: {string.Join(",", missing)}）。" +
            "可能是重力在波到达前拉走了 tile（WaveInterval >= ReceiveLockDuration）。");
    }

    /// <summary>
    /// 验证 ExplosionConfig 中所有 WaveInterval 都小于对应的 ReceiveLockDuration。
    /// 纯配置校验，不需要跑模拟，确保改配置时不会破坏时序不变式。
    /// </summary>
    [Fact]
    public void ExplosionConfig_AllWaveIntervals_LessThanReceiveLockDuration()
    {
        var config = new Match3.Core.Systems.PowerUps.ExplosionConfig();

        // 默认爆炸
        Assert.True(config.DefaultWaveInterval < ReceiveLockTimings.ExplosionClear,
            $"DefaultWaveInterval({config.DefaultWaveInterval}) must be < ExplosionClear({ReceiveLockTimings.ExplosionClear})");

        // 火箭
        Assert.True(config.RocketWaveInterval < ReceiveLockTimings.ExplosionClear,
            $"RocketWaveInterval({config.RocketWaveInterval}) must be < ExplosionClear({ReceiveLockTimings.ExplosionClear})");

        // 方块炸弹
        Assert.True(config.AreaBombWaveInterval < ReceiveLockTimings.ExplosionClear,
            $"AreaBombWaveInterval({config.AreaBombWaveInterval}) must be < ExplosionClear({ReceiveLockTimings.ExplosionClear})");

        // 双彩球
        Assert.True(DoubleColorBombConstants.WipeInterval < ReceiveLockTimings.ColorBombBatchClear,
            $"DoubleColorBomb WipeInterval({DoubleColorBombConstants.WipeInterval}) must be < ColorBombBatchClear({ReceiveLockTimings.ColorBombBatchClear})");
    }

    #region Helpers

    private static GameState CreateIsolatedState(int width, int height)
    {
        var rng = new StubRandom();
        return new GameState(width, height, 6, rng);
    }

    private static void FillBoard(GameState state, ElementType type)
    {
        int id = 1;
        for (int y = 0; y < state.Height; y++)
            for (int x = 0; x < state.Width; x++)
                state.SetTile(x, y, new Tile(id++, type, x, y));
    }

    #endregion
}
