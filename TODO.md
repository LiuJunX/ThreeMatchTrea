# TODO

开发过程中的想法和待办事项。

## 待处理

<!-- 格式: - [ ] 描述 (来源/日期) -->

## 功能想法

<!-- 新功能、改进想法 -->

## 性能优化

<!-- 性能相关的优化点 -->
- [ ] OutlineEffect 描边性能：当前每 tile 一个额外 draw call + MaterialPropertyBlock 打断 SRP Batcher（64 tile = +64 draw call）。可考虑 URP Renderer Feature 方案或 GPU Instancing 合批 (2026-03-07)

## 代码重构

<!-- 代码质量、架构改进 -->

## 已完成

<!-- 完成后移到这里，保留记录 -->
- [x] AI 关卡编辑器 E2E 测试 — Web 项目已删除 (2026-03)
- [x] IntentExecutor 集成测试 — Web 项目已删除 (2026-03)
