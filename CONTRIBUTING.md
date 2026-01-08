# Contributing (简要)

- 随机管理
  - 所有随机必须通过 Match3.Random 的 SeedManager 和 RandomDomain。
  - 禁止在 Core/Web 使用 System.Random 或 Guid.NewGuid。
  - 新增随机点需先在 RandomDomain 中声明新域，并通过 SeedManager 注入。

- 代码检查
  - 规范测试会在违规时失败：src/Match3.Tests/CodingStandardsTests.cs。
  - 测试会扫描 src/Match3.Core 与 src/Match3.Web 的代码，确保未直接使用 System.Random/Guid。

- 模块边界
  - Match3.Random 为独立底层模块；Core/Web/AI 仅消费随机，不实现随机。
  - 需要固定随机时，使用 SeedManager.SetOverride(domain, seed) 在测试或装配层配置。

- 变更建议
  - 新增随机域时，优先使用枚举，不使用字符串，避免 GC 与误拼写。
