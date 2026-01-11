namespace Match3.Core.Models.Enums
{
    public enum BombType
    {
        None = 0,
        
        /// <summary>
        /// Explodes in a circular area (e.g., 3x3).
        /// </summary>
        Area,
        
        /// <summary>
        /// Explodes a whole row.
        /// </summary>
        Horizontal,
        
        /// <summary>
        /// Explodes a whole column.
        /// </summary>
        Vertical,

        // Legacy compatibility
        Color,
        Ufo,
        Square3x3
    }
}
