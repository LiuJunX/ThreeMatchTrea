using System.Linq;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Integration;

/// <summary>
/// 炸弹系统端到端集成测试。
/// 验证跨系统边界的完整生命周期（激活 → 爆炸波 → 连锁 → 重力 → 补充 → 稳定）。
///
/// 断言策略：只验证不变量（消除量、连锁次数、区域清空、是否达到稳定态），
/// 不验证精确棋盘状态，以容忍随机数和执行顺序的合理变化。
///
/// 这些测试是重构炸弹系统内部接线的安全网。
/// </summary>
public class BombLifecycleIntegrationTests
{
    private const float Dt = 1f / 60f;
    private const int MaxTicks = 1200;

    // ──────────────────────────────────────────────
    //  1. Rocket × Rocket 交换 → 十字消除
    // ──────────────────────────────────────────────

    [Fact]
    public void RocketPlusRocket_Swap_ClearsRowAndColumn()
    {
        // 7×7 棋盘，HRocket(3,3) 和 VRocket(4,3) 相邻
        // 交换触发十字：整行 + 整列
        var state = CreateNonMatchingBoard(7, 7);
        state.SetTile(3, 3, new Tile(100, ElementType.HorizontalRocket, 3, 3));
        state.SetTile(4, 3, new Tile(101, ElementType.VerticalRocket, 4, 3));

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        engine.ApplyMove(new Position(3, 3), new Position(4, 3));
        RunUntilStable(engine);

        var events = collector.GetEvents().ToList();

        // 应有组合事件
        Assert.True(events.OfType<BombComboEvent>().Any(),
            "Rocket×Rocket 交换应触发 BombComboEvent");

        // 十字消除：整行(7) + 整列(7) - 交点(1) = 至少 13 个位置
        var destroyed = events.OfType<TileDestroyedEvent>().Count();
        Assert.True(destroyed >= 13,
            $"十字应消除 ≥13 个 tile，实际 {destroyed}");

        Assert.True(engine.IsStable(), "应达到稳定态");
    }

    // ──────────────────────────────────────────────
    //  2. Rocket 爆炸波命中 Square → 连锁 5×5
    // ──────────────────────────────────────────────

    [Fact]
    public void RocketExplosion_HitsSquare_ChainReaction()
    {
        // 7×7 棋盘
        // HRocket(0,3) 激活 → 清除第 3 行 → 波命中 Square(6,3) → 5×5 连锁
        var state = CreateNonMatchingBoard(7, 7);
        state.SetTile(0, 3, new Tile(100, ElementType.HorizontalRocket, 0, 3));
        state.SetTile(6, 3, new Tile(101, ElementType.Square5x5, 6, 3));

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        engine.ActivateBomb(new Position(0, 3));
        RunUntilStable(engine);

        var events = collector.GetEvents().ToList();
        var activations = events.OfType<BombActivatedEvent>().ToList();

        // HRocket 激活 + Square 连锁 = 至少 2 次
        Assert.True(activations.Count >= 2,
            $"应有 ≥2 次炸弹激活，实际 {activations.Count}");

        // Square 应标记为 chain reaction
        Assert.True(activations.Any(e => e.IsChainReaction),
            "Square 应被标记为 chain reaction");

        // 消除量：行(7) + 5×5 中非重叠部分，至少 15
        var destroyed = events.OfType<TileDestroyedEvent>().Count();
        Assert.True(destroyed >= 15,
            $"Rocket 行 + Square 5×5 应消除 ≥15，实际 {destroyed}");

        Assert.True(engine.IsStable(), "应达到稳定态");
    }

    // ──────────────────────────────────────────────
    //  3. ColorBomb + Rocket 交换 → 会话 → 批量激活
    // ──────────────────────────────────────────────

    [Fact]
    public void ColorBombPlusRocket_Swap_SessionTransformsAndActivates()
    {
        // 5×5 棋盘，大量 Item1（目标色），少量 Item2 避免全同
        var state = CreateIsolatedState(5, 5);
        int id = 1;
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(id++, ElementType.Item1, x, y));

        // 放几个 Item2 避免全同色
        state.SetTile(0, 0, new Tile(200, ElementType.Item2, 0, 0));
        state.SetTile(4, 4, new Tile(201, ElementType.Item2, 4, 4));

