using Match3.Core.Structs.Grid;
using Match3.Core.Structs.Handles;

namespace Match3.Core.Models.Grid
{
    /// <summary>
    /// The high-level Grid API (Facade).
    /// Wraps GridData to provide convenient access methods.
    /// Replaces the old object-based Match3Grid.
    /// </summary>
    public class Match3Grid
    {
        private readonly GridData _data;

        public int Width => _data.Width;
        public int Height => _data.Height;

        public Match3Grid(int width, int height)
        {
            _data = new GridData(width, height);
        }

        // --- Tile Access ---

        public TileHandle GetTile(int x, int y)
        {
            return new TileHandle(_data, x, y);
        }

        // --- Unit Management (Factory) ---

        public UnitHandle CreateUnit(int x, int y, int type, int color = 0)
        {
            int id = _data.AllocateUnit();
            
            // Initialize Component Data
            _data.Units[id].Type = type;
            _data.Units[id].Color = color;
            _data.UnitHealths[id].Value = 1;
            _data.UnitHealths[id].MaxValue = 1;

            // Link to Tile
            var tile = GetTile(x, y);
            if (tile.IsValid)
            {
                tile.SetUnit(id);
            }

            return new UnitHandle(_data, id);
        }
        
        public CoverHandle CreateCover(int x, int y, int type)
        {
            int id = _data.AllocateCover();
            
            _data.Covers[id].Type = type;
            _data.CoverHealths[id].Value = 1;
            
            var tile = GetTile(x, y);
            if (tile.IsValid)
            {
                tile.SetCover(id);
            }
            
            return new CoverHandle(_data, id);
        }

        // --- Destruction (Recycling) ---

        public void DestroyUnit(UnitHandle unit)
        {
            if (!unit.IsValid) return;
            _data.FreeUnit(unit.Id);
        }

        public void DestroyUnitAt(int x, int y)
        {
            var tile = GetTile(x, y);
            if (tile.HasUnit)
            {
                var unit = tile.Unit;
                tile.ClearUnit(); // Remove from grid
                DestroyUnit(unit); // Recycle ID
            }
        }
        
        public void DestroyCover(CoverHandle cover)
        {
            if (!cover.IsValid) return;
            _data.FreeCover(cover.Id);
        }
    }
}
