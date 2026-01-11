namespace Match3.Core.Structs.Components
{
    /// <summary>
    /// Pure data struct for Unit properties.
    /// </summary>
    public struct UnitComponent
    {
        public int Type; // E.g., ItemConfig ID
        public int Color; // 0=None, 1=Red, 2=Blue...
        public int FeatureFlags; // Bitmask for special properties (e.g., IsBomb)
    }
}
