using System;
using System.Diagnostics;
using Match3.Random;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Core.Models.Grid;

/// <summary>
/// A pure data representation of the game state.
/// This is the "Component" in our ECS-like architecture.
/// </summary>
public struct GameState
{
    /// <summary>
    /// The 1D array representing the 2D grid (Tile layer).
    /// Index = y * Width + x
    /// </summary>
    public Tile[] Grid;

    /// <summary>
    /// The ground layer (beneath tiles).
    /// Ground elements are damaged when tiles above are destroyed.
    /// </summary>
    public Ground[] GroundLayer;

    /// <summary>
    /// The cover layer (above tiles).
    /// Cover elements protect tiles from being destroyed.
    /// </summary>
    public Cover[] CoverLayer;

    /// <summary>
    /// Per-cell lock layer. Each uint packs ref-counted locks via CellLockOps.
    /// </summary>
    public uint[] CellLocks;

    public int Width;
    public int Height;
    public int TileTypesCount;
    public int Score;
    public int MoveCount;
    public int NextTileId;

    // Difficulty control fields (for SpawnModel)
    public int MoveLimit;
    public float TargetDifficulty;

    /// <summary>
    /// The currently selected position for swapping.
    /// Part of the Input State.
    /// </summary>
    public Position SelectedPosition;

    // We store the seed or state of RNG to ensure determinism if we implement a custom PRNG struct.
    // For simplicity now, we'll keep the reference to IRandom, but in a pure ECS/DOTS,
    // this would be a 'RandomComponent' struct with internal state.
    public IRandom Random;

    /// <summary>
    /// Objective progress tracking (fixed size 4).
    /// </summary>
    public ObjectiveProgress[] ObjectiveProgress;

    /// <summary>
    /// Current level status (InProgress, Victory, Defeat).
    /// </summary>
    public LevelStatus LevelStatus;

    public GameState(int width, int height, int tileTypesCount, IRandom random)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");

