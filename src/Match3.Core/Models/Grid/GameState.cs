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
    /// Static structure layer. Defines Void/Slot/Wall/Spawner/Sink roles.
    /// Array size: Width * Height.
    /// </summary>
    public CellKind[] Cells;

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

    /// <summary>
    /// Obstacle layer. Stores complex multi-stage blockers (Box, Bush, Safe, etc.).
    /// Obstacles are the cell's primary content — separate from Tile and Cover.
    /// Array size: Width * Height. Empty cells have Obstacle.Empty (Type=None).
    /// </summary>
    public Obstacle[] ObstacleLayer;

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

    /// <summary>
    /// Current simulation time in seconds. Updated each tick by the game loop.
    /// Used by CanMatch to skip tiles that are still in their protection window.
    /// </summary>
    public float SimulationTime;

    public GameState(int width, int height, int tileTypesCount, IRandom random)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");

        Width = width;
        Height = height;
        TileTypesCount = tileTypesCount;
        var size = width * height;
        
        Cells = new CellKind[size]; // Default Void(0)
        Grid = new Tile[size];
        GroundLayer = new Ground[size];
        CoverLayer = new Cover[size];
        CellLocks = new uint[size];
        ObstacleLayer = new Obstacle[size];
        
        Score = 0;
        MoveCount = 0;
        NextTileId = 1;
        MoveLimit = 20;  // Default, should be set from LevelConfig
        TargetDifficulty = 0.5f;  // Default medium
        SelectedPosition = Position.Invalid;
        Random = random;
        ObjectiveProgress = new ObjectiveProgress[4];
        LevelStatus = LevelStatus.InProgress;
        SimulationTime = 0f;

        // Init Cells to Slot by default to avoid breaking existing logic immediately
        Array.Fill(Cells, CellKind.Slot);
    }

    /// <summary>
    /// Clone without RNG — returns NullRandom (fail-fast).
    /// Caller must assign Random or use <see cref="Clone(IRandom)"/> instead.
    /// </summary>
    public GameState Clone()
    {
        var clone = CloneInternal();
        clone.Random = NullRandom.Instance;
        return clone;
    }

    /// <summary>
    /// Clone with explicit RNG — one-step safe copy.
    /// </summary>
    public GameState Clone(IRandom rng)
    {
        var clone = CloneInternal();
        clone.Random = rng;
        return clone;
    }

    private GameState CloneInternal()
    {
        var clone = this;
        int size = Width * Height;
        clone.Cells = new CellKind[size];
        clone.Grid = new Tile[size];
        clone.GroundLayer = new Ground[size];
        clone.CoverLayer = new Cover[size];
        clone.CellLocks = new uint[size];
        clone.ObstacleLayer = new Obstacle[size];
        Array.Copy(Cells, clone.Cells, size);
        Array.Copy(Grid, clone.Grid, size);
        Array.Copy(GroundLayer, clone.GroundLayer, size);
        Array.Copy(CoverLayer, clone.CoverLayer, size);
        Array.Copy(CellLocks, clone.CellLocks, size);
        Array.Copy(ObstacleLayer, clone.ObstacleLayer, size);
        clone.ObjectiveProgress = new ObjectiveProgress[4];
        Array.Copy(ObjectiveProgress, clone.ObjectiveProgress, 4);
        return clone;
    }

    #region Tile Layer Access

    public readonly Tile GetTile(int x, int y) => Grid[y * Width + x];

    public readonly Tile GetTile(Position p) => Grid[p.Y * Width + p.X];

    public void SetTile(int x, int y, Tile tile) => Grid[y * Width + x] = tile;

    public void SetTile(Position p, Tile tile) => Grid[p.Y * Width + p.X] = tile;

    public readonly ElementType GetType(int x, int y) => Grid[y * Width + x].Type;

    public readonly ElementType GetType(Position p) => Grid[p.Y * Width + p.X].Type;

    #endregion

    #region Cell Layer Access (NEW)

    public readonly CellKind GetCell(int x, int y) => Cells[y * Width + x];
    public void SetCell(int x, int y, CellKind kind) => Cells[y * Width + x] = kind;
    
    // IsHole is now IsVoid (CellKind.Void)
    public readonly bool IsVoid(int x, int y) => Cells[y * Width + x] == CellKind.Void;
    public readonly bool IsVoid(Position p) => Cells[p.Y * Width + p.X] == CellKind.Void;
    
    // Backward compatibility for IsHole
    public readonly bool IsHole(int x, int y) => IsVoid(x, y);
    public readonly bool IsHole(Position p) => IsVoid(p);

    #endregion

    public readonly ref Ground GetGround(int x, int y) => ref GroundLayer[y * Width + x];

    public readonly ref Ground GetGround(Position p) => ref GroundLayer[p.Y * Width + p.X];

    public void SetGround(int x, int y, Ground ground) => GroundLayer[y * Width + x] = ground;

    public void SetGround(Position p, Ground ground) => GroundLayer[p.Y * Width + p.X] = ground;

    public readonly bool HasGround(int x, int y) => GroundLayer[y * Width + x].Type != GroundType.None;

    public readonly bool HasGround(Position p) => GroundLayer[p.Y * Width + p.X].Type != GroundType.None;

    #region Cover Layer Access
    
    public readonly ref Cover GetCover(int x, int y) => ref CoverLayer[y * Width + x];
    
    public readonly ref Cover GetCover(Position p) => ref CoverLayer[p.Y * Width + p.X];
    
    public void SetCover(int x, int y, Cover cover) => CoverLayer[y * Width + x] = cover;
    
    public void SetCover(Position p, Cover cover) => CoverLayer[p.Y * Width + p.X] = cover;
    
    public readonly bool HasCover(int x, int y) => CoverLayer[y * Width + x].Type != CoverType.None;
    
    public readonly bool HasCover(Position p) => CoverLayer[p.Y * Width + p.X].Type != CoverType.None;
    
    #endregion

    #region Obstacle Layer Access

    public readonly ref Obstacle GetObstacle(int x, int y) => ref ObstacleLayer[y * Width + x];

    public readonly ref Obstacle GetObstacle(Position p) => ref ObstacleLayer[p.Y * Width + p.X];

    public void SetObstacle(int x, int y, Obstacle obstacle) => ObstacleLayer[y * Width + x] = obstacle;

    public void SetObstacle(Position p, Obstacle obstacle) => ObstacleLayer[p.Y * Width + p.X] = obstacle;

    public readonly bool HasObstacle(int x, int y) => ObstacleLayer[y * Width + x].Type != ObstacleType.None;

    public readonly bool HasObstacle(Position p) => ObstacleLayer[p.Y * Width + p.X].Type != ObstacleType.None;

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
        if (tile.Type == ElementType.None) return false;
        if (tile.IsFalling) return false;

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
        if (CellLockOps.IsLocked(CellLocks[idx], CellLockType.Matching)) return false;
        if (Grid[idx].ProtectUntil > SimulationTime) return false;
        return true;
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
        return new LockToken(0, idx, types);
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
    public readonly bool IsValid(Position p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

    /// <summary>
    /// Checks if coordinates are within the grid boundaries.
    /// </summary>
    public readonly bool IsValid(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    #endregion
}
