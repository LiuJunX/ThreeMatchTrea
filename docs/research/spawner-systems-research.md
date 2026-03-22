# Match3 Spawner/Generator 系统深度研究报告

> 研究日期：2026-03-21
> 目标：为 Match3Explore 项目的 Spawner 系统设计提供行业参考和跨领域灵感

---

## Part 1: 头部 Match3 游戏 Spawner 设计分析

### 1.1 Candy Crush Saga (King)

**Spawner 类型体系**

Candy Crush 拥有业界最丰富的 Spawner 类型体系，远不止简单的颜色棋子生成：

| Spawner 类型 | 生成物 | 触发规则 |
|---|---|---|
| Candy Cannon（糖果炮） | 普通糖果、条纹糖果、包装糖果、色彩炸弹、果冻鱼 | 持续从棋盘顶部填充 |
| Chocolate Fountain（巧克力喷泉） | 巧克力（阻碍物） | 每 2 步生成 1 个，向邻格扩散 |
| Dark Chocolate Fountain | 黑巧克力（更强阻碍物） | 类似巧克力喷泉但更难清除 |
| Magic Mixer（魔法搅拌机） | 预设阻碍物 | 可被摧毁（5次命中），按预定间隔生成 |
| Lucky Candy Cannon | 幸运糖果（用于达成收集目标） | 关卡特定条件 |
| Bobber | 果冻鱼 | 特定关卡 |
| Candy Cobra | 在蛇经过的空格上生成棋子 | 路径填充 |

**关键洞察**：Candy Crush 的 Spawner 不仅是颜色棋子的来源，更是**难度设计的主要工具**。Magic Mixer 会主动向棋盘注入阻碍物，Chocolate Fountain 会让棋盘逐渐恶化——这些"负面 Spawner"构成了关卡压力的核心来源。

**"被操控"的证据**

社区广泛报告（King Community 论坛）：
- 算法在关卡最后几步会"调整掉落以帮助玩家回到正轨"
- 存在一个"设定的通关模式"，关卡难度取决于"你能偏离这个模式多少次"
- 玩家连续失败多次后，明显感到棋盘变得"更友好"

King 官方从未承认在运行时动态调整 spawn 概率，但其 AI 辅助关卡设计系统（见下文 King AI 部分）通过预先确定 seed 来实现同等效果。

**King 的 AI 模拟系统（GDC 2016/2024）**

King 公开的核心方法：
- 使用强化学习 agent 模拟玩家行为，每个关卡运行 **1000+ 次**模拟
- 通过 seed 决定整局的棋子序列（**确定性生成**），不同 seed 的同一关卡胜率差异可达 **15%-75%**
- 设计师使用 AI Tweaking Tool 自动调整关卡参数直到达到目标胜率
- GDC 2024 披露：使用"类人"的 RL agent 而非最优 agent，目的是衡量普通玩家体验

**Blocker 统计框架（GDC 2020，Lucien "Yen-Chu" Chen）**

King 为 Blocker（阻碍物/Spawner 生成物）建立了类 RPG 的"属性表"，包括：
- HP（需要多少次命中才能消除）
- 扩散性（是否会自主扩张，如巧克力）
- 干扰度（对玩家操作空间的影响）
- 联动性（与其他元素的交互方式）

这套语言让设计师、程序、美术、策划能用统一指标讨论难度调节。

---

### 1.2 Royal Match (Dream Games)

**"公平感"设计哲学**

Royal Match 的核心竞争力是**可感知的公平性**：

1. **公平 Shuffle**：当棋盘无解时，立即自动重排，而非强迫玩家使用道具（Gardenscapes 等竞品曾有此设计）
2. **战略性道具放置**：通过匹配生成的特殊道具（火箭、TNT 等）"100%放置在对玩家最有利的位置"——这在所有 Match3 游戏中是独一无二的
3. **无负面 Spawner**：没有类似 Candy Crush 的巧克力喷泉等向棋盘注入障碍的机制

**Spawner 配置特点**

Royal Match 使用 4-5 种颜色棋子（Crown、Book、Shield、Leaf + 偶尔额外颜色）。其 Spawner 特征：
- 仅生成颜色棋子，特殊道具完全由玩家匹配产生
- 颜色均匀分布（社区无大规模"不公平"投诉）
- 难度控制主要通过 **关卡布局 + 步数限制**，而非 Spawner 偏向

**学术分析（Wurzburg 大学，2024 CoG 论文 "The Royal Crush"）**

