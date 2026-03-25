# Cupboard 橱柜 — 关卡设计指南

## 关卡设计价值

Cupboard 是"打碎后释放收集物"的容器——2 阶段 HP，死亡释放 Plate：
- **收集物生产**：Plate 目标的唯一来源
- **双步挑战**：先打碎 Cupboard(2 hit)，再收集释放的 Plate
- **位置规划**：Plate 在 Cupboard 原位生成，位置可预判

## 属性速查

| 属性 | 值 |
|------|-----|
| 最大 HP | 2 |
| 消除方式 | 任何消除源（匹配、炸弹、相邻消除） |
| 死亡效果 | 在原位释放 Plate 收集物 |

## 放置原则

### 宜

- **可达位置**：玩家能消除旁边方块触发伤害
- **不在 Spawner 行**：释放的 Plate 需要能参与后续匹配/收集
- **配合 Plate 收集目标**：有 Cupboard 才有 Plate
- **数量 = Plate 目标数**：每个 Cupboard 产出 1 个 Plate

### 忌

- **没有 Plate 目标**：释放的 Plate 无意义
- **被完全包围**：无法触发伤害
- **Plate 目标数 > Cupboard 数量**：不可能完成

## 组合建议

| 搭配元素 | 效果 | 推荐度 |
|----------|------|--------|
| Plate 收集目标 | 必须组合 | ★★★ |
| Box | 好：先清周围 Box，再打 Cupboard | ★★☆ |
| Ice（下方） | 好：Plate 释放后落下破 Ice | ★★☆ |

## 常见设计错误

1. **Plate 目标但没放 Cupboard** → 无法产出 Plate
2. **Cupboard 放在死角** → 无法击打
3. **忘记 Cupboard 占格子（Obstacle 层）** → 不能同时放 Tile
