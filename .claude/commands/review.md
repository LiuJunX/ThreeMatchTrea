---
allowed-tools: Read, Grep, Glob, Bash(dotnet:*), Bash(git:*)
description: Automated code review for CI/scripts (use natural language for daily work)
---

# 自动化审查命令

**用途**：CI/定时任务/脚本调用（非日常使用）

日常使用请直接说"检查代码"、"深度 review"等自然语言，Claude 会自动识别。

## 命令参数

```bash
/review [--scope=conversation|git|all] [--depth=quick|deep]
```

| 参数 | 值 | 说明 |
|------|-----|------|
| `--scope` | `conversation`（默认） | 本次对话修改的文件 |
| | `git` | git status 所有修改文件 |
| | `all` | 整个项目 |
| `--depth` | `quick`（默认） | 快速审查（构建+测试+基础规范） |
| | `deep` | 深度审查（+命名职责+依赖方向+API表面积+覆盖分析） |

## 使用示例

```bash
# 每日定时任务
/review --scope=all --depth=deep

# PR 检查
/review --scope=git --depth=quick

# 快速增量（等同于默认）
/review
```
