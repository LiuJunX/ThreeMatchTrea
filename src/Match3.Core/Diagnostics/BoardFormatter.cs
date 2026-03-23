using System;
using System.Text;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Diagnostics;

/// <summary>
/// Formats board state (LevelConfig or GameState) as human/AI-readable multi-layer text.
/// Empty layers are omitted. Each cell is 4 chars wide (3 content + 1 space).
/// </summary>
public static class BoardFormatter
{
    /// <summary>Format a LevelConfig (design-time view).</summary>
    public static string Format(LevelConfig config, string? title = null)
    {
        int w = config.Width, h = config.Height;
        var sb = new StringBuilder(512);

        // Header
        FormatHeader(sb, title, w, h, config.MoveLimit, config.TargetDifficulty);

        // Objectives
        FormatObjectives(sb, config.Objectives);

        // Spawner summary
        if (config.Spawners != null && config.Spawners.Length > 0)
            FormatSpawners(sb, config.Spawners);

        // Layers — extract from flat arrays
        FormatTileLayer(sb, w, h, (x, y) =>
        {
            int i = y * w + x;
            return config.Grid != null && i < config.Grid.Length ? config.Grid[i] : ElementType.None;
        });

        FormatCellLayer(sb, w, h, (x, y) =>
        {
            int i = y * w + x;
            return config.Cells != null && i < config.Cells.Length ? config.Cells[i] : CellKind.Slot;
        });

        FormatObstacleLayer(sb, w, h, (x, y) =>
        {
            int i = y * w + x;
            var type = config.Obstacles != null && i < config.Obstacles.Length ? config.Obstacles[i] : ObstacleType.None;
            byte stage = config.ObstacleStages != null && i < config.ObstacleStages.Length ? config.ObstacleStages[i] : (byte)0;
            byte state = config.ObstacleStates != null && i < config.ObstacleStates.Length ? config.ObstacleStates[i] : (byte)0;
            return (type, stage, state);
        });

        FormatGroundLayer(sb, w, h, (x, y) =>
        {
            int i = y * w + x;
            var type = config.Grounds != null && i < config.Grounds.Length ? config.Grounds[i] : GroundType.None;
            byte hp = config.GroundHealths != null && i < config.GroundHealths.Length ? config.GroundHealths[i] : (byte)0;
            return (type, hp);
        });

        FormatCoverLayer(sb, w, h, (x, y) =>
        {
            int i = y * w + x;
            var type = config.Covers != null && i < config.Covers.Length ? config.Covers[i] : CoverType.None;
            byte hp = config.CoverHealths != null && i < config.CoverHealths.Length ? config.CoverHealths[i] : (byte)0;
            return (type, hp);
        });

        return sb.ToString().TrimEnd();
    }

    /// <summary>Format a GameState snapshot (runtime view).</summary>
    public static string Format(ref GameState state, GameStateFormatContext ctx = default)
    {
        int w = state.Width, h = state.Height;
        int len = w * h;

        // Extract all cell data upfront (ref GameState can't be captured in lambdas)
        var tiles = new ElementType[len];
        var cells = new CellKind[len];
        var obstacles = new (ObstacleType type, byte stage, byte state)[len];
        var grounds = new (GroundType type, byte health)[len];
        var covers = new (CoverType type, byte health)[len];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                tiles[i] = state.GetTile(x, y).Type;
                cells[i] = state.GetCell(x, y);
                ref var obs = ref state.GetObstacle(x, y);
                obstacles[i] = (obs.Type, obs.Stage, obs.State);
                ref var gnd = ref state.GetGround(x, y);
                grounds[i] = (gnd.Type, gnd.Health);
                ref var cov = ref state.GetCover(x, y);
                covers[i] = (cov.Type, cov.Health);
            }

        var sb = new StringBuilder(512);

        // Header
        FormatHeader(sb, ctx.Title, w, h, ctx.RemainingMoves, null);

        // Objective progress
        if (ctx.Objectives != null)
            FormatObjectiveProgress(sb, ctx.Objectives, ctx.ObjectiveProgress);

