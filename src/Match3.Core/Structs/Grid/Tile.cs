using Match3.Core.Models.Enums;

namespace Match3.Core.Structs.Grid
{
    /// <summary>
    /// A lightweight struct representing a cell's state.
    /// Stores IDs instead of references.
    /// </summary>
    public struct Tile
    {
        public TileType Topology;
        public int UnitId;   // 0 = None
        public int CoverId;  // 0 = None
        public int GroundId; // 0 = None
    }
}