        // ColorBomb 和 HRocket 相邻
        state.SetTile(2, 2, new Tile(100, ElementType.ColorBomb, 2, 2));
        state.SetTile(3, 2, new Tile(101, ElementType.HorizontalRocket, 3, 2));

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        engine.ApplyMove(new Position(2, 2), new Position(3, 2));
        RunUntilStable(engine);

        var events = collector.GetEvents().ToList();

        // 应触发 ColorBomb session
        Assert.True(events.OfType<ColorBombSessionStartEvent>().Any(),
            "ColorBomb + Rocket 组合应触发 session");

        // Item1 应被大量消除
        var item1Destroyed = events.OfType<TileDestroyedEvent>()
            .Count(e => e.Type == ElementType.Item1);
        Assert.True(item1Destroyed >= 10,
            $"应消除大量 Item1（目标色），实际消除 {item1Destroyed}");

        Assert.True(engine.IsStable(), "应达到稳定态");
    }

    // ──────────────────────────────────────────────
    //  4. ColorBomb × ColorBomb 交换 → 全屏消除
    // ──────────────────────────────────────────────

    [Fact]
    public void DoubleColorBomb_Swap_ClearsEntireBoard()
    {
        // 7×7 棋盘，两颗 ColorBomb 相邻
        var state = CreateNonMatchingBoard(7, 7);
        state.SetTile(3, 3, new Tile(100, ElementType.ColorBomb, 3, 3));
        state.SetTile(4, 3, new Tile(101, ElementType.ColorBomb, 4, 3));

        // 统计初始 tile 数（含 ColorBomb）
        int originalTileCount = 0;
        for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
                if (state.GetTile(x, y).Type != ElementType.None)
                    originalTileCount++;

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        engine.ApplyMove(new Position(3, 3), new Position(4, 3));
        RunUntilStable(engine);

        var events = collector.GetEvents().ToList();

        // 应有组合事件
        Assert.True(events.OfType<BombComboEvent>().Any(),
            "双彩球应触发 BombComboEvent");

        // 全屏消除：所有原始 tile 都应被消除
        var destroyed = events.OfType<TileDestroyedEvent>().Count();
        Assert.True(destroyed >= originalTileCount,
            $"双彩球应消除全部 {originalTileCount} 个 tile，实际 {destroyed}");

        Assert.True(engine.IsStable(), "应达到稳定态");
    }

    // ──────────────────────────────────────────────
    //  5. UFO 飞弹命中炸弹 → 连锁
    // ──────────────────────────────────────────────

    [Fact]
    public void UfoProjectile_HitsBomb_ChainReaction()
    {
        // 3×9 棋盘，大部分 void — 确保 Square 是唯一远程目标
        var state = CreateIsolatedState(3, 9);

        // 全部设为 void
        for (int y = 0; y < 9; y++)
            for (int x = 0; x < 3; x++)
                state.SetCell(x, y, CellKind.Void);

        // 开放 UFO 小十字区域 (center=1,1)
        state.SetCell(1, 0, CellKind.Slot);
        state.SetCell(0, 1, CellKind.Slot);
        state.SetCell(1, 1, CellKind.Slot);
        state.SetCell(2, 1, CellKind.Slot);
        state.SetCell(1, 2, CellKind.Slot);

        // 开放远程目标位置 — Square 是唯一可选目标
        state.SetCell(1, 7, CellKind.Slot);

        // 放置 tile
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.Item2, 0, 1));
        state.SetTile(1, 1, new Tile(3, ElementType.Ufo, 1, 1));
        state.SetTile(2, 1, new Tile(4, ElementType.Item3, 2, 1));
        state.SetTile(1, 2, new Tile(5, ElementType.Item4, 1, 2));
        state.SetTile(1, 7, new Tile(6, ElementType.Square5x5, 1, 7));

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        engine.ActivateBomb(new Position(1, 1));
        RunUntilStable(engine);

        var events = collector.GetEvents().ToList();
        var activations = events.OfType<BombActivatedEvent>().ToList();

        // UFO 激活 + Square 连锁 = 至少 2 次
        Assert.True(activations.Count >= 2,
            $"UFO → Square 连锁应有 ≥2 次激活，实际 {activations.Count}");

        Assert.True(engine.IsStable(), "应达到稳定态");
    }

    // ──────────────────────────────────────────────
    //  6. 三级连锁 HRocket → VRocket → Square
    // ──────────────────────────────────────────────

    [Fact]
    public void ThreeLevelChain_AllBombsActivate()
    {
        // 9×7 棋盘
        // HRocket(0,3) → 行消除 → 命中 VRocket(8,3) → 列消除 → 命中 Square(8,0)
        var state = CreateNonMatchingBoard(9, 7);
        state.SetTile(0, 3, new Tile(100, ElementType.HorizontalRocket, 0, 3));
        state.SetTile(8, 3, new Tile(101, ElementType.VerticalRocket, 8, 3));
        state.SetTile(8, 0, new Tile(102, ElementType.Square5x5, 8, 0));

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        engine.ActivateBomb(new Position(0, 3));
        RunUntilStable(engine);

        var events = collector.GetEvents().ToList();
        var activations = events.OfType<BombActivatedEvent>().ToList();

        // 三级：HRocket → VRocket → Square
        Assert.True(activations.Count >= 3,
            $"三级连锁应有 ≥3 次激活，实际 {activations.Count}");

        // 至少 2 个标记为 chain reaction（VRocket + Square）
        var chainCount = activations.Count(e => e.IsChainReaction);
        Assert.True(chainCount >= 2,
            $"应有 ≥2 次 chain reaction，实际 {chainCount}");

        // 大量消除：行(9) + 列(7) - 交点(1) + 5×5 非重叠部分
        var destroyed = events.OfType<TileDestroyedEvent>().Count();
        Assert.True(destroyed >= 20,
            $"三级连锁应消除 ≥20，实际 {destroyed}");

        Assert.True(engine.IsStable(), "应达到稳定态");
    }

    // ──────────────────────────────────────────────
    //  7. 每种炸弹类型都能单独激活并稳定
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(ElementType.HorizontalRocket)]
    [InlineData(ElementType.VerticalRocket)]
    [InlineData(ElementType.Square5x5)]
    [InlineData(ElementType.ColorBomb)]
    [InlineData(ElementType.Ufo)]
    public void SingleBomb_Activate_ReachesStableState(ElementType bombType)
    {
        var state = CreateNonMatchingBoard(7, 7);
        state.SetTile(3, 3, new Tile(100, bombType, 3, 3));

        var collector = new BufferedEventCollector();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: collector);

        engine.ActivateBomb(new Position(3, 3));
        RunUntilStable(engine);

        // 炸弹自身应被消除
        Assert.NotEqual(bombType, engine.State.GetTile(3, 3).Type);

        // 应有激活事件（ColorBomb 走 session 路径，产生 ColorBombSessionStartEvent）
        var events = collector.GetEvents().ToList();
        bool hasBombEvent = events.OfType<BombActivatedEvent>().Any();
        bool hasSessionEvent = events.OfType<ColorBombSessionStartEvent>().Any();
        Assert.True(hasBombEvent || hasSessionEvent,
            $"{bombType} 激活应产生 BombActivatedEvent 或 ColorBombSessionStartEvent");

        Assert.True(engine.IsStable(), $"{bombType} 激活后应达到稳定态");
    }

    #region Helpers

    private static GameState CreateIsolatedState(int width, int height)
    {
        return new GameState(width, height, 6, new StubRandom());
    }

    /// <summary>
    /// 创建不匹配棋盘：5 色交替，保证无意外 3 连。
    /// </summary>
    private static GameState CreateNonMatchingBoard(int width, int height)
    {
        var state = CreateIsolatedState(width, height);
        var types = new[]
        {
            ElementType.Item1, ElementType.Item3, ElementType.Item2,
            ElementType.Item4, ElementType.Item5
        };
        int id = 1;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                state.SetTile(x, y, new Tile(id++, types[(x + y * 2) % types.Length], x, y));
        return state;
    }

    private static void RunUntilStable(SimulationEngine engine)
    {
        for (int i = 0; i < MaxTicks; i++)
        {
            engine.Tick(Dt);
            if (i > 60 && engine.IsStable()) break;
        }
    }

    #endregion
}
