using System;
using Match3.Core.Structs.Grid;

namespace Match3.Core.Structs.Handles
{
    /// <summary>
    /// A user-friendly wrapper around a Unit ID.
    /// Provides OOP-like access to SoA data.
    /// </summary>
    public readonly struct UnitHandle
    {
        private readonly GridData _data;
        public readonly int Id;
        private readonly int _generation;

        public UnitHandle(GridData data, int id)
        {
            _data = data;
            Id = id;
            _generation = data.UnitGenerations[id];
        }

        public bool IsValid => Id > 0 && _data != null && _data.UnitGenerations[Id] == _generation;

        // --- Properties (Direct Array Access) ---
        
        public ref int Color => ref _data.Units[Id].Color;
        public ref int Type => ref _data.Units[Id].Type;
        
        public ref int Health => ref _data.UnitHealths[Id].Value;
        public ref int MaxHealth => ref _data.UnitHealths[Id].MaxValue;

        // --- Methods ---
        
        public void TakeDamage(int amount)
        {
            Health -= amount;
        }
    }
}
