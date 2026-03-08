# Chain 锁链

## 概述

Chain（锁链）是 Cover 层级障碍物，位于 Tile 上方。锁链允许方块参与匹配，但阻止玩家交换和重力移动。

## 属性

| 属性 | 值 |
|------|-----|
| CoverType | `Chain` |
| 默认 HP | 1 |
| 阻止匹配 | ✗ |
| 阻止交换 | ✓ |
| 阻止移动 | ✓ |
| 动态 | ✗（静态） |

## 规则

### 消除

- **触发**：锁链内的方块被匹配消除时
- **伤害**：HP - 1
- **摧毁**：HP = 0 时移除，发送 `CoverDestroyedEvent`

### 行为特性

- **允许匹配**：锁链内的方块可以参与匹配检测
- **阻止交换**：玩家无法选择或交换锁链内的方块
- **阻止移动**：锁链内的方块不受重力影响，固定在原位

### 与 Cage 的区别

| 特性 | Cage | Chain |
|------|------|-------|
| 匹配 | ✗ 阻止 | ✓ 允许 |
| 消除方式 | 仅爆炸波及 | 匹配或爆炸 |

## 代码位置

| 文件 | 说明 |
|------|------|
| `src/Match3.Core/Models/Enums/CoverType.cs` | 枚举定义 |
| `src/Match3.Core/Models/Grid/Cover.cs` | 数据结构 |
| `src/Match3.Core/Models/Grid/CoverRules.cs` | 规则查询 |
| `src/Match3.Core/Systems/Layers/CoverSystem.cs` | 消除逻辑 |

## 相关文档

- [Ground/Cover 层级设计](../04-adr/0007-ground-cover-layer.md)
- [Cage 笼子](./cover-cage.md)
- [Bubble 气泡](./cover-bubble.md)
