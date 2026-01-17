# Replay System (回放系统)

## 概述

回放系统实现了 Command 模式，支持游戏录像和确定性回放。通过记录玩家命令和初始状态快照，可以完整重现游戏过程。

## 架构

```
Match3.Core/
├── Commands/
│   ├── IGameCommand.cs        # 命令接口
│   ├── SwapCommand.cs         # 交换命令
│   ├── TapCommand.cs          # 点击命令
│   └── CommandHistory.cs      # 命令历史（线程安全）
│
└── Replay/
    ├── GameStateSnapshot.cs   # 状态快照（可序列化）
    ├── GameRecording.cs       # 完整录像数据
    └── ReplayController.cs    # 回放控制器
```

## 核心接口

### IGameCommand

```csharp
public interface IGameCommand
{
    Guid Id { get; }
    long IssuedAtTick { get; }
    bool Execute(SimulationEngine engine);
    bool CanExecute(in GameState state);
}
```

### CommandHistory

```csharp
public sealed class CommandHistory
{
    public bool IsRecording { get; set; }
    public int Count { get; }

    public void Record(IGameCommand command);
    public IReadOnlyList<IGameCommand> GetCommands();
    public void Clear();
}
```

### ReplayController

```csharp
public sealed class ReplayController : IDisposable
{
    public ReplayState State { get; }           // Stopped, Playing, Paused, Completed
    public float PlaybackSpeed { get; set; }    // 1.0 = 正常速度
    public float Progress { get; }              // 0.0 ~ 1.0

    public void Play();
    public void Pause();
    public void Stop();
    public void Seek(float progress);
    public void StepForward();
    public void Tick(float deltaTime);

    public event Action<IGameCommand>? CommandExecuted;
    public event Action? PlaybackCompleted;
}
```

## 使用示例

### 录制游戏

```csharp
// 1. 创建游戏会话
var factory = new GameServiceBuilder().Build();
var session = factory.CreateGameSession(config);

// 2. 记录初始状态
var initialSnapshot = GameStateSnapshot.FromState(session.Engine.State);
var commandHistory = new CommandHistory();

// 3. 执行并记录命令
var swapCmd = new SwapCommand
{
    From = new Position(0, 0),
    To = new Position(1, 0),
    IssuedAtTick = currentTick
};
commandHistory.Record(swapCmd);
swapCmd.Execute(session.Engine);

// 4. 创建录像
var recording = GameRecording.Create(
    initialSnapshot,
    config.RngSeed,
    commandHistory.GetCommands(),
    durationTicks,
    finalScore,
    totalMoves
);
```

### 回放游戏

```csharp
// 1. 加载录像
var recording = LoadRecording();

// 2. 创建回放控制器
var factory = new GameServiceBuilder().Build();
var controller = new ReplayController(recording, factory);

// 3. 控制回放
controller.PlaybackSpeed = 2.0f;  // 2倍速
controller.Play();

// 4. 每帧更新
controller.Tick(deltaTime);

// 5. 跳转
controller.Seek(0.5f);  // 跳到50%位置
```

## 确定性保证

回放系统依赖以下条件确保确定性：

1. **固定随机种子** - `GameRecording.RandomSeed` 保存并恢复
2. **域隔离随机流** - `SeedManager` 为不同域提供独立随机流
3. **命令时序** - `IssuedAtTick` 记录命令发出的精确时刻
4. **状态快照** - `GameStateSnapshot` 完整保存棋盘状态

## 状态快照内容

| 字段 | 类型 | 说明 |
|------|------|------|
| Width, Height | int | 棋盘尺寸 |
| TileTypes[] | TileType[] | 方块类型 |
| BombTypes[] | BombType[] | 炸弹类型 |
| CoverLayers[] | Cover[] | 覆盖层 |
| GroundLayers[] | Ground[] | 地面层 |
| NextTileId | long | 下一个方块ID |
| Score | long | 当前分数 |
| MoveCount | long | 移动次数 |

---

## TODO

### 测试覆盖

- [ ] `CommandHistory` 单元测试
  - 并发录制测试
  - 清空/获取命令测试

- [ ] `GameStateSnapshot` 单元测试
  - FromState/ToState 往返一致性
  - 边界情况（空棋盘、满炸弹）

- [ ] `ReplayController` 单元测试
  - Play/Pause/Stop 状态转换
  - Seek 前进/后退
  - 命令执行顺序验证

### Seek 优化

当前 `Seek()` 实现的限制：

```csharp
// 当前实现：只执行命令，不 Tick 引擎
while (_currentTick < targetTick)
{
    ExecuteNextCommandIfReady();
    _currentTick++;
    // 缺少: _engine.Tick(TickDuration);
}
```

**问题**：
- 快速跳转时，物理/匹配逻辑不会执行
- 目标位置的状态可能与实际播放时不同

**解决方案选项**：

1. **精确 Seek**（慢但准确）
   ```csharp
   while (_currentTick < targetTick)
   {
       ExecuteNextCommandIfReady();
       _engine.Tick(TickDuration);
       _currentTick++;
   }
   ```

2. **检查点系统**（快且准确）
   - 每 N 秒保存一个状态快照
   - Seek 时先跳到最近的检查点，再向前模拟

3. **当前设计**（快但不精确）
   - 适用于只关心命令顺序的场景
   - UI 显示进度条时可接受

---

## 相关文档

- 架构概览: `docs/01-architecture/overview.md`
- 事件系统: `docs/04-adr/0003-event-sourcing-simulation.md`
