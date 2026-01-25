# Unity 项目规则

## 源码位置

Unity 通过 DLL 引用核心库，**源码位于**：

- `../src/Match3.Core/` - 核心逻辑（Match3Engine, SimulationEngine, Events）
- `../src/Match3.Presentation/` - 表现层（Player, RenderCommand, VisualState）
- `../src/Match3.Random/` - 随机数服务

修改 Unity 代码前，**必须先理解 Core 的设计**。

## 必读文档

- `../docs/04-adr/0004-pure-player-architecture.md` - Player 架构设计
- `../docs/04-adr/0003-event-sourcing-and-tick-based-simulation.md` - 事件驱动架构
- `../docs/01-architecture/core-patterns.md` - 核心约束和性能规范

## 架构约束

### 禁止

- ❌ 在 MonoBehaviour 中直接操作 `Match3Grid`
- ❌ 绕过 `Player` 直接处理 `GameEvent`
- ❌ 在 View 层实现游戏逻辑

### 必须

- ✅ 通过 `RenderCommand` 驱动所有视觉变化
- ✅ 输入事件转换为 Core 的 `InputSystem` 调用
- ✅ 使用 `VisualState` 进行插值渲染

## 命名规范

| 类型 | 命名 | 示例 |
|------|------|------|
| MonoBehaviour 视图 | `XxxView` | `TileView`, `BoardView` |
| 控制器 | `XxxController` | `InputController`, `GameController` |
| ScriptableObject | `XxxConfig` | `TileConfig`, `AnimationConfig` |

## 目录结构

```
Assets/
├── Plugins/Match3/        # Core DLLs（构建脚本同步）
├── Scripts/               # Unity 特定代码
│   ├── Views/             # MonoBehaviour 视图组件
│   ├── Controllers/       # 输入和游戏流程控制
│   └── Bridge/            # Core 与 Unity 的桥接层
├── Prefabs/
├── Scenes/
└── Resources/
```

## DLL 同步

DLL 通过 **PostBuild 自动同步**，只需在项目根目录执行：

```bash
dotnet build src/Match3.Presentation -c Release
```

或者告诉 Claude："同步到 Unity"