Daniel Eckmann 的论文分析了 Royal Match 的 Match-3 机制，棋子从指定的 Spawn Point 以**均匀分布采样**（uniform distribution with replacement）。这是目前关于 Royal Match 生成算法最接近技术细节的公开资料。

**商业成功**：$4B+ 终身收入，~55M MAU，持续稳居全球收入榜前列。证明"公平感"是可以商业化的。

---

### 1.3 Toon Blast / Toy Blast (Peak Games, 现 Zynga)

**Collapse 机制下的 Spawner**

作为消除类（tap-to-blast）而非交换类（swap），Toon Blast 的 Spawner 有不同侧重：
- 点击 2+ 相邻同色方块即可消除（无需交换）
- 由于消除面积更大、更不可控，Spawner 的颜色平衡对局面影响更大
- 社区大量报告"算法被操控"——特别是在锦标赛期间

**社区发现的模式**

App Store 和社区评论揭示：
- "根据玩家的时刻（玩了多久、是否在锦标赛中、锦标赛进展如何）设计某些阶段迫使玩家花钱"
- "游戏看起来是随机的，但它可以给你无法通关的布局，然后在多次尝试后给你更好的棋子"
- "开发商保留根据玩家给予不同奖励的权利"

**关键差异**：Collapse 机制中颜色数量的影响远大于 Swap 机制——减少一种颜色等于指数级增加可消除面积。这意味着 Spawner 的颜色权重调节是 Collapse 类游戏最强力的难度旋钮。

---

### 1.4 Homescapes / Gardenscapes (Playrix)

**元素设计流程（2019 公开资料）**

Playrix 的 Level Designer Lead Aleksandr Shilyaev 公开了 5 阶段元素创建流程：
1. 研究竞品游戏获取灵感
2. 头脑风暴 20-30 个创意，根据游戏特性过滤
3. 原型测试：验证元素与棋子和其他元素的组合
4. 视觉设计：贴合主题（花园/家居）
5. 集成到元素引入体系

**关键配置文档**

Playrix 使用一份 **Master Document** 规定：
- 元素互斥规则：例如"不会在同一关卡中同时出现 Ivy 和 Honey"
- 难度曲线对标：与 Candy Crush 等竞品的元素引入速度对齐
- Soft Launch 时 300 关 16 种机制，半年后扩展到 1300 关 29 种机制

**难度目标**：关卡按 80%-95% 的通过率区间设定，通过元素组合和步数限制调节。

**Spawner 特征**：
- 公开资料中未找到关于 Spawner 颜色权重配置的具体参数
- 难度控制主要依赖阻碍物组合和步数限制
- 支持 **Chip Drop Chance**（棋子掉落概率）设置，但具体数值未公开

---

### 1.5 Puzzle & Dragons (GungHo)

**独特的 Spawner 哲学**

P&D 的核心差异：
- 6x5 棋盘，5-6 种颜色 Orb（珠子），外加心珠（回血）和干扰珠（毒/暗/干扰）
- 玩家在**一次操作中移动一颗珠子**，利用路径推挤其他珠子来组合 combo
- 没有传统 Spawner——消除后空位从顶部 **Skyfall**（天降）填充
- Skyfall 是纯随机的（~1/6 每种颜色概率）

**Skyfall 操控猜测**

社区技术：
- "将重复色珠相邻放置，期望天降匹配（1/6 概率）"
- 利用级联消除使新珠分批下落，统计上增加匹配概率（**不是算法操控，而是物理机制利用**）
- 无确切证据表明 GungHo 操控天降概率

**对 Spawner 设计的启示**：P&D 证明了"纯随机 Spawner + 高技巧上限操作"是一种可行的设计方向。难度来自敌人（需要特定颜色输出）和棋盘状态，而非 Spawner 偏向。

---

### 1.6 Triple Match 3D 及类似 3D 匹配游戏

**空间维度的 Spawner 差异**

3D 匹配游戏（Triple Match 3D、Match Triple 3D 等）的生成机制与传统 2D 有本质不同：
- 棋子散布在 **3D 空间**中，玩家需要找到并点击三个相同的棋子
- "Spawner"概念被替换为 **场景生成器**——在 3D 空间中放置一定数量的棋子组
- 难度控制通过：棋子遮挡关系、空间深度、相似棋子的视觉混淆

**对传统 Match3 Spawner 的启示**有限，但其**视觉干扰作为难度工具**的思路可以借鉴——例如在传统 Match3 中通过增加视觉相似的颜色来提升辨识难度。

