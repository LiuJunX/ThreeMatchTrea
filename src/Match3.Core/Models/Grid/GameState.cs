using System;
using Match3.Random;
using Match3.Core.Models.Enums;

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

    public int Width;
    public int Height;
    public int TileTypesCount;
    public long Score;
    public long MoveCount;
    public long NextTileId;

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

    public GameState(int width, int height, int tileTypesCount, IRandom random)
    {
        Width = width;
        Height = height;
        TileTypesCount = tileTypesCount;
        var size = width * height;
        Grid = new Tile[size];
        GroundLayer = new Ground[size];
        CoverLayer = new Cover[size];
        Score = 0;
        MoveCount = 0;
        NextTileId = 1;
        MoveLimit = 20;  // Default, should be set from LevelConfig
        TargetDifficulty = 0.5f;  // Default medium
        SelectedPosition = Position.Invalid;
        Random = random;
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
        Array.Copy(Grid, clone.Grid, Grid.Length);
        Array.Copy(GroundLayer, clone.GroundLayer, GroundLayer.Length);
        Array.Copy(CoverLayer, clone.CoverLayer, CoverLayer.Length);
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
    /// A tile cannot be swapped if it has any cover.
    /// </summary>
    public readonly bool CanInteract(int x, int y)
    {
        var cover = CoverLayer[y * Width + x];
        return !CoverRules.BlocksSwap(cover.Type);
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
        var cover = CoverLayer[y * Width + x];
        return !CoverRules.BlocksMatch(cover.Type);
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
        var cover = CoverLayer[y * Width + x];
        return !CoverRules.BlocksMovement(cover.Type);
    }

    /// <summary>
    /// Returns true if the tile at this position can move (gravity).
    /// </summary>
    public readonly bool CanMove(Position p) => CanMove(p.X, p.Y);

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
