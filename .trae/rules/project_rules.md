---
alwaysApply: true
---
# Project Rules (AI Instructions)

本文件是 AI 助手的行为指南。技术规范详见领域文档（真源）。

## 0. Interaction Protocol (Highest Priority)
- **Confirm Before Action**: For any ambiguous or uncertain requirements, the AI MUST pause and ask the user for confirmation before proceeding.
- **No Assumptions**: Do NOT make assumptions to fill in gaps in requirements. Clarify first.
- **Plan Confirmation**: For complex tasks, propose a plan and wait for explicit user approval.

## 1. Project Structure
- `Match3.Core` - 纯业务逻辑，无 UI 依赖，定义接口与核心流程
- `Match3.Random` - 统一随机入口（IRandom、SeedManager、RandomDomain）
- `Match3.Web` - Blazor 应用与视图层（IGameView、输入意图）
- `Match3.Editor` - 跨平台编辑器逻辑（无 UI 框架依赖）
- `Match3.ConfigTool` - 配置工具
- `Match3.*.Tests` - 各模块对应的测试项目

## 2. Code Style & Conventions
遵循 `docs/02-guides/coding-standards.md`（真源）

## 3. Design Patterns
- 模型驱动：Core 为唯一真源；坐标实时；用 Update/Tick 推进
- 视图只渲染：禁止插值/物理/独立计时（禁用 CSS 过渡/Task.Delay 位移）
- 分层职责：Controller 管理逻辑与状态；IGameView 仅渲染与输入意图
- DDD：使用领域语言（Gravity、Matching）；高内聚

## 4. Best Practices
- 先看上下文：修改/新增前检索既有文件与约定
- 复用现有结构：如 Position、GameBoard
- 安全：空值检查，Try* 模式
- 验证：实现后运行测试并确认行为

## 5. Git
- 提交信息用祈使句且具体；原子提交，配置/逻辑/文档分开

## 6. Autonomous Workflow
- 规划 → 测试 → 实现 → dotnet test → 通过交付；失败修复
- 复杂逻辑（>50行）主动拆分为小方法

## 7. Testing Strategy
遵循 `docs/testing-guidelines.md`（真源）
- 新增逻辑前优先编写测试（TDD-lite）
- 修改核心逻辑后必须运行 `dotnet test`

## 8. Documentation Maintenance
- **Docs-as-Code**: Documentation lives in `/docs`.
- **Sync Rule**: Update `docs/01-architecture/overview.md` when changing core components.
- **ADR Required**: Create ADR in `docs/04-adr` for major architectural decisions.

## 9. Quick Prohibitions (Must Memorize)
遵循 `docs/01-architecture/core-patterns.md`（真源）

以下为快速禁令，详细说明见真源文档：
- **PROHIBITED**: `new List<T>()` in hot paths → Use `Pools.ObtainList<T>()`
- **PROHIBITED**: `$"..."` interpolation in hot paths → Use template logging
- **PROHIBITED**: `Console.WriteLine` → Use `IGameLogger`
- **PROHIBITED**: `System.Random` → Use `Match3.Random`
- **PROHIBITED**: `Match3.Core` referencing `Match3.Web`
- **PROHIBITED**: State in Logic classes → State belongs in Structs

## 10. Modularization
遵循 `docs/01-architecture/core-patterns.md` §6（真源）

新功能必须实现为独立 System：
1. 定义接口 `I{Name}System` 在 `Match3.Core/Systems/{Domain}/`
2. 实现 `{Name}System` 在同目录
3. 通过构造函数注入

## 11. Cross-Platform Portability
- Inner layers (Core/Editor) MUST NOT depend on outer layers (Web/Unity/UI/IO)
- `Match3.Editor` MUST NOT reference `System.IO`, `System.Console`, `Microsoft.AspNetCore`
- All side effects via interfaces (`IFileSystemService`, `IPlatformService`)

## 12. Single Source of Truth (Documentation)
真源文档标识：`<!-- SOURCE_OF_TRUTH: xxx -->`

### 真源文档列表
| 领域 | 真源文档 |
|------|----------|
| 代码风格 | `docs/02-guides/coding-standards.md` |
| 架构/性能 | `docs/01-architecture/core-patterns.md` |
| 测试要求 | `docs/testing-guidelines.md` |

### 规范
- **新增规范前**：先检查是否已有真源，有则引用
- **引用格式**：`遵循 docs/xxx.md（真源）`
- **禁止复制**：不得将真源内容复制到其他文件
- **修改规范**：只修改真源文档，引用处自动生效

