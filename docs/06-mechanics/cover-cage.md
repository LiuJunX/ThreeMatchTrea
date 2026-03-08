# Cage 笼子

## 概述

Cage（笼子）是 Cover 层级障碍物，位于 Tile 上方。笼子完全封锁方块，阻止匹配、交换和移动。

## 属性

| 属性 | 值 |
|------|-----|
| CoverType | `Cage` |
| 默认 HP | 1 |
| 阻止匹配 | ✓ |
| 阻止交换 | ✓ |
| 阻止移动 | ✓ |
| 动态 | ✗（静态） |

## 规则

### 消除

- **触发**：笼子内的方块被消除时（爆炸波及等）
- **伤害**：HP - 1
- **摧毁**：HP = 0 时移除，发送 `CoverDestroyedEvent`

### 行为特性

- **阻止匹配**：笼子内的方块不参与匹配检测
- **阻止交换**：玩家无法选择或交换笼子内的方块
- **阻止移动**：笼子内的方块不受重力影响，固定在原位

## 代码位置

| 文件 | 说明 |
|------|------|
| `src/Match3.Core/Models/Enums/CoverType.cs` | 枚举定义 |
| `src/Match3.Core/Models/Grid/Cover.cs` | 数据结构 |
| `src/Match3.Core/Models/Grid/CoverRules.cs` | 规则查询 |
| `src/Match3.Core/Systems/Layers/CoverSystem.cs` | 消除逻辑 |

## 相关文档

- [Ground/Cover 层级设计](../04-adr/0007-ground-cover-layer.md)
- [Chain 锁链](./cover-chain.md)
- [Bubble 气泡](./cover-bubble.md)