---

## Part 2: 专利分析

### 2.1 King.com Ltd 专利

King.com Ltd 在 Justia 专利数据库中有大量专利，以下是与 Spawner/生成系统最相关的：

**US9387401B2 — "Method for implementing a computer game"**
- 描述了 Bubble Witch Saga 的泡泡发射器机制
- 关键技术：展示"即将发射的泡泡"和"下一个泡泡"，允许玩家交换选择顺序
- **难度调节方法**：使用数据库存储"玩家平均失败次数"，如果大量失败则调整"分数目标、可用时间或步数"
- 启示：King 的专利倾向于描述**宏观难度调节**而非微观 spawn 算法

**US9724602 — "Method for implementing a computer game"**
- 描述了 Match-3 Clicker 机制（点击 2+ 同色方块消除）
- 涵盖了"段落条件"——游戏元素组被替换为新组的机制
- 生成新元素以填充棋盘的基本流程

**US20140080560A1 — 更早期的 Match-3 实现**
- 描述了检测匹配条件、移除匹配元素、生成新元素填充棋盘的核心流程
- 涉及"层"概念——一种元素占据多个 tile，另一种占据单个 tile

**关键发现**：King 的专利 **没有** 公开描述基于玩家状态的实时 spawn 概率调整算法。他们的难度控制更多体现在：
1. 关卡设计层面（预计算的布局和 seed）
2. 宏观参数调整（步数、分数目标）
3. AI 辅助关卡迭代（非运行时）

### 2.2 EA 动态难度调整专利（对比参考）

**US20170259177A1 / US9919217B2 — "Dynamic Difficulty Adjustment"**

这是游戏行业最具争议的 DDA 专利，由 EA 于 2016 年提交，2021 年获批：

**核心技术架构**：
```
玩家行为数据 → ML 预测模型（流失率预测）
    → 映射到难度等级
    → 映射到具体 "Knob" 值
    → 应用到游戏参数
```

**可调节的 "Knobs"（旋钮）**：
- **Seed 值**：影响关卡布局生成的随机种子
- 物品掉落率和类型
- 敌人生命值/难度
- 角色能力（速度、准确度、跳跃距离）
- 控制灵敏度
- 武器可用性

**监控的玩家信号**：
- 游戏频率和时长
- 成功/失败率
- 每个目标的尝试次数
- 消费金额（游戏内和真实货币）
- 游戏进度
- 最近数据权重高于历史数据

**ML 方法**：逻辑回归、线性回归、K-means 聚类、决策树、朴素贝叶斯、深度学习。偏好简单模型（惩罚复杂度）。

**与 Spawner 的关系**：专利明确提到通过选择不同的 seed 值来影响"随机或伪随机数生成器"，从而控制"游戏内事件发生的概率"。**这与本项目 `SpawnContext.TargetDifficulty` 的设计方向高度一致**。

**争议与澄清**：EA 被集体诉讼指控在 FIFA 中使用 DDA 推动氪金。EA 展示了代码证明"DDA 技术从未被用于 FIFA、Madden 或 NHL"，原告撤诉。但专利本身描述的技术完全适用于 Match3。

### 2.3 Playrix / Dream Games 专利

在公开专利数据库中 **未找到** Playrix 或 Dream Games 关于 Match3 spawn 算法的专利。这两家公司的竞争优势更多体现在执行层面（美术品质、关卡打磨）而非算法专利。

---

## Part 3: 非 Match3 领域的"受控随机注入"类比系统

### 3.1 Tetris 7-Bag 随机器

**历史演变**

| 时期 | 系统 | 特征 |
|---|---|---|
| 1989 NES Tetris | 纯随机（带偏转） | 可能连续不出 I 块 30+ 个 |
| 2001 The New Tetris | 63-Bag（每种 9 个） | 允许策略性叠方 |
| 2001+ Guideline | **7-Bag** | 每 7 个一组全排列 |
| TGM 系列 | 35-Bag 变体 | 折中方案 |

**7-Bag 核心算法**：
1. 将 7 种方块（I/J/L/O/S/T/Z）放入"袋子"
2. 随机排列（7! = 5040 种排列）
3. 按顺序取出
4. 袋子空了后生成新袋子

**关键统计**：
- 两个 I 块之间最多间隔 **12 个**方块（跨袋边界）
- S/Z 块连续出现最多 **4 个**
- 完全消除了"旱灾"（drought）问题

