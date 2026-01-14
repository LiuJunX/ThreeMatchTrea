# 测试指南

## 测试分层策略

### 1. 单元测试（使用 Stub）
- **目的**：验证单个系统的逻辑正确性
- **特点**：快速、隔离、可控
- **局限**：无法发现系统协作问题

### 2. 集成测试（使用真实系统）
- **目的**：验证多系统协作行为
- **必须包含**：
  - GravitySystem + AnimationSystem 协作
  - 多帧连续模拟（不能只测最终状态）
  - 使用 `AnimationTestHelper` 进行多帧测试

### 3. 端到端测试
- **目的**：验证完整用户流程
- **包含**：输入 → 交换 → 动画 → 匹配 → 消除 → 重力 → 填充

---

## 新功能测试检查清单

### 交互功能（交换、点击等）
- [ ] 所有方向都测试了吗？（Up/Down/Left/Right）
- [ ] 有效操作测试了吗？（产生 match）
- [ ] 无效操作测试了吗？（不产生 match，需要回退）
- [ ] 边界位置测试了吗？（棋盘边缘）

### 动画相关功能
- [ ] 有多帧测试吗？（不能只检查最终状态）
- [ ] 动画过程中的中间状态验证了吗？
- [ ] 使用了真实的 AnimationSystem 还是 Stub？
- [ ] 稳定性检测同时检查了物理和动画吗？

### 多系统交互功能
- [ ] 有独立的集成测试吗？
- [ ] 测试了系统执行顺序的影响吗？
- [ ] 测试了边界条件下的系统协作吗？

---

## AnimationTestHelper 使用规范

### 何时使用
- 需要多帧模拟的测试
- 涉及动画完成后再执行后续逻辑的测试
- 需要验证动画过程（不只是结果）的测试

### 关键方法

```csharp
// 运行动画直到稳定（仅动画系统）
helper.AnimateUntilStable(ref state, animationSystem);

// 运行物理+动画直到稳定（同时检查两者）
helper.UpdateUntilStable(ref state, gravitySystem, animationSystem);

// 模拟交换并收集动画数据
var result = helper.SimulateSwap(ref state, animationSystem, indexA, indexB);

// 模拟无效交换（交换 → 动画 → 回退 → 动画）
var result = helper.SimulateInvalidSwap(ref state, animationSystem, indexA, indexB);
```

### 稳定性检测注意事项

```csharp
// 错误：只检查动画稳定性（会提前退出）
stable = animationSystem.Animate(ref state, dt);

// 正确：同时检查物理和动画稳定性
bool animStable = animationSystem.Animate(ref state, dt);
bool physicsStable = physics.IsStable(in state);
stable = animStable && physicsStable;
```

---

## 常见遗漏场景

### 1. 方向遗漏
```csharp
// 错误：只测一个方向
engine.OnSwipe(pos, Direction.Right);

// 正确：测试所有相关方向
[Theory]
[InlineData(Direction.Right)]
[InlineData(Direction.Left)]
[InlineData(Direction.Up)]
[InlineData(Direction.Down)]
public void OnSwipe_AllDirections_ShouldWork(Direction dir) { ... }
```

### 2. Stub 隐藏问题
```csharp
// Stub 会隐藏真实行为
private class StubAnimationSystem : IAnimationSystem
{
    public bool IsVisualAtTarget(...) => true; // 总是返回 true！
}

// 需要对应的集成测试使用真实 AnimationSystem
[Fact]
public void Integration_WithRealAnimationSystem() { ... }
```

### 3. 只测最终状态
```csharp
// 错误：只验证最终结果
RunAnimation();
Assert.Equal(expectedFinalPosition, tile.Position);

// 正确：也验证中间过程
var firstFrame = animationSystem.Animate(ref state, dt);
Assert.False(firstFrame, "第一帧应该在动画中");
Assert.True(tile.Position.Y > 0 && tile.Position.Y < target, "应该在移动中");
```

---

## PR 自查清单

提交 PR 前，确认：

1. **单元测试**：新功能的逻辑正确性
2. **集成测试**：与其他系统的协作正确性
3. **方向/边界**：覆盖所有输入变体
4. **多帧动画**：使用 AnimationTestHelper 测试动画流程
5. **回归测试**：运行 `dotnet test` 确保没有破坏现有功能
