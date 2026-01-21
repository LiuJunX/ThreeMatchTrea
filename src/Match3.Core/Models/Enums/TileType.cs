using System;
using Match3.Core.Attributes;

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
        [AIMapping(0, "Red")]    Red = 1 << 7,
        [AIMapping(1, "Green")]  Green = 1 << 8,
        [AIMapping(2, "Blue")]   Blue = 1 << 9,
        [AIMapping(3, "Yellow")] Yellow = 1 << 10,
        [AIMapping(4, "Purple")] Purple = 1 << 11,
        [AIMapping(5, "Orange")] Orange = 1 << 12
    }
}
