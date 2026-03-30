# 竞品早期关卡分析 (Level 1-50)

> 本文档汇编 Royal Match / Candy Crush Saga / Toon Blast / Toy Blast 前 50 关的元素引入节奏、步数、
> 棋盘设计、目标数量、难度爬坡等数据，供关卡设计参考。
>
> 数据来源：公开 Wiki、Medium 分析文章、walkthrough 网站、官方帮助中心。
> 因各游戏持续更新（Candy Crush 经历 2021 大改版），部分关卡细节可能与当前版本有出入。

---

## 1. Royal Match

### 1.1 核心机制

- **类型**: 传统 Match-3（交换消除）
- **棋盘**: 标准方形/矩形网格，后期引入不规则形状（菱形、H 形、分割区域）
- **颜色数**: 4-5 色
- **Power-up 生成**:
  - 4 连直线 → Rocket（清一行/列）
  - 4 连方块 → Propeller（随机清一个目标元素，起飞时清周围）
  - 5 连 L/T 形 → TNT（2 格半径爆炸）
  - 5 连直线 → Light Ball（清同色全部）

### 1.2 元素引入时间线

**关键发现**: 新障碍引入节奏为 "5 → 9 → 13 → 21 → 31 → 41 → 51"，间隔为 4, 4, 8, 10, 10, 10。前期密集，后期放缓。

| 关卡范围 | 引入内容 | 说明 |
|---------|---------|------|
| L1-3 | **Box**（箱子） | 相邻消除即破，单层。最基础障碍 |
| L4-6 | **Grass**（草地） | 在其上消除即清。教会"在特定位置消除" |
| ~L5 | Box（深化） | Box 作为主要障碍持续出现 |
| L8 | 首个 **Booster** 引入 | 通过关卡奖励或教学解锁 |
| ~L9 | Grass（深化） | Grass 需 2 次消除清除 |
| ~L13 | **Cupboard**（橱柜） | 需 8 次相邻消除才能全清。首个多层/长期障碍 |
| L14 | 第二个 Booster | |
| L17 | 第三个 Booster | |
| L19 | 第四个 Booster | |
| ~L21 | **Mailbox**（邮箱） | 相邻消除产出信件，收集类目标。首个"生产型"障碍 |
| ~L31 | **Chain**（锁链） | 只能用 power-up 击破，释放下方物品。首个"必须用 power-up"的障碍 |
| ~L41 | **Royal Egg**（皇家蛋） | 单次消除即清。引入时刻意降低难度让玩家熟悉 |
| ~L51 | 新一轮障碍 | 继续 10 关一个新障碍的节奏 |

### 1.3 步数与难度

| 关卡范围 | 步数 | 难度特征 |
|---------|------|---------|
| L1-20 | 30-38 步 | 非常宽裕，几乎不可能失败 |
| L21-60 | 25-30 步 | 开始收紧 |
| L61-100 | 23-27 步 | 进一步收紧 |

### 1.4 难度曲线模式

采用 **5-10 关难度周期**:
- 每个周期的第 1 关刻意简单（存信任）
- 难度逐渐攀升，第 5 关通常是周期内最难
- 新障碍引入关刻意降低难度，只出现新障碍本身，不与旧障碍混合
- 熟悉后 5 关密集训练，然后插入 2-3 关缓冲，再继续强化

### 1.5 目标设计

- 早期几乎全部是"清除棋盘上所有障碍"（full board clear）
- 单关通常 1 个主目标（如"清除所有 Box"或"清除所有 Grass"）
- 前 50 关中仅出现过 1 次同时 4 种障碍的情况
- 早期以 2 种障碍组合为主，后期增至 3 种

### 1.6 设计特点

- **速度极快**: 动画速度是同类最快之一，cascade 期间允许并发消除
- **Power-up 极慷慨**: TNT 爆炸范围比竞品大，Propeller 智能重定向
- **"帮你"的感觉**: 无匹配时不强制使用 power-up（竞品常见强制消耗）
- **每 10 关一组装饰区域**: L1, L10, L21, L41, L61, L81 开启新区域

---

## 2. Candy Crush Saga

### 2.1 核心机制

- **类型**: 传统 Match-3（交换消除）
- **棋盘**: 始终适配 9x9 矩阵，但实际可用区域各关不同
- **颜色数**: 通常 5-6 色
- **Special Candy 生成**:
  - 4 连 → Striped Candy（清一行/列）
  - L/T 形 → Wrapped Candy（爆炸两次）
  - 5 连 → Color Bomb（清同色全部）
  - Fish（后期引入）→ 自动攻击 3 个目标

