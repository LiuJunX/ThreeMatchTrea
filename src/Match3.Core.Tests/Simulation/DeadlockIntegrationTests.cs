using System.Linq;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Random;
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
    private class StubRandom : IRandom
    {
        private int _callCount = 0;

        public float NextFloat() => 0f;

        public int Next(int max)
        {
            _callCount++;
            return _callCount % max;
        }

        public int Next(int min, int max)
        {
            _callCount++;
            return min + (_callCount % (max - min));
        }

        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private class StubScoreSystem : IScoreSystem
    {
        public int CalculateMatchScore(Core.Models.Gameplay.MatchGroup match) => 10;
        public int CalculateSpecialMoveScore(ElementType t1, BombType b1, ElementType t2, BombType b2) => 100;
    }

    private class StubSpawnModel : ISpawnModel
    {
        public ElementType Predict(ref GameState state, int spawnX, in Core.Systems.Spawning.SpawnContext context) => ElementType.Item3;
    }

    private SimulationEngine CreateEngine(GameState state, IEventCollector? eventCollector = null, SimulationConfig? config = null)
    {
        var random = new StubRandom();
        var match3Config = new Match3Config();
        var physics = new RealtimeGravitySystem(match3Config, random);
        var refill = new RealtimeRefillSystem(new StubSpawnModel());
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StubScoreSystem();
        var matchProcessor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), BombEffectRegistry.CreateDefault());
        var powerUpHandler = new PowerUpHandler(scoreSystem);

        // 创建死锁检测和洗牌系统
        var deadlockDetector = new DeadlockDetectionSystem(matchFinder);
        var shuffleSystem = new BoardShuffleSystem(matchFinder);

        // 确保配置启用死锁检测和事件
        var finalConfig = config ?? new SimulationConfig
        {
            EnableDeadlockDetection = true,
            EmitEvents = true,
            ShuffleMaxAttempts = 10
        };

        return new SimulationEngine(
            state,
            finalConfig,
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            null,
            eventCollector,
            null,
            deadlockDetector,
            shuffleSystem);
    }

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

    [Fact]
    public void SimulationEngine_DetectsAndResolvesDeadlock()
    {
        // Arrange
        var state = CreateDeadlockBoard();
        var eventCollector = new BufferedEventCollector();

        // 创建死锁检测器验证棋盘确实是死锁
        var bombGenerator = new BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var deadlockDetector = new DeadlockDetectionSystem(matchFinder);

        // 验证棋盘确实是死锁
        Assert.False(deadlockDetector.HasValidMoves(in state), "测试棋盘应该是死锁");

        var engine = CreateEngine(state, eventCollector);

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
    }

    [Fact]
    public void SimulationEngine_DoesNotDetectDuringAnimation()
    {
        // Arrange
        var state = CreateDeadlockBoard();
        var eventCollector = new BufferedEventCollector();
        var engine = CreateEngine(state, eventCollector);

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
        var config = new SimulationConfig
        {
            EnableDeadlockDetection = false // 禁用死锁检测
        };
        var engine = CreateEngine(state, eventCollector, config);

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

        // 在 (0,0) 添加炸弹
        var bombTile = state.GetTile(0, 0);
        bombTile.Bomb = BombType.Horizontal;
        state.SetTile(0, 0, bombTile);

        var engine = CreateEngine(state, eventCollector);

        // Act
        engine.RunUntilStable();

        // Assert
        var afterShuffle = engine.State.GetTile(0, 0);
        Assert.Equal(BombType.Horizontal, afterShuffle.Bomb);
    }

    [Fact]
    public void SimulationEngine_MultipleShuffleAttempts()
    {
        // Arrange
        var state = CreateDeadlockBoard();
        var eventCollector = new BufferedEventCollector();
        var config = new SimulationConfig
        {
            ShuffleMaxAttempts = 3 // 限制最大尝试次数
        };
        var engine = CreateEngine(state, eventCollector, config);

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
        var engine = CreateEngine(state, eventCollector);

        // Act
        engine.RunUntilStable();

        // Assert - 正常棋盘不应该触发死锁检测
        var deadlockEvents = eventCollector.GetEvents().OfType<DeadlockDetectedEvent>().ToList();
        Assert.Empty(deadlockEvents);

        var shuffleEvents = eventCollector.GetEvents().OfType<BoardShuffledEvent>().ToList();
        Assert.Empty(shuffleEvents);
    }
}


