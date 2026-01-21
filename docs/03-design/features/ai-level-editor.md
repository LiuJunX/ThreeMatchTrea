# 编辑器功能：AI 对话式关卡编辑

| 文档状态 | 作者 | 日期 | 对应版本 |
| :--- | :--- | :--- | :--- |
| **Implemented** | AI Assistant | 2026-01-21 | v1.0 |

## 1. 概述 (Overview)

AI 对话式关卡编辑允许用户通过自然语言描述来创建和修改关卡，无需手动操作复杂的 UI 控件。

### 1.1 功能定位

| 用途 | 描述 |
| :--- | :--- |
| **快速原型** | 通过描述快速生成关卡草稿 |
| **批量操作** | "把整个第一行都设为冰块" 等批量修改 |
| **参数调整** | 自然语言调整步数、目标等参数 |
| **学习辅助** | 新手可通过对话了解关卡设计 |

### 1.2 核心特性

- **自然语言理解**：支持中文自然语言输入
- **意图识别**：将自然语言转换为具体操作意图
- **上下文感知**：AI 了解当前关卡状态
- **可插拔架构**：支持多种 LLM 提供商（DeepSeek/OpenAI/Claude）

## 2. 系统架构

### 2.1 架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                    AI 对话式关卡编辑系统                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │              Match3.Web (Blazor)                         │   │
│   │  ┌───────────────────────────────────────────────────┐  │   │
│   │  │  AIChatPanel.razor                                │  │   │
│   │  │  • 浮动聊天窗口 UI                                 │  │   │
│   │  │  • 消息列表展示                                    │  │   │
│   │  │  • 输入框和发送按钮                                │  │   │
│   │  └───────────────────────────────────────────────────┘  │   │
│   │                         │                                │   │
│   │                         ▼                                │   │
│   │  ┌───────────────────────────────────────────────────┐  │   │
│   │  │  WebLevelAIChatService                            │  │   │
│   │  │  • 实现 ILevelAIChatService                       │  │   │
│   │  │  • 构建系统提示词                                  │  │   │
│   │  │  • 解析 AI 响应 JSON                              │  │   │
│   │  └───────────────────────────────────────────────────┘  │   │
│   │                         │                                │   │
│   │                         ▼                                │   │
│   │  ┌───────────────────────────────────────────────────┐  │   │
│   │  │  ILLMClient (可插拔)                              │  │   │
│   │  │  ├─ OpenAICompatibleClient (DeepSeek/OpenAI)     │  │   │
│   │  │  └─ ClaudeClient (备选)                          │  │   │
│   │  └───────────────────────────────────────────────────┘  │   │
│   └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│   ═══════════════════════════════════════════════════════════   │
│                                                                 │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │              Match3.Editor (.NET Standard 2.1)           │   │
│   │  ┌───────────────────────────────────────────────────┐  │   │
│   │  │  LevelAIChatViewModel                             │  │   │
│   │  │  • 对话状态管理                                    │  │   │
│   │  │  • 消息历史                                        │  │   │
│   │  │  • 发送/接收消息                                   │  │   │
│   │  └───────────────────────────────────────────────────┘  │   │
│   │                         │                                │   │
│   │                         ▼                                │   │
│   │  ┌───────────────────────────────────────────────────┐  │   │
│   │  │  IntentExecutor                                   │  │   │
│   │  │  • 解析 LevelIntent                               │  │   │
│   │  │  • 调用 ViewModel/GridManipulator 执行操作        │  │   │
│   │  └───────────────────────────────────────────────────┘  │   │
│   └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 文件清单

