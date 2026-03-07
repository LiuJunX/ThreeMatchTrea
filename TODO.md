# TODO

开发过程中的想法和待办事项。

## 待处理

<!-- 格式: - [ ] 描述 (来源/日期) -->

- [ ] AI 关卡编辑器 E2E 测试：验证 AI 意图执行后 UI 正确更新（如添加笼子到 Cover 层）(2026-01-21)
- [ ] IntentExecutor 集成测试：验证 PaintCover/PaintGround 传入 None 类型时调用 Clear 方法（需引入 mocking 框架）(2026-01-21)

## 功能想法

<!-- 新功能、改进想法 -->

## 性能优化

<!-- 性能相关的优化点 -->
- [ ] OutlineEffect 描边性能：当前每 tile 一个额外 draw call + MaterialPropertyBlock 打断 SRP Batcher（64 tile = +64 draw call）。可考虑 URP Renderer Feature 方案或 GPU Instancing 合批 (2026-03-07)

## 代码重构

<!-- 代码质量、架构改进 -->

## 已完成

<!-- 完成后移到这里，保留记录 -->
