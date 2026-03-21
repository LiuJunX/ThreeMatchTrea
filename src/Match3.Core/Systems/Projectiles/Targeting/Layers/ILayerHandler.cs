using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.Layers;

/// <summary>
/// Evaluates a single layer in the cell stack for UFO targeting.
/// Implementations must be stateless (parallel-safe).
/// </summary>
public interface ILayerHandler
{
    /// <summary>Layer index in the penetration order.</summary>
    byte LayerIndex { get; }

    /// <summary>
    /// Evaluate this layer at the given position.
    /// Returns default (Value=0, HitCapacity=0) if the layer is empty/inactive.
    /// Returns Value=ushort.MaxValue to signal "cell is untargetable" (e.g., immune obstacle).
    /// </summary>
    HitLayer Evaluate(in GameState state, int x, int y, UfoTargetConfig config);
}
