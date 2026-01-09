# ThreeMatchTrea
AI-Assisted Match-3 Game Engine (C# / Blazor)

## 📚 Documentation Index

We treat documentation as code. All architectural decisions and guides are located in the `docs/` directory.

### 🏛️ Architecture & Design
- **[Architecture Overview](docs/01-architecture/overview.md)**: High-level design, layering strategy, and DOD principles.
- **[Core Patterns](docs/01-architecture/core-patterns.md)**: Object pooling, zero-allocation logging, and randomness.

### 📖 Developer Guides
- **[Coding Standards](docs/02-guides/coding-standards.md)**: Naming conventions, style guides, and AI context rules.
- **[Setup Guide](docs/02-guides/setup.md)**: (Coming Soon) How to build and run locally.

### 📜 Decisions (ADR)
- **[0001-adopt-zstring](docs/04-adr/0001-adopt-zstring.md)**: Why we use `ZString` for logging.

---

## 🚀 Quick Start (Development Workflow)

### 1. ⚡ Fast Automated Testing
Validate logic and UI components in milliseconds without a browser.
```powershell
dotnet test
```

### 2. 🔥 Hot Reload Development
Start the Web project with hot-reload enabled.
```cmd
.\run-web.bat
```
*(Automatically handles port 5015 conflicts)*

## Project Structure
- `src/Match3.Core`: Pure C# game logic (Data-Oriented, No UI).
- `src/Match3.Web`: Blazor Server UI layer.
- `src/Match3.Tests`: Unit & Scenario tests.
- `src/Match3.ConfigTool`: Binary configuration generator.