        // Layers
        FormatTileLayer(sb, w, h, (x, y) => tiles[y * w + x]);
        FormatCellLayer(sb, w, h, (x, y) => cells[y * w + x]);
        FormatObstacleLayer(sb, w, h, (x, y) => obstacles[y * w + x]);
        FormatGroundLayer(sb, w, h, (x, y) => grounds[y * w + x]);
        FormatCoverLayer(sb, w, h, (x, y) => covers[y * w + x]);

        return sb.ToString().TrimEnd();
    }

    // ── Header ──────────────────────────────────────────────

    private static void FormatHeader(StringBuilder sb, string? title, int w, int h, int? moves, float? difficulty)
    {
        if (title != null)
            sb.AppendLine($"=== {title} ({w}x{h}) ===");
        else
            sb.AppendLine($"=== {w}x{h} ===");

        var parts = new StringBuilder();
        if (moves.HasValue) parts.Append($"Moves: {moves.Value}");
        if (difficulty.HasValue)
        {
            if (parts.Length > 0) parts.Append(" | ");
            parts.Append($"Difficulty: {difficulty.Value:F2}");
        }
        if (parts.Length > 0)
            sb.AppendLine(parts.ToString());

        sb.AppendLine();
    }

    // ── Objectives ──────────────────────────────────────────

    private static void FormatObjectives(StringBuilder sb, LevelObjective[]? objectives)
    {
        if (objectives == null) return;
        bool any = false;
        for (int i = 0; i < objectives.Length; i++)
        {
            var obj = objectives[i];
            if (obj.TargetCount <= 0) continue;
            if (!any) { sb.AppendLine("--- Objectives ---"); any = true; }
            sb.AppendLine($" {i + 1}. {obj.TargetLayer}/{FormatObjectiveElement(obj)} x{obj.TargetCount}");
        }
        if (any) sb.AppendLine();
    }

    private static void FormatObjectiveProgress(StringBuilder sb, LevelObjective[] objectives, int[]? progress)
    {
        bool any = false;
        for (int i = 0; i < objectives.Length; i++)
        {
            var obj = objectives[i];
            if (obj.TargetCount <= 0) continue;
            if (!any) { sb.AppendLine("--- Objectives ---"); any = true; }
            int current = progress != null && i < progress.Length ? progress[i] : 0;
            sb.AppendLine($" {i + 1}. {obj.TargetLayer}/{FormatObjectiveElement(obj)} x{obj.TargetCount}  [{current}/{obj.TargetCount}]");
        }
        if (any) sb.AppendLine();
    }

    private static string FormatObjectiveElement(LevelObjective obj)
    {
        return obj.TargetLayer switch
        {
            ObjectiveTargetLayer.Tile => TileSymbol((ElementType)obj.ElementType),
            ObjectiveTargetLayer.Obstacle => ObstacleSymbol((ObstacleType)obj.ElementType),
            ObjectiveTargetLayer.Ground => GroundSymbol((GroundType)obj.ElementType),
            ObjectiveTargetLayer.Cover => CoverSymbol((CoverType)obj.ElementType),
            _ => obj.ElementType.ToString()
        };
    }

    // ── Spawners ────────────────────────────────────────────

    private static void FormatSpawners(StringBuilder sb, SpawnerConfig[] spawners)
    {
        sb.AppendLine("--- Spawners ---");
        foreach (var sp in spawners)
        {
            var cols = sp.Columns != null ? string.Join(",", sp.Columns) : "all";
            var mode = sp.Preset != null ? "Preset" : sp.Weights != null ? "Weighted" : "Default";
            sb.AppendLine($" [{sp.Id}] cols={cols} mode={mode}");
        }
        sb.AppendLine();
    }

    // ── Layer Rendering ─────────────────────────────────────

    private delegate ElementType TileReader(int x, int y);
    private delegate CellKind CellReader(int x, int y);
    private delegate (ObstacleType type, byte stage, byte state) ObstacleReader(int x, int y);
    private delegate (GroundType type, byte health) GroundReader(int x, int y);
    private delegate (CoverType type, byte health) CoverReader(int x, int y);

    private static void FormatTileLayer(StringBuilder sb, int w, int h, TileReader read)
    {
        bool any = false;
        for (int y = 0; y < h && !any; y++)
            for (int x = 0; x < w && !any; x++)
                if (read(x, y) != ElementType.None) any = true;
        if (!any) return;

        sb.AppendLine("--- Tiles ---");
        WriteColumnHeader(sb, w);
        for (int y = 0; y < h; y++)
        {
            sb.Append($"{y,2} |");
            for (int x = 0; x < w; x++)
                sb.Append($" {TileSymbol(read(x, y)),-3}");
            sb.AppendLine();
        }
        sb.AppendLine();
    }

    private static void FormatCellLayer(StringBuilder sb, int w, int h, CellReader read)
    {
        // Only render if there are non-Slot cells
        bool any = false;
        for (int y = 0; y < h && !any; y++)
            for (int x = 0; x < w && !any; x++)
            {
                var c = read(x, y);
                if (c != CellKind.Slot && c != CellKind.Spawner) any = true;
            }
        if (!any) return;

        sb.AppendLine("--- Cells ---");
        WriteColumnHeader(sb, w);
        for (int y = 0; y < h; y++)
        {
            sb.Append($"{y,2} |");
            for (int x = 0; x < w; x++)
                sb.Append($" {CellSymbol(read(x, y)),-3}");
            sb.AppendLine();
        }
        sb.AppendLine();
    }

    private static void FormatObstacleLayer(StringBuilder sb, int w, int h, ObstacleReader read)
    {
        bool any = false;
        for (int y = 0; y < h && !any; y++)
            for (int x = 0; x < w && !any; x++)
                if (read(x, y).type != ObstacleType.None) any = true;
        if (!any) return;

        sb.AppendLine("--- Obstacles ---");
        WriteColumnHeader(sb, w);
        for (int y = 0; y < h; y++)
        {
            sb.Append($"{y,2} |");
            for (int x = 0; x < w; x++)
            {
                var (type, stage, state) = read(x, y);
                sb.Append($" {ObstacleCell(type, stage, state),-3}");
            }
            sb.AppendLine();
        }
        sb.AppendLine();
    }

    private static void FormatGroundLayer(StringBuilder sb, int w, int h, GroundReader read)
    {
        bool any = false;
        for (int y = 0; y < h && !any; y++)
            for (int x = 0; x < w && !any; x++)
                if (read(x, y).type != GroundType.None) any = true;
        if (!any) return;

        sb.AppendLine("--- Grounds ---");
        WriteColumnHeader(sb, w);
        for (int y = 0; y < h; y++)
        {
            sb.Append($"{y,2} |");
            for (int x = 0; x < w; x++)
            {
                var (type, hp) = read(x, y);
                sb.Append($" {GroundCell(type, hp),-3}");
            }
            sb.AppendLine();
        }
        sb.AppendLine();
    }

    private static void FormatCoverLayer(StringBuilder sb, int w, int h, CoverReader read)
    {
        bool any = false;
        for (int y = 0; y < h && !any; y++)
            for (int x = 0; x < w && !any; x++)
                if (read(x, y).type != CoverType.None) any = true;
        if (!any) return;

        sb.AppendLine("--- Covers ---");
        WriteColumnHeader(sb, w);
        for (int y = 0; y < h; y++)
        {
            sb.Append($"{y,2} |");
            for (int x = 0; x < w; x++)
            {
                var (type, hp) = read(x, y);
                sb.Append($" {CoverCell(type, hp),-3}");
            }
            sb.AppendLine();
        }
        sb.AppendLine();
    }

    private static void WriteColumnHeader(StringBuilder sb, int w)
    {
        sb.Append("    ");
        for (int x = 0; x < w; x++)
            sb.Append($" {x,-3}");
        sb.AppendLine();
    }

    // ── Symbol Mapping ──────────────────────────────────────

    private static string TileSymbol(ElementType t) => t switch
    {
        ElementType.None => ".",
        ElementType.Item1 => "R",
        ElementType.Item2 => "G",
        ElementType.Item3 => "B",
        ElementType.Item4 => "Y",
        ElementType.Item5 => "P",
        ElementType.Item6 => "O",
        ElementType.HorizontalRocket => "H>",
        ElementType.VerticalRocket => "V^",
        ElementType.ColorBomb => "CB",
        ElementType.Ufo => "UF",
        ElementType.Square5x5 => "5x",
        ElementType.Bird => "Bd",
        ElementType.Pearl => "Pl",
        ElementType.Plate => "Pt",
        ElementType.Envelope => "Ev",
        ElementType.Diamond => "Dm",
        ElementType.Unmatchable => "XX",
        _ => "?"
    };

    private static string CellSymbol(CellKind c) => c switch
    {
        CellKind.Void => "##",
        CellKind.Slot => ".",
        CellKind.Wall => "WL",
        CellKind.Spawner => "Sp",
        CellKind.Sink => "Sk",
        _ => "?"
    };

    private static string ObstacleSymbol(ObstacleType t) => t switch
    {
        ObstacleType.None => ".",
        ObstacleType.Box => "Bx",
        ObstacleType.Bush => "Bu",
        ObstacleType.Safe => "Sf",
        ObstacleType.ColorBox => "Cx",
        ObstacleType.MagicHat => "Mh",
        ObstacleType.Curtain => "Ct",
        ObstacleType.Cupboard => "Cp",
        ObstacleType.Mailbox => "Mb",
        ObstacleType.Owl => "Ow",
        ObstacleType.Stone => "St",
        ObstacleType.PotionBottle => "Pb",
        _ => "?"
    };

    private static string GroundSymbol(GroundType t) => t switch
    {
        GroundType.None => ".",
        GroundType.Ice => "Ic",
        GroundType.Grass => "Gs",
        GroundType.Leaves => "Lv",
        _ => "?"
    };

    private static string CoverSymbol(CoverType t) => t switch
    {
        CoverType.None => ".",
        CoverType.Cage => "Cg",
        CoverType.Chain => "Ch",
        CoverType.Bubble => "Bb",
        CoverType.Honey => "Hn",
        CoverType.Frost => "Fr",
        _ => "?"
    };

    // ── Composite Cell Symbols (with stage/health) ──────────

    private static string ObstacleCell(ObstacleType type, byte stage, byte state)
    {
        if (type == ObstacleType.None) return ".";
        var sym = ObstacleSymbol(type);

        // Color variants: append color initial
        if (type == ObstacleType.ColorBox || type == ObstacleType.Curtain)
        {
            var colorChar = ColorInitial((ElementType)state);
            return $"{sym[0]}{colorChar}";
        }

        // Stage > 1: append digit
        if (stage > 1) return $"{sym}{stage}";
        return sym;
    }

    private static string GroundCell(GroundType type, byte health)
    {
        if (type == GroundType.None) return ".";
        var sym = GroundSymbol(type);
        if (health > 1) return $"{sym[0]}{sym[1]}{health}"[..3];
        return sym;
    }

    private static string CoverCell(CoverType type, byte health)
    {
        if (type == CoverType.None) return ".";
        var sym = CoverSymbol(type);
        if (health > 1) return $"{sym[0]}{sym[1]}{health}"[..3];
        return sym;
    }

    private static char ColorInitial(ElementType color) => color switch
    {
        ElementType.Item1 => 'R',
        ElementType.Item2 => 'G',
        ElementType.Item3 => 'B',
        ElementType.Item4 => 'Y',
        ElementType.Item5 => 'P',
        ElementType.Item6 => 'O',
        _ => '?'
    };
}

/// <summary>
/// Optional context for GameState formatting (runtime info not in GameState itself).
/// </summary>
public struct GameStateFormatContext
{
    public string? Title;
    public int? RemainingMoves;
    public LevelObjective[]? Objectives;
    public int[]? ObjectiveProgress;
}
