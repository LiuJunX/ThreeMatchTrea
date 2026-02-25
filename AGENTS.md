# AGENTS.md

## Cursor Cloud specific instructions

### Services overview

This is a C# Match-3 game engine with a Blazor WebAssembly UI. No databases, Docker, or external services are required for core development. Standard commands are in the `Makefile` and `README.md`.

### Running the web app

The web app (`Match3.Web`) has a pre-existing DI validation bug: `WebPlatformService` depends on `IJSRuntime` which is not registered on the server side. This causes a crash in Development mode (where `ValidateOnBuild` is enabled by default in .NET 9+). Workaround:

```bash
# Publish then run from publish dir (serves Blazor WASM static assets correctly)
dotnet publish src/Match3.Web/Match3.Web.csproj -c Debug -o /tmp/match3-publish
cd /tmp/match3-publish && ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="http://0.0.0.0:5015" dotnet Match3.Web.dll
```

The app is then available at `http://localhost:5015`.

### Running tests

- `dotnet test` runs all test suites. See `Makefile` for aliases (`make test`, `make test-verbose`).
- **Core.Tests simulation/performance tests are extremely slow on cloud VMs** (can take 20+ minutes). To run Core.Tests without performance tests: `dotnet test src/Match3.Core.Tests --filter "FullyQualifiedName!~Performance"`
- Even non-performance simulation tests in Core.Tests can be slow. For quick validation, target specific test categories: `dotnet test src/Match3.Core.Tests --filter "FullyQualifiedName~Config"` or `FullyQualifiedName~Matching`.
- 3 performance test failures and 2 `ChoreographerPlayerIntegrationTests` failures are pre-existing and not caused by environment setup.

### Lint / format

- `dotnet format --verify-no-changes` — the repo has pre-existing formatting issues (whitespace, end-of-line markers), so this command exits non-zero on the current codebase.

### .NET SDK

The project requires .NET SDK 10.0+ (via `global.json` with `rollForward: latestMajor`) plus the ASP.NET Core 9.0 runtime (since `Match3.Web` targets `net9.0`). Both are installed to `$HOME/.dotnet` and added to `PATH` via `~/.bashrc`.
