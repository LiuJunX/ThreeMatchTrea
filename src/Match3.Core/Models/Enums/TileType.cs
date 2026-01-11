using System;

namespace Match3.Core.Models.Enums
{
    /// <summary>
    /// Defines the structural properties of a grid cell.
    /// Can be combined as flags.
    /// </summary>
    [Flags]
    public enum TileType
    {
        None = 0,
        
        /// <summary>
        /// A standard playable cell.
        /// </summary>
        Normal = 1 << 0,
        
        /// <summary>
        /// A wall that blocks movement or matches.
        /// Note: Often handled as a separate layer or edge property, 
        /// but can be a cell type for "solid rock" cells.
        /// </summary>
        Wall = 1 << 1,
        
        /// <summary>
        /// A spawn point for new items.
        /// </summary>
        Spawner = 1 << 2,
        
        /// <summary>
        /// A collection point (e.g., for ingredients to drop out).
        /// </summary>
        Sink = 1 << 3,

        /// <summary>
        /// A void cell (empty space, items fall through or around).
        /// </summary>
        Hole = 1 << 4,

        // Legacy compatibility
        Rainbow = 1 << 5,
        Bomb = 1 << 6,
        
        // Colors (Legacy/Editor support)
        Red = 1 << 7,
        Green = 1 << 8,
        Blue = 1 << 9,
        Yellow = 1 << 10,
        Purple = 1 << 11,
        Orange = 1 << 12
    }
}
