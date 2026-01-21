# ThreeMatchTrea

AI-Assisted Match-3 Game Engine (C# / Blazor)

## Key Features

- **Event-Sourced Simulation**: Tick-based engine with deterministic replay support
- **AI Difficulty Analysis**: Win rate, deadlock detection, and multi-tier player simulation
- **Deep Analysis**: 7 advanced metrics including flow curve, skill sensitivity, and P95 clear attempts
- **AI Level Editor**: Natural language level creation and modification via LLM integration
- **Slot-Based Grid**: 5-layer architecture (Topology, Ground, Unit, Cover, Aux)
- **Cross-Platform Core**: Pure C# logic with zero UI dependencies

## Quick Start

### Prerequisites
- .NET SDK 8.0+ ([Download](https://dotnet.microsoft.com/download))
- Mac: `brew install dotnet`

### Run Tests
```bash
dotnet test
```

### Hot Reload Development

**Windows:**
```cmd
.\run-web.bat
```

**Mac/Linux:**
```bash
chmod +x run-web.sh   # first time only
./run-web.sh
```

Visit http://localhost:5015

## Project Structure

```
src/
├── Match3.Core           # Pure game logic (Event Sourcing, Simulation, AI)
├── Match3.Presentation   # Animation & visual state management
├── Match3.Random         # Unified RNG (SeedManager, RandomDomain)
├── Match3.Editor         # Cross-platform level editor logic
├── Match3.Web            # Blazor Server UI
├── Match3.ConfigTool     # Configuration generator
└── Match3.*.Tests        # Test projects
```

## Documentation

All documentation lives in `/docs` (Docs-as-Code).

### Architecture
| Document | Description |
|----------|-------------|
| [Architecture Overview](docs/01-architecture/overview.md) | Layering, event system, simulation engine |
| [Core Patterns](docs/01-architecture/core-patterns.md) | Object pooling, zero-allocation logging |
| [Level Analysis](docs/01-architecture/level-analysis-system.md) | AI difficulty analysis system |
| [Deep Analysis](docs/01-architecture/deep-analysis-design.md) | Advanced metrics for level quality |

### Guides
| Document | Description |
|----------|-------------|
| [Coding Standards](docs/02-guides/coding-standards.md) | Naming conventions, style guide |
| [LLM Configuration](docs/02-guides/llm-configuration.md) | AI chat service setup |

### Features
| Document | Description |
|----------|-------------|
| [AI Level Editor](docs/03-design/features/ai-level-editor.md) | Natural language level editing |
| [Replay System](docs/03-design/features/replay-system.md) | Deterministic game recording |
| [Matching System](docs/03-design/features/matching-system.md) | Match detection and bomb generation |

### ADR (Architecture Decision Records)
- [0001-adopt-zstring](docs/04-adr/0001-adopt-zstring.md): ZString for logging
- [0003-event-sourcing](docs/04-adr/0003-event-sourcing-simulation.md): Event-sourced simulation
- [0004-pure-player](docs/04-adr/0004-pure-player-architecture.md): Presentation architecture
