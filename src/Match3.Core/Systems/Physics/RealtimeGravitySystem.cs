using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;
using Match3.Random;

namespace Match3.Core.Systems.Physics;

public class RealtimeGravitySystem : IPhysicsSimulation
{
    private const float SnapThreshold = 0.01f;
    // Reduced from 0.05f to 0.001f to prevent premature snapping to temporary targets (Phantom Blocks)
    // when running at low game speeds (small deltaTime).
    private const float FloorSnapDistance = 0.001f;
    private const float SlideSpeedMultiplier = 8.0f;
    private const float SlideGravityFactor = 0.6f;
    private const float FallingFollowDistance = 1.0f;

    private readonly Match3Config _config;
    private readonly IRandom _random;
    
    // Frame buffers
    private readonly HashSet<int> _reservedSlots = new HashSet<int>();
    private readonly HashSet<int> _newlyOccupiedSlots = new HashSet<int>();

    private readonly struct TargetInfo
    {
        public readonly Vector2 Position;
        public readonly float InheritedVelocityY;
        public readonly bool FoundDynamicTarget;

        public TargetInfo(Vector2 position, float inheritedVelocityY, bool foundDynamicTarget)
        {
            Position = position;
            InheritedVelocityY = inheritedVelocityY;
            FoundDynamicTarget = foundDynamicTarget;
        }
    }

    public RealtimeGravitySystem(Match3Config config, IRandom random)
    {
        _config = config;
        _random = random;
    }

    public void Update(ref GameState state, float deltaTime)
    {
        ResetFrameBuffers();
        ProcessShuffledColumns(ref state, deltaTime);
    }

