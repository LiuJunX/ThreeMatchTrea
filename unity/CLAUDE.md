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
- ❌ 重复实现 Core 已有功能

### 必须

- ✅ 通过 `RenderCommand` 驱动所有视觉变化
- ✅ 输入事件转换为 Core 的 `InputSystem` 调用
- ✅ 使用 `VisualState` 进行插值渲染

### 实现新功能前

1. **先搜索 Core** - `Grep "关键词" src/Match3.Core/`
2. **Unity 只做桥接** - 不重复实现 Core 已有逻辑

## 命名规范

| 类型 | 命名 | 示例 |
|------|------|------|
| MonoBehaviour 视图 | `XxxView` | `TileView`, `BoardView` |
| 控制器 | `XxxController` | `InputController`, `GameController` |
| ScriptableObject | `XxxConfig` | `TileConfig`, `AnimationConfig` |

## 渲染架构

视图层通过 `IBoardView` 接口抽象，支持 2D/3D 切换：

| 模式 | View | Tile | 工厂 |
|------|------|------|------|
| View2D | `BoardView` | `TileView` (SpriteRenderer) | `SpriteFactory` |
| View3D | `Board3DView` | `Tile3DView` (MeshFilter+MeshRenderer) | `MeshFactory` |

- `GameController.RenderMode` 枚举控制切换
- `ResourceService` 统一资源加载接口（可替换 Resources.Load 为 Addressables 等）
- BoardView 创建推迟到 `Initialize()`（非 `Awake()`）

## 目录结构

```
Assets/
├── Plugins/Match3/        # Core DLLs（构建脚本同步）
├── Scripts/               # Unity 特定代码
│   ├── Views/             # MonoBehaviour 视图组件 (BoardView, Board3DView, TileView, Tile3DView)
│   ├── Controllers/       # 输入和游戏流程控制
│   ├── Bridge/            # Core 与 Unity 的桥接层
│   ├── Pools/             # 对象池、工厂 (SpriteFactory, MeshFactory, ViewFactory)
│   └── Services/          # 服务抽象 (ResourceService, IResourceLoader)
├── Prefabs/
├── Scenes/
└── Resources/
    └── Art/Gems/Models/   # 3D 模型 (FBX，从 Match3Art 导出)
```

## DLL 同步

DLL 通过 **PostBuild 自动同步**，只需在项目根目录执行：

```bash
dotnet build src/Match3.Presentation -c Release
```

或者告诉 Claude："同步到 Unity"

## 配置系统

配置文件位于项目根目录 `config/`，Unity 开发时直接从这里读取。

```
config/
├── game/match3.json      # 游戏规则配置
├── visual/colors.json    # 颜色配置
├── visual/animation.json # 动画时长配置
├── levels/               # 关卡配置
└── schemas/              # JSON Schema（IDE 提示）
```

### 使用方式

```csharp
var config = UnityConfigProvider.Instance.GetVisualConfig();
var color = config.TileColors["Red"]; // "#E63333"
```

### 发布构建

构建时会**自动复制** `config/` 到 `StreamingAssets/config/`（由 `BuildPreprocessor` 处理）。

手动同步：菜单 **Match3 > Sync Config to StreamingAssets**
