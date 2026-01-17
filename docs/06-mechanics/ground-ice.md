# Ice 冰块

## 概述

Ice（冰块）是 Ground 层级障碍物，位于 Tile 下方。玩家需要在冰块上方消除方块来打碎冰块。

## 属性

| 属性 | 值 |
|------|-----|
| GroundType | `Ice` |
| HP 范围 | 1 - 3 |
| 默认 HP | 1 |
| 编辑器支持 | ✅ 可编辑 HP |

## 规则

### 消除

- **触发**：冰块上方的 Tile 被消除时（匹配、爆炸等）
- **伤害**：HP - 1
- **摧毁**：HP = 0 时移除，发送 `GroundDestroyedEvent`

### 无特殊行为

- 不影响匹配检测
- 不阻挡 Tile 重力下落
- 不阻止玩家交换

## 层级

| HP | 所需消除次数 | 视觉 |
|----|-------------|------|
| 1 | 1 | 浅色冰块 |
| 2 | 2 | 中等冰块，有裂纹 |
| 3 | 3 | 深色冰块，完整 |

## 代码位置

| 文件 | 说明 |
|------|------|
| `src/Match3.Core/Models/Enums/GroundType.cs` | 枚举定义 |
| `src/Match3.Core/Models/Grid/Ground.cs` | 数据结构 |
| `src/Match3.Core/Systems/Layers/GroundSystem.cs` | 消除逻辑 |

## 相关文档

- [Ground/Cover 层级设计](../04-adr/adr-007-ground-cover-layer.md)
- [Honey 蜂蜜](./ground-honey.md)