    public bool IsStable(in GameState state)
    {
        for (int x = 0; x < state.Width; x++)
        {
            for (int y = 0; y < state.Height; y++)
            {
                if (!IsTileStable(state.GetTile(x, y), x, y))
                {
                    return false;
                }
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetFrameBuffers()
    {
        _reservedSlots.Clear();
        _newlyOccupiedSlots.Clear();
    }

    private void ProcessShuffledColumns(ref GameState state, float dt)
    {
        var columnIndices = Pools.ObtainList<int>(state.Width);
        try
        {
            InitializeColumnIndices(columnIndices, state.Width);
            ShuffleColumnIndices(columnIndices);

            foreach (var x in columnIndices)
            {
                ProcessColumn(ref state, x, dt);
            }
        }
        finally
        {
            Pools.Release(columnIndices);
        }
    }

    private void InitializeColumnIndices(List<int> indices, int width)
    {
        for (int i = 0; i < width; i++) indices.Add(i);
    }

    private void ShuffleColumnIndices(List<int> indices)
    {
        int n = indices.Count;
        while (n > 1)
        {
            n--;
            int k = _random.Next(0, n + 1);
            (indices[k], indices[n]) = (indices[n], indices[k]);
        }
    }

    private void ProcessColumn(ref GameState state, int x, float dt)
    {
        for (int y = state.Height - 1; y >= 0; y--)
        {
            var tile = state.GetTile(x, y);
            
            if (ShouldSkipTile(tile, x, y, state.Width)) continue;
            
            var target = DetermineTarget(ref state, x, y);
            SimulatePhysics(ref tile, target, dt);
            UpdateGridPosition(ref state, x, y, tile);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldSkipTile(Tile tile, int x, int y, int width)
    {
        return tile.Type == TileType.None || 
               tile.IsSuspended || 
               _newlyOccupiedSlots.Contains(y * width + x);
    }

    private TargetInfo DetermineTarget(ref GameState state, int x, int y)
    {
        int checkY = y + 1;
        
        // 1. Try Vertical Move
        if (checkY < state.Height)
        {
            if (CanMoveTo(ref state, x, checkY))
            {
                return FindLowestVerticalTarget(ref state, x, checkY);
            }
            
            // 2. Try Follow Falling Blocker
            var below = state.GetTile(x, checkY);
            if (below.Type != TileType.None && below.IsFalling)
            {
                return new TargetInfo(
                    new Vector2(x, below.Position.Y - FallingFollowDistance),
                    below.Velocity.Y,
                    true
                );
            }
            
            // 3. Try Diagonal Slide
        // Only allow sliding if the tile directly below is an Obstacle (Suspended)
        // Normal tiles should not cause sliding; they should form a stable stack.
        var tileBelow = state.GetTile(x, checkY);
        if (tileBelow.IsSuspended)
        {
            return FindDiagonalTarget(ref state, x, checkY, y);
        }

        // If blocked by a normal tile, stay put.
        return new TargetInfo(new Vector2(x, y), 0f, false);
        }

        // Bottom of grid
        return new TargetInfo(new Vector2(x, y), 0f, false);
    }

    private TargetInfo FindLowestVerticalTarget(ref GameState state, int x, int startY)
    {
        int floorY = startY;
        
        for (int k = startY + 1; k < state.Height; k++)
        {
            if (CanMoveTo(ref state, x, k))
            {
                floorY = k;
            }
            else
            {
                var below = state.GetTile(x, k);
                if (below.IsFalling)
                {
                    ReserveSlot(x, floorY, state.Width);
                    return new TargetInfo(
                        new Vector2(x, below.Position.Y - FallingFollowDistance),
                        below.Velocity.Y,
                        true
                    );
                }
                break;
            }
        }

        ReserveSlot(x, floorY, state.Width);
        return new TargetInfo(new Vector2(x, floorY), 0f, false);
    }

    private TargetInfo FindDiagonalTarget(ref GameState state, int x, int checkY, int originalY)
    {
        // Check if there is a tile directly above the diagonal targets that might want to fall vertically.
        // If there is, we should NOT slide there, because vertical fall has priority.
        
        bool canLeft = x > 0 && CanMoveTo(ref state, x - 1, checkY) && IsOverheadClear(ref state, x - 1, originalY);
        bool canRight = x < state.Width - 1 && CanMoveTo(ref state, x + 1, checkY) && IsOverheadClear(ref state, x + 1, originalY);
        
        int targetX = -1;

        if (canLeft && canRight)
        {
            targetX = _random.Next(0, 2) == 0 ? x - 1 : x + 1;
        }
        else if (canLeft)
        {
            targetX = x - 1;
        }
        else if (canRight)
        {
            targetX = x + 1;
        }

        if (targetX != -1)
        {
            ReserveSlot(targetX, checkY, state.Width);
            return new TargetInfo(new Vector2(targetX, checkY), 0f, false);
        }

        return new TargetInfo(new Vector2(x, originalY), 0f, false);
    }

    private void SimulatePhysics(ref Tile tile, TargetInfo target, float dt)
    {
        ApplyHorizontalMotion(ref tile, target.Position.X, dt);
        ApplyVerticalMotion(ref tile, target, dt);
    }

    private void ApplyHorizontalMotion(ref Tile tile, float targetX, float dt)
    {
        float diffX = targetX - tile.Position.X;
        if (Math.Abs(diffX) > SnapThreshold)
        {
            float moveX = Math.Sign(diffX) * SlideSpeedMultiplier * dt;
            if (Math.Abs(moveX) > Math.Abs(diffX)) moveX = diffX;
            
            tile.Position.X += moveX;
            tile.Velocity.X = Math.Sign(diffX) * SlideSpeedMultiplier;
            tile.IsFalling = true;
        }
        else
        {
            tile.Position.X = targetX;
            tile.Velocity.X = 0;
        }
    }

    private void ApplyVerticalMotion(ref Tile tile, TargetInfo target, float dt)
    {
        if (tile.Position.Y < target.Position.Y - FloorSnapDistance)
        {
            // Apply Gravity
            tile.IsFalling = true;

            // 如果速度接近0，应用初始速度（解决"前半格慢"问题）
            if (tile.Velocity.Y < _config.InitialFallSpeed)
            {
                tile.Velocity.Y = _config.InitialFallSpeed;
            }

            // Reduce gravity when sliding to create a smoother arc
            float gravityScale = (Math.Abs(tile.Velocity.X) > SnapThreshold) ? SlideGravityFactor : 1.0f;
            tile.Velocity.Y += _config.GravitySpeed * gravityScale * dt;

            if (tile.Velocity.Y > _config.MaxFallSpeed)
                tile.Velocity.Y = _config.MaxFallSpeed;

            // 先更新位置，再更新速度（修正积分顺序）
            tile.Position.Y += tile.Velocity.Y * dt;

            // Collision Check
            if (tile.Position.Y >= target.Position.Y)
            {
                SnapToTargetY(ref tile, target);
            }
        }
        else
        {
            // Stable or Near Floor
            if (tile.IsFalling || Math.Abs(tile.Position.Y - target.Position.Y) > float.Epsilon)
            {
                SnapToTargetY(ref tile, target);
            }
        }
    }

    private void SnapToTargetY(ref Tile tile, TargetInfo target)
    {
        tile.Position.Y = target.Position.Y;
        
        if (target.FoundDynamicTarget)
        {
            tile.Velocity.Y = target.InheritedVelocityY;
            tile.IsFalling = true;
        }
        else
        {
            tile.Velocity.Y = 0;
            tile.IsFalling = false;
        }
    }

    private void UpdateGridPosition(ref GameState state, int currentX, int currentY, Tile tile)
    {
        int visualX = (int)Math.Floor(tile.Position.X + 0.5f);
        int visualY = (int)Math.Floor(tile.Position.Y + 0.5f);

        if (HasMovedToNewCell(state, visualX, visualY, currentX, currentY))
        {
            var targetSlot = state.GetTile(visualX, visualY);
            if (targetSlot.Type == TileType.None)
            {
                state.SetTile(visualX, visualY, tile);
                state.SetTile(currentX, currentY, new Tile(0, TileType.None, currentX, currentY));
                
                // Mark new slot as occupied to prevent double-processing in the same frame
                // (e.g., if we process columns in random order and this tile moves to a later column)
                _newlyOccupiedSlots.Add(visualY * state.Width + visualX);
                
                return; // State updated, loop continues with next tile
            }
        }

        // Update tile in current slot (e.g. position change within cell)
        state.SetTile(currentX, currentY, tile);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasMovedToNewCell(GameState state, int visualX, int visualY, int currentX, int currentY)
    {
        return (visualX != currentX || visualY != currentY) &&
               visualX >= 0 && visualX < state.Width &&
               visualY >= 0 && visualY < state.Height;
    }

    private bool IsTileStable(Tile tile, int x, int y)
    {
        if (tile.Type == TileType.None) return true;
        
        return Math.Abs(tile.Velocity.Y) <= SnapThreshold && 
               Math.Abs(tile.Velocity.X) <= SnapThreshold && 
               Math.Abs(tile.Position.Y - y) <= SnapThreshold && 
               Math.Abs(tile.Position.X - x) <= SnapThreshold;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanMoveTo(ref GameState state, int x, int y)
    {
        return IsInsideGrid(state, x, y) && 
               state.GetTile(x, y).Type == TileType.None && 
               !IsReserved(x, y, state.Width);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsOverheadClear(ref GameState state, int targetX, int targetY)
    {
        // If the slot directly above the target (which is at targetY) has a tile,
        // we assume that tile intends to fall into the target slot (targetY + 1).
        // Therefore, we should not slide into it.
        return state.GetTile(targetX, targetY).Type == TileType.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsInsideGrid(GameState state, int x, int y)
    {
        return x >= 0 && x < state.Width && y >= 0 && y < state.Height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReserveSlot(int x, int y, int width)
    {
        _reservedSlots.Add(y * width + x);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsReserved(int x, int y, int width)
    {
        return _reservedSlots.Contains(y * width + x);
    }
}
    