namespace Match3.Core.Config;

/// <summary>
/// Defines an item in the game.
/// This structure maps to the "Items" sheet in Feishu.
/// </summary>
public struct ItemConfig
{
    public int Id;
    public string Name;
    public int Cost;
    public int Power;
    
    // For string, we might want to store offset or just string. 
    // Strings are reference types, so they will cause GC overhead if created frequently.
    // But for config data which is long-lived, standard string is usually fine.
    // If extreme optimization is needed, we would use an ID into a StringTable.
    // For this prototype, we stick to string.

    public override string ToString()
    {
        return $"Item {Id}: {Name} (Cost: {Cost}, Power: {Power})";
    }
}
