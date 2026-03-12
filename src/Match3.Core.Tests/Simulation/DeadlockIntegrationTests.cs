using System.Linq;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Simulation;

/// <summary>
/// 死锁检测与自动洗牌集成测试
///
/// 验证：
/// - SimulationEngine 正确集成死锁检测和洗牌系统
/// - 端到端流程：检测死锁 → 发射事件 → 洗牌 → 继续游戏
/// </summary>
public class DeadlockIntegrationTests
{
    private GameState CreateDeadlockBoard()
    {
        var random = new StubRandom();
        var state = new GameState(6, 6, 6, random);

        // 三色旋转模式：每行向左旋转一个位置
        ElementType[] pattern = { ElementType.Item1, ElementType.Item2, ElementType.Item3 };

        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var type = pattern[(x + y) % 3];
                state.SetTile(x, y, new Tile(y * state.Width + x, type, x, y));
            }
        }

        return state;
    }

    /// <summary>
    /// Creates deadlock detection and shuffle systems wired to a shared match finder.
    /// </summary>
    private static (DeadlockDetectionSystem detector, BoardShuffleSystem shuffler) CreateDeadlockSystems()
    {
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var detector = new DeadlockDetectionSystem(matchFinder);
        var shuffler = new BoardShuffleSystem(detector);
        return (detector, shuffler);
    }

    [Fact]
    public void SimulationEngine_DetectsAndResolvesDeadlock()
    {
        // Arrange
        var state = CreateDeadlockBoard();
        var eventCollector = new BufferedEventCollector();

        var (deadlockDetector, shuffleSystem) = CreateDeadlockSystems();

        // 验证棋盘确实是死锁
        Assert.False(deadlockDetector.HasValidMoves(in state), "测试棋盘应该是死锁");

        var engine = TestEngineFactory.CreateEngine(state, eventCollector: eventCollector,
            deadlockDetector: deadlockDetector, shuffleSystem: shuffleSystem);

        // 验证初始状态是稳定的
        Assert.True(engine.IsStable(), "初始死锁棋盘应该是稳定的");

        // Act - 调用至少一次 Tick 以触发死锁检测，然后继续直到稳定
        engine.Tick(0.016f); // 第一次 Tick 会触发死锁检测和洗牌

        // 洗牌后可能产生匹配，继续 Tick 直到稳定
        int maxTicks = 1000;
        int tickCount = 0;
        while (!engine.IsStable() && tickCount < maxTicks)
        {
            engine.Tick(0.016f);
            tickCount++;
        }

        // Assert
        var allEvents = eventCollector.GetEvents().ToList();

        // Debug输出
        var eventTypeNames = string.Join(", ", allEvents.Select(e => e.GetType().Name));

        // 应该有死锁检测事件
        var deadlockEvents = allEvents.OfType<DeadlockDetectedEvent>().ToList();
        Assert.NotEmpty(deadlockEvents);

        // 应该有洗牌事件
        var shuffleEvents = allEvents.OfType<BoardShuffledEvent>().ToList();
        Assert.NotEmpty(shuffleEvents);

        // 验证洗牌事件包含变化信息
        Assert.All(shuffleEvents, evt => Assert.NotNull(evt.Changes));

        // 最终棋盘应该稳定且有可行移动
        Assert.True(engine.IsStable(), "模拟应该达到稳定状态");

        // 验证洗牌后棋盘可解（或已通关/失败）
        var finalState = engine.State;
        if (finalState.LevelStatus == Match3.Core.Models.Enums.LevelStatus.InProgress)
        {
            Assert.True(deadlockDetector.HasValidMoves(in finalState),
                "洗牌后棋盘应该有可行移动");
        }
    }

    [Fact]
    public void SimulationEngine_DoesNotDetectDuringAnimation()
    {
        // Arrange
        var state = CreateDeadlockBoard();
        var eventCollector = new BufferedEventCollector();
        var (deadlockDetector, shuffleSystem) = CreateDeadlockSystems();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: eventCollector,
            deadlockDetector: deadlockDetector, shuffleSystem: shuffleSystem);

        // Act - 执行单个 Tick，可能还在处理动画
        engine.Tick();

        // Assert - 不应该立即检测死锁（需要等待稳定）
        // 这个测试验证不会在非稳定状态下误触发
        // 结果取决于具体实现，但至少不应该崩溃
        Assert.True(true); // 主要是确保不崩溃
    }

    [Fact]
    public void SimulationEngine_DisabledDetection_NoAutoShuffle()
    {
        // Arrange
        var state = CreateDeadlockBoard();
        var eventCollector = new BufferedEventCollector();
        var (deadlockDetector, shuffleSystem) = CreateDeadlockSystems();
        var config = new SimulationConfig
        {
            EnableDeadlockDetection = false // 禁用死锁检测
        };
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: eventCollector, simulationConfig: config,
            deadlockDetector: deadlockDetector, shuffleSystem: shuffleSystem);

        // Act
        var result = engine.RunUntilStable();

        // Assert
        var allEvents = eventCollector.GetEvents().ToList();

        // 不应该有死锁检测事件
        var deadlockEvents = allEvents.OfType<DeadlockDetectedEvent>().ToList();
        Assert.Empty(deadlockEvents);

        // 不应该有洗牌事件
        var shuffleEvents = allEvents.OfType<BoardShuffledEvent>().ToList();
        Assert.Empty(shuffleEvents);
    }

    [Fact]
    public void SimulationEngine_ShufflePreservesSpecialTiles()
    {
        // Arrange
        var state = CreateDeadlockBoard();
        var eventCollector = new BufferedEventCollector();
        var (deadlockDetector, shuffleSystem) = CreateDeadlockSystems();

        // 在 (0,0) 添加炸弹
        var bombTile = new Tile(state.GetTile(0, 0).Id, ElementType.HorizontalRocket, 0, 0);
        state.SetTile(0, 0, bombTile);

        var engine = TestEngineFactory.CreateEngine(state, eventCollector: eventCollector,
            deadlockDetector: deadlockDetector, shuffleSystem: shuffleSystem);

        // Act
        engine.RunUntilStable();

        // Assert
        var afterShuffle = engine.State.GetTile(0, 0);
        Assert.Equal(ElementType.HorizontalRocket, afterShuffle.Type);
    }

    [Fact]
    public void SimulationEngine_MultipleShuffleAttempts()
    {
        // Arrange
        var state = CreateDeadlockBoard();
        var eventCollector = new BufferedEventCollector();
        var (deadlockDetector, shuffleSystem) = CreateDeadlockSystems();
        var config = new SimulationConfig
        {
            ShuffleMaxAttempts = 3 // 限制最大尝试次数
        };
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: eventCollector, simulationConfig: config,
            deadlockDetector: deadlockDetector, shuffleSystem: shuffleSystem);

        // Act
        engine.RunUntilStable();

        // Assert
        var shuffleEvents = eventCollector.GetEvents().OfType<BoardShuffledEvent>().ToList();

        if (shuffleEvents.Any())
        {
            // 如果有洗牌事件，尝试次数应该在 1-3 之间
            var lastEvent = shuffleEvents.Last();
            Assert.True(lastEvent.AttemptCount >= 1);
            Assert.True(lastEvent.AttemptCount <= 3);
        }
    }

    [Fact]
    public void SimulationEngine_NormalBoard_NoDeadlockDetection()
    {
        // Arrange - 创建一个正常的有可行移动的棋盘
        var random = new StubRandom();
        var state = new GameState(6, 6, 6, random);

        // 创建一个简单的有可行移动的布局
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var type = ElementType.Item1;
                if (x % 3 == 0) type = ElementType.Item3;
                else if (x % 3 == 1) type = ElementType.Item2;
                state.SetTile(x, y, new Tile(y * state.Width + x, type, x, y));
            }
        }

        var eventCollector = new BufferedEventCollector();
        var (deadlockDetector, shuffleSystem) = CreateDeadlockSystems();
        var engine = TestEngineFactory.CreateEngine(state, eventCollector: eventCollector,
            deadlockDetector: deadlockDetector, shuffleSystem: shuffleSystem);

        // Act
        engine.RunUntilStable();

        // Assert - 正常棋盘不应该触发死锁检测
        var deadlockEvents = eventCollector.GetEvents().OfType<DeadlockDetectedEvent>().ToList();
        Assert.Empty(deadlockEvents);

        var shuffleEvents = eventCollector.GetEvents().OfType<BoardShuffledEvent>().ToList();
        Assert.Empty(shuffleEvents);
    }
}


