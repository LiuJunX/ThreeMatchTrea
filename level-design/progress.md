# 关卡制作进展

每次做关卡前先读本文件，了解当前进度和下一步任务。完成后更新本文件。

---

## 当前状态

**L001 — Dam Break（进行中）**

### 已完成

1. **设计哲学和框架搭建**
   - `level-design/framework.md` 已创建：心理学驱动设计、体验即教学、低地板高天花板
   - `level-design/patterns/` 7 个体验模式文档（决堤、引爆、层层揭开、内外翻转、自教学、Boss、双目标）
   - 旧的 `docs/03-design/level-design.md` 已删除，知识库已迁移

2. **三个系统改动**（2353 测试全通过）
   - `LevelConfig.TileTypesCount`：每关可指定颜色数（L001 用 4 色）
   - `ElementType.KeepEmpty = 255`：Grid 中标记初始空 Slot
   - `PhasePresetCondition`：Spawner 阶段触发（障碍全清后激活 Preset 队列）

3. **L001 Design Intent**（`level-design/levels/L001.md`）
   - 体验目标：决堤式空间突变
   - 分析数据：4 色 / 12r+12b / 28 步 → Casual 93%（需要进一步优化）

### 待解决

**L001 设计方案升级：精编式渐进揭示**

当前设计是"单层水坝 + 随机开局"，讨论后确定升级为：

- **双层水坝**（Row 3 + Row 5），制造两次递进式惊喜
- **预排初始 tile**（不用随机），确保 Move 1 只有 1 个有效 swap
- **Preset 掉落**控制前几个 tile 的颜色，确保 cascade 结果可控
- **上方只留 2 行**（Row 1-2），减少可能的 swap 数量，强化控制

技术要点：
- 受控阶段只用 3-match（不用火箭/炸弹，避免位置不确定性）
- 3-match 紧贴 Box 行 → 消除波及破坏 Box
- Box 全清后 PhasePresetCondition 触发掉一个 HorizontalRocket（已实现）

**下一步具体任务：**

1. 写搜索脚本：找出满足"2×8 布局只有 1 个有效 swap + 该 swap 紧贴 Box 行"的颜色排列
2. 确定 Preset 掉落序列，确保 Move 1 cascade 后的局面可控
3. 跑模拟验证双层水坝的 cascade 行为
4. 调参（步数、目标数量）使胜率达到 98%+
5. 更新 `level_001.json` 和 `L001.md`

---

## L002-L003 方向（待设计）

前 3 关应形成体验递进：
- L001：决堤（空间突变，被动惊喜）
- L002：待定（可能用"引爆"或"内外翻转"模式，引入主动策略）
- L003：待定（组合多种元素，第一个有真正决策点的关卡）

---

## 系统需求备忘

以下是做关过程中发现的、已实现或待实现的系统特性：

| 特性 | 状态 | 说明 |
|------|------|------|
| LevelConfig.TileTypesCount | ✅ 已实现 | 每关颜色数控制 |
| ElementType.KeepEmpty | ✅ 已实现 | 初始空 Slot |
| PhasePresetCondition | ✅ 已实现 | 障碍全清后触发 Preset |
| 障碍内容物（破碎释放） | 待实现 | 泛化 Cupboard 的释放模式 |
| Spawner 激活/停用 | 待实现 | Spawner 绑定激活条件 |
| Spawner 权重软切换 | 待实现 | 目标完成后渐变颜色分布 |
| 新障碍品种（TreasureBox 等） | 待实现 | 有固定死亡效果的新障碍 |
