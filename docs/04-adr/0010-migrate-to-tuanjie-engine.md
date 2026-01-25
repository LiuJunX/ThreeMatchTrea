# ADR-0010: 迁移至团结引擎 (Tuanjie Engine)

* **Status**: Accepted
* **Deciders**: LiuJun, Claude (AI Assistant)
* **Date**: 2026-01-24

## Context and Problem Statement

项目需要从当前的 Blazor Web 架构迁移到支持多平台的游戏引擎。目标平台为：微信小游戏、华为鸿蒙（首选），iOS/Android（后期）。同时需要保证深度 AI 计算（MCTS、动态难度分析）的性能。

核心问题：**如何在保留现有 C# 代码资产的同时，实现高性能跨平台发布？**

## Decision Drivers

* **平台覆盖**：微信小游戏 + 鸿蒙 HarmonyOS NEXT 是首要目标
* **性能需求**：MCTS 分析、深度难度评估需要接近原生的计算性能
* **代码复用**：~21,000 行 C# 核心代码，希望最小化迁移成本
* **AI 辅助开发**：需要选择 AI 编程助手擅长的技术栈
* **开发效率**：1 人团队，需要快速迭代验证市场

## Considered Options

1. **自研引擎 (TypeScript + PixiJS)**
2. **自研引擎 (AssemblyScript + WASM)**
3. **自研引擎 (C++ 核心 + 多平台绑定)**
4. **Cocos Creator (TypeScript)**
5. **Unity 国际版**
6. **团结引擎 (Tuanjie Engine)**

## Decision Outcome

Chosen option: **团结引擎 (Tuanjie Engine)**

### 理由

1. **平台支持最佳**：微信小游戏官方深度优化 + 鸿蒙 HarmonyOS NEXT 首批适配
2. **代码复用最大**：C# 核心代码几乎不用修改，直接复制进项目
3. **性能有保障**：IL2CPP 编译保证 AI 计算性能，DOTS/Burst 可进一步优化
4. **AI 辅助友好**：C# 是 AI 最擅长的语言之一
5. **国内支持**：本土团队，技术支持便捷

### Positive Consequences

* Match3.Core 核心逻辑可直接复用，迁移工作量最小
* IL2CPP 确保 MCTS、DeepAnalysis 等计算密集代码的性能
* 一套代码覆盖所有目标平台
* 现有架构设计（事件溯源、Tick 驱动、模块化 System）完全适用
* 可以继续使用 AI 辅助开发，保持高效迭代

### Negative Consequences

* 表现层需要用 Unity 方式重写（约 30% 工作量）
* 团结引擎 AI 能力相对 Unity 国际版滞后
* 受引擎生态限制
* 需要学习 Unity/团结引擎框架

## Validation

* 迁移后运行现有单元测试，确保核心逻辑正确性
* 性能基准测试：MCTS 模拟速度、深度分析耗时
* 各平台导出测试：微信小游戏、鸿蒙、iOS、Android

## Pros and Cons of the Options

### Option 1: 自研引擎 (TypeScript + PixiJS)

* Good: AI 辅助效率最高
* Good: 开发迭代快
* Bad: **性能不足**，TypeScript 比 C# 慢 2-5 倍（对象密集场景）
* Bad: 需要全部重写核心代码
* Bad: 自己处理渲染、动画、粒子、跨平台适配

### Option 2: 自研引擎 (AssemblyScript + WASM)

* Good: 性能接近原生
* Good: 语法类似 TypeScript，AI 辅助可用
* Bad: 没有 IL2CPP，不如 C# 生态成熟
* Bad: 需要全部重写核心代码
* Bad: 仍需自己处理引擎层问题

### Option 3: 自研引擎 (C++ 核心)

* Good: **性能最好**
* Bad: **开发效率低**，AI 辅助效果差
* Bad: 内存管理易出错，调试困难
* Bad: 每个平台需要单独绑定层

### Option 4: Cocos Creator

* Good: 微信小游戏官方支持
* Good: 鸿蒙适配
* Good: 包体小
* Bad: **需要将 C# 全部翻译成 TypeScript**（~21,000 行）
* Bad: TypeScript 性能不如 IL2CPP

### Option 5: Unity 国际版

* Good: 功能最全，AI 能力领先
* Good: IL2CPP + DOTS 性能好
* Good: C# 代码可直接使用
* Bad: **鸿蒙支持需要额外适配**
* Bad: 微信小游戏支持不如团结引擎优化
* Bad: 包体相对较大

### Option 6: 团结引擎 (Tuanjie Engine) [CHOSEN]

* Good: **微信小游戏官方深度优化**
* Good: **鸿蒙 HarmonyOS NEXT 首批适配**
* Good: **C# 代码直接复用**
* Good: IL2CPP 保证性能
* Good: 国内团队支持
* Bad: AI 能力相对 Unity 国际版滞后（可用外部 AI 工具弥补）

## Migration Strategy

```
Phase 1: 环境搭建 ✅ 已完成
├── 创建 unity/ 工程目录（嵌套结构）
├── 配置 PostBuild 自动同步 DLL
├── 添加 unity/CLAUDE.md AI 开发规则
└── 限制 LangVersion 为 C# 10（Unity 兼容）

Phase 2: 核心验证
├── 运行单元测试
├── 验证 MCTS、深度分析功能
└── 性能基准测试

Phase 3: 表现层重写
├── 用 Unity 组件重写渲染
├── 实现动画系统（RenderCommand → MonoBehaviour）
├── 实现粒子特效

Phase 4: 平台导出
├── 微信小游戏测试
├── 鸿蒙测试
├── iOS/Android 测试
```

## Implementation Details

### DLL 集成方案

采用 **PostBuild 自动同步**，而非源码引用：

```
src/Match3.Core           →  unity/Assets/Plugins/Match3/Match3.Core.dll
src/Match3.Presentation   →  unity/Assets/Plugins/Match3/Match3.Presentation.dll
src/Match3.Random         →  unity/Assets/Plugins/Match3/Match3.Random.dll
(NuGet) ZString           →  unity/Assets/Plugins/Match3/ZString.dll
(NuGet) Unsafe            →  unity/Assets/Plugins/Match3/System.Runtime.CompilerServices.Unsafe.dll
```

**同步命令**：
```bash
dotnet build src/Match3.Presentation -c Release
# 或使用 make unity
# 或告诉 Claude："同步到 Unity"
```

### 目录结构

```
unity/
├── CLAUDE.md              # AI 开发规则，指向 Core 源码
├── .gitignore             # Unity 专用忽略规则
├── Assets/
│   ├── Plugins/Match3/    # 自动同步的 DLL
│   └── Scripts/
│       ├── Views/         # MonoBehaviour 视图组件
│       ├── Controllers/   # 输入和游戏流程控制
│       └── Bridge/        # Core 与 Unity 的桥接层
├── Packages/
└── ProjectSettings/
```

### C# 版本兼容

为确保 Unity 兼容，Core 项目限制为 C# 10：

```xml
<LangVersion>10.0</LangVersion>
<TargetFramework>netstandard2.1</TargetFramework>
```

## References

* 团结引擎官网: https://unity.cn/tuanjie
* 本次讨论记录: 2026-01-24 与 Claude 的技术选型对话