### 2.2 元素引入时间线

**Episode 1: Candy Town (L1-10)** — 全部 Very Easy

| 关卡 | 类型 | 关键引入 |
|------|------|---------|
| L1 | Candy Order | 教学：基础消除。首个关卡类型 |
| L2 | Candy Order | **1 层 Frosting（糖霜）** 首次出现 |
| L3 | Candy Order | 教学：Striped Candy 制作 |
| L5 | Candy Order | 教学：Wrapped Candy 制作 |
| L7 | Candy Order | 教学：Color Bomb 制作 |
| L8 | **Jelly** | **Jelly 关卡类型**首次出现 |
| L9 | Jelly | **Double Jelly + 多层 Frosting** 首次出现 |
| L10 | Jelly | **2-3 层 Frosting** 首次出现。本 Episode 最难关 |

**Episode 2: Candy Factory (L11-20)** — 全部 Very Easy

| 关卡 | 类型 | 关键引入 |
|------|------|---------|
| L11 | Ingredients | **Bubblegum Pop（1 层）** 首次出现 |
| L12 | - | **2-3 层 Bubblegum Pop** |
| L13 | - | **Teleporter（传送门）** 首次出现 |
| L14 | - | **Wrapped Candy Cannon** 首次出现 |
| L15 | - | **Marmalade（果酱锁）** 首次出现 |
| L17 | **Ingredients** | **Ingredients 关卡类型**首次出现（带樱桃/榛子下落） |
| L18 | Ingredients | 首个带 Frosting 的 Ingredients 关 |
| L20 | **Timed** | **Timed 关卡类型**首次出现（已于 2021 年移除） |

**Episode 3: Lemonade Lake (L21-30)** — 难度开始上升

| 关卡 | 类型 | 关键引入 |
|------|------|---------|
| L21 | Jelly+Blocker | **Icing（糖衣/冰块）** 首次作为 blocker 出现 |
| L23 | - | "可以说是第一个真正困难的关卡" |
| L25 | Jelly+Blocker | **Licorice Lock（甘草锁）** 首次出现。首个有 2 种 blocker 的关卡 |
| L28 | Jelly | 22 步，12 个 Jelly 含 2 个隐藏 |
| L50 步 | - | L25 给到 50 步来学习新 blocker |

**Episode 4: Chocolate Mountains (L31-50)**

| 关卡 | 类型 | 关键引入 |
|------|------|---------|
| L36 | Jelly | **Licorice Swirl** 正式引入 |
| L40 | Jelly | 30 步，阶梯形 Lock 设计 |
| L43 | Timed | 本 Episode 首个 Timed 关 |
| L50 | Jelly | "Chocolate Mountains 最难关"，需要 Striped+Color Bomb |

**Episode 5: Minty Meadow (L51-65)**

| 关卡 | 关键引入 |
|------|---------|
| L51 | **Chocolate（巧克力）** 首次出现。会自动扩散！ |
| L52 | 首个有 3 种 blocker 的关卡（Chocolate + Lock + Jelly） |

**Episode 6: Easter Bunny Hills (L66-80)**

| 关卡 | 关键引入 |
|------|---------|
| L66 | **Teleporter** 开始影响 Ingredients 玩法 |

### 2.3 步数

- Episode 1-2 (L1-20): 步数宽裕，几乎无法失败
- L17: 19 步（少数早期紧张关）
- L25: 50 步（引入新 blocker 时给大量步数）
- L28: 22 步
- L40: 30 步
- L55: 15 步（"真正的beast"）

### 2.4 难度曲线模式

- L1-20：纯教学，全部 Very Easy（平均难度 1/10）
- L21-30：开始上升，L23 是"第一个难关"
- L31-50：Licorice 系列让难度持续攀升
- L51+：Chocolate 扩散机制大幅提升难度
- 关键设计：**新元素引入时给充足步数**（如 L25 的 50 步），让玩家安全学习

### 2.5 设计特点

- **5 种关卡类型**轮换（现为 4 种，Timed 已移除），提供玩法多样性
- **Special Candy 教学极其显式**: L3 教 Striped, L5 教 Wrapped, L7 教 Color Bomb
- **每 15 关一个 Episode**，每个 Episode 有叙事主题
- **Blocker 组合是核心难度杠杆**: 从 1 种到 2 种到 3 种渐进
- **250M 月活用户**验证了这套渐进设计

---

## 3. Toon Blast

### 3.1 核心机制

