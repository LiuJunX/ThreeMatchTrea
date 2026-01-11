using System;
using Match3.Core.Models.Enums;
using Match3.Core.Structs.Grid;
using Match3.Core.Primitives;

namespace Match3.Core.Structs.Handles
{
    /// <summary>
    /// A wrapper for a grid cell position (x, y).
    /// Provides access to the tile data and elements at this position.
    /// </summary>
    public readonly struct TileHandle
    {
        private readonly GridData _data;
        public readonly int X;
        public readonly int Y;
        private readonly int _index;

        public TileHandle(GridData data, int x, int y)
        {
            _data = data;
            X = x;
            Y = y;
            _index = data.GetIndex(x, y);
        }

        public bool IsValid => _index >= 0 && _data != null;

        // --- Tile Properties ---

        public ref TileType Topology => ref _data.Tiles[_index].Topology;

        // --- Element Accessors ---

        public bool HasUnit => IsValid && _data.Tiles[_index].UnitId > 0;
        public bool HasCover => IsValid && _data.Tiles[_index].CoverId > 0;

        public UnitHandle Unit
        {
            get
            {
                if (!HasUnit) return new UnitHandle(_data, 0); // Generation for 0 is irrelevant/safe
                return new UnitHandle(_data, _data.Tiles[_index].UnitId);
            }
        }
        
        public CoverHandle Cover
        {
            get
            {
                if (!HasCover) return new CoverHandle(_data, 0);
                return new CoverHandle(_data, _data.Tiles[_index].CoverId);
            }
        }

        // --- Mutators ---

        public void SetUnit(int unitId)
        {
            if (IsValid) _data.Tiles[_index].UnitId = unitId;
        }

        public void ClearUnit()
        {
            if (IsValid) _data.Tiles[_index].UnitId = 0;
        }
        
        public void SetCover(int coverId)
        {
            if (IsValid) _data.Tiles[_index].CoverId = coverId;
        }

        public bool IsWalkable()
        {
            if (!IsValid) return false;
            var t = Topology;
            return t != TileType.None && !t.HasFlag(TileType.Wall);
        }
    }
}
