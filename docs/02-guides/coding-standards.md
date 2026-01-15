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
3.  **CSS Isolation**: Use `.razor.css` files. No `<style>` tags in razor.

## 3. Documentation
*   **XML Comments**: All public members in `Match3.Core` require XML documentation.
*   **Comments**: Explain "why" and "how", not "what". Skip obvious code.

## 4. Testing
*   **Unit Tests**: Test Logic classes with mocked interfaces.
*   **Architecture Tests**: `Match3.Tests.Architecture` enforces layer rules automatically.

## Related Documents
*   Architecture & Performance: `docs/01-architecture/core-patterns.md`
*   Testing Guidelines: `docs/testing-guidelines.md`
