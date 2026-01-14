using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Core;

/// <summary>
/// Manages the visual state of tiles (position interpolation).
/// Separates the visual physics from the logical grid state.
/// </summary>
public class AnimationSystem : IAnimationSystem
{
    private readonly Match3Config _config;
    private const float Epsilon = 0.01f;

    public bool IsVisuallyStable { get; private set; } = true;

    public AnimationSystem(Match3Config config)
    {
        _config = config;
    }

    /// <summary>
    /// Updates tile visual positions towards their logical grid positions.
    /// Returns true if all tiles are stable (at their target positions).
    /// </summary>
    public bool Animate(ref GameState state, float dt)
    {
        bool allStable = true;
        for (int i = 0; i < state.Grid.Length; i++)
        {
            ref var tile = ref state.Grid[i];
            if (tile.Type == TileType.None) continue;

            // 跳过正在掉落的 tile，让 RealtimeGravitySystem 控制其位置
            if (tile.IsFalling) continue;

            int x = i % state.Width;
            int y = i / state.Width;
            var targetPos = new Vector2(x, y);

            if (Vector2.DistanceSquared(tile.Position, targetPos) > Epsilon * Epsilon)
            {
                allStable = false;
                var dir = targetPos - tile.Position;
                float dist = dir.Length();
                float move = _config.GravitySpeed * dt;
                
                if (move >= dist)
                {
                    tile.Position = targetPos;
                }
                else
                {
                    tile.Position += Vector2.Normalize(dir) * move;
                }
            }
            else
            {
                tile.Position = targetPos; // Snap
            }
        }
        
        IsVisuallyStable = allStable;
        return allStable;
    }

    public bool IsVisualAtTarget(in GameState state, Position p)
    {
        var tile = state.GetTile(p.X, p.Y);
        if (tile.Type == TileType.None) return true; // Empty is considered stable

        var target = new Vector2(p.X, p.Y);
        return Vector2.DistanceSquared(tile.Position, target) <= Epsilon * Epsilon;
    }
}
