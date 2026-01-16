using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;
using Match3.Random;

namespace Match3.Core.Systems.Physics;

public class RealtimeGravitySystem : IPhysicsSimulation
{
    private const float SnapThreshold = 0.01f;
    private const float FloorSnapDistance = 0.001f;
    private const float SlideSpeedMultiplier = 8.0f;
    private const float SlideGravityFactor = 0.6f;

    private readonly Match3Config _config;
    private readonly IRandom _random;

    // Frame buffers
    private readonly HashSet<int> _reservedSlots = new HashSet<int>();
    private readonly HashSet<int> _newlyOccupiedSlots = new HashSet<int>();

    // Target resolver
    private readonly GravityTargetResolver _targetResolver;

    public RealtimeGravitySystem(Match3Config config, IRandom random)
    {
        _config = config;
        _random = random;
        _targetResolver = new GravityTargetResolver(random, _reservedSlots);
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
                if (!IsTileStable(in state, x, y))
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

            if (ShouldSkipTile(in state, tile, x, y)) continue;

            var target = _targetResolver.DetermineTarget(ref state, x, y);
            SimulatePhysics(ref tile, target, dt);
            UpdateGridPosition(ref state, x, y, tile);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldSkipTile(in GameState state, Tile tile, int x, int y)
    {
        if (tile.Type == TileType.None) return true;
        if (tile.IsSuspended) return true;
        if (_newlyOccupiedSlots.Contains(y * state.Width + x)) return true;

        // Check if static cover blocks movement
        if (!state.CanMove(x, y)) return true;

        return false;
    }

    private void SimulatePhysics(ref Tile tile, GravityTargetResolver.TargetInfo target, float dt)
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

    private void ApplyVerticalMotion(ref Tile tile, GravityTargetResolver.TargetInfo target, float dt)
    {
        if (tile.Position.Y < target.Position.Y - FloorSnapDistance)
        {
            // Apply Gravity
            tile.IsFalling = true;

            if (tile.Velocity.Y < _config.InitialFallSpeed)
            {
                tile.Velocity.Y = _config.InitialFallSpeed;
            }

            float gravityScale = (Math.Abs(tile.Velocity.X) > SnapThreshold) ? SlideGravityFactor : 1.0f;
            tile.Velocity.Y += _config.GravitySpeed * gravityScale * dt;

            if (tile.Velocity.Y > _config.MaxFallSpeed)
                tile.Velocity.Y = _config.MaxFallSpeed;

            tile.Position.Y += tile.Velocity.Y * dt;

            if (tile.Position.Y >= target.Position.Y)
            {
                SnapToTargetY(ref tile, target);
            }
        }
        else
        {
            if (tile.IsFalling || Math.Abs(tile.Position.Y - target.Position.Y) > float.Epsilon)
            {
                SnapToTargetY(ref tile, target);
            }
        }
    }

    private void SnapToTargetY(ref Tile tile, GravityTargetResolver.TargetInfo target)
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
                // Move tile to new position
                state.SetTile(visualX, visualY, tile);
                state.SetTile(currentX, currentY, new Tile(0, TileType.None, currentX, currentY));

                // Sync dynamic cover with the tile
                SyncDynamicCover(ref state, currentX, currentY, visualX, visualY);

                _newlyOccupiedSlots.Add(visualY * state.Width + visualX);

                return;
            }
        }

        state.SetTile(currentX, currentY, tile);
    }

    /// <summary>
    /// Sync dynamic cover when a tile moves to a new position.
    /// </summary>
    private void SyncDynamicCover(ref GameState state, int fromX, int fromY, int toX, int toY)
    {
        var cover = state.GetCover(fromX, fromY);

        // Only move if cover is dynamic
        if (cover.Type != CoverType.None && cover.IsDynamic)
        {
            state.SetCover(toX, toY, cover);
            state.SetCover(fromX, fromY, Cover.Empty);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasMovedToNewCell(GameState state, int visualX, int visualY, int currentX, int currentY)
    {
        return (visualX != currentX || visualY != currentY) &&
               visualX >= 0 && visualX < state.Width &&
               visualY >= 0 && visualY < state.Height;
    }

    private bool IsTileStable(in GameState state, int x, int y)
    {
        var tile = state.GetTile(x, y);

        if (tile.Type == TileType.None) return true;

        // Tiles blocked by static cover are considered stable
        if (!state.CanMove(x, y)) return true;

        return Math.Abs(tile.Velocity.Y) <= SnapThreshold &&
               Math.Abs(tile.Velocity.X) <= SnapThreshold &&
               Math.Abs(tile.Position.Y - y) <= SnapThreshold &&
               Math.Abs(tile.Position.X - x) <= SnapThreshold;
    }
}
