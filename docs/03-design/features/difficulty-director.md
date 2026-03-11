# 实时难度控制系统（Difficulty Director）

> **状态**：设计阶段，未实现
> **前置**：`core-patterns.md §15`（Clone 线程安全约束已落地）

## 1. 目标

在关卡设计之外，通过运行时动态机制控制难度和心流：
- 掉落内容的实时调控
- 逻辑先行（DryRun），通过模拟结果反向修改过程
- 让"差一点过关"和"刚好过关"成为可控的设计手段

## 2. 系统架构

```
                    ┌──────────────┐
                    │   Director   │  维护 intensity curve + 玩家压力模型
                    └──────┬───────┘
                           │ DifficultyContext
              ┌────────────┼────────────┐
              ▼            ▼            ▼
       ┌──────────┐ ┌───────────┐ ┌──────────────┐
       │SpawnPolicy│ │ DryRun    │ │ NearMissCtrl │
       │ 掉落策略  │ │ 预模拟    │ │ 差一点控制   │
       └──────────┘ └───────────┘ └──────────────┘
              │            │            │
              ▼            ▼            ▼
         Spawner      SimEngine     最后 N 步
         权重表       Best-of-N    特殊评分
```

## 3. 掉落生成控制（Supply-side）

### 3.1 颜色权重调控

现有 `RuleBasedSpawnModel` 已实现 Help / Challenge / Balance / Neutral 四种策略。
扩展方向：

| 层次 | 输入信号 | 调控方式 |
|------|---------|---------|
| 棋盘均衡 | 各色存量 | 少的加权（已实现：SpawnBalanced） |
| 目标牵引 | 目标进度 vs 剩余步数 | 目标相关色加权 |
| 连击保底 | 连续 N 步无消除 | 强制生成可消组合 |
| 死局预防 | 可用 move 数 | 在可用 move < 阈值时干预 |

### 3.2 Cascade 感知掉落

生成候选掉落 → DryRun 模拟 → 按连锁深度评分 → 选取最优。
见 §4 DryRun。

### 3.3 Power-up 种子

- 跨局连败 N 次 → 提高局内自然出现 power-up 的几率
- 关卡中后期（步数剩余 < 30%）→ 适当放出强力道具掉落

## 4. DryRun 预模拟

### 4.1 核心方案：Best-of-N

```
玩家操作后:
  1. 生成 N 组不同的随机种子 (N = 4~8)
  2. 每组种子跑完整 cascade（Clone + RunToStable）
  3. 评分函数给每组结果打分
  4. 选最高分的种子注入正式引擎执行
```

信息论视角：Best-of-4 = top 25%，Best-of-8 = top 12.5%。
无需精确控制，只需比纯随机"更好"。

### 4.2 性能估算

| 场景 | 单次 DryRun | Best-of-8 总耗时 |
|------|-----------|-----------------|
| 当前（简单元素） | ~0.05ms | ~0.4ms |
| 成熟期（复杂元素） | 0.5-5ms | 4-40ms |

时间窗口：玩家操作到第一波掉落出现 **~700ms（~40帧@60fps）**，充裕。

### 4.3 执行策略

| 策略 | 适用条件 | 复杂度 |
|------|---------|--------|
| **Sync** | 总耗时 < 4ms | 零 |
| **分帧** | 4-40ms，每帧跑 1-2 个候选 | 低（简单调度器） |
| **多线程** | > 40ms，或 N 很大 | 中（Task.Run） |

Best-of-N 天然适合多线程（embarrassingly parallel）：
- 每个候选完全独立（独立 Clone + 独立 RNG）
- Core 是纯 C# DLL，无 Unity API 依赖
- 线程安全前提已在 `core-patterns.md §15` 建立

**开发顺序：先 Sync → 按需切分帧 → 最后才上多线程。**

### 4.4 进阶：逐波贪心（Wave-by-Wave Greedy）

如果 Best-of-N 粒度不够，可在每一波 cascade 单独决策：

```
Wave 1: 空位出现 → 生成 K 组候选 → 各跑一波 → 选最好 → 确定
Wave 2: 新空位 → 生成 K 组候选 → 各跑一波 → 选最好 → 确定
...
```

蝴蝶效应被限制在单波内。计算量：K × 波数 × 单波模拟。

### 4.5 时机与挂载点

```
玩家交换
  ├─ 交换动画 ──────────── ~200ms ─┐
  ├─ 初始消除动画 ───────── ~300ms  ├─ DryRun 窗口
  ├─ 重力下落动画 ───────── ~200ms ─┘
  ▼
  Spawner 生成第一波 ← 此时需要 DryRun 结果
```

挂载点：`RealtimeRefillSystem.Update()` 调用 `SpawnModel.Predict()` 之前，
由 Director 注入已选定的种子。

## 5. 评分函数

评分函数是整个系统的设计核心，权重由 Director 动态设定。