        Width = width;
        Height = height;
        TileTypesCount = tileTypesCount;
        var size = width * height;
        Grid = new Tile[size];
        GroundLayer = new Ground[size];
        CoverLayer = new Cover[size];
        CellLocks = new uint[size];
        Score = 0;
        MoveCount = 0;
        NextTileId = 1;
        MoveLimit = 20;  // Default, should be set from LevelConfig
        TargetDifficulty = 0.5f;  // Default medium
        SelectedPosition = Position.Invalid;
        Random = random;
        ObjectiveProgress = new ObjectiveProgress[4];
        LevelStatus = LevelStatus.InProgress;
    }

    public GameState Clone()
    {
        var clone = new GameState(Width, Height, TileTypesCount, Random);
        clone.Score = Score;
        clone.MoveCount = MoveCount;
        clone.NextTileId = NextTileId;
        clone.MoveLimit = MoveLimit;
        clone.TargetDifficulty = TargetDifficulty;
        clone.SelectedPosition = SelectedPosition;
        // Use logical size (Width * Height) instead of array length
        // to support arrays from ArrayPool which may be larger
        int size = Width * Height;
        Array.Copy(Grid, clone.Grid, size);
        Array.Copy(GroundLayer, clone.GroundLayer, size);
        Array.Copy(CoverLayer, clone.CoverLayer, size);
        Array.Copy(CellLocks, clone.CellLocks, size);
        clone.ObjectiveProgress = new ObjectiveProgress[4];
        Array.Copy(ObjectiveProgress, clone.ObjectiveProgress, 4);
        clone.LevelStatus = LevelStatus;
        // Note: IRandom is shared reference here.
        // For true MCTS/branching, we would need a cloneable/struct RNG.
        return clone;
    }

    #region Tile Layer Access

    public readonly Tile GetTile(int x, int y) => Grid[y * Width + x];

    public readonly Tile GetTile(Position p) => Grid[p.Y * Width + p.X];

    public void SetTile(int x, int y, Tile tile) => Grid[y * Width + x] = tile;

    public void SetTile(Position p, Tile tile) => Grid[p.Y * Width + p.X] = tile;

    public readonly TileType GetType(int x, int y) => Grid[y * Width + x].Type;

    public readonly TileType GetType(Position p) => Grid[p.Y * Width + p.X].Type;

    #endregion

    #region Ground Layer Access

    public readonly ref Ground GetGround(int x, int y) => ref GroundLayer[y * Width + x];

    public readonly ref Ground GetGround(Position p) => ref GroundLayer[p.Y * Width + p.X];

    public void SetGround(int x, int y, Ground ground) => GroundLayer[y * Width + x] = ground;

    public void SetGround(Position p, Ground ground) => GroundLayer[p.Y * Width + p.X] = ground;

    public readonly bool HasGround(int x, int y) => GroundLayer[y * Width + x].Type != GroundType.None;

    public readonly bool HasGround(Position p) => GroundLayer[p.Y * Width + p.X].Type != GroundType.None;

    #endregion

    #region Cover Layer Access

    public readonly ref Cover GetCover(int x, int y) => ref CoverLayer[y * Width + x];

    public readonly ref Cover GetCover(Position p) => ref CoverLayer[p.Y * Width + p.X];

    public void SetCover(int x, int y, Cover cover) => CoverLayer[y * Width + x] = cover;

    public void SetCover(Position p, Cover cover) => CoverLayer[p.Y * Width + p.X] = cover;

    public readonly bool HasCover(int x, int y) => CoverLayer[y * Width + x].Type != CoverType.None;

    public readonly bool HasCover(Position p) => CoverLayer[p.Y * Width + p.X].Type != CoverType.None;

    #endregion

    #region Convenience Query Methods

    /// <summary>
    /// Returns true if the tile at this position can be swapped by the player.
    /// A tile cannot be swapped if it has cover, is falling, or is suspended.
    /// </summary>
    public readonly bool CanInteract(int x, int y)
    {
        var idx = y * Width + x;
        var cover = CoverLayer[idx];
        if (CoverRules.BlocksSwap(cover.Type)) return false;
        if (CellLockOps.IsLocked(CellLocks[idx], CellLockType.Swap)) return false;

        var tile = GetTile(x, y);
        if (tile.Type == TileType.None) return false;
        if (tile.IsFalling || tile.IsSuspended) return false;

        return true;
    }

    /// <summary>
    /// Returns true if the tile at this position can be swapped by the player.
    /// </summary>
    public readonly bool CanInteract(Position p) => CanInteract(p.X, p.Y);

    /// <summary>
    /// Returns true if the tile at this position can participate in matching.
    /// </summary>
    public readonly bool CanMatch(int x, int y)
    {
        var idx = y * Width + x;
        var cover = CoverLayer[idx];
        if (CoverRules.BlocksMatch(cover.Type)) return false;
        return !CellLockOps.IsLocked(CellLocks[idx], CellLockType.Matching);
    }

    /// <summary>
    /// Returns true if the tile at this position can participate in matching.
    /// </summary>
    public readonly bool CanMatch(Position p) => CanMatch(p.X, p.Y);

    /// <summary>
    /// Returns true if the tile at this position can move (gravity).
    /// Static covers block movement.
    /// </summary>
    public readonly bool CanMove(int x, int y)
    {
        var idx = y * Width + x;
        var cover = CoverLayer[idx];
        if (CoverRules.BlocksMovement(cover.Type)) return false;
        return !CellLockOps.IsLocked(CellLocks[idx], CellLockType.Drop);
    }

    /// <summary>
    /// Returns true if the tile at this position can move (gravity).
    /// </summary>
    public readonly bool CanMove(Position p) => CanMove(p.X, p.Y);

    #endregion

    #region Cell Lock Operations

    public void Lock(int x, int y, CellLockType types)
    {
        var idx = y * Width + x;
        CellLocks[idx] = CellLockOps.Lock(CellLocks[idx], types);
    }

    public void Lock(Position p, CellLockType types) => Lock(p.X, p.Y, types);

    public void Unlock(int x, int y, CellLockType types)
    {
        var idx = y * Width + x;
        CellLocks[idx] = CellLockOps.Unlock(CellLocks[idx], types);
    }

    public void Unlock(Position p, CellLockType types) => Unlock(p.X, p.Y, types);

    public readonly bool IsLocked(int x, int y, CellLockType type)
    {
        return CellLockOps.IsLocked(CellLocks[y * Width + x], type);
    }

    public readonly bool IsLocked(Position p, CellLockType type) => IsLocked(p.X, p.Y, type);

    public LockToken AcquireLock(int x, int y, CellLockType types)
    {
        var idx = y * Width + x;
        CellLocks[idx] = CellLockOps.Lock(CellLocks[idx], types);
        return new LockToken(idx, types);
    }

    public void ReleaseLock(LockToken token)
    {
        Debug.Assert(CellLockOps.AllLockedAboveZero(CellLocks[token.CellIndex], token.Types),
            "ReleaseLock: ref-count already 0 for one or more lock types, possible double-release");
        CellLocks[token.CellIndex] = CellLockOps.Unlock(CellLocks[token.CellIndex], token.Types);
    }

    public readonly bool CanReceive(int x, int y)
    {
        return !CellLockOps.IsLocked(CellLocks[y * Width + x], CellLockType.Receive);
    }

    public readonly bool CanReceive(Position p) => CanReceive(p.X, p.Y);

    public readonly bool CanDestroy(int x, int y)
    {
        return !CellLockOps.IsLocked(CellLocks[y * Width + x], CellLockType.Indestructible);
    }

    public readonly bool CanDestroy(Position p) => CanDestroy(p.X, p.Y);

    #endregion

    #region Utility

    public readonly int Index(int x, int y) => y * Width + x;

    public readonly int Index(Position p) => p.Y * Width + p.X;

    /// <summary>
    /// Checks if a position is within the grid boundaries.
    /// </summary>
    public readonly bool IsValid(Position p)
    {
        return p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
    }

    /// <summary>
    /// Checks if coordinates are within the grid boundaries.
    /// </summary>
    public readonly bool IsValid(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    #endregion
}
