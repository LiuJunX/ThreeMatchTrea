---
alwaysApply: true
---
# Project Rules (Trae)

## 0. Interaction Protocol (Highest Priority)
- **Confirm Before Action**: For any ambiguous or uncertain requirements, the AI MUST pause and ask the user for confirmation before proceeding.
- **No Assumptions**: Do NOT make assumptions to fill in gaps in requirements. Clarify first.
- **Plan Confirmation**: For complex tasks, propose a plan and wait for explicit user approval.

## 1. Project Structure
- Match3.Core：纯业务逻辑，无 UI 依赖，定义接口与核心流程
- Match3.Random：统一随机入口（IRandom、SeedManager、RandomDomain、RandomStreamFactory）
- Match3.Web：应用装配与视图层（IGameView、输入意图）
- Match3.Editor: Cross-platform editor logic and tools (No UI framework dependencies)
- Match3.Tests：单元/场景测试（含编码规范）
- Match3.ConsoleDemo：控制台演示 UI（若存在）

## 2. Code Style & Conventions
- 4 空格缩进；Allman 大括号；文件级命名空间；私有字段 _camelCase；公共成员/类型 PascalCase；接口 I 前缀；类型明显用 var

## 3. Design Patterns
- 模型驱动：Core 为唯一真源；坐标实时；用 Update/Tick 推进
- 视图只渲染：禁止插值/物理/独立计时（禁用 CSS 过渡/Task.Delay 位移）
- 分层职责：Controller 管理逻辑与状态；IGameView 仅渲染与输入意图；依赖注入用构造函数
- **DDD (Domain-Driven Design)**: Use ubiquitous language (e.g., Gravity, Matching). High Cohesion: Code that changes together stays together.

## 4. Best Practices
- 先看上下文：修改/新增前检索既有文件与约定
- 复用现有结构：如 Position、GameBoard
- 安全：空值检查，Try* 模式
- 注释：公共 API 用 XML；复杂说明“为何/如何”；简单不注
- 验证：实现后运行测试并确认行为

## 5. Git
- 提交信息用祈使句且具体；原子提交，配置/逻辑/文档分开，避免混合变更

## 6. Autonomous Workflow（面向助手）
- 规划 → 测试 → 实现 → dotnet test → 通过交付；失败修复
- 遇到复杂逻辑（>50行）时，主动拆分为小方法以降低认知负担

## 7. Testing Strategy
- 新增逻辑前优先编写测试（TDD-lite）
- 修改核心逻辑（MatchFinder, Gravity）后必须运行 `dotnet test`
- 保持测试用例的原子性和独立性

## 8. Documentation Maintenance
- **Docs-as-Code**: Documentation lives in `/docs`.
- **Sync Rule**: Update `docs/01-architecture/overview.md` when changing core components.
- **ADR Required**: Create a new ADR in `docs/04-adr` for major architectural decisions (e.g., adding a new dependency).
- **API Docs**: All core interfaces (`Match3.Core/Interfaces`) MUST have XML comments.

## 9. AI Context Guidelines (For AI Agents)
### Core Services Whitelist
- **Object Creation**: MUST use `Pools.Rent<T>()` for hot-path objects. PROHIBITED: `new T()` inside loops.
- **Logging**: MUST use `IGameLogger.LogInfo<T>()` with templates. PROHIBITED: `Console.WriteLine` or `$"..."` interpolation.
- **String Ops**: MUST use `ZString` for formatting. PROHIBITED: `StringBuilder` (unless pooled) or `+` operator.
- **Randomness**: MUST use `Match3.Random` interfaces. PROHIBITED: `System.Random`.

### Architecture Red Lines
- **No UI in Core**: `Match3.Core` must NEVER reference `Match3.Web`.
- **Stateless Logic**: Logic classes must remain stateless. State belongs in `Structs`.
- **Reference**: See `docs/01-architecture/core-patterns.md` for detailed architectural constraints.

## 10. Mandatory Modularization (Priority: ★★★★★)
All new features MUST be implemented as independent Systems.
1.  **Define Interface**: `I{Name}System` in `Match3.Core.Interfaces`.
2.  **Implement System**: `{Name}System` in `Match3.Core.Systems`.
3.  **Inject**: Pass via constructor to `Match3Controller`.
4.  **No God Classes**: `Match3Controller` must only coordinate; it must not contain business logic.

## 11. Cross-Platform Portability (Priority: ★★★★★)
Ensure all core logic and editor tools are portable to Unity and other platforms.
1.  **Dependency Rule**: 
    - Inner layers (Core/Editor Logic) MUST NOT depend on outer layers (Web/Unity/UI/IO).
    - `Match3.Editor` MUST NOT reference `System.IO`, `System.Console`, `Microsoft.AspNetCore`, or `UnityEngine`.
2.  **State Management**:
    - UI MUST be stateless and serve only as a projection of the ViewModel.
    - View layers (Razor/MonoBehaviour) MUST NOT hold business state (e.g., `LevelConfig`).
3.  **Abstractions**:
    - All side effects (File IO, Alerts, Clipboard, Time) MUST be accessed via interfaces (e.g., `IFileSystemService`, `IPlatformService`).