```csharp
float Evaluate(GameState finalState, DryRunResult result, DirectorContext ctx)
{
    float score = 0;

    // 1. 可用移动数（3-6 最佳，太多=无聊，太少=焦虑）
    int moves = CountValidMoves(finalState);
    score += Gaussian(moves, mean: 4.5f, sigma: 1.5f) * ctx.MoveWeight;

    // 2. 目标进度贡献（本次 cascade 推进了多少目标）
    score += CalcObjectiveProgress(result) * ctx.ProgressWeight;

    // 3. 连锁深度（适度连锁=爽，过多=失控感）
    score += Gaussian(result.CascadeDepth, mean: ctx.DesiredCascade, sigma: 1f)
             * ctx.CascadeWeight;

    // 4. 颜色多样性（棋盘不应被单色主导）
    score += CalcColorEntropy(finalState) * ctx.DiversityWeight;

    // 5. Near-miss 控制（最后几步特殊评分）
    if (ctx.IsEndgame)
        score += NearMissScore(result, ctx);

    return score;
}
```

### 各阶段权重参考

| 阶段 | ProgressWeight | CascadeWeight | MoveWeight |
|------|---------------|---------------|------------|
| 开局（步数 > 70%） | 低 | 高（给爽感） | 中 |
| 中局（30%-70%） | 高（推目标） | 中 | 中 |
| 残局（< 30%） | 极高 | 低 | 低 |
| Near-miss | 特殊 | — | — |

## 6. Director 状态模型

```csharp
class DirectorState
{
    float PlayerPressure;     // 当前压力指数 (0-1)
    float[] TargetProgress;   // 各目标完成率
    float MovesRatio;         // 已用步数 / 总步数
    int   RecentCascades;     // 近 N 步的连锁次数
    int   CrossSessionFails;  // 跨局连败次数（需持久化）
    AnimationCurve IntensityCurve;  // 目标紧张度曲线
}
```

### Intensity Curve

```
步数:  30    25    20    15    10    5     0
       ┌─────┬─────┬─────┬─────┬─────┬─────┐
紧张:  低    低    中    高    极高   ？
       └─────┴─────┴─────┴─────┴─────┴─────┘
                                      ↑
                              关键决策点:
                              让玩家"差一点过"还是"刚好过"
```

### Near-miss 工程

最后 3-5 步评估玩家能否过关：
- **铁定过不了** → 给好掉落，让目标接近但不到（制造"差一点"）
- **能过** → 正常放行，给漂亮的终结连锁（仪式感）
- **在边缘** → 最佳状态，保持张力

### Rubber Banding（橡皮筋效应）

- 跨局连败 N 次 → 下一局整体难度系数下调
- 局内前半段打差 → 后半段获得更多帮助
- 反向收紧要谨慎（玩家对"变难"比"变简单"敏感得多）

## 7. 行业参考

| 来源 | 机制 | 可借鉴点 |
|------|------|---------|
| Candy Crush (King) | Color balancer + 专利描述的末尾种子调整 | 颜色均衡 + near-miss |
| Royal Match | 目标色加权 | 目标牵引掉落 |
| Toon Blast (Peak) | 难关后期倾斜掉落 | 连败 rubber banding |
| Left 4 Dead (Valve) | AI Director + intensity curve | 心流节奏编排 |
| Mario Kart | Rubber banding | 跨局难度调节 |
| Puzzle & Dragons | 天降 combo 概率控制 | Cascade 感知掉落 |

## 8. 实施路线

| Phase | 内容 | 前置 |
|-------|------|------|
| **P1** | Best-of-N + 简单评分（可用 move 数 + 目标进度） | §15 约束（已完成） |
| **P2** | Director + 动态权重 + 跨局 Rubber Banding | P1 + 持久化 |
| **P3** | 逐波贪心 + Near-miss + Intensity Curve | P2 + 大量 playtest 数据 |

## 9. 代码入口

| 组件 | 现有文件 | 改动 |
|------|---------|------|
| DryRun 执行 | `SimulationEngine.Clone()` | 新增 `RunToStable()` 返回评估数据 |
| 种子注入 | `RuleBasedSpawnModel.Predict()` | 接受 Director 注入的种子 |
| 评分函数 | 新建 | `Systems/Director/BoardEvaluator.cs` |
| Director | 新建 | `Systems/Director/DifficultyDirector.cs` |
| 状态追踪 | 新建 | `Systems/Director/DirectorState.cs` |

## 10. 约束与风险

- **可观察性**：DryRun 结果和选择过程必须可 log，否则调参无从下手
- **确定性**：DryRun 使用独立 RNG，不影响正式引擎的确定性回放
- **SpawnModel._rng**：条件安全，并行 DryRun 时 SpawnModel 必须用 `state.Random`
- **Match3Config 共享**：Physics clone 共享 Match3Config（事实不可变），如未来 session 内可变需额外克隆
- **过度干预**：Director 调控力度需要上限，否则玩家会察觉"被操控"
