using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Random;

namespace Match3.Core.Config;

/// <summary>
/// Generates CellKind[] arrays for board shapes.
/// Void cells define the shape; Spawner cells are placed at the top of each column region.
/// </summary>
public static class ShapeTemplates
{
    /// <summary>
    /// Generate cell layout for the given shape and dimensions.
    /// </summary>
    public static CellKind[] Generate(string shape, int width, int height, IRandom rng)
    {
        var cells = new CellKind[width * height];
        Array.Fill(cells, CellKind.Slot);

        switch (shape.ToLowerInvariant())
        {
            case "rectangle":
                break; // All Slot, nothing to void
            case "cross":
                ApplyCross(cells, width, height);
                break;
            case "diamond":
                ApplyDiamond(cells, width, height);
                break;
            case "l-shape":
                ApplyLShape(cells, width, height);
                break;
            case "hourglass":
                ApplyHourglass(cells, width, height);
                break;
            case "compartment":
                ApplyCompartment(cells, width, height, rng);
                break;
        }

        PlaceSpawners(cells, width, height);
        return cells;
    }

    /// <summary>Count non-Void cells (playable area).</summary>
    public static int CountSlots(CellKind[] cells)
    {
        int count = 0;
        foreach (var c in cells)
            if (c != CellKind.Void) count++;
        return count;
    }

    // ── Shape Generators ──

    /// <summary>Cross: void the four corner blocks (each ~width/3 × height/3).</summary>
    private static void ApplyCross(CellKind[] cells, int w, int h)
    {
        int cx = w / 3;
        int cy = h / 3;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool topLeft = x < cx && y < cy;
                bool topRight = x >= w - cx && y < cy;
                bool botLeft = x < cx && y >= h - cy;
                bool botRight = x >= w - cx && y >= h - cy;
                if (topLeft || topRight || botLeft || botRight)
                    cells[y * w + x] = CellKind.Void;
            }
    }

    /// <summary>Diamond: void cells where Manhattan distance from center > radius.</summary>
    private static void ApplyDiamond(CellKind[] cells, int w, int h)
    {
        float cx = (w - 1) / 2f;
        float cy = (h - 1) / 2f;
        float radius = Math.Min(w, h) / 2f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dist = Math.Abs(x - cx) + Math.Abs(y - cy);
                if (dist > radius)
                    cells[y * w + x] = CellKind.Void;
            }
    }

    /// <summary>L-shape: void the top-right quadrant.</summary>
    private static void ApplyLShape(CellKind[] cells, int w, int h)
    {
        int cutX = (w + 1) / 2; // left portion width (slightly more than half)
        int cutY = (h + 1) / 2; // top portion height
        for (int y = 0; y < cutY; y++)
            for (int x = cutX; x < w; x++)
                cells[y * w + x] = CellKind.Void;
    }

    /// <summary>Hourglass: narrow the middle rows by voiding sides.</summary>
    private static void ApplyHourglass(CellKind[] cells, int w, int h)
    {
        float cy = (h - 1) / 2f;
        int maxIndent = Math.Max(1, w / 3);
        for (int y = 0; y < h; y++)
        {
            // Narrowest at center, widest at top/bottom
            float t = 1f - Math.Abs(y - cy) / cy; // 0 at edges, 1 at center
            int indent = (int)(maxIndent * t);
            for (int x = 0; x < indent; x++)
                cells[y * w + x] = CellKind.Void;
            for (int x = w - indent; x < w; x++)
                cells[y * w + x] = CellKind.Void;
        }
    }

    /// <summary>
    /// Compartment: split board into 2 rooms with a narrow corridor.
    /// Vertical split: left room and right room connected by a 2-cell-wide passage in the middle.
    /// </summary>
    private static void ApplyCompartment(CellKind[] cells, int w, int h, IRandom rng)
    {
        // Split column: the wall column
        int splitX = w / 2;
        // Corridor position: 2 adjacent rows somewhere in the middle
        int corridorY = h / 3 + rng.Next(0, Math.Max(1, h / 3));
        int corridorHeight = 2;

        for (int y = 0; y < h; y++)
        {
            bool isCorridor = y >= corridorY && y < corridorY + corridorHeight;
            if (!isCorridor)
                cells[y * w + splitX] = CellKind.Void;
        }
    }

    // ── Spawner Placement ──

    /// <summary>
    /// For each column, the topmost non-Void cell becomes a Spawner.
    /// This ensures every playable column receives new tiles.
    /// </summary>
    private static void PlaceSpawners(CellKind[] cells, int w, int h)
    {
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int idx = y * w + x;
                if (cells[idx] != CellKind.Void)
                {
                    cells[idx] = CellKind.Spawner;
                    break;
                }
            }
        }
    }
}
