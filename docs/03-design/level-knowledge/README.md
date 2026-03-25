# 关卡设计知识库

供 AI 和策划在制作关卡时参考的知识文档。按树状结构组织：

```
level-knowledge/
├── README.md                         ← 本索引
├── core/                             ← 核心机制与设计原则
│   ├── matching.md                   ← 匹配机制与颜色数设计
│   ├── gravity-and-cascade.md        ← 掉落连锁与棋盘高度
│   ├── bombs-and-powerups.md         ← 炸弹道具与障碍交互
│   └── difficulty-levers.md          ← 5 种难度调节手段优先级
├── elements/                         ← 各元素的关卡设计指南
│   ├── obstacle-box.md               ← Box（任意消除，1-4HP）
│   ├── obstacle-bush.md              ← Bush（任意消除，1-5HP，死亡产 Grass）
│   ├── obstacle-safe.md              ← Safe（仅道具，1-5HP）
│   ├── obstacle-colorbox.md          ← ColorBox（仅对应颜色/道具，1-3HP）
│   ├── obstacle-magichat.md          ← MagicHat（永久生成器，产 Diamond）
│   ├── obstacle-curtain.md           ← Curtain（全局色消除触发）
│   ├── obstacle-cupboard.md          ← Cupboard（2HP，死亡释放 Plate）
│   ├── obstacle-mailbox.md           ← Mailbox（永久生成器，产 Envelope）
│   ├── obstacle-owl.md               ← Owl（仅道具，1HP）
│   ├── obstacle-stone.md             ← Stone（仅道具，1-3HP）
│   ├── obstacle-potionbottle.md      ← PotionBottle（4色子瓶位掩码）
│   ├── cover-cage.md                 ← Cage（阻止匹配+交换+移动）
│   ├── cover-chain.md                ← Chain（阻止交换+移动，允许匹配）
│   ├── cover-bubble.md               ← Bubble（动态，跟随掉落，阻止交换）
│   ├── cover-honey.md                ← Honey（全阻止，仅相邻消除）
│   ├── cover-frost.md                ← Frost（阻止交换+移动，允许匹配）
│   ├── ground-ice.md                 ← Ice（上方消除时破碎）
│   ├── ground-grass.md               ← Grass（Bush/Flowerpot 产出）
│   ├── ground-leaves.md              ← Leaves（Flowerpot 产出）
│   └── moving-obstacles.md           ← 5 种移动障碍物综合指南
└── patterns/                         ← 关卡设计模板
    ├── tutorial-intro.md             ← 教学关模板
    ├── boss-level.md                 ← Boss 关模板
    └── dual-objective.md             ← 双目标组合技巧
```

## 使用方式

Generator 在生成关卡时，根据当前关卡用到的元素，读取对应的 `elements/` 文档获取设计建议。每个关卡生成时会输出 `level_XXX.design.md`，其中列出应阅读的知识文档。

## 元素文档结构

每个元素文档包含：
- **关卡设计价值**：这个元素为玩家体验贡献了什么
- **属性速查**：HP、消除方式、免疫、死亡效果
- **放置原则**：宜/忌/注意事项
- **组合建议**：和哪些元素搭配效果好/不好（★评级）
- **目标设计**：以此元素为目标时的注意事项
- **常见错误**：容易踩的坑

## 元素分类速查

### 按消除方式

| 分类 | 元素 |
|------|------|
| 任意消除 | Box, Bush, Cupboard, RoyalEgg, Vase, Oyster, Flowerpot |
| 仅道具 | Safe, Owl, Stone, PorcelainPiggy |
| 颜色匹配 | ColorBox, PotionBottle |
| 全局色消除 | Curtain |
| 不可消除（生成器） | MagicHat, Mailbox |
| 相邻消除 | Honey |

### 按死亡效果

| 效果 | 元素 |
|------|------|
| 无 | Box, Safe, Owl, Stone, ColorBox, Curtain, RoyalEgg, Vase, PorcelainPiggy |
| 产出 Grass | Bush（十字 4 格）, Flowerpot（3×3 共 9 格） |
| 释放收集物 | Cupboard(→Plate), Oyster(→Pearl) |
| 持续产出 | MagicHat(→Diamond), Mailbox(→Envelope) |

## 与 `docs/06-mechanics/` 的区别

- `06-mechanics/` = 代码实现视角（属性、规则、代码位置）
- `level-knowledge/` = 关卡设计视角（怎么用它做好关卡）
