# 产出链（Production Chain）

## 体验目标

"打碎东西之后居然长出了新东西！"——破坏→创造的转化感。消除行为不仅清除障碍，还通过死亡效果或生成器产出新元素，这些产物本身成为下一步的目标。

## 核心原理

三消中的"产出"有两条路径：

| 路径 | 触发方式 | 产物 | 目标性 |
|------|---------|------|-------|
| **死亡效果** | 障碍 HP 归零时触发 | Grass（Bush）、Leaves（Flowerpot）、Plate（Cupboard）、Pearl（Oyster） | 产物即目标 |
| **生成器** | 邻接消除累积/即时 | Diamond（MagicHat，3 次累积）、Envelope（Mailbox，即时） | 产物需收集 |

产出链的核心体验：**一次消除行为产生多层后果**。

## 组合模式

### 决堤花园型（Dam Break + Death Effect）

```
·  ·  ·  ·  ·  ·  ·  ·     ← 主活动区
■  ♣  ■  ♣  ♣  ■  ♣  ■     ← 坝行：Box ■ + Bush ♣ 交替
E  E  E  E  E  E  E  E     ← KeepEmpty（等待填充）
E  E  E  E  E  E  E  E
```

**三层后果叠加**：
1. 破坝 → tiles 涌入空区（空间释放）
2. Bush 死亡 → Grass 在空区生长（产物生成）
3. Grass 成为收集目标（新目标层）

关键数值：
- 4 Bush × 4 方向 = 最多 16 格 Grass（实际 8-12 格，受空间竞争）
- Grass 目标 ≤ 实际产出的 60-75%（留容错）
- KeepEmpty ≤ 2 行（多了死锁率飙升：4 行 = 28%，2 行 = 18%）

### 发电站型（Generator + Aligned Objective）

```
·  ·  ·  ·  ·  ·  ·  ·
·  ·  ·  M  ·  ·  ·  ·     ← MagicHat 在中心（4 邻接全开）
·  ·  ·  ·  ·  ·  ·  ·
·  ·  ·  M  ·  ·  ·  ·     ← 第二个 MagicHat
·  ·  ·  ·  ·  ·  ·  ·
K  K  K  K  K  K  K  K     ← Sink 行收集 Diamond
```

**持续产出**：
- MagicHat 每 3 次邻接消除产出 1 Diamond
- 中心放置 = 自然消除就能触发（不需要刻意喂）
- Diamond 下落到 Sink 完成收集

关键数值：
- MagicHat 需要 4 个自由邻接 Slot → 只放在中心区域
- 30 步内 2 个 MagicHat 约产出 4-8 Diamond
- Diamond 目标设得保守（1-2 个），因为产出链间接

### 进阶型：生成器 + bomb-only 障碍

```
·  ·  ·  M  ■  ·  ·  ·     ← MagicHat 和 Safe 邻接
```

**协同增效**：玩家为了打 Safe 在其旁边反复消除 → 同时给邻接的 MagicHat 充能。
一个行为，两个收益。这是"同向双目标"的高级形态——不是目标本身同向，而是**达成手段**同向。

## 阶段感

### 决堤花园型

1. **蓄力**：上区正常消除（掌控）
2. **释放**：坝碎裂 → tiles 涌入 + Bush 死亡（双重惊喜）
3. **Grass 生长**：预期违背——不只是空间释放，还有东西长出来
4. **新目标**：收集 Grass（有序收尾）

### 发电站型

1. **正常游玩**：消除推进 tile 收集目标（胜任感）
2. **产出惊喜**：MagicHat 冒出 Diamond → "这是什么？"
3. **理解机制**：第二次产出后理解"旁边消除 = 充能"
4. **主动利用**：集中在 MagicHat 旁消除（效率感）

## 心理学对应

- **因果反转**：破坏 → 创造的意外转化，正向情绪加倍
- **免费获得感**：产出不需要额外操作成本——正常消除"顺便"触发
- **渐进理解**：生成器的累积机制需要多次观察才能理解，宽裕步数允许自然学习

## 适用场景

- **决堤花园型**：L10-15，Bush 首次出场后，与 Dam Break 结合
- **发电站型**：L20+，MagicHat/Mailbox 首次出场或 Diamond/Envelope 收集关
- **协同增效型**：L25+，生成器 + bomb-only 障碍的高级组合

## 产出量计算速查

| 产出源 | 产出方式 | 单次产出 | 30 步预估 |
|--------|---------|---------|----------|
| Bush | 死亡 → 4 方向 Grass | 4 格 | n × 4（n = Bush 数，一次性） |
| Flowerpot | 死亡 → 3×3 Grass/Leaves | 最多 9 格 | n × 9（受空间限制） |
| Cupboard | 死亡 → 1 Plate | 1 个 | n × 1（n = Cupboard 数） |
| Oyster | 死亡 → 1 Pearl | 1 个 | n × 1（移动位置不确定） |
| MagicHat | 每 3 次邻接消除 | 1 Diamond | 2-4 个/MagicHat |
| Mailbox | 每次邻接消除 | 1 Envelope | 8-15 个/Mailbox |

目标数 ≤ 预估产出 × 0.6-0.75（留容错）。

## 验证关卡

- `test_pattern_dam_garden.json` — 决堤花园（分析 30.6%，Hard，死锁 17.6%）
- `test_pattern_power_station.json` — 发电站（分析 50.4%，Medium）
