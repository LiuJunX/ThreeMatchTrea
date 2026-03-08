<!-- SOURCE_OF_TRUTH: 代码风格规范 -->
<!-- 其他文档应引用此文件，不应复制内容 -->

# Match3 Coding Standards

本文件是代码风格和编码规范的**真源文档**。

## 1. Code Style
*   **Format**: 4 spaces indent, CRLF, Allman braces (start on new line).
*   **Naming**:
    *   `_camelCase` for private fields.
    *   `PascalCase` for public members/classes.
    *   `IInterface` prefix for interfaces.
*   **Namespaces**: Use file-scoped namespaces (e.g., `namespace Match3.Core;`).
*   **Type Inference**: Use `var` when type is obvious from context.

## 2. Code Organization
1.  **Single Responsibility**: Split classes > 300 lines.
2.  **State Management**: Use explicit State structs. Logic classes must be stateless.

## 3. Event Subscription

Lambda 表达式创建的委托无法取消订阅，会导致内存泄漏。

```csharp
// ❌ 禁止：Lambda 无法取消订阅
event += () => DoSomething();
event += x => HandleValue(x);

// ✅ 方法组：可正确取消订阅
event += OnEventFired;
event -= OnEventFired;

// ✅ 缓存委托：需要 Lambda 时的替代方案
private Action _handler;

void Subscribe()
{
    _handler = () => DoSomething();
    event += _handler;
}

void OnDestroy()
{
    event -= _handler;
}
```

**Unity 特别注意**：MonoBehaviour 必须在 `OnDestroy` 中取消所有事件订阅。

## 4. Documentation
*   **XML Comments**: All public members in `Match3.Core` require XML documentation.
*   **Comments**: Explain "why" and "how", not "what". Skip obvious code.

## 5. Testing
*   **Unit Tests**: Test Logic classes with mocked interfaces.
*   **Architecture Tests**: `Match3.Tests.Architecture` enforces layer rules automatically.

## Related Documents
*   Architecture & Performance: `docs/01-architecture/core-patterns.md`
*   Testing Guidelines: `docs/testing-guidelines.md`
