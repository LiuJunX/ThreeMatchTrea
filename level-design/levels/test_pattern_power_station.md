# Pattern: Power Station — 发电站

## 速览

| 项目 | 值 |
|------|-----|
| 体验目标 | 玩家发现 MagicHat 在正常消除中"免费"产出 Diamond |
| 体验模式 | 协同增效型 |
| 棋盘 | 9×9，81 格（含 Sink 行） |
| 步数 | 30 |
| 颜色 | 4 色 |
| 元素 | MagicHat ×2（中心列对称） |
| 目标 | 收集 1 Diamond + 15 红 |
| 分析胜率 | Population 50.4%（Medium） |
| 新概念 | MagicHat 累积邻接消除 → 产出 Diamond |

---

## 设计意图

### 核心发现

MagicHat 累积 3 次邻接消除后产出 Diamond。放在棋盘中心（4 个邻接 Slot 全开）= 正常消除就会自然触发。玩家不需要"刻意喂"MagicHat，只需在附近正常游玩。

**Aha! 时刻**: "我一直在旁边消除，MagicHat 自己就冒出 Diamond 了！"

### 情感弧线

```
开局（1-8步）：正常消除红色，推进收集目标 → 胜任感
中盘（9-15步）：MagicHat 旁边的消除累积到 3 次 → Diamond 出现 → 惊喜
转折（~第12步）：看到 Diamond 产出 → 理解 MagicHat 机制 → 认知升级
收尾（16-30步）：可能主动在 MagicHat 旁消除加速产出 → 效率感
```

### 心理学分析

**免费获得感（核心）**

Diamond 的产出不需要额外操作成本——玩家在推进红色收集目标时，邻接的消除"顺便"给 MagicHat 充能。
这种"一个行为两个收益"的感觉类似于"意外奖励"，触发正向情绪。

**渐进理解**

MagicHat 的累积机制（3 次 → 产出）需要多次观察才能理解。
30 步的宽裕时间允许玩家自然观察 2-3 次产出周期，逐步建立心智模型。

### 新手 vs 老手

- **新手**：正常消除红色 → MagicHat 自动产出 Diamond → "哦，这个帽子会变东西"
- **老手**：识别 MagicHat 后主动在旁边集中消除 → 高效触发 → 提前完成

---

## 棋盘设计

```
S  S  S  S  S  S  S  S  S     ← Row 0: Spawner
·  ·  ·  ·  ·  ·  ·  ·  ·     ← Row 1-2: 正常区
·  ·  ·  ·  ·  ·  ·  ·  ·
·  ·  ·  ·  M  ·  ·  ·  ·     ← Row 3: MagicHat #1 在中心列
·  ·  ·  ·  ·  ·  ·  ·  ·     ← Row 4: 间隔行
·  ·  ·  ·  M  ·  ·  ·  ·     ← Row 5: MagicHat #2 在中心列
·  ·  ·  ·  ·  ·  ·  ·  ·     ← Row 6-7: 正常区
·  ·  ·  ·  ·  ·  ·  ·  ·
K  K  K  K  K  K  K  K  K     ← Row 8: Sink（Diamond 收集）
```

### 布局逻辑

- MagicHat 在中心列 col 4：4 个方向都有空 Slot = 最高触发效率
- 两个 MagicHat 垂直间隔（Row 3 和 Row 5）：覆盖棋盘上下区域
- Sink 行在底部：Diamond 产出后自然下落到 Sink 完成收集
- 9×9 大棋盘：充足空间保证消除密度

### 进阶版预想

加入 Safe 与 MagicHat 邻接放置 → "打 Safe 时同时给 MagicHat 充能" = 同向双目标的高级形态。
在基础版（本关）验证 MagicHat 产出链后，进阶版才有设计基础。

---

## 关键决策记录

1. **Diamond 目标只要 1 个**：MagicHat 需要 3 次邻接消除才产出 1 个 Diamond，链条间接。目标设为 1 是为了让随机 AI 也能通关验证机制
2. **移除 Safe（v1 有 Safe 邻接）**：v1 的 MagicHat+Safe 双障碍阻断 tile 流动导致 0% 胜率。先单独验证 MagicHat，Safe 版本作为进阶
3. **Sink 行设计**：Diamond 是 collectible 类型，需要到达 Sink 才算收集。9×9 底行 Sink 保证 Diamond 无论从哪个 MagicHat 产出都能自然落到 Sink

---

## 修改指南

- **动这里会影响体验**：MagicHat 位置必须有 3-4 个自由邻接 Slot，否则触发率骤降
- **安全调参区间**：步数 25-35 可调。Diamond 目标可增至 2-3（增加后胜率显著下降）
- **已知边界**：Diamond 收集依赖 Sink 行。无 Sink 时 Diamond 堆积在底部不计入目标
- **进阶路径**：MagicHat + Safe 邻接 → MagicHat + Mailbox 双生产 → MagicHat 作为 Boss 关组件

---

## 分析数据

| 指标 | 值 |
|------|-----|
| 样本数 | 500 次模拟 |
| Population 胜率 | 50.4% |
| 难度评级 | Medium |
| 死锁率 | 0.0% |
| 平均步数使用率 | 70%（21/30） |
| 平均剩余步数 | 10.1 |
| 失败目标完成度 | 62.7% |