**对 Match3 Spawner 的启示**：
> **核心原则：有界随机（Bounded Randomness）**
> 不是"每次独立随机选色"，而是"在一个周期内保证每种颜色出现合理次数"。可以实现为"N-Bag"系统——例如 5 色游戏用 15-Bag（每色 3 个），保证短期内颜色均衡。

### 3.2 Slay the Spire 的多流 Seed 系统

**架构细节（来自 forgottenarbiter.github.io 逆向分析）**

Slay the Spire 从单个 64-bit seed 初始化 **7 个独立 RNG 流**：

```
monsterRng  — 怪物遭遇选择
eventRng    — 事件房结果
cardRng     — 卡牌稀有度和具体卡牌
potionRng   — 药水掉落
treasureRng — 宝箱内容
relicRng    — 遗物生成
merchantRng — 商店机制
```

另有 5 个 RNG 在每层楼重新初始化（战斗内用）。

**卡牌奖励生成算法**：
```
每次战斗后生成 3 张卡牌奖励：
对于每张卡牌，从 cardRng 取 3 个数字：
  [稀有度判定, 具体卡牌选择, 是否升级]

稀有度判定：0-99 的整数
  < 35 → 罕见(Uncommon)
  其他 → 普通(Common)
  特殊条件 → 稀有(Rare)
```

**相关性问题**：由于所有 RNG 流从相同状态初始化，产生了意想不到的关联：
- "如果第一张卡牌奖励是罕见的，你也会获得药水掉落"
- "如果第一个战斗不是 Cultist，第一个事件房会是特定事件"

**对 Match3 Spawner 的启示**：
> **核心原则：Seed 确定性 + 流隔离**
> 用 seed 确保可重放性，但不同系统（颜色生成、特殊道具、关卡事件）应使用**独立的 RNG 流**避免相关性。本项目的 `Match3.Random` 模块已有 `RandomDomain` 概念，方向正确。

### 3.3 Hades 的 Boon（祝福）提供系统

**受控随机机制**：
- 每次选择从 3 个"符合资格"的 Boon 中挑 1 个
- **资格过滤**：排斥槽位（攻击/特殊/冲刺/施法/召唤各只能有 1 个 Boon）
- **池消耗**：从某个神取的 Boon 越多，该神剩余 Boon 池越小，后续获得特定 Boon 概率提高
- **Keepsake 保证**：装备某神的 Keepsake，保证下次 Boon 来自该神
- **替换升级**：一个神可以提出替换另一个神的 Boon，且替换会将稀有度提升一级
- **Seed 预览**：放弃重来会回到相同的 seed 起点

**对 Match3 Spawner 的启示**：
> **核心原则：池消耗 + 资格过滤**
> Spawner 可以维护一个"近期已生成"的颜色池，已出现多次的颜色权重降低（类似本项目的 `SpawnBalanced` 策略）。结合"必须保证某些条件"的过滤器（类似关卡目标需要的颜色）。

### 3.4 Gacha/Loot 系统的 Pity Timer

**三层递进保底体系（以原神为例）**

| 层级 | 机制 | 触发条件 |
|---|---|---|
| 基础概率 | 0.6% 五星 | 每次抽取 |
| 软保底（Soft Pity） | 概率从 0.6% 跳升至 ~6.6%/次 | 第 74 抽起 |
| 硬保底（Hard Pity） | 100% 五星 | 第 90 抽 |

**Reservoir Method（水库法）**：
创建一个固定大小的抽取池，预先分配奖励位置，确保整体概率符合计划。不依赖实时概率调整。

**Dota 2 的伪随机分布（PRD）**

更精确的数学模型：`P(N) = C * N`

| 名义概率 | C 值 | 首次触发概率 | 保证触发次数 |
|---|---|---|---|
| 5% | 0.0038 | 0.38% | ~263次 |
| 10% | 0.0147 | 1.47% | ~68次 |
| 15% | 0.0322 | 3.22% | ~31次 |
| 20% | 0.0557 | 5.57% | ~18次 |
| 25% | 0.0847 | 8.47% | ~12次 |
| 30% | 0.1189 | 11.89% | ~9次 |
| 50% | 0.3021 | 30.21% | ~4次 |

PRD 的效果：标准差显著低于真随机，连续不触发的极端情况被消除。

**对 Match3 Spawner 的启示**：
> **核心原则：递增概率消除极端值**
> 当某种颜色长时间未出现时，其 spawn 权重应递增（PRD 模式）。这比简单的均匀随机或 Bag 系统更精细，且保持了"随机感"。可以直接应用于本项目的 `SpawnBalanced` 策略改进。

