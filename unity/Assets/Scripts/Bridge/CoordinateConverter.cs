using Match3.Core.Models.Grid;
using UnityEngine;
using SysVector2 = System.Numerics.Vector2;

namespace Match3.Unity.Bridge
{
    /// <summary>
    /// Coordinate conversion between Grid space and Unity World space.
    /// Grid: (0,0) at TOP-left, Y increases DOWNWARD (Core convention).
    /// Unity: Y increases UPWARD, so we flip Y axis.
    /// </summary>
    public static class CoordinateConverter
    {
        /// <summary>
        /// Convert grid position (float) to Unity world position.
        /// Centers the tile in the cell. Flips Y axis.
        /// </summary>
        /// <param name="gridPos">Grid position from VisualState (System.Numerics.Vector2).</param>
        /// <param name="cellSize">Size of each cell in world units.</param>
        /// <param name="origin">Board origin in world space (top-left corner).</param>
        /// <param name="height">Board height for Y-flip calculation.</param>
        public static Vector3 GridToWorld(SysVector2 gridPos, float cellSize, Vector2 origin, int height)
        {
            float worldX = origin.x + gridPos.X * cellSize + cellSize * 0.5f;
            // Flip Y: Grid Y=0 is top, Unity Y increases upward
            float worldY = origin.y + (height - 1 - gridPos.Y) * cellSize + cellSize * 0.5f;
            return new Vector3(worldX, worldY, 0f);
        }

        /// <summary>
        /// Convert grid position (float) to Unity world position (without height param for compatibility).
        /// NOTE: Prefer the overload with height parameter for correct Y-flip.
        /// </summary>
        public static Vector3 GridToWorld(SysVector2 gridPos, float cellSize, Vector2 origin)
        {
            // This overload assumes height is stored elsewhere or caller handles flip
            // For backward compatibility, just do direct mapping (caller should use height version)
            float worldX = origin.x + gridPos.X * cellSize + cellSize * 0.5f;
            float worldY = origin.y - gridPos.Y * cellSize - cellSize * 0.5f;
            return new Vector3(worldX, worldY, 0f);
        }

        /// <summary>
        /// Convert grid position (int) to Unity world position.
        /// Centers the tile in the cell. Flips Y axis.
        /// </summary>
        public static Vector3 GridToWorld(Position gridPos, float cellSize, Vector2 origin, int height)
        {
            float worldX = origin.x + gridPos.X * cellSize + cellSize * 0.5f;
            // Flip Y: Grid Y=0 is top, Unity Y increases upward
            float worldY = origin.y + (height - 1 - gridPos.Y) * cellSize + cellSize * 0.5f;
            return new Vector3(worldX, worldY, 0f);
        }

        /// <summary>
        /// Convert Unity world position to grid position (integer).
        /// Returns Position.Invalid if outside bounds.
        /// </summary>
        public static Position WorldToGrid(Vector3 worldPos, float cellSize, Vector2 origin, int width, int height)
        {
            int gridX = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
            // Flip Y back: Unity Y to Grid Y
            int gridY = height - 1 - Mathf.FloorToInt((worldPos.y - origin.y) / cellSize);

            if (gridX < 0 || gridX >= width || gridY < 0 || gridY >= height)
            {
                return Position.Invalid;
            }

            return new Position(gridX, gridY);
        }

        /// <summary>
        /// Convert Unity world position to grid position (float, for smooth tracking).
        /// </summary>
        public static SysVector2 WorldToGridFloat(Vector3 worldPos, float cellSize, Vector2 origin, int height)
        {
            float gridX = (worldPos.x - origin.x) / cellSize - 0.5f;
            // Flip Y back
            float gridY = (height - 1) - ((worldPos.y - origin.y) / cellSize - 0.5f);
            return new SysVector2(gridX, gridY);
        }

        /// <summary>
        /// Get the world-space bounds of the board.
        /// </summary>
        public static Rect GetBoardBounds(int width, int height, float cellSize, Vector2 origin)
        {
            return new Rect(
                origin.x,
                origin.y,
                width * cellSize,
                height * cellSize
            );
        }

        /// <summary>
        /// Convert System.Numerics.Vector2 to UnityEngine.Vector3 (z=0).
        /// </summary>
        public static Vector3 ToVector3(SysVector2 v)
        {
            return new Vector3(v.X, v.Y, 0f);
        }

        /// <summary>
        /// Convert UnityEngine.Vector3 to System.Numerics.Vector2 (ignore z).
        /// </summary>
        public static SysVector2 ToSysVector2(Vector3 v)
        {
            return new SysVector2(v.x, v.y);
        }
    }
}
