# 关卡编辑器重构计划 (Editor Core Refactoring)

本计划旨在将 `LevelEditorViewModel` 解耦为独立的逻辑模块，实现逻辑与显示分离，并建立自动化测试基础。

## 1. 新增逻辑层 (Match3.Editor/Logic)
我们将创建纯 C# 类来承载核心逻辑，不依赖任何 UI 框架。

*   **`EditorSession.cs` (状态容器)**
    *   管理 `EditorMode` (Level/Scenario)。
    *   持有当前编辑的数据 (`LevelConfig`, `ScenarioConfig`)。
    *   管理 `IsDirty` 状态。
    *   提供状态变更事件。

*   **`GridManipulator.cs` (网格算法)**
    *   `ResizeGrid(LevelConfig source, int newWidth, int newHeight)`: 纯函数式的尺寸调整。
    *   `GenerateRandomLevel(int width, int height, int seed)`: 随机生成逻辑。
    *   `PaintTile(LevelConfig config, int index, TileType type, BombType bomb)`: 绘图逻辑。
    *   **目的**：可独立测试网格操作，无需启动模拟器。

*   **`SimulationRunner.cs` (模拟控制)**
    *   封装 `Match3Engine` 的生命周期。
    *   处理 `Update` 循环和 `Timer`（如果需要与平台解耦，将 Timer 抽象）。
    *   处理 `Recording` 录制逻辑。

## 2. 重构表现层 (Match3.Editor/ViewModels)
*   **`LevelEditorViewModel.cs` (瘦身)**
    *   移除所有网格处理算法和模拟逻辑。
    *   通过组合（Composition）持有 `EditorSession`, `GridManipulator`, `SimulationRunner`。
    *   作为 View (Blazor) 与 Logic 之间的胶水层，负责数据绑定和命令转发。

## 3. 单元测试 (Match3.Editor.Tests)
*   **`GridManipulatorTests.cs`**: 测试调整大小是否保留数据、随机生成是否确定性。
*   **`EditorSessionTests.cs`**: 测试状态切换和脏标记逻辑。

## 执行步骤
1.  在 `Match3.Editor` 项目中创建 `Logic` 目录。
2.  实现 `EditorSession` 和 `GridManipulator` (优先保证核心数据操作正确)。
3.  实现 `SimulationRunner` 剥离模拟逻辑。
4.  重构 `LevelEditorViewModel` 对接新模块。
5.  添加基础单元测试验证重构结果。

此方案将优先完成代码结构的拆分与重构，随后再统一解决编译与运行问题。