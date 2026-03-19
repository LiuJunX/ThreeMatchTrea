using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;
using Match3.Random;

namespace Match3.Core.Systems.Physics;

/// <summary>
/// Physics simulation with single-cell gravity, diagonal dead-zone slide,
/// and one-frame lookahead snap strategy.
/// </summary>
public class RealtimeGravitySystem : IPhysicsSimulation
{
    private const float SnapThreshold = 0.01f;
    private const float FloorSnapDistance = 0.001f;

    private readonly Match3Config _config;
    private readonly IRandom _random;

    // Frame buffers
    private readonly HashSet<int> _newlyOccupiedSlots = new HashSet<int>();

    // Target resolver
    private readonly IGravityTargetResolver _targetResolver;

    public RealtimeGravitySystem(Match3Config config, IRandom random)
        : this(config, random, new GravityTargetResolver(random))
    {
    }

    public RealtimeGravitySystem(Match3Config config, IRandom random, IGravityTargetResolver targetResolver)
    {
        _config = config;
        _random = random;
        _targetResolver = targetResolver;
    }

    public void Update(ref GameState state, float deltaTime)
    {
        ResetFrameBuffers();
        ProcessShuffledColumns(ref state, deltaTime);
        ClearStaleFallingFlags(ref state);
    }

    public bool IsStable(in GameState state)
    {
        for (int x = 0; x < state.Width; x++)
        {
            for (int y = 0; y < state.Height; y++)
            {
                if (!IsTileStable(in state, x, y))
                    return false;
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetFrameBuffers()
    {
        _targetResolver.ClearReservations();
        _newlyOccupiedSlots.Clear();
    }

    private void ProcessShuffledColumns(ref GameState state, float dt)
    {
        var columnIndices = Pools.ObtainList<int>(state.Width);
        try
        {
            for (int i = 0; i < state.Width; i++) columnIndices.Add(i);
            ShuffleColumnIndices(columnIndices);
            foreach (var x in columnIndices)
                ProcessColumn(ref state, x, dt);
        }
        finally
        {
            Pools.Release(columnIndices);
        }
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

            if (ShouldSkipTile(in state, tile, x, y))
            {
                // Clear falling state for immovable tiles (e.g. under Drop lock or cover)
                if (tile.IsFalling && tile.Type != ElementType.None && !state.CanMove(x, y))
                {
                    tile.IsFalling = false;
                    tile.Velocity.Y = 0;
                    tile.Velocity.X = 0;
                    tile.Position.X = x;
                    tile.Position.Y = y;
                    state.SetTile(x, y, tile);
                }
                continue;
            }

            var target = _targetResolver.DetermineTarget(ref state, x, y);
            SimulatePhysics(ref state, ref tile, target, x, y, dt);
            UpdateGridPosition(ref state, x, y, tile);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldSkipTile(in GameState state, Tile tile, int x, int y)
    {
        if (tile.Type == ElementType.None) return true;
        if (_newlyOccupiedSlots.Contains(y * state.Width + x)) return true;
        if (!state.CanMove(x, y)) return true;
        return false;
    }

    private void SimulatePhysics(
        ref GameState state, ref Tile tile,
        IGravityTargetResolver.TargetInfo target,
        int gx, int gy, float dt)
    {
        bool isDiagonal = (int)target.Position.X != gx;

        if (tile.Position.Y < target.Position.Y - FloorSnapDistance)
        {
            // Apply gravity (identical for vertical and diagonal)
            tile.IsFalling = true;

            if (tile.Velocity.Y < _config.InitialFallSpeed)
                tile.Velocity.Y = _config.InitialFallSpeed;

            tile.Velocity.Y += _config.GravitySpeed * dt;
            if (tile.Velocity.Y > _config.MaxFallSpeed)
                tile.Velocity.Y = _config.MaxFallSpeed;

            float deltaY = tile.Velocity.Y * dt;
            tile.Position.Y += deltaY;

            // Diagonal: X derived from grid origin + Y offset (zero accumulation error)
            if (isDiagonal)
            {
                float dirX = Math.Sign(target.Position.X - gx);
                float progress = Math.Max(0, tile.Position.Y - gy);
                tile.Position.X = gx + progress * dirX;
                tile.Velocity.X = tile.Velocity.Y * dirX;

                // Clamp: X must not overshoot target
                if ((dirX > 0 && tile.Position.X > target.Position.X) ||
                    (dirX < 0 && tile.Position.X < target.Position.X))
                {
                    tile.Position.X = target.Position.X;
                    tile.Velocity.X = 0;
                }
            }
            else if (Math.Abs(tile.Position.X - gx) > SnapThreshold)
            {
                // Not diagonal, but px misaligned — complete previous diagonal's X movement
                float dirX = Math.Sign(gx - tile.Position.X);
                tile.Position.X += Math.Abs(deltaY) * dirX;
                tile.Velocity.X = Math.Abs(tile.Velocity.Y) * dirX;
                if ((dirX > 0 && tile.Position.X > gx) ||
                    (dirX < 0 && tile.Position.X < gx))
                {
                    tile.Position.X = gx;
                    tile.Velocity.X = 0;
                }
            }

            // Snap if overshot target
            if (tile.Position.Y >= target.Position.Y)
                SnapWithPeek(ref state, ref tile, target);
        }
        else
        {
            if (tile.IsFalling || Math.Abs(tile.Position.Y - target.Position.Y) > FloorSnapDistance)
                SnapWithPeek(ref state, ref tile, target);
        }
    }

    /// <summary>
    /// One-frame lookahead snap: peek at the target cell to decide strategy.
    /// </summary>
    private void SnapWithPeek(
        ref GameState state, ref Tile tile,
        IGravityTargetResolver.TargetInfo target)
    {
        tile.Velocity.X = 0;
        int tx = (int)target.Position.X;
        int ty = (int)target.Position.Y;

        var peek = _targetResolver.PeekNextMove(ref state, tx, ty);

        switch (peek)
        {
            case NextMoveType.Vertical:
                // Continue vertically — snap X, preserve Y overshoot + vy
                tile.Position.X = tx;
                tile.IsFalling = true;
                break;

            case NextMoveType.Diagonal:
                // Continue with diagonal — snap both (prevent cross-direction error), preserve vy
                tile.Position.Y = ty;
                tile.Position.X = tx;
                tile.IsFalling = true;
                break;

            default: // Stop
                tile.Position.Y = ty;
                tile.Position.X = tx;
                tile.Velocity.Y = 0;
                tile.IsFalling = false;
                break;
        }
    }

    private void UpdateGridPosition(ref GameState state, int currentX, int currentY, Tile tile)
    {
        int visualX = (int)Math.Floor(tile.Position.X + 0.5f);
        int visualY = (int)Math.Floor(tile.Position.Y + 0.5f);

        if (HasMovedToNewCell(state, visualX, visualY, currentX, currentY))
        {
            // Hole = doesn't exist. Teleport to exit, as if cells are directly connected.
            if (state.IsVoid(visualX, visualY))
            {
                int lastHole = GravityTargetResolver.FindHoleZoneExit(in state, visualX, visualY);
                int exitY = lastHole + 1;
                if (exitY < state.Height &&
                    state.GetTile(visualX, exitY).Type == ElementType.None &&
                    !state.HasObstacle(visualX, exitY))
                {
                    tile.Position.Y += exitY - visualY; // skip hole zone distance
                    state.SetTile(visualX, exitY, tile);
                    state.SetTile(currentX, currentY, new Tile(0, ElementType.None, currentX, currentY));
                    SyncDynamicCover(ref state, currentX, currentY, visualX, exitY);
                    _newlyOccupiedSlots.Add(exitY * state.Width + visualX);
                }
                else
                {
                    // Exit blocked: stay above hole
                    state.SetTile(currentX, currentY, tile);
                }
                return;
            }

            var targetSlot = state.GetTile(visualX, visualY);
            if (targetSlot.Type == ElementType.None && !state.HasObstacle(visualX, visualY))
            {
                state.SetTile(visualX, visualY, tile);
                state.SetTile(currentX, currentY, new Tile(0, ElementType.None, currentX, currentY));
                SyncDynamicCover(ref state, currentX, currentY, visualX, visualY);
                _newlyOccupiedSlots.Add(visualY * state.Width + visualX);
                return;
            }
        }

        state.SetTile(currentX, currentY, tile);
    }

    private void SyncDynamicCover(ref GameState state, int fromX, int fromY, int toX, int toY)
    {
        var cover = state.GetCover(fromX, fromY);
        if (cover.Type != CoverType.None && cover.IsDynamic && !state.IsVoid(toX, toY))
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
        if (tile.Type == ElementType.None) return true;
        if (tile.IsFalling) return false;
        if (!state.CanMove(x, y)) return true;

        return Math.Abs(tile.Velocity.Y) <= SnapThreshold &&
               Math.Abs(tile.Velocity.X) <= SnapThreshold &&
               Math.Abs(tile.Position.Y - y) <= SnapThreshold &&
               Math.Abs(tile.Position.X - x) <= SnapThreshold;
    }

    private void ClearStaleFallingFlags(ref GameState state)
    {
        for (int x = 0; x < state.Width; x++)
        {
            for (int y = 0; y < state.Height; y++)
            {
                var tile = state.GetTile(x, y);
                if (!tile.IsFalling || tile.Type == ElementType.None) continue;

                if (Math.Abs(tile.Velocity.Y) <= SnapThreshold &&
                    Math.Abs(tile.Velocity.X) <= SnapThreshold &&
                    Math.Abs(tile.Position.Y - y) <= SnapThreshold &&
                    Math.Abs(tile.Position.X - x) <= SnapThreshold)
                {
                    tile.IsFalling = false;
                    state.SetTile(x, y, tile);
                }
            }
        }
    }

    /// <inheritdoc />
    public virtual IPhysicsSimulation CloneForSimulation(IRandom newRandom)
    {
        return new RealtimeGravitySystem(_config, newRandom);
    }
}

/// <summary>
/// Standard physics simulation with gravity.
/// Alias for RealtimeGravitySystem with a standardized name.
/// </summary>
public class StandardGravitySystem : RealtimeGravitySystem
{
    public StandardGravitySystem(Match3Config config, IRandom random)
        : base(config, random)
    {
    }

    public StandardGravitySystem(Match3Config config, IRandom random, IGravityTargetResolver targetResolver)
        : base(config, random, targetResolver)
    {
    }
}