- **类型**: Collapse（点击消除，非交换）
- **棋盘**: 早期为标准矩形网格，L10 后出现轻微变形（倒三角等）
- **颜色数**: 早期 3-4 色，随关卡增加
- **Power-up 生成**:
  - 5 连同色 → Rocket（清一行/列）
  - 7 连同色 → Bomb（清 8 格半径）
  - 9 连同色 → Disco Ball（清全部同色）

### 3.2 元素引入时间线

**Episode 1: Toon Trip (L1-20)** — 教学+核心机制建立

| 关卡 | 引入内容 | 说明 |
|------|---------|------|
| L1 | **Balloon（气球）** | 72 个气球 / 36 步。首个障碍。相邻消除即破 |
| L4 | **Crate（木箱）** | 需要 1-3 次相邻消除（分层） |
| L7 | **Pre-level Boosters** 解锁 | Rocket/Bomb/Disco Ball 可在开局前放置。同时引入 **2 目标** |
| L9 | **Extreme & Lock Crate** | 强化版木箱。同时解锁 **Hammer** booster（关卡结束奖励） |
| L11 | **Purple Cubes + Bubble** | 新颜色 + 新障碍。Bubble 需直接在其上消除。同时解锁 **Hammer** |
| L12 | **Boxing Glove** booster | 关卡结束奖励解锁 |
| L17 | **Anvil** booster | |
| L19 | **Dice** booster | 打乱棋盘所有方块 |

**Episode 2+ (L21-50)** — 更多障碍，根据分析文章:

| 关卡 | 引入内容 | 说明 |
|------|---------|------|
| ~L14 | 新障碍层 | 障碍间隔 ~L11, L14, L21, L31... |
| ~L21 | **Light Bulb** | 需 2 次相邻消除（先点亮再消除） |
| ~L31 | **Can Toss** | 需 9 次相邻消除。首个"耐久型"障碍 |
| ~L31+ | **Magic Hat** | 每次相邻消除产出 1 个兔子（收集目标） |
| ~L40+ | **Soap** | 先变成 Bubble，再需直接消除 |
| ~L40+ | **Rubber Duck** | 需在其下方消除使其落到底部 |

### 3.3 步数

- L1: 36 步（72 个气球，极其宽裕）
- 随关卡推进步数逐渐收紧
- 步数限制是主要难度调节手段

### 3.4 难度曲线模式

采用 **10 关难度周期**:
- L1-5: 难度逐渐上升，L5 为周期内高峰（标记为 "Hard"）
- L6-8: 短暂缓和
- L9-10: 再次攀升，L10 为周期最高峰（标记为 "Super Hard"）
- 偶尔会有"无标记难关"打破规律

### 3.5 教学方式

- **两步教学法**: 新障碍首次出现时:
  1. 动画手指图标 + 简短文字说明
  2. 紧接着让玩家实际操作
- 引入新障碍的关卡**只包含该新障碍**，不与旧障碍混合
- 引入关给充裕步数

### 3.6 设计特点

- **20 关一个 Chapter**，每个 Chapter 有主题（丛林、深海、沙漠等）
- **Star Chest 在 L15 解锁**，1-14 关的星星无实际意义
- **Collapse 机制**比 Match-3 更适合大面积消除，power-up 更容易生成
- **点击操作**比交换更低门槛（无"无效操作"挫败感）

---

## 4. Toy Blast

### 4.1 核心机制

- **类型**: Collapse（点击消除，与 Toon Blast 同为 Peak Games 出品）
- **棋盘**: 标准矩形网格，早期变化小
- **颜色数**: 早期 3-4 色
- **Power-up 生成**:
  - 5-6 连同色 → Rotor（清一行/列）
  - 7-8 连同色 → TNT（清周围 8 格）
  - 9+ 连同色 → Rubik's Cube（清全部同色）

### 4.2 元素引入时间线

**Episode 1: Playful Pastures (L1-15)**

| 关卡 | 引入内容 | 说明 |
|------|---------|------|
| L1 | 基础彩色方块 | 点击 2+ 同色消除。极简教学，几乎不可能失败 |
| ~L5-8 | **Wooden Box** | 相邻消除即破 |
| L15 | **Beach Ball** | 56 个 Beach Ball。相邻消除即清 |

**Episode 2: Sunshine Shores (L16-30)**

| 关卡 | 引入内容 | 说明 |
|------|---------|------|
| L16-20 | **Bubble** | 需在其下方消除使泡泡破裂 |
| ~L20+ | **White Lego** | 单次相邻消除即破 |

**Episode 3+ (L31-50)**

