---
name: test-check
description: |
  测试覆盖深度分析（专项）。

  触发词：测试覆盖率、测试够不够、缺什么测试、测试质量、测试完整性
  不触发：运行测试、跑测试、检查代码（这些用 check skill）

  与 check skill 的区别：
  - check skill：综合检查，包含基础测试运行
  - test-check skill：专项分析，详细的测试覆盖报告
allowed-tools: Read, Grep, Glob, Bash(dotnet test:*)
---

# 测试覆盖深度分析

**定位**：测试专项分析，提供详细的覆盖报告和改进建议

与 check skill 的区别：
- `check`：综合检查，测试只看通过/失败
- `test-check`：专项分析，详细的覆盖率、缺失测试、质量评估

## 检查标准（真源）

- `docs/testing-guidelines.md` - 测试指南

## 分析内容

### 1. 测试覆盖映射
| 源文件 | 测试文件 |
|--------|---------|
| `Match3.Core/Systems/**/*.cs` | `Match3.Core.Tests/Systems/**/*Tests.cs` |
| `Match3.Editor/**/*.cs` | `Match3.Editor.Tests/**/*Tests.cs` |

### 2. 缺失测试识别
- 扫描源文件的公共方法
- 检查是否有对应测试

### 3. 测试质量评估（按 testing-guidelines.md）
- 输入变体覆盖（所有方向、边界）
- 集成测试存在（真实系统交互）
- 多帧行为验证（动画、物理）

## 输出格式

```
## 测试覆盖分析报告

### 覆盖统计
- 源文件: X 个
- 已覆盖: Y 个 (Z%)
- 缺失测试: N 个

### 缺失测试详情
| 文件 | 方法 | 建议 |
|------|------|------|
| XxxSystem.cs | ProcessXxx | 需要单元测试 |

### 质量检查
| 检查项 | 状态 | 说明 |
|--------|------|------|
| 输入变体 | ⚠️ | SwapSystem 只测了 Right 方向 |
| 集成测试 | ✅ | 有真实系统测试 |
| 多帧验证 | ❌ | 缺少 AnimationTestHelper 使用 |

### 改进建议
1. ...
2. ...
```
