# Leaves 落叶 — 关卡设计指南

## 关卡设计价值

Leaves 与 Grass 类似，由 Flowerpot 死亡效果大面积产出：
- **大面积覆盖**：Flowerpot 的 3×3 扩散比 Bush 的十字形更广
- **后期目标**：在 Advanced 阶段引入，作为 Ground 目标的变体
- **视觉区分**：与 Grass 不同的视觉效果

## 属性速查

| 属性 | 值 |
|------|-----|
| HP | 1（蓝图中可配更高） |
| 消除方式 | 上方 Tile 被消除时扣 HP |
| 来源 | Flowerpot 死亡（3×3 区域）/ 关卡初始配置 |

## 放置原则

与 Grass 相同的基本规则，区别在于来源：
- **Grass 来源**：Bush（十字 4 格）
- **Leaves 来源**：Flowerpot（3×3 共 9 格）

### 宜

- **配合 Flowerpot**：Flowerpot 是 Leaves 的主要生产源
- **预放时成片**：与 Grass 相同的视觉区域设计
- **目标数量合理**：每个 Flowerpot 最多产 9 格 Leaves

### 忌

- **与 Grass 同关大量混用**：视觉上容易混淆
- **同格放 Ice 或 Grass**：同一格只有一个 Ground

## 组合建议

| 搭配元素 | 效果 | 推荐度 |
|----------|------|--------|
| Flowerpot + Leaves 目标 | 核心组合 | ★★★ |
| Bush + Grass 目标 | 不同区域双 Ground 目标 | ★★☆ |
| Ice | 不能：同格互斥 | ✗ |

## 常见设计错误

1. **没有 Flowerpot 但有 Leaves 目标（也没有预放）** → 无法产出
2. **Leaves + Grass 同格** → 不可能，Ground 层互斥
3. **Flowerpot 被困无法被清除** → Leaves 无法产出
