---
name: unity
description: |
  同步 Core DLL 到 Unity 项目。

  触发词：同步到Unity、更新Unity、去Unity开发、Unity同步、更新DLL

  不触发：Unity报错（这个需要诊断问题）
allowed-tools: Bash(dotnet build:*), Bash(ls:*), Read
---

# Unity 同步

## 执行流程

### 1. 构建 Release 版本

```bash
dotnet build src/Match3.Presentation -c Release
```

这会自动同步以下 DLL 到 `unity/Assets/Plugins/Match3/`：
- Match3.Core.dll
- Match3.Presentation.dll
- Match3.Random.dll
- ZString.dll（依赖）
- System.Runtime.CompilerServices.Unsafe.dll（依赖）

### 2. 验证同步结果

检查 DLL 文件是否存在且时间戳已更新：
```bash
ls -la unity/Assets/Plugins/Match3/*.dll
```

### 3. 输出结果

```
## Unity 同步完成

| DLL | 大小 | 更新时间 |
|-----|------|----------|
| Match3.Core.dll | xxx KB | HH:MM:SS |
| Match3.Presentation.dll | xxx KB | HH:MM:SS |
| Match3.Random.dll | xxx KB | HH:MM:SS |
| ZString.dll | xxx KB | (依赖) |

Unity 项目位置：`unity/`
可以打开 Unity 编辑器继续开发了。
```

## 错误处理

### 构建失败
- 显示编译错误
- 提示修复后重新同步

### DLL 未更新
- 检查 PostBuild 配置
- 检查目标目录权限
