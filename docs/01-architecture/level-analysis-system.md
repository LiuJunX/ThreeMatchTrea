# Level Analysis System Architecture

## Overview

关卡分析系统提供两种互补的分析方法来评估关卡难度：

1. **MCTS 理论分析** - 通过蒙特卡洛树搜索找到理论最优胜率
2. **玩家群体模拟** - 模拟不同技能水平玩家的实际表现

两者结合可以得出：
- 理论难度上限（最强 AI 能达到的胜率）
- 实际玩家体验（各技能层级的预期胜率）
- 优化空间（理论与实际的差距）

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    ComprehensiveLevelAnalyzer                           │
│                  (综合分析器 - 协调两种分析方法)                          │
└───────────────────────┬─────────────────────────────────────────────────┘
                        │
        ┌───────────────┴───────────────┐
        ▼                               ▼
┌───────────────────┐         ┌─────────────────────────┐
│   MCTSAnalyzer    │         │ StrategyDrivenAnalysis  │
│   (理论分析)       │         │      Service            │
│                   │         │   (玩家群体模拟)          │
└────────┬──────────┘         └───────────┬─────────────┘
         │                                │
         ▼                                ▼
┌───────────────────┐         ┌─────────────────────────┐
│    MCTSNode       │         │  SyntheticPlayerStrategy│
│  (搜索树节点)      │         │     + PlayerProfile     │
└───────────────────┘         │    (合成玩家策略)         │
                              └─────────────────────────┘
                                          │
                                          ▼
                              ┌─────────────────────────┐
                              │  SharedSimulationContext│
                              │    (共享模拟上下文)       │
                              └─────────────────────────┘
                                          │
                                          ▼
                              ┌─────────────────────────┐
                              │   SimulationEngine      │
                              │    (核心模拟引擎)        │
                              └─────────────────────────┘
