using System.Collections.Generic;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.Modifiers;

/// <summary>
/// Group coordination for elements that belong to a group (e.g., Buttons, Vines, Batteries).
/// HP=1 â†?tier=4, urgency=255. Not the weakest in group â†?tier=0.
/// Framework placeholder â€?currently no group elements exist in the project.
/// </summary>
public sealed class GroupModifier : IScoreModifier
{
    public static readonly GroupModifier Instance = new();

    public int Order => 20;

    public void Precompute(
        in GameState state,
        UfoTargetConfig config,
        Dictionary<int, int> precomputeCache)
    {
        // No group elements currently â€?no-op.
    }

    public void Modify(
        ref EvalResult result,
        in GameState state,
        int x, int y,
        UfoTargetConfig config,
        Dictionary<int, int> precomputeCache)
    {
        // No group elements currently â€?no-op.
    }
}
