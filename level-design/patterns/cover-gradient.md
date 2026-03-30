# Cover 难度梯度（Cover Difficulty Gradient）

## 核心发现

4 种 Cover 的代码行为差异形成了一条清晰的难度阶梯。这条阶梯决定了 Cover 系统的引入顺序和组合策略。

## 难度阶梯

```
Frost ──→ Chain ──→ Cage ──→ Honey
(最易)                        (最难)
```

| Cover | 允许匹配 | 邻接伤害 | 破解方式 | 特性 |
|-------|---------|---------|---------|------|
| **Frost** | ✅ | ✅ (Match/ColorBomb) | 匹配附近即可 | 多米诺连锁瓦解 |
| **Chain** | ✅ | ❌ | 消除下方 tile | 稳定、无连锁 |
| **Cage** | ❌ | ❌ | 炸弹直接命中 | 需要造炸弹 |
| **Honey** | ❌ | ✅ (仅 Match/ColorBomb) | 旁边匹配消除 | **炸弹完全无效** |

### 为什么 Honey 比 Cage 更难

直觉上 Cage（阻止匹配 + 无邻接）应该最难。但实际上：
- Cage 可以被任何炸弹（Rocket、Square5x5、UFO）一击即碎
- Honey 的邻接伤害**只响应 Match/ColorBomb，不响应 Bomb** — 炸弹在 Honey 旁边爆炸 → Honey 纹丝不动
- 这意味着 Honey 的"合法攻击手段"更窄：必须在 Honey 旁边**匹配消除**，不能靠炸弹

### Bubble（特殊）

Bubble 不在主梯度上 — 它是唯一动态 Cover（跟随 tile 下落），允许匹配但阻止交换。
- 难度类似 Chain（允许匹配）
- 独特价值：移动性创造位置预判挑战

**⚠️ 已知问题**：Bubble 移动到有 Cage/Honey/Chain 的 cell 时，`SetCover()` 会覆写已有 Cover → 静态 Cover 静默消失。设计时避免 Bubble 和静态 Cover 在可接触区域共存。

## Frost Cascade 效应

Frost 场（大面积 Frost）会产生"多米诺瓦解"：

```
一次匹配 → Frost 邻接伤害 → 周围 Frost 破碎 → 暴露新 tile → 新匹配 → 更多 Frost 碎
```

验证数据：18 Frost（6×3 块）平均 8 步清完，91.8% 胜率。

**设计用途**：
- Cover 系统首次出场（Frost 最宽容，首次体验不会卡关）
- 多幕关卡的 Act 1（信心建设 → 然后引入更难的 Cover）
- "解冻"视觉主题的关卡

**不适合**：挑战关卡（太容易瓦解）

## Cover 引入建议顺序

| 阶段 | 引入 Cover | 关卡示例 |
|------|-----------|---------|
| L13-15 | **Frost** | Frost 场作为首次 Cover 体验，多米诺瓦解带来惊喜 |
| L16-18 | **Chain** | Chain 对比 Frost — "这个不会连锁碎？必须消掉下面的！" |
| L19-22 | **Cage** | Cage 对比 Chain — "这个连匹配都不行？只能用炸弹！" |
| L23-25 | **Honey** | Honey 对比 Cage — "炸弹也不行？？只能旁边匹配！" |

每次引入都是前一种的"升级版"，自然形成发现阶梯。

## Cover 组合设计

| 组合 | 体验 | 用途 |
|------|------|------|
| Frost + Chain | 对比教学："为什么这个碎了那个没碎？" | 机制理解关 |
| Cage + Honey | 双重封锁：Cage 需要炸弹 + Honey 需要匹配 | 高难度关卡 |
| Frost 外环 + Cage 内核 | 剥洋葱：先融 Frost → 暴露 Cage → 造炸弹打 Cage | 层层揭开变体 |

## 验证关卡

- `test_pattern_frost_cascade.json` — Frost 场瓦解（分析 91.8%，VeryEasy）
- `test_pattern_honey_fortress.json` — Honey 环保护内核（分析 82.2%，Easy）