```

## Core Components

### 1. ComprehensiveLevelAnalyzer

综合分析器，协调 MCTS 和玩家群体模拟两种方法。

```csharp
public sealed class ComprehensiveLevelAnalyzer
{
    // 执行综合分析
    Task<ComprehensiveAnalysisResult> AnalyzeAsync(
        LevelConfig levelConfig,
        ComprehensiveAnalysisConfig? config = null,
        IProgress<ComprehensiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

**输出内容**：
| 字段 | 说明 |
|------|------|
| `TheoreticalOptimalWinRate` | 理论最优胜率 (MCTS) |
| `MinMovesToWin` | 最少通关步数 |
| `CriticalMoves` | 关键决策点列表 |
| `PopulationResult` | 玩家群体模拟完整结果 |
| `WeightedAverageWinRate` | 加权平均胜率 |
| `DifficultyGap` | 难度差距（理论 - 实际）|
| `OverallDifficultyRating` | 整体难度评级 |
| `Suggestions` | 调整建议列表 |

### 2. MCTSAnalyzer

基于蒙特卡洛树搜索的理论分析器。

```
┌─────────────────────────────────────────┐
│              MCTS 流程                   │
├─────────────────────────────────────────┤
│  1. Selection   - UCB1 选择最优节点      │
│  2. Expansion   - 展开新的子节点         │
│  3. Rollout     - 策略引导的随机模拟     │
│  4. Backprop    - 回传奖励值            │
└─────────────────────────────────────────┘
```

**UCB1 公式**：
```
UCB1 = exploitation + exploration
     = (wins / visits) + C * sqrt(ln(parent_visits) / visits)
```

**配置参数** (`MCTSConfig`)：
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `SimulationsPerMove` | 100 | 每次决策的模拟次数 |
| `ExplorationConstant` | 1.414 | UCB1 探索常数 |
| `MaxRolloutDepth` | 30 | Rollout 最大深度 |
| `UseGuidedRollout` | true | 是否使用策略引导 |
| `RolloutSkillLevel` | 0.7 | Rollout 策略技能水平 |
| `TotalGames` | 50 | 分析的总局数 |

### 3. StrategyDrivenAnalysisService

策略驱动的关卡分析服务，支持玩家群体模拟。

```csharp
public interface ILevelAnalysisService
{
    Task<LevelAnalysisResult> AnalyzeAsync(
        LevelConfig levelConfig,
        AnalysisConfig? config = null,
        IProgress<SimulationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

**模拟模式** (`SimulationMode`)：
| 模式 | 说明 |
|------|------|
| `Random` | 完全随机选择 |
| `PlayerPopulation` | 玩家群体模拟（默认）|
| `Greedy` | 贪婪策略 |
| `BombPriority` | 炸弹优先策略 |

### 4. SyntheticPlayerStrategy + PlayerProfile

合成玩家策略，模拟不同技能水平的玩家行为。

**PlayerProfile 预设**：
| 层级 | SkillLevel | BombPreference | ObjectiveFocus | 典型权重 |
|------|------------|----------------|----------------|----------|
| Novice | 0.3 | 0.5 | 0.5 | 15% |
| Casual | 0.5 | 0.8 | 0.8 | 50% |
| Core | 0.7 | 1.0 | 1.0 | 30% |
| Expert | 0.9 | 1.2 | 1.2 | 5% |

**评分公式**：
```csharp
score = baseScore                           // 基础分（消除数 + 得分）
      + bombBonus * BombPreference          // 炸弹加成
      + positionBonus                       // 位置偏好
      + noise * (1 - SkillLevel)            // 随机噪声（技能越低噪声越大）
```

### 5. SharedSimulationContext

共享模拟上下文，优化性能避免重复创建引擎组件。

```csharp
internal sealed class SharedSimulationContext : IDisposable
{
    // 可复用组件
    RealtimeGravitySystem GetPhysics();
    ClassicMatchFinder GetMatchFinder();
    StandardMatchProcessor GetMatchProcessor();
    PowerUpHandler GetPowerUpHandler();
    RealtimeRefillSystem GetRefill();

    // 每次模拟创建新实例（避免状态污染）
    LevelObjectiveSystem CreateObjectiveSystem();

    // 快速预览
    (long scoreGained, int tilesCleared, bool isValid) QuickPreviewMove(
        in GameState state, Position from, Position to);
}
```

**线程安全**：使用 `ThreadLocal<SharedSimulationContext>` 确保每个线程有独立实例。

## Data Flow

### 综合分析流程

```
LevelConfig
    │
    ▼
┌─────────────────────────────────────────────────────────┐
│              ComprehensiveLevelAnalyzer                  │
│  ┌─────────────────────────────────────────────────────┐│
│  │ Phase 1: MCTS Analysis (可选)                       ││
│  │   └── MCTSAnalyzer.AnalyzeAsync()                   ││
│  │       └── N 局 MCTS 对弈                             ││
│  │           └── 每步 M 次模拟选最优                    ││
│  └─────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────┐│
│  │ Phase 2: Player Population Simulation               ││
│  │   └── StrategyDrivenAnalysisService.AnalyzeAsync()  ││
│  │       └── 各层级玩家并行模拟                          ││
│  │           └── SyntheticPlayerStrategy 选择           ││
│  └─────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────┐│
│  │ Phase 3: Result Aggregation                          ││
│  │   └── 计算难度差距、生成建议                          ││
│  └─────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────┘
    │
    ▼
ComprehensiveAnalysisResult
```

### 单次模拟流程

```
GameState (初始)
    │
    ├──► ValidMoveDetector.FindAllValidMoves()
    │       └── 找出所有合法移动
    │
    ├──► Strategy.ScoreMove() (对每个移动评分)
    │       └── QuickPreviewMove() 获取预览信息
    │
    ├──► 选择最高分移动
    │
    ├──► SimulationEngine.ApplyMove()
    │       └── RunUntilStable()
    │
    ├──► 检查胜利/失败条件
    │
    └──► 循环直到游戏结束
```

## Output Structures

### LevelAnalysisResult

```csharp
public sealed class LevelAnalysisResult
{
    float WinRate;                    // 整体胜率
    float AverageMovesUsed;           // 平均使用步数
    float DeadlockRate;               // 死锁率
    int SimulationCount;              // 模拟次数

    PlayerTierResult[]? TierResults;  // 分层结果
    StageProgressDistribution? ProgressDistribution;  // 进度分布
    Dictionary<int, int>? RemainingMovesDistribution; // 剩余步数分布
}
```

### StageProgressDistribution

```csharp
public sealed class StageProgressDistribution
{
    // 各阶段的平均进度 (0%-100% → 10个区间)
    float[] AverageProgressByStage;

    // 失败原因分布
    Dictionary<string, float> FailureReasonDistribution;
}
```

## Performance Considerations

### 优化策略

1. **组件复用** - `SharedSimulationContext` 缓存可复用的系统组件
2. **并行执行** - 使用 `Parallel.ForEach` 并行模拟多局游戏
3. **任务打散** - 不同层级的任务交错执行，避免负载不均
4. **零分配事件** - 使用 `NullEventCollector` 跳过事件收集

### 性能数据（参考）

| 分析类型 | 配置 | 典型耗时 |
|----------|------|----------|
| MCTS | 50局 × 100模拟/步 | 5-15分钟 |
| 玩家群体 | 1000局并行 | 1-3分钟 |
| 综合分析 | MCTS + 群体 | 6-18分钟 |

## Usage Examples

### 快速分析

```csharp
var analyzer = new ComprehensiveLevelAnalyzer();
var result = await analyzer.AnalyzeAsync(levelConfig, new ComprehensiveAnalysisConfig
{
    RunMCTSAnalysis = false,  // 跳过 MCTS（更快）
    PopulationSimulationCount = 500,
    UseParallel = true
});

Console.WriteLine($"胜率: {result.PopulationResult.WinRate:P1}");
Console.WriteLine($"难度: {result.OverallDifficultyRating}");
```

### 完整分析

```csharp
var mctsConfig = new MCTSConfig
{
    TotalGames = 30,
    SimulationsPerMove = 50
};

var analyzer = new ComprehensiveLevelAnalyzer(mctsConfig);
var result = await analyzer.AnalyzeAsync(levelConfig, new ComprehensiveAnalysisConfig
{
    RunMCTSAnalysis = true,
    MCTSTotalGames = 30,
    PopulationSimulationCount = 1000,
    PopulationConfig = new PlayerPopulationConfig
    {
        Tiers = new[]
        {
            new PlayerTierConfig { Name = "新手", SkillLevel = 0.3f, Weight = 0.2f },
            new PlayerTierConfig { Name = "休闲", SkillLevel = 0.5f, Weight = 0.5f },
            new PlayerTierConfig { Name = "核心", SkillLevel = 0.7f, Weight = 0.25f },
            new PlayerTierConfig { Name = "高手", SkillLevel = 0.9f, Weight = 0.05f }
        }
    }
});

Console.WriteLine(result.GenerateSummary());
```

### 自定义策略

```csharp
var config = new AnalysisConfig
{
    Mode = SimulationMode.PlayerPopulation,
    PopulationConfig = new PlayerPopulationConfig
    {
        Tiers = new[]
        {
            new PlayerTierConfig
            {
                Name = "目标导向玩家",
                SkillLevel = 0.6f,
                BombPreference = 0.5f,
                ObjectiveFocus = 1.5f,  // 更关注目标
                Weight = 1.0f
            }
        }
    }
};

var service = new StrategyDrivenAnalysisService();
var result = await service.AnalyzeAsync(levelConfig, config);
```

## Difficulty Rating Scale

| 评级 | Casual 胜率 | 说明 |
|------|-------------|------|
| TooEasy | ≥75% | 过于简单 |
| Easy | 60-75% | 简单 |
| Balanced | 45-60% | 平衡 |
| Challenging | 30-45% | 有挑战 |
| Hard | 15-30% | 困难 |
| VeryHard | <15% | 非常困难 |
| PossiblyUnfair | MCTS <50% | 可能不公平 |

## File Structure

```
Match3.Core/
├── Analysis/
│   ├── ILevelAnalysisService.cs      # 接口定义
│   ├── StrategyDrivenAnalysisService.cs  # 策略驱动分析
│   ├── ComprehensiveLevelAnalyzer.cs # 综合分析器
│   ├── AnalysisUtility.cs            # 共享工具方法
│   ├── SharedSimulationContext.cs    # 共享模拟上下文
│   └── MCTS/
│       ├── MCTSAnalyzer.cs           # MCTS 分析器
│       └── MCTSNode.cs               # MCTS 树节点
└── AI/
    └── Strategies/
        └── SyntheticPlayerStrategy.cs # 合成玩家策略

Match3.Core.Tests/
└── Analysis/
    ├── StrategyDrivenAnalysisServiceTests.cs
    ├── MCTSAnalyzerTests.cs
    └── ComprehensiveLevelAnalyzerTests.cs
```

## See Also

- [Core Architecture](./overview.md) - 核心架构概述
- [AI Service](./overview.md#7-ai-service-match3coreai) - AI 服务接口
- [Level Design](../03-design/level-design.md) - 关卡设计规范