### 3.5 Dead Cells 的约束式关卡生成

**混合架构**：

```
固定世界框架（岛屿连接图，不变）
  → Biome 内部使用 概念图（Graph）
    → Graph 定义：关卡长度、特殊房间数、迷宫比例、入口到出口距离
    → 从手工房间库中随机选择，测试是否符合 Graph 约束
    → 不符合则换一个房间重试
  → 敌人放置使用参数化规则
    → 每种怪物有：tile 价值（占多少难度预算）、最大数量限制、互斥规则、空间需求
    → 总敌人数 = 战斗区域 tile 总数 / 比率（如 1:5）
```

**对 Match3 Spawner 的启示**：
> **核心原则：约束满足 + 预算系统**
> Spawner 配置可以类比为"每关的颜色预算"——不直接指定每种颜色的概率，而是指定约束（如"红色至少占 15%"、"不超过 3 种颜色连续出现"、"每 20 颗棋子内每种颜色至少出现 2 次"），由系统自动求解满足这些约束的生成方案。

### 3.6 EA FIFA 动态难度调整（DDA）

**专利核心（US20170259177A1）**

已在 Part 2.2 详述。核心争议点：

1. **EA 的立场**：DDA 技术存在于专利中，但"从未用于 FIFA/Madden/NHL 的在线多人模式"
2. **社区的怀疑**：大量玩家报告在 Ultimate Team 中感受到"橡皮筋效应"
3. **法律结果**：2021 年集体诉讼，原告在查看 EA 源代码后撤诉

**对 Match3 Spawner 的启示**：
> **核心原则：透明度是信任的基础**
> 即使实施了 DDA，也应在某种程度上让玩家感知"系统在帮忙"而非"系统在作弊"。Royal Match 的做法（道具放在最佳位置，玩家能看到）比 Candy Crush 的隐式调整更受欢迎。建议在 JSON 配置中保持 DDA 参数**可审计**。

---

## Part 4: 学术/GDC 研究

### 4.1 GDC 演讲

**"Blockers: Analyzing Difficulty Drivers in Candy Crush Games"（GDC 2020）**
- 演讲者：Lucien "Yen-Chu" Chen（King）
- 核心：为 Match3 阻碍物建立 RPG 式属性体系，作为跨工作室的通用设计语言
- GDC Vault: `gdcvault.com/play/1026879`

**"How King Uses AI in Candy Crush"（GDC 2016/2024）**
- 核心：RL agent 模拟玩家行为，1000+ 次模拟测算关卡胜率
- GDC 2024 更新：使用 TensorFlow 训练"类人"agent
- GDC Vault: `gdcvault.com/play/1023858`

**"How King Builds Tournament Levels for Candy Crush All Stars"**
- 锦标赛关卡需要更精确的难度控制
- 通过 seed 选择保证公平性（所有玩家同 seed）

### 4.2 学术论文

**"Dynamic Difficulty Adjustment for Maximized Engagement in Digital Games"（EA Research）**
- 关键发现：同一关卡不同 seed 的胜率差异从 15% 到 75% 不等
- 结论：seed 选择本身就是最强力的难度控制手段

**"Efficient Difficulty Level Balancing in Match-3 Puzzle Games"（MDPI Electronics, 2023）**
- PPO vs SAC 算法对比
- SAC 算法更适合 Match3 关卡难度平衡
- 自动化关卡参数调优

**"Statistical Modelling of Level Difficulty in Puzzle Games"（arXiv, 2021）**
- 用通过概率作为难度客观度量
- 推导了棋盘棋子描述符与难度的关系模型

**"Improving Conditional Level Generation using Automated Validation in Match-3 Games"（arXiv, 2024）**
- 条件式关卡生成 + 自动验证
- 颜色棋子从 Spawn Point 以均匀分布采样
- 自动创建 tile-spawning spot 作为后处理步骤

**"The Royal Crush: Analysis of Match-3 Mechanics"（Wurzburg, 2024 CoG）**
- 对 Royal Match 机制的系统分析
- 提供了 Match3 机制分类学框架

**"Operationalising Difficulty in Puzzle Games"（ITU Copenhagen, PhD Thesis, 2022）**
- Match3 中难度最常用"通过率/尝试次数"定义
- 难度定义直接影响变现（IAP/广告）

### 4.3 行业分析

