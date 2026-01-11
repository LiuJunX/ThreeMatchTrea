using System;
using Match3.Core.Structs.Grid;
using Match3.Core.Models.Enums;

namespace Match3.Core.Structs.Handles
{
    /// <summary>
    /// A user-friendly wrapper around a Cover ID.
    /// </summary>
    public readonly struct CoverHandle
    {
        private readonly GridData _data;
        public readonly int Id;
        private readonly int _generation;

        public CoverHandle(GridData data, int id)
        {
            _data = data;
            Id = id;
            _generation = data.CoverGenerations[id];
        }

        public bool IsValid => Id > 0 && _data != null && _data.CoverGenerations[Id] == _generation;

        // --- Properties ---
        
        public ref int Type => ref _data.Covers[Id].Type;
        public ref CoverAttachmentMode Mode => ref _data.Covers[Id].Mode;
        
        public ref int Health => ref _data.CoverHealths[Id].Value;
        public ref int MaxHealth => ref _data.CoverHealths[Id].MaxValue;
    }
}
