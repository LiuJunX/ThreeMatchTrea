using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.Layers;

/// <summary>
/// Evaluates the Tile layer (Items, Bombs, Collectibles, Unmatchable).
/// ColorBomb is excluded (existing UFO rule). Does not block penetration to Ground.
/// </summary>
public sealed class TileLayerHandler : ILayerHandler
{
    public static readonly TileLayerHandler Instance = new();

    public byte LayerIndex => 2;

    public HitLayer Evaluate(in GameState state, int x, int y, UfoTargetConfig config)
    {
        var tile = state.GetTile(x, y);
        if (tile.Type == ElementType.None)
            return default;

        // ColorBomb excluded — existing UFO rule
        if (tile.Type.IsColorBomb())
            return new HitLayer { Value = ushort.MaxValue }; // untargetable

        ushort value;
        if (tile.Type.IsColor())
            value = config.TileColorBaseValue;
        else if (tile.Type.IsBomb())
            value = config.TileBombBaseValue;
        else if (tile.Type.IsCollectible())
            value = config.TileCollectibleBaseValue;
        else
            value = config.TileUnmatchableBaseValue;

        return new HitLayer
        {
            Layer = LayerIndex,
            Value = value,
            HitCapacity = 1,
            IsTarget = IsObjective(in state, tile.Type),
            BlocksPenetration = false // Tile doesn't block → Ground below can contribute
        };
    }

    private static bool IsObjective(in GameState state, ElementType elementType)
    {
        for (int i = 0; i < state.ObjectiveProgress.Length; i++)
        {
            ref var p = ref state.ObjectiveProgress[i];
            if (p.IsActive && !p.IsCompleted
                && p.TargetLayer == ObjectiveTargetLayer.Tile
                && p.ElementType == (int)elementType)
                return true;
        }
        return false;
    }
}