**Gamigion: "Automatic Difficulty Adjustment in Match3 Levels"**

最直接相关的行业文档，揭示了 3 种实际使用的 DDA 方法：

1. **颜色掉落概率修改**：例如设置蓝=100%、黄=100%、红=50%，使红色棋子约为其他颜色的一半。**20-30% 的概率偏移玩家无法察觉**但对道具生成率影响显著
2. **预制组合（Pre-Made Combinations）**：控制下落棋子使其自动形成道具（如条纹/包装）
3. **Seed 控制**：预先确定整局的棋子序列，根据目标难度选择对应 seed

**Deconstructor of Fun: "Royal Match - The New King from Turkey?"**
- Dream Games 从竞品中"精挑细选最好的机制并抛光到极致"
- 核心差异化是"快速、流畅、公平"的体验

**Naavik: "The Royal Blueprint: Easy to Copy ... Or Not?"**
- Royal Match 产生 $4B+ 收入
- 成功秘诀不在单一机制而在整体打磨度
- 后来者难以复制的不是算法而是运营积累

---

## Part 5: 与本项目现有系统的映射分析

### 当前实现回顾

本项目 (`Match3Explore`) 的 Spawner 系统已有良好的基础架构：

| 组件 | 路径 | 职责 |
|---|---|---|
| `ISpawnModel` | `Systems/Spawning/ISpawnModel.cs` | Spawner 策略接口 |
| `RuleBasedSpawnModel` | `Systems/Spawning/RuleBasedSpawnModel.cs` | 基于规则的策略实现 |
| `SpawnContext` | `Systems/Spawning/SpawnContext.cs` | 难度/进度上下文 |
| `BoardAnalyzer` | `Systems/Spawning/BoardAnalyzer.cs` | 棋盘状态分析 |
| `StandardTileGenerator` | `Systems/Generation/StandardTileGenerator.cs` | 遗留简单生成器 |
| `LevelConfig.TargetDifficulty` | `Config/LevelConfig.cs` | 关卡难度配置 |

### 当前策略矩阵

```
DetermineStrategy(context) →
  FailedAttempts >= 3        → Help（生成匹配，怜悯规则）
  RemainingMoves <= 3 且进度 < 90%  → Help
  GoalProgress > 70% 且 Moves > 5  → Challenge（避免匹配）
  TargetDifficulty < 0.3     → Help
  TargetDifficulty > 0.7     → Challenge
  默认                       → Balance（颜色均衡）
```

### 与行业最佳实践的差距

| 能力 | 当前状态 | 行业标准 | 建议方向 |
|---|---|---|---|
| 颜色均衡 | `SpawnBalanced` 反比权重 | Tetris 7-Bag / PRD | 考虑 N-Bag 或 PRD 替换简单反比 |
| Anti-streak | 避免与列顶相同 | Tetris 最大旱灾限制 | 增加全局颜色旱灾检测 |
| 特殊道具生成 | 不支持 | CCS 有专用 Spawner 类型 | 扩展 `ISpawnModel` 支持 `ElementType` 之外的生成 |
| 负面 Spawner | 不支持 | CCS 巧克力喷泉/Magic Mixer | 可作为 ObstacleSystem 功能而非 Spawner |
| Per-Spawner 配置 | 全局单一模型 | CCS 不同列可配不同 Cannon | 支持 per-column SpawnRule |
| Seed 确定性 | 已有 | CCS/StS 行业标准 | 已到位 |
| DDA 信号 | `SpawnContext` 有基础信号 | EA 专利级别的 ML 预测 | 当前足够，可后续扩展 |
| JSON 可配置 | `TargetDifficulty` 仅 1 个浮点 | 行业用 MasterSheet | 扩展为 `SpawnConfig` 结构 |

---

## 核心洞察

基于全部研究，以下是对本项目 Spawner 系统设计最重要的 10 个洞察：

### 1. Seed 选择是最强力的难度旋钮

King 的研究表明，**同一关卡不同 seed 的胜率差异可达 15%-75%**。与其在运行时复杂调整 spawn 概率，不如在关卡设计阶段通过 AI 模拟筛选合适的 seed 范围。本项目的 `LevelAnalysisService` 和 MCTS 分析已为此奠定基础。

### 2. 颜色概率偏移 20-30% 玩家无感知

Gamigion 的行业实践表明，将某种颜色的 spawn 概率调低 20-30%，玩家无法察觉但对道具生成率和关卡难度有显著影响。这意味着 `SpawnContext.TargetDifficulty` 可以映射到具体的颜色权重偏移，且偏移量在 20-30% 内是安全的。

