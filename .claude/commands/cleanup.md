---
allowed-tools: Read, Grep, Glob, Edit, Bash(git:*)
description: Scan and fix code issues like unused imports
---

# 自动化清理命令

**用途**：CI/定时任务/脚本调用（非日常使用）

日常使用请直接说"清理代码"、"整理一下"等自然语言，Claude 会自动识别。

## 命令参数

```bash
/cleanup [--scope=incremental|all]
```

| 参数 | 值 | 说明 |
|------|-----|------|
| `--scope` | `incremental`（默认） | 只清理修改的文件 |
| | `all` | 清理整个项目 |

## 清理内容

1. **未使用的 using 声明** - 删除未引用的命名空间导入
2. **重复的 using 声明** - 删除重复导入
3. **using 排序** - System 开头优先，然后按字母顺序
4. **空白行规范** - 文件末尾单个空行
5. **尾随空格** - 删除行尾空格

## 排除目录

- `**/obj/**`
- `**/bin/**`
- `**/*.g.cs`（生成的文件）