| 层 | 文件 | 职责 |
| :--- | :--- | :--- |
| **Editor** | `Interfaces/ILevelAIChatService.cs` | AI 服务接口定义 |
| **Editor** | `Models/AIChatModels.cs` | 数据模型（LevelContext, AIChatResponse, LevelIntent） |
| **Editor** | `Models/ChatMessage.cs` | 聊天消息模型 |
| **Editor** | `Logic/IntentExecutor.cs` | 意图执行器 |
| **Editor** | `Logic/LevelContextBuilder.cs` | 关卡上下文构建器 |
| **Editor** | `ViewModels/LevelAIChatViewModel.cs` | 对话 ViewModel |
| **Web** | `Services/AI/LLMOptions.cs` | LLM 配置选项 |
| **Web** | `Services/AI/ILLMClient.cs` | LLM 客户端接口 |
| **Web** | `Services/AI/OpenAICompatibleClient.cs` | OpenAI 兼容客户端 |
| **Web** | `Services/AI/WebLevelAIChatService.cs` | AI 服务实现 |
| **Web** | `Components/.../AIChatPanel.razor` | 聊天 UI 组件 |

### 2.3 接口定义

```csharp
// ILevelAIChatService.cs
public interface ILevelAIChatService
{
    bool IsAvailable { get; }

    Task<AIChatResponse> SendMessageAsync(
        string message,
        LevelContext context,
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default);
}
```

```csharp
// ILLMClient.cs
public interface ILLMClient
{
    bool IsAvailable { get; }

    Task<LLMResponse> SendAsync(
        List<LLMMessage> messages,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> SendStreamAsync(
        List<LLMMessage> messages,
        CancellationToken cancellationToken = default);
}
```

## 3. 支持的操作

### 3.1 意图类型

| 意图类型 | 参数 | 描述 |
| :--- | :--- | :--- |
| `SetGridSize` | width, height | 设置网格大小 (3-12) |
| `SetMoveLimit` | moves | 设置步数限制 (1-99) |
| `SetObjective` | index?, layer, elementType, count | 设置/添加目标 |
| `RemoveObjective` | index | 移除目标 |
| `PaintTile` | x, y, tileType, bombType? | 绘制单个格子 |
| `PaintTileRegion` | x1, y1, x2, y2, tileType, bombType? | 绘制区域 |
| `PaintCover` | x, y, coverType | 放置覆盖物 |
| `PaintCoverRegion` | x1, y1, x2, y2, coverType | 区域覆盖物 |
| `PaintGround` | x, y, groundType | 放置地面 |
| `PaintGroundRegion` | x1, y1, x2, y2, groundType | 区域地面 |
| `PlaceBomb` | x, y, bombType, tileType? | 放置炸弹 |
| `GenerateRandomLevel` | - | 随机生成关卡 |
| `ClearRegion` | x1?, y1?, x2?, y2? | 清空区域 |
| `ClearAll` | - | 清空整个网格 |

### 3.2 可用元素

| 类型 | 可选值 |
| :--- | :--- |
| **TileType** | Red, Green, Blue, Yellow, Purple, Orange, Rainbow, None |
| **BombType** | None, Horizontal, Vertical, Color, Ufo, Square5x5 |
| **CoverType** | None, Cage, Chain, Bubble |
| **GroundType** | None, Ice |

### 3.3 自然语言示例

| 用户输入 | 识别的意图 |
| :--- | :--- |
| "把网格改成 10x10" | `SetGridSize {width: 10, height: 10}` |
| "步数限制 25 步" | `SetMoveLimit {moves: 25}` |
| "目标是消除 30 个红色方块" | `SetObjective {layer: Tile, elementType: 0, count: 30}` |
| "在中间放一个彩虹炸弹" | `PlaceBomb {x: center, y: center, bombType: Color}` |
| "第一行全部放冰块" | `PaintGroundRegion {x1: 0, y1: 0, x2: width-1, y2: 0, groundType: Ice}` |
| "左下角 3x3 区域放笼子" | `PaintCoverRegion {x1: 0, y1: height-3, x2: 2, y2: height-1, coverType: Cage}` |
| "随机生成关卡" | `GenerateRandomLevel {}` |

## 4. 数据流程

### 4.1 消息处理流程

