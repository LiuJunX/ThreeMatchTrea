using System;
using Match3.Core.Structs.Components;

namespace Match3.Core.Structs.Grid
{
    /// <summary>
    /// The central data repository for the Grid ECS.
    /// Manages all component arrays and entity IDs.
    /// </summary>
    public class GridData
    {
        // --- Board Layout ---
        public readonly int Width;
        public readonly int Height;
        public readonly Tile[] Tiles; // 1D Array: index = y * Width + x

        // --- Component Pools (SoA) ---
        // Unit Components
        public readonly UnitComponent[] Units;
        public readonly HealthComponent[] UnitHealths;
        public readonly int[] UnitGenerations;
        
        // Cover Components
        public readonly CoverComponent[] Covers;
        public readonly HealthComponent[] CoverHealths;
        public readonly int[] CoverGenerations;

        // --- ID Management ---
        private const int MaxEntities = 1024; // Hard limit for MVP, can be dynamic later
        private int _nextUnitId = 1; // 0 is reserved for "None"
        private int _nextCoverId = 1;
        
        private readonly System.Collections.Generic.Stack<int> _freeUnitIds = new System.Collections.Generic.Stack<int>();
        private readonly System.Collections.Generic.Stack<int> _freeCoverIds = new System.Collections.Generic.Stack<int>();

        public GridData(int width, int height)
        {
            Width = width;
            Height = height;
            Tiles = new Tile[width * height];
            
            // Pre-allocate pools
            Units = new UnitComponent[MaxEntities];
            UnitHealths = new HealthComponent[MaxEntities];
            UnitGenerations = new int[MaxEntities];
            
            Covers = new CoverComponent[MaxEntities];
            CoverHealths = new HealthComponent[MaxEntities];
            CoverGenerations = new int[MaxEntities];
            
            // Initialize generations to 1
            for(int i = 0; i < MaxEntities; i++) {
                UnitGenerations[i] = 1;
                CoverGenerations[i] = 1;
            }
        }

        public int AllocateUnit()
        {
            if (_freeUnitIds.Count > 0)
            {
                int id = _freeUnitIds.Pop();
                // Clean up old data to prevent dirty reads
                Units[id] = default;
                UnitHealths[id] = default;
                return id;
            }

            if (_nextUnitId >= MaxEntities) throw new Exception("Unit pool exhausted");
            return _nextUnitId++;
        }
        
        public void FreeUnit(int id)
        {
            if (id <= 0 || id >= MaxEntities) return;
            
            // Increment generation to invalidate existing handles
            UnitGenerations[id]++;
            
            _freeUnitIds.Push(id);
        }

        public int AllocateCover()
        {
            if (_freeCoverIds.Count > 0)
            {
                int id = _freeCoverIds.Pop();
                Covers[id] = default;
                CoverHealths[id] = default;
                return id;
            }

            if (_nextCoverId >= MaxEntities) throw new Exception("Cover pool exhausted");
            return _nextCoverId++;
        }
        
        public void FreeCover(int id)
        {
            if (id <= 0 || id >= MaxEntities) return;
            
            CoverGenerations[id]++;
            
            _freeCoverIds.Push(id);
        }
        
        // Helper to get array index
        public int GetIndex(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return -1;
            return y * Width + x;
        }
    }
}