| 关卡 | 引入内容 | 说明 |
|------|---------|------|
| ~L31+ | **Colored Lego（多层）** | 需多次消除 |
| ~L31+ | **Grate** | 多次消除型障碍 |
| ~L40+ | **Easter Egg / UFO** | 进阶多层障碍 |

### 4.3 步数与难度

- L1: 极其宽裕，设计为不可失败
- 早期关卡步数充裕，目标简单
- 难度通过增加障碍层数和减少步数实现

### 4.4 设计特点

- **每 15 关一个 Episode**（前 12 个 Episode），之后 20 关一个 Episode
- **3 星系统**: 清除越多方块，星星越多
- **玩具主题贯穿**: 障碍都是玩具相关（积木、气球、泡泡）
- **与 Toon Blast 共享核心引擎**，但玩具主题让视觉更清晰
- **Booster 合成标记**: 可形成 booster 的组合会显示特殊标记

---

## 5. 跨游戏对比表

### 5.1 元素引入节奏对比

| 关卡范围 | Royal Match | Candy Crush Saga | Toon Blast | Toy Blast |
|---------|-------------|-----------------|------------|-----------|
| L1-3 | Box（基础障碍） | 基础消除 + Frosting | Balloon（基础障碍） | 基础方块消除 |
| L4-6 | Grass | Striped/Wrapped/Color Bomb 教学 | Crate | 基础方块消除 |
| L7-10 | 首个 Booster (L8) | Jelly 关卡类型 (L8) + 2层 Frosting (L10) | Booster 解锁 (L7) + Lock Crate (L9) | Wooden Box |
| L11-15 | 基础障碍组合深化 | Bubblegum + Teleporter + Marmalade + Ingredients 关 | Bubble + Purple Cubes (L11) + Boxing Glove (L12) | Beach Ball (L15) |
| L16-20 | Booster 全部解锁 (L14-19) | Timed 关 (L20) | Anvil (L17) + Dice (L19) | Bubble |
| L21-30 | Mailbox (~L21) | Icing (L21) + Licorice Lock (L25) | Light Bulb (~L21) | White Lego |
| L31-40 | Chain (~L31) | Licorice Swirl (L36) | Can Toss + Magic Hat (~L31) | Colored Lego + Grate |
| L41-50 | Royal Egg (~L41) | Chocolate Mountains 最难关 (L50) | Soap + Rubber Duck | Easter Egg / UFO |
| L51 | 新障碍 (~L51) | Chocolate（会扩散！） | 继续新障碍 | 继续新障碍 |

### 5.2 关键设计参数对比

| 参数 | Royal Match | Candy Crush Saga | Toon Blast | Toy Blast |
|------|-------------|-----------------|------------|-----------|
| **消除类型** | Match-3（交换） | Match-3（交换） | Collapse（点击） | Collapse（点击） |
| **早期步数** | 30-38 | 充裕（部分无限） | 36 (L1) | 极充裕 |
| **中期步数** | 25-30 | 19-50（波动大） | 逐渐收紧 | 逐渐收紧 |
| **棋盘** | 标准→不规则 | 9x9 矩阵框架 | 标准→轻微变形 | 标准矩形 |
| **关卡类型数** | 1 种（清除） | 4-5 种轮换 | 1 种（清除/收集） | 1 种（清除/收集） |
| **新障碍引入间隔** | ~10 关（前期 4 关） | ~5-8 关 | ~3-10 关（不规则） | ~10-15 关 |
| **教学方式** | 无显式教学 | 极显式（箭头+动画） | 两步法（动画+实操） | 极简（几乎无教学） |
| **前 50 关障碍总数** | ~6 种 | ~8-10 种 | ~9 种 | ~5-6 种 |
| **Episode 结构** | 10 关/区域 | 10-15 关/Episode | 20 关/Chapter | 15 关/Episode |
| **难度周期** | 5-10 关 | ~10-15 关 | 10 关 | ~15 关 |
| **Pre-placed Power-up** | 偶尔（学习目的） | 是（L7 pre-placed Striped） | 是（L7 解锁 pre-level booster） | 否 |
| **新元素引入关难度** | 刻意降低 | 给大量步数 | 只出现新障碍 | 刻意降低 |

### 5.3 Power-up 生成条件对比

| 条件 | Royal Match | Candy Crush Saga | Toon Blast | Toy Blast |
|------|-------------|-----------------|------------|-----------|
| **线性清除** | 4 连直线 → Rocket | 4 连 → Striped | 5 连 → Rocket | 5-6 连 → Rotor |
| **范围爆炸** | 5 连 L/T → TNT | L/T 形 → Wrapped | 7 连 → Bomb | 7-8 连 → TNT |
| **全色清除** | 5 连直线 → Light Ball | 5 连 → Color Bomb | 9 连 → Disco Ball | 9+ 连 → Rubik's Cube |
| **特殊** | 4 连方块 → Propeller | — | — | — |

