---
name: build-harmony
description: |
  构建鸿蒙包并安装到手机。

  触发词：鸿蒙、build harmony、安装到手机、导出鸿蒙、鸿蒙包、build and run harmony

  不触发：Unity构建报错（需要诊断问题）
allowed-tools: Bash(dotnet build:*), Bash(ls:*), Bash(cp:*), Bash(hdc:*), Bash("*hdc*":*), Bash("*Tuanjie*":*), Read, mcp__mcp-unity__recompile_scripts, mcp__mcp-unity__execute_menu_item, mcp__mcp-unity__get_console_logs
---

# 鸿蒙 Build & Run

## 环境信息

- **团结引擎**: `C:/Program Files/Tuanjie/Hub/Editor/2022.3.62t2/Editor/Tuanjie.exe`
- **OpenHarmony SDK**: `C:/Users/liujun/AppData/Local/OpenHarmony/Sdk/20`
- **Node.js**: `C:/Program Files/Huawei/DevEco Studio/tools/node` (v18)
- **JDK**: 团结自带 `OpenHarmonyPlayer/OpenJDK`
- **HDC**: `C:/Program Files/Huawei/DevEco Studio/sdk/default/openharmony/toolchains/hdc.exe`
- **签名文件**: `unity/user.p12`, `unity/debug.cer`, `unity/debug.p7b`
- **Keystore alias**: `match3explore`, 密码见 `signing/README.md`

## 执行流程

### 1. 同步 Core DLL

```bash
dotnet build src/Match3.Presentation -c Release
```

### 2. 触发鸿蒙构建

通过 MCP 执行编辑器菜单：

```
mcp__mcp-unity__execute_menu_item("Match3/Build OpenHarmony")
```

构建会阻塞编辑器，MCP 可能超时，这是正常的。

### 3. 等待构建完成

等待用户确认构建完成，或轮询 console logs 检查结果：

```
mcp__mcp-unity__get_console_logs(logType: "error", includeStackTrace: false, limit: 5)
```

成功标志：`[BuildOpenHarmony] Build succeeded!`
失败标志：`[BuildOpenHarmony] Build failed:`

### 4. 复制 HAP 文件

构建产物在 Temp 目录，复制到 builds：

```bash
cp "unity/Temp/StagingArea/Tools/entry-default-signed.hap" "builds/OpenHarmony/Match3Explore.hap"
```

### 5. 安装到手机

```bash
"/c/Program Files/Huawei/DevEco Studio/sdk/default/openharmony/toolchains/hdc.exe" install "builds/OpenHarmony/Match3Explore.hap"
```

### 6. 输出结果

```
## 鸿蒙 Build & Run 完成

| 步骤 | 状态 |
|------|------|
| DLL 同步 | ✅ |
| 构建 HAP | ✅ |
| 安装到设备 | ✅ |

包大小：xx MB
设备：xxx
```

## 错误处理

### 构建失败
- 读取 error logs 诊断问题
- 常见问题：SDK 版本不匹配、签名文件过期、Node.js 路径无效

### 设备未连接
```bash
"/c/Program Files/Huawei/DevEco Studio/sdk/default/openharmony/toolchains/hdc.exe" list targets
```
提示用户用 USB 连接手机并开启开发者模式。

### 安装失败
- 签名不匹配：先卸载旧版本再安装
- Profile 过期：需要重新从 AppGallery Connect 下载