### 3. 用 N-Bag 或 PRD 替代简单均匀随机

当前的 `SpawnRandom` 是纯均匀随机，`SpawnBalanced` 是反比权重。行业最佳实践是：
- **短期保证**：N-Bag 系统（如 15-Bag for 5 colors）保证每 15 颗棋子内每种颜色出现 3 次
- **渐进保证**：PRD（`P(N) = C * N`）让长时间未出现的颜色概率递增
建议实现为可配置选项：`"balanceMode": "bag" | "prd" | "weighted"`

### 4. 负面 Spawner 是难度设计的核心工具，但要克制

Candy Crush 的巧克力喷泉和 Magic Mixer 是最有效的难度提升工具。但 Royal Match 的成功证明**完全不使用负面 Spawner** 也能成功——关键是让难度来源透明可预期。建议将负面生成作为 ObstacleSystem 的功能而非 Spawner 的职责。

### 5. Per-Spawner 配置是进阶需求

Candy Crush 的 Candy Cannon 可以按列配置不同的生成物。建议 JSON 配置支持：

```json
{
  "spawnPoints": [
    { "column": 0, "weights": { "Item1": 1.0, "Item2": 1.0, "Item3": 0.5 } },
    { "column": 3, "weights": "default" },
    { "column": 7, "specialChance": 0.05, "specialType": "Rocket" }
  ]
}
```

但默认值应该足够好，大多数关卡不需要逐列配置。

### 6. 公平感比实际公平更重要

Royal Match 的 $4B 收入证明了"可感知的公平"是最强变现策略。具体措施：
- 自动 Shuffle 而非消耗道具
- 特殊道具放在最佳位置（玩家可见的帮助）
- 怜悯规则在 3 次失败后明确生效
- 本项目的 `FailedAttempts >= 3 → Help` 方向正确

### 7. 多 RNG 流必须隔离

Slay the Spire 的案例警示：从同一 seed 初始化的多个 RNG 流会产生**意想不到的相关性**。本项目的 `RandomDomain` 概念正确，但需确保：
- 颜色生成、特殊道具、关卡事件使用独立 domain
- 各 domain 的 seed 通过 hash 派生（避免初始状态相同）

### 8. 最少配置原则：3 个数字控制一切

研究表明，大多数 Match3 关卡的 Spawner 只需要 3 个核心参数：
- **颜色数量**（3-6，最强力的难度旋钮）
- **目标难度**（0.0-1.0，映射到颜色权重偏移和策略选择）
- **步数限制**（与 Spawner 协同的宏观难度控制）

当前的 `LevelConfig` 已有 `TargetDifficulty` 和 `MoveLimit`，加上 `TileTypesCount` 就是一个完整的最小配置。

### 9. AI 模拟验证是闭环的关键

King 的方法论：设计 → AI 模拟 1000 次 → 检查胜率 → 调整 → 重复。本项目的 `LevelAnalysisService` + MCTS 已经实现了这个闭环的基础。建议将"目标胜率"加入 `LevelConfig`，让分析服务自动判断关卡是否达标。

### 10. Spawner 的终极形态是约束求解器

Dead Cells 的约束式生成启发我们：最灵活的 Spawner 不是"指定每种颜色的概率"，而是"指定约束，系统自动满足"：
- "红色不少于 15%"
- "连续同色不超过 3 颗"
- "每 20 颗内每种颜色至少 2 次"
- "不在最后 3 步生成玩家不需要的颜色"

这可以在当前 `RuleBasedSpawnModel` 的基础上逐步演进，无需一步到位。

---

## 参考来源

