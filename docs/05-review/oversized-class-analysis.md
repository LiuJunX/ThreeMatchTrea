# 超 300 行类分析报告

> 统计基准：代码行数（含空行和注释），阈值 300 行。
> 日期：2026-03-18

## 概览

| 类 | 文件 | 行数 | 判定 |
|----|------|------|------|
| `SimulationEngine` | `Simulation/SimulationEngine.cs` | 668 | 建议拆分 |
| `BombComboHandler` | `Systems/PowerUps/BombComboHandler.cs` | 403 | 可接受 |
| `SimulationMatchHandler` | `Simulation/SimulationMatchHandler.cs` | 359 | 建议拆分 |
| `BombResolution` | `Systems/PowerUps/BombResolution.cs` | 338 | 可接受 |

---

## 详细分析

### 1. `SimulationEngine`（668 行）— 建议拆分

**职责分布：**
- Tick 循环（Phase 0-7）：~180 行
- 生命周期管理（构造、Clone、Dispose、RestoreState、ResetCounters）：~150 行
- 输入处理委托（ApplyMove、HandleTap）：~40 行
- 状态查询（IsStable、HasPendingMatches）：~20 行
- 辅助（ClearImmovableTilePhysicsState）：~25 行
- 字段声明 + 属性：~60 行

**建议：**
提取 `SimulationLifecycleManager`，封装 Clone / RestoreState / ResetCounters 逻辑。
这些方法共享 `_currentTick`、`_elapsedTime`、`_pendingMoveState` 等帧状态重置逻辑，
与 Tick 循环的核心关注点不同。

**风险：** 低。Clone 和 RestoreState 访问大量 private 字段，提取后需要 internal 访问或 friend assembly。暂不执行。

---

### 2. `BombComboHandler`（403 行）— 可接受

**职责分布：**
- 10 种炸弹组合处理方法，每种 10-20 行
- 路由方法 `HandleCombo`：~30 行
- 辅助方法（CollectPositions 等）：~60 行

**判定：**
行数高但职责单一（炸弹组合路由），每个方法短小，逻辑平铺。
拆分只会引入不必要的间接层。保留现状。

---

### 3. `SimulationMatchHandler`（359 行）— 建议拆分

**职责分布：**
- 核心匹配处理（ProcessStableMatches）：~80 行
- 弹射影响处理（ProcessProjectileImpacts）：~50 行
- 事件发射（EmitMatchEvents、EmitEliminationEvents）：~80 行
- 查询（HasPendingMatches）：~10 行

**建议：**
提取 `MatchEventEmitter`，封装 EmitMatchEvents / EmitEliminationEvents。
事件格式化与核心匹配逻辑无关，且事件结构变更频繁（新增字段），隔离后减少改动范围。

**风险：** 低。事件发射方法是纯函数，无状态依赖。暂不执行。

---

### 4. `BombResolution`（338 行）— 可接受

**职责分布：**
- 构造函数（DI 注入 8 个依赖）：~30 行
- 炸弹激活入口：~40 行
- 具体炸弹效果委托：~100 行
- Clone 支持：~20 行

**判定：**
构造函数膨胀是 DI 容器问题，非类本身职责过重。
核心逻辑（炸弹激活路由）仅 ~100 行。保留现状。

---

## 附录：`LevelConfig.Grid` 重命名建议

**现状：** `LevelConfig.Grid` 属性类型为 `ElementType[]`，存储初始元素布局。
命名 `Grid` 暗示二维网格结构，实际是扁平数组，且语义上是"初始元素配置"而非"网格"。

**建议重命名：** `Grid` → `InitialElements`

**影响范围：** 10+ 文件（Config 解析、测试、序列化、编辑器工具），属于中等风险重命名。

**决策：** 本批次不执行。记录在此供后续 sprint 评估。如执行，需：
1. 全局替换 `LevelConfig.Grid` → `LevelConfig.InitialElements`
2. JSON 配置文件兼容处理（`[JsonPropertyName("Grid")]` 或迁移脚本）
3. 更新 ConfigParser 和序列化测试
