# TODO

开发过程中的想法和待办事项。

## 待处理

<!-- 格式: - [ ] 描述 (来源/日期) -->
- [ ] 邻接消除设计 review：当前 CanReactAdjacent 混合了"任意邻接"（Box/Bush/Cupboard）和"同色邻接"（ColorBox）两种语义，由 NotifyBatchElimination 统一驱动。随着 ColorBox 引入，考虑是否需要拆分为独立路径（如 IAdjacentReaction 策略），使颜色匹配逻辑更显式、可扩展。关联：Curtain 的全局色消除也是另一种颜色反应模式 (2026-03-22)
- [ ] SimulationInvariantTests seed=1337 残留锁：ColorBomb combo session 的 Indestructible lock (0x10) 在 move 23 后残留于 index 11。已加防御性清理但根因未定位，疑似 session 生命周期时序问题 (2026-03-22)

## 功能想法

<!-- 新功能、改进想法 -->

## 性能优化

<!-- 性能相关的优化点 -->
- [ ] EngineSnapshot Clone 优化：当前每 tick 一次 GameState.Clone()（60~120次/秒），每次 6 个 new array + Array.Copy。方向：①双缓冲预分配代替 new+Clone；②有状态系统（ColorBomb sessions, LockScheduler, Projectile, Explosion）数据拉到 GameState 用固定数组，整体 Array.Copy 一把拷 (2026-03-19)
- [ ] OutlineEffect 描边性能：当前每 tile 一个额外 draw call + MaterialPropertyBlock 打断 SRP Batcher（64 tile = +64 draw call）。可考虑 URP Renderer Feature 方案或 GPU Instancing 合批 (2026-03-07)

## 代码重构

<!-- 代码质量、架构改进 -->
- [ ] 有状态系统快照统一：ProjectileSystem、ExplosionSystem 目前 restore 时直接清空（与 ColorBomb/LockScheduler 已修复的问题相同）。如果出现投射物/爆炸中途丢失，需按相同 pattern 补快照。长期考虑将所有跨 tick 状态统一到 GameState 或独立的 ActiveEffectsState 层 (2026-03-19)

## 已完成

<!-- 完成后移到这里，保留记录 -->
- [x] AI 关卡编辑器 E2E 测试 — Web 项目已删除 (2026-03)
- [x] IntentExecutor 集成测试 — Web 项目已删除 (2026-03)