### 专利
- [US20170259177A1 - EA Dynamic Difficulty Adjustment](https://patents.google.com/patent/US20170259177A1/en)
- [US9919217B2 - EA DDA (Granted)](https://patents.google.com/patent/US9919217B2/en)
- [US9387401B2 - King Method for implementing a computer game](https://patents.google.com/patent/US9387401)
- [US9724602 - King Match-3 Clicker Method](https://patents.justia.com/patent/9724602)
- [King.com Ltd Patent Portfolio](https://patents.justia.com/assignee/king-com-ltd)

### GDC 演讲
- [Blockers: Analyzing Difficulty Drivers in Candy Crush Games](https://gdcvault.com/play/1026879/Blockers-Analyzing-Difficulty-Drivers-in)
- [How King Uses AI in Candy Crush](https://gdcvault.com/play/1023858/How-King-Uses-AI-in)
- [King Researchers at GDC 2024](https://gamesbeat.com/king-researchers-talk-about-the-results-of-using-ai-at-gdc-2024/)
- [AI Making Candy Crush Less Frustrating](https://www.digitaltrends.com/gaming/candy-crush-saga-ai-interview-gdc-2024/)

### 学术论文
- [The Royal Crush: Analysis of Match-3 Mechanics (Wurzburg 2024)](https://downloads.hci.informatik.uni-wuerzburg.de/2024-CoG-RoyalCrush.pdf)
- [Efficient Difficulty Balancing in Match-3 (MDPI 2023)](https://www.mdpi.com/2079-9292/12/21/4456)
- [Statistical Modelling of Level Difficulty](https://arxiv.org/pdf/2107.03305)
- [Improving Conditional Level Generation (arXiv 2024)](https://arxiv.org/html/2409.06349v2)
- [Dynamic Difficulty Adjustment for Maximized Engagement (EA)](https://www.researchgate.net/publication/322414816)
- [Operationalising Difficulty in Puzzle Games (ITU PhD)](https://en.itu.dk/-/media/EN/Research/PhD-Programme/PhD-defences/2022/PhD-Thesis---Final-version---Jeppe-Kristensen.pdf)
- [Gacha Game Analysis (Tsinghua)](https://magickd.github.io/papers/gacha.pdf)

### 行业分析
- [Royal Match - The New King from Turkey? (Deconstructor of Fun)](https://www.deconstructoroffun.com/blog/2021/3/21/royal-match-the-new-king-from-turkey)
- [The Royal Blueprint (Naavik)](https://naavik.co/digest/why-dream-games-success-is-a-challenge-to-replicate/)
- [Match-3 Level Design Principles (Gamigion)](https://www.gamigion.com/match-3-level-design-principles/)
- [Automatic Difficulty Adjustment in Match3 (Gamigion)](https://www.gamigion.com/automatic-difficulty-adjustment-in-match3-levels/)
- [Playrix: Creating Levels and Elements](https://gameworldobserver.com/2019/09/27/playrix-levels-elements-match-3)
- [45 Match-3 Mechanics (Gamedeveloper)](https://www.gamedeveloper.com/design/45-match-3-mechanics)
- [Toon Blast Game Analysis (Medium)](https://medium.com/@ekinmelissezer/game-analysis-of-toon-blast-mechanics-level-design-difficulty-patterns-and-monetization-signals-022748ae51b4)
- [Royal Match Game Analysis (Medium)](https://medium.com/@ekinmelissezer/game-analysis-for-royal-match-and-toon-blast-9c4bff8ef48b)

### 技术参考
- [Tetris Random Generator (TetrisWiki)](https://tetris.wiki/Random_Generator)
- [History of Tetris Randomizers (Simon Laroche)](https://simon.lc/the-history-of-tetris-randomizers)
- [Slay the Spire Correlated Randomness](https://forgottenarbiter.github.io/Correlated-Randomness/)
- [Dota 2 Pseudo-Random Distribution (Liquipedia)](https://liquipedia.net/dota2/Pseudo_Random_Distribution)
- [Dead Cells Level Design Hybrid Approach](https://deepnight.net/tutorial/the-level-design-of-dead-cells-a-hybrid-approach/)
- [Candy Crush Saga Spawners (Fandom Wiki)](https://candycrush.fandom.com/wiki/Category:Spawners)
- [Hearthstone Pity Timer (Fandom Wiki)](https://hearthstone.fandom.com/wiki/Card_pack_statistics)
- [Genshin Impact Pity System](https://www.oreateai.com/blog/understanding-the-genshin-impact-pity-system-your-guide-to-smart-pulls/)

### 社区分析
- [King Community: Nothing is Random](https://community.king.com/en/candy-crush-saga/discussion/429811/tip-for-new-players-nothing-is-random)
- [EA DDA Controversy (Dexerto)](https://www.dexerto.com/fifa/what-is-dda-in-gaming-fifa-22-ea-1766885/)
- [FIFA DDA Lawsuit Dropped (PC Gamer)](https://www.pcgamer.com/fifa-dynamic-difficulty-lawsuit-dropped-after-plaintiffs-talk-to-eas-engineers/)
- [EA Fair Play Statement](https://www.ea.com/en-gb/news/fair-play-and-dynamic-difficulty-adjustment)
