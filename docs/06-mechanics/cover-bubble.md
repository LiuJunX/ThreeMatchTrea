# Bubble 气泡

## 概述

Bubble（气泡）是 Cover 层级障碍物，位于 Tile 上方。气泡是唯一的**动态 Cover**，会跟随方块一起移动。

## 属性

| 属性 | 值 |
|------|-----|
| CoverType | `Bubble` |
| 默认 HP | 1 |
| 阻止匹配 | ✗ |
| 阻止交换 | ✓ |
| 阻止移动 | ✗ |
| 动态 | ✓ |

## 规则

### 消除

- **触发**：气泡内的方块被匹配消除时
- **伤害**：HP - 1
- **摧毁**：HP = 0 时移除，发送 `CoverDestroyedEvent`

### 行为特性

- **允许匹配**：气泡内的方块可以参与匹配检测
- **阻止交换**：玩家无法选择或交换气泡内的方块
- **允许移动**：气泡内的方块受重力影响正常下落
- **动态跟随**：气泡会跟随方块一起移动（通过 `SyncDynamicCovers` 实现）

### 与静态 Cover 的区别

| 特性 | Cage/Chain | Bubble |
|------|------------|--------|
| 移动 | ✗ 固定 | ✓ 跟随 |
| 位置 | 绑定格子 | 绑定方块 |

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
- [Chain 锁链](./cover-chain.md)