---

## 6. 关键启示（与本项目设计哲学的关系）

### 6.1 与竞品共同点

所有 4 款游戏都遵循以下规律：
1. **新障碍引入时降低难度** — 让玩家安全学习
2. **前 10-15 关极简** — 建立基本操作信心
3. **障碍逐步组合** — 从单障碍到双障碍到三障碍
4. **步数是主要难度调节杠杆**
5. **Power-up 在前 10 关内教会**

### 6.2 本项目的差异化机会

根据 framework.md 的设计哲学，与竞品的关键差异：

1. **"体验即教学" vs 竞品的显式教学**
   - Candy Crush 用箭头+动画教每个 Special Candy
   - Toon Blast 用两步动画教学法
   - **本项目**: 不用弹窗，通过关卡结构让玩家自然学会。这是核心赌注

2. **"不做速通教学关" vs 竞品的纯教学 Episode**
   - Candy Crush L1-10 是纯教学，无挑战
   - Toon Blast L1 是 72 气球 / 36 步的送分关
   - **本项目**: L1 就是有设计的真正关卡

3. **"认知负荷通过单关元素数量控制" vs 竞品的时间线锁定**
   - 竞品在 L1-20 严格锁定可用元素
   - **本项目**: 不限制可用性，而是通过每关放置的元素数量控制认知负荷

4. **元素引入密度参考**
   - Royal Match 前 50 关引入 ~6 种障碍（最克制）
   - Candy Crush 前 50 关引入 ~8-10 种（最激进）
   - 本项目的"有限元素组合爆炸"哲学更接近 Royal Match

### 6.3 步数参考

- 竞品早期普遍给 30+ 步
- Royal Match 是最慷慨的（30-38 步/前 20 关）
- Candy Crush 在引入新元素时给超多步（如 L25 给 50 步）
- **启示**: 前 20 关步数建议不低于 25-30，确保玩家有充足空间探索

### 6.4 难度周期参考

- Royal Match: 5-10 关一个周期
- Toon Blast: 10 关一个周期
- Candy Crush: 10-15 关一个 Episode（含 easy → medium → hard → boss 节奏）
- **共性**: 每个周期第 1 关都是"存信任"关

---

## Sources

- [Game Analysis of Royal Match (Medium)](https://medium.com/@ekinmelissezer/game-analysis-for-royal-match-and-toon-blast-9c4bff8ef48b)
- [Game Analysis of Toon Blast (Medium)](https://medium.com/@ekinmelissezer/game-analysis-of-toon-blast-mechanics-level-design-difficulty-patterns-and-monetization-signals-022748ae51b4)
- [Royal Match Help Center - Game Elements](https://dreamgames.helpshift.com/hc/en/3-royal-match/section/56-game-elements-1732263700/)
- [Candy Crush Levels Guide - World One Ep 1-3](https://www.withoutthesarcasm.com/posts/candy-crush-levels-guide-world-one-episodes-1-3/)
- [Candy Crush Levels Guide - World One Ep 4-6](https://www.withoutthesarcasm.com/posts/candy-crush-levels-guide-world-one-episodes-4-6/)
- [Royal Match Breakdown Part 3 (Substack)](https://pranaysarepaka.substack.com/p/royal-match-breakdown-part-3)
- [Royal Match - Deconstructor of Fun](https://www.deconstructoroffun.com/blog/2021/3/21/royal-match-the-new-king-from-turkey)
- [Toon Blast Wiki - Toon Trip Episode](https://toonblast.fandom.com/wiki/Toon_Trip)
- [Toon Blast Wiki - Episodes](https://toonblast.fandom.com/wiki/Episodes)
- [Toy Blast Wiki - Levels](https://toyblast.fandom.com/wiki/Levels)
- [Toy Blast Wiki - Level Items](https://toyblast.fandom.com/wiki/Level_Items)
- [Playrix: Creating Levels for Match-3 (Game World Observer)](https://gameworldobserver.com/2019/09/27/playrix-levels-elements-match-3)
- [Room 8 Studio: Match-3 Level Design](https://room8studio.com/news/smart-casual-the-state-of-tile-puzzle-games-level-design-part-1/)
- [Royal Match Level Design (Gamigion)](https://www.gamigion.com/royal-match-level-design-insights/)
- [Candy Crush Saga - Wikipedia](https://en.wikipedia.org/wiki/Candy_Crush_Saga)
