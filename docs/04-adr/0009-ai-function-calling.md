# ADR-0009: AI 编辑器采用 Function Calling

* **Status**: Archived (Web 项目已移除)
* **Deciders**: AI Assistant, Development Team
* **Date**: 2026-01-21

## Context and Problem Statement

AI 对话式关卡编辑器 v1.0 通过解析 LLM 响应中的 JSON 来提取操作意图。这种方式存在可靠性问题：LLM 可能输出格式不符合预期的 JSON，或在文本中混入 JSON，导致解析失败。

如何提高 AI 编辑器执行操作的可靠性，同时支持新的分析功能？

## Decision Drivers

* **可靠性**：JSON 解析容易因格式问题失败，需要更可靠的方式
* **类型安全**：参数应有明确的类型约束（整数范围、枚举值）
* **可扩展性**：需要支持新增分析工具（关卡分析、深度分析）
* **标准化**：遵循行业标准，便于切换 LLM 提供商
* **开发效率**：减少提示词工程的复杂度

## Considered Options

1. **继续使用 JSON 解析** - 优化提示词和解析逻辑
2. **采用 Function Calling** - 使用 OpenAI 兼容的 tools API
3. **采用结构化输出** - 使用 JSON Schema 约束输出格式

## Decision Outcome

Chosen option: **"采用 Function Calling"**，因为：
- 原生支持参数类型约束和枚举值
- LLM 明确知道何时调用工具、调用哪个工具
- 支持多轮工具调用，适合分析 + 编辑的复合场景
- 主流 LLM 提供商（DeepSeek、OpenAI、Azure）均支持

### Positive Consequences

* **可靠性提升**：工具调用由 LLM 原生支持，不依赖 JSON 解析
* **类型安全**：参数有 minimum/maximum/enum 约束，减少无效输入
* **多工具并行**：LLM 可在单次响应中调用多个工具
* **分析集成**：自然支持分析工具，执行后返回结果给 LLM 继续对话
* **提示词简化**：无需在系统提示词中描述 JSON 格式

### Negative Consequences

* **提供商依赖**：需要 LLM 支持 Function Calling（tools 参数）
* **流式受限**：工具调用模式下流式输出较复杂，当前降级为非流式
* **参数转换**：snake_case (API) → camelCase (代码) 需要转换

## Validation

* **单元测试**: 覆盖工具调用解析和参数转换

> **注意**: 实现代码已随 `Match3.Web` 项目移除。本 ADR 保留作为架构决策记录。

## Pros and Cons of the Options

### Option 1: 继续使用 JSON 解析

* Good, because 无需修改现有架构
* Good, because 所有 LLM 都支持
* Bad, because LLM 输出格式不稳定
* Bad, because 提示词需要大量篇幅描述格式要求
* Bad, because 难以扩展复杂场景

### Option 2: 采用 Function Calling ✓

* Good, because 原生类型约束，参数验证由 LLM 完成
* Good, because 支持多轮工具调用，适合复合任务
* Good, because 行业标准，主流提供商均支持
* Bad, because 需要 LLM 支持 tools 参数
* Bad, because 流式输出处理较复杂

### Option 3: 采用结构化输出 (JSON Schema)

* Good, because 保证输出符合指定 schema
* Bad, because 不是所有 LLM 都支持
* Bad, because 不支持多轮交互和工具执行结果反馈

## References

* [OpenAI Function Calling](https://platform.openai.com/docs/guides/function-calling)
* [DeepSeek API - Function Calling](https://platform.deepseek.com/api-docs/function-calling)
