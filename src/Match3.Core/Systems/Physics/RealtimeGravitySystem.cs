using System;
using System.Runtime.CompilerServices;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
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
    private readonly IGravityTargetResolver _targetResolver;

    // Frame buffers (bool[] for hot-path performance)
    private bool[] _newlyOccupied = Array.Empty<bool>();
    private int[] _columnOrder = Array.Empty<int>();

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
        EnsureBufferCapacity(state);
        ResetFrameBuffers(state);
        ProcessShuffledColumns(ref state, deltaTime);
    }

    public bool IsStable(in GameState state)
    {
        for (int x = 0; x < state.Width; x++)
            for (int y = 0; y < state.Height; y++)
                if (!IsTileStable(in state, x, y))
                    return false;
        return true;
    }

    private void EnsureBufferCapacity(GameState state)
    {
        int size = state.Width * state.Height;
        if (_newlyOccupied.Length < size)
            _newlyOccupied = new bool[size];
        if (_columnOrder.Length < state.Width)
            _columnOrder = new int[state.Width];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetFrameBuffers(GameState state)
    {
        _targetResolver.ClearReservations();
        Array.Clear(_newlyOccupied, 0, state.Width * state.Height);
    }

    private void ProcessShuffledColumns(ref GameState state, float dt)
    {
        int w = state.Width;
        for (int i = 0; i < w; i++) _columnOrder[i] = i;

        // Fisher-Yates shuffle
        for (int n = w - 1; n > 0; n--)
        {
            int k = _random.Next(0, n + 1);
            (_columnOrder[k], _columnOrder[n]) = (_columnOrder[n], _columnOrder[k]);
        }

        for (int i = 0; i < w; i++)
            ProcessColumn(ref state, _columnOrder[i], dt);
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

            var target = ResolveTarget(ref state, ref tile, x, y);
            SimulatePhysics(ref state, ref tile, target, x, y, dt);
            UpdateGridPosition(ref state, x, y, tile);
        }
    }

    /// <summary>
    /// Single-cell lock: if the tile is mid-diagonal-slide (px ≠ gx),
    /// continue the committed direction instead of re-evaluating.
    /// </summary>
    private IGravityTargetResolver.TargetInfo ResolveTarget(
        ref GameState state, ref Tile tile, int gx, int gy)
    {
        float driftX = tile.Position.X - gx;
        if (Math.Abs(driftX) > SnapThreshold)
        {
            // Tile is mid-slide — derive target from committed direction
            int slideDir = Math.Sign(driftX);
            int targetX = gx + slideDir;
            int checkY = gy + 1;
            // Skip holes below for the target row
            while (checkY < state.Height && state.IsHole(gx, checkY))
                checkY++;

            if (checkY < state.Height &&
                state.IsValid(targetX, checkY) &&
                !state.IsHole(targetX, checkY) &&
                !state.HasObstacle(targetX, checkY) &&
                state.GetTile(targetX, checkY).Type == ElementType.None)
            {
                return new IGravityTargetResolver.TargetInfo(targetX, checkY);
            }

            // Target no longer available — abort slide, snap back to grid column
            tile.Position.X = gx;
            tile.Velocity.X = 0;
        }

        return _targetResolver.DetermineTarget(ref state, gx, gy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldSkipTile(in GameState state, Tile tile, int x, int y)
    {
        if (tile.Type == ElementType.None) return true;
        if (_newlyOccupied[state.Index(x, y)]) return true;
        if (!state.CanMove(x, y)) return true;
        return false;
    }

    private void SimulatePhysics(
        ref GameState state, ref Tile tile,
        IGravityTargetResolver.TargetInfo target,
        int gx, int gy, float dt)
    {
        bool isDiagonal = target.X != gx;

        if (tile.Position.Y < target.Y - FloorSnapDistance)
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
                float dirX = Math.Sign(target.X - gx);
                float progress = Math.Max(0, tile.Position.Y - gy);
                tile.Position.X = gx + progress * dirX;
                tile.Velocity.X = tile.Velocity.Y * dirX;

                // Clamp: X must not overshoot target
                if ((dirX > 0 && tile.Position.X > target.X) ||
                    (dirX < 0 && tile.Position.X < target.X))
                {
                    tile.Position.X = target.X;
                    tile.Velocity.X = 0;
                }
            }
            else if (Math.Abs(tile.Position.X - gx) > SnapThreshold)
            {
                // Not diagonal, but px misaligned — complete previous diagonal's X movement
                float dirX = Math.Sign(gx - tile.Position.X);
                tile.Position.X += deltaY * dirX;
                tile.Velocity.X = tile.Velocity.Y * dirX;
                if ((dirX > 0 && tile.Position.X > gx) ||
                    (dirX < 0 && tile.Position.X < gx))
                {
                    tile.Position.X = gx;
                    tile.Velocity.X = 0;
                }
            }

            // Snap if overshot target
            if (tile.Position.Y >= target.Y)
                SnapWithPeek(ref state, ref tile, target);
        }
        else
        {
            if (tile.IsFalling || Math.Abs(tile.Position.Y - target.Y) > FloorSnapDistance)
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

        var peek = _targetResolver.PeekNextMove(ref state, target.X, target.Y);

        switch (peek)
        {
            case NextMoveType.Vertical:
                // Continue vertically — snap X, preserve Y overshoot + vy
                tile.Position.X = target.X;
                tile.IsFalling = true;
                break;

            case NextMoveType.Diagonal:
                // Continue with diagonal — snap both (prevent cross-direction error), preserve vy
                tile.Position.Y = target.Y;
                tile.Position.X = target.X;
                tile.IsFalling = true;
                break;

            default: // Stop
                tile.Position.Y = target.Y;
                tile.Position.X = target.X;
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
                if (lastHole < 0)
                {
                    state.SetTile(currentX, currentY, tile);
                    return;
                }

                int exitY = lastHole + 1;
                if (exitY < state.Height &&
                    state.GetTile(visualX, exitY).Type == ElementType.None &&
                    !state.HasObstacle(visualX, exitY))
                {
                    tile.Position.Y += exitY - visualY;
                    state.SetTile(visualX, exitY, tile);
                    state.SetTile(currentX, currentY, new Tile(0, ElementType.None, currentX, currentY));
                    SyncDynamicCover(ref state, currentX, currentY, visualX, exitY);
                    _newlyOccupied[state.Index(visualX, exitY)] = true;
                }
                else
                {
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
                _newlyOccupied[state.Index(visualX, visualY)] = true;
                return;
            }
        }

        state.SetTile(currentX, currentY, tile);
    }

    private static void SyncDynamicCover(ref GameState state, int fromX, int fromY, int toX, int toY)
    {
        var cover = state.GetCover(fromX, fromY);
        if (cover.Type != CoverType.None && cover.IsDynamic && !state.IsVoid(toX, toY))
        {
            state.SetCover(toX, toY, cover);
            state.SetCover(fromX, fromY, Cover.Empty);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasMovedToNewCell(GameState state, int visualX, int visualY, int currentX, int currentY)
    {
        return (visualX != currentX || visualY != currentY) &&
               visualX >= 0 && visualX < state.Width &&
               visualY >= 0 && visualY < state.Height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAtRest(in Tile tile, int x, int y)
    {
        return Math.Abs(tile.Velocity.Y) <= SnapThreshold &&
               Math.Abs(tile.Velocity.X) <= SnapThreshold &&
               Math.Abs(tile.Position.Y - y) <= SnapThreshold &&
               Math.Abs(tile.Position.X - x) <= SnapThreshold;
    }

    private static bool IsTileStable(in GameState state, int x, int y)
    {
        var tile = state.GetTile(x, y);
        if (tile.Type == ElementType.None) return true;
        if (tile.IsFalling) return false;
        if (!state.CanMove(x, y)) return true;
        return IsAtRest(in tile, x, y);
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
