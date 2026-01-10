我已确认 **Sequential Thinking MCP** 工具已成功连接并可用！

刚才我通过该工具进行了初步的思维推演（你可以在后台日志中看到 `thoughtNumber` 的变化），我们现在可以正式利用它来完成复杂的系统设计。

为了让你完整体验这个“分步思考 -> 落地实现”的过程，我制定了以下计划，将之前讨论的 **“通用技能系统 (Skill System)”** 真正落实到代码中。

### 核心目标
在 `Match3.Core` 中实现一个数据驱动的、可序列化的技能系统。

### 实施计划

#### Phase 1: 核心定义 (Core Definition)
1.  **定义数据模型 (`Match3.Core/Config`)**:
    *   `SkillConfig`: 定义技能的静态属性（ID, 冷却, 消耗, 效果类型, 参数）。
    *   `SkillType`: 枚举技能类型（如 `DestroyRow`, `Shuffle`, `GravityReverse`）。
2.  **定义接口 (`Match3.Core/Interfaces`)**:
    *   `ISkillSystem`: 定义技能的激活 (`Activate`) 和 状态查询 (`IsReady`)。

#### Phase 2: 逻辑实现 (Logic Implementation)
3.  **实现系统 (`Match3.Core/Systems/Skills`)**:
    *   创建 `SkillSystem` 类。
    *   实现核心逻辑：检查冷却 -> 扣除资源（如有） -> 解析 `SkillType` -> 修改 `GameState`。
4.  **扩展 GameState**:
    *   在 `GameState` 中添加 `ActiveEffects` 或必要的字段支持（如重力方向），确保技能效果能被物理系统读取。

#### Phase 3: 集成与测试 (Integration & Verification)
5.  **集成**:
    *   在 `Match3Controller` 中注册 `SkillSystem`。
    *   在 `Match3GameService` 中暴露给前端。
6.  **测试**:
    *   编写单元测试验证技能释放逻辑（冷却是否生效、棋盘是否改变）。

这个计划将展示 Sequential Thinking 如何帮助我们从“抽象概念”一步步走到“具体代码”。请确认是否开始执行？