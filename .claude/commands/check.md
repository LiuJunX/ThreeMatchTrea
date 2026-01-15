---
allowed-tools: Read, Grep, Glob, Bash(dotnet:*), Bash(git:*)
description: Automated code check for CI/scripts (use natural language for daily work)
---

# 自动化检查命令

**用途**：CI/定时任务/脚本调用（非日常使用）

日常使用请直接说"检查代码"、"全项目深度检查"等自然语言，Claude 会自动识别。

## 命令参数

```bash
/check [--scope=incremental|all] [--depth=quick|deep]
```

| 参数 | 值 | 说明 |
|------|-----|------|
| `--scope` | `incremental`（默认） | 只检查修改的文件 |
| | `all` | 检查整个项目 |
| `--depth` | `quick`（默认） | 快速检查 |
| | `deep` | 深度检查 |

## 使用示例

```bash
# 每日定时任务
/check --scope=all --depth=deep

# PR 检查
/check --scope=all --depth=quick

# 快速增量（等同于默认）
/check
```

## 检查内容

### 快速检查
- 构建验证（dotnet build）
- 测试运行（dotnet test）
- 基础规范检查

### 深度检查
- 构建验证
- 测试运行
- 代码风格详细检查
- 测试覆盖分析
- 真源一致性检查

## 输出格式

```
## 检查执行摘要

**模式**：[scope] + [depth]
**范围**：N 个文件

### 检查结果
| 检查项 | 状态 |
|--------|------|
| 构建验证 | ✅ / ❌ |
| 测试运行 | ✅ / ❌ |
| 代码风格 | ✅ / ⚠️ |
| 测试覆盖 | ✅ / ⚠️ |
| 真源一致性 | ✅ / ❌ |
```