```
用户输入
    │
    ▼
┌─────────────────┐
│ LevelAIChatVM   │ ─── 构建上下文
│ SendMessageAsync│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ LevelContext    │ ─── 当前关卡状态
│ Builder.Build() │     (尺寸、目标、元素统计)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ WebLevelAI      │ ─── 构建系统提示词
│ ChatService     │     + 历史消息
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ ILLMClient      │ ─── 调用外部 API
│ SendAsync()     │     (DeepSeek/OpenAI)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ ParseResponse   │ ─── 提取 JSON
│                 │     解析 intents
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ IntentExecutor  │ ─── 执行每个 intent
│ Execute()       │     修改关卡配置
└─────────────────┘
```

### 4.2 系统提示词结构

```
1. 角色定义
   "你是一个 Match3 消除游戏的关卡编辑助手..."

2. 当前关卡状态
   - 网格大小: 8 x 8
   - 步数限制: 20
   - 当前目标: [...]
   - 网格摘要: Tiles: Red=10 Blue=8...

3. 可执行操作列表
   - SetGridSize, SetMoveLimit, ...

4. 可用元素
   - TileType, BombType, CoverType, GroundType

5. 响应格式要求
   JSON: { message, intents[] }
```

## 5. 用户界面

### 5.1 浮动聊天窗口

- **位置**：右侧固定浮动
- **宽度**：360px
- **组件**：消息列表 + 输入区域
- **折叠**：点击按钮可显示/隐藏

### 5.2 消息类型

| 类型 | 样式 | 说明 |
| :--- | :--- | :--- |
| 用户消息 | 蓝色背景，右对齐 | 用户输入 |
| AI 回复 | 深色背景，左对齐 | AI 响应文本 |
| 错误消息 | 红色边框 | 请求失败或取消 |
| 意图标签 | 小徽章 | 显示执行的操作 |

### 5.3 交互状态

| 状态 | UI 表现 |
| :--- | :--- |
| 空闲 | 输入框可用，发送按钮启用 |
| 等待响应 | 输入框禁用，显示加载动画 |
| AI 不可用 | 显示警告提示 |

## 6. 配置

### 6.1 LLM 配置选项

```json
// appsettings.json
{
  "LLM": {
    "Provider": "DeepSeek",
    "BaseUrl": "https://api.deepseek.com/v1",
    "ApiKey": "sk-xxx",
    "Model": "deepseek-chat",
    "MaxTokens": 2048,
    "Temperature": 0.7
  }
}
```

### 6.2 切换提供商

| Provider | BaseUrl | 说明 |
| :--- | :--- | :--- |
| DeepSeek | `https://api.deepseek.com/v1` | 默认，性价比高 |
| OpenAI | `https://api.openai.com/v1` | OpenAI 官方 |
| Azure | `https://xxx.openai.azure.com/` | Azure OpenAI |
| 本地 | `http://localhost:11434/v1` | Ollama 等本地部署 |

## 7. 错误处理

| 错误类型 | 处理方式 |
| :--- | :--- |
| 网络错误 | 显示错误消息，允许重试 |
| API 密钥无效 | 提示检查配置 |
| 响应解析失败 | 返回原文，intents 为空 |
| 请求取消 | 显示"请求已取消" |

## 8. 扩展性

### 8.1 添加新的 LLM 提供商

1. 实现 `ILLMClient` 接口
2. 在 `Program.cs` 注册服务
3. 配置 `appsettings.json`

### 8.2 添加新的意图类型

1. 在 `LevelIntentType` 枚举添加新类型
2. 在 `IntentExecutor.Execute()` 添加 case 分支
3. 更新 `WebLevelAIChatService.BuildSystemPrompt()` 添加说明

## 9. 版本历史

| 版本 | 日期 | 变更内容 |
| :--- | :--- | :--- |
| v1.0 | 2026-01-21 | 初始实现：支持 DeepSeek/OpenAI，14 种意图类型，浮动聊天窗口 |
