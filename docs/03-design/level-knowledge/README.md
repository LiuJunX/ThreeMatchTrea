# 关卡设计知识库（元素技术参考）

本目录是各元素和核心机制的**技术参考**，供 AI 和策划在制作关卡时查阅。

设计方法论和框架见 `level-design/framework.md`（项目根目录）。

```
level-knowledge/
├── README.md                         ← 本索引
├── core/                             ← 核心机制（做关前全部必读）
│   ├── matching.md                   ← 匹配机制与颜色数设计
│   ├── gravity-and-cascade.md        ← 掉落、斜滑、死区、cascade
│   ├── bombs-and-powerups.md         ← 炸弹生成、触发方式、链式反应
│   ├── bomb-combos.md                ← 炸弹交换组合效果（全表）
│   ├── ufo-mechanics.md              ← UFO 目标选择、投射物、重定向
│   ├── spawner-mechanics.md          ← 生成条件优先级、帮助模式、PRD
│   ├── objective-tracking.md         ← 目标计数规则（什么消除方式算数）
│   ├── deadlock-shuffle.md           ← 死锁检测、洗牌行为
│   └── difficulty-levers.md          ← 5 种难度调节手段优先级
└── elements/                         ← 各元素的关卡设计指南
    ├── obstacle-box.md               ← Box（任意消除，1-4HP）
    ├── obstacle-bush.md              ← Bush（任意消除，1-5HP，死亡产 Grass）
    ├── obstacle-safe.md              ← Safe（仅道具，1-5HP）
    ├── obstacle-colorbox.md          ← ColorBox（仅对应颜色/道具，1-3HP）
    ├── obstacle-magichat.md          ← MagicHat（永久生成器，产 Diamond）
    ├── obstacle-curtain.md           ← Curtain（全局色消除触发）
    ├── obstacle-cupboard.md          ← Cupboard（2HP，死亡释放 Plate）
    ├── obstacle-mailbox.md           ← Mailbox（永久生成器，产 Envelope）
    ├── obstacle-owl.md               ← Owl（仅道具，1HP）
    ├── obstacle-stone.md             ← Stone（仅道具，1-3HP）
    ├── obstacle-potionbottle.md      ← PotionBottle（4色子瓶位掩码）
    ├── cover-cage.md                 ← Cage（阻止匹配+交换+移动）
    ├── cover-chain.md                ← Chain（阻止交换+移动，允许匹配）
    ├── cover-bubble.md               ← Bubble（动态，跟随掉落，阻止交换）
    ├── cover-honey.md                ← Honey（全阻止，仅相邻消除）
    ├── cover-frost.md                ← Frost（阻止交换+移动，允许匹配）
    ├── ground-ice.md                 ← Ice（上方消除时破碎）
    ├── ground-grass.md               ← Grass（Bush/Flowerpot 产出）
    ├── ground-leaves.md              ← Leaves（Flowerpot 产出）
    └── moving-obstacles.md           ← 5 种移动障碍物综合指南
```

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
| 产出 Grass | Bush（十字 4 格）, Flowerpot（3x3 共 9 格） |
| 释放收集物 | Cupboard(→Plate), Oyster(→Pearl) |
| 持续产出 | MagicHat(→Diamond), Mailbox(→Envelope) |

### 按认知成本

| 认知成本 | 元素 | 原因 |
|---------|------|------|
| 低 | Ice, Box, Grass | 视觉自解释，破坏逻辑直观 |
| 中 | Cage, Honey, Chain, Frost, Bubble | 需理解"它限制了什么" |
| 高 | MagicHat, PotionBottle, ColorBox, Curtain | 需理解条件性交互规则 |
