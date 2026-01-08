using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Interfaces;
using Match3.Core.Logic;
using Match3.Core.Structs;
using Match3.Random;

namespace Match3.Core;

/// <summary>
/// Orchestrates the game logic, handling moves, matches, and cascading effects.
/// Acts as the bridge between the GameState data and the IGameView presentation.
/// </summary>
public sealed class Match3Controller
{
    private GameState _state;
    private readonly Match3Config _config;
    private readonly IGameView _view;
    
    private readonly IMatchFinder _matchFinder;
    private readonly IMatchProcessor _matchProcessor;
    private readonly IGravitySystem _gravitySystem;
    private readonly IPowerUpHandler _powerUpHandler;
    private readonly ITileGenerator _tileGenerator;
    private readonly IGameLogger _logger;

    // Animation constants
    private const float Epsilon = 0.01f;

    // Async/Real-time state tracking
    // We lock tiles by ID because their position might change due to gravity while swapping
    private readonly HashSet<long> _lockedTileIds = new();
    private bool _isVisuallyStable = true;
    
    private class SwapTask
    {
        public long IdA;
        public long IdB;
        public bool CheckMatch;
    }
    private readonly List<SwapTask> _activeSwaps = new();

    public GameState State => _state;
    public bool IsIdle => _activeSwaps.Count == 0 && _isVisuallyStable;
    public Position SelectedPosition { get; private set; } = Position.Invalid;
    public string StatusMessage { get; private set; } = "Ready";

    public Match3Controller(
        Match3Config config,
        IRandom rng, 
        IGameView view,
        IMatchFinder matchFinder,
        IMatchProcessor matchProcessor,
        IGravitySystem gravitySystem,
        IPowerUpHandler powerUpHandler,
        ITileGenerator tileGenerator,
        IGameLogger logger,
        LevelConfig? levelConfig = null)
    {
        _config = config;
        _view = view;
        _matchFinder = matchFinder;
        _matchProcessor = matchProcessor;
        _gravitySystem = gravitySystem;
        _powerUpHandler = powerUpHandler;
        _tileGenerator = tileGenerator;
        _logger = logger;

        _state = new GameState(_config.Width, _config.Height, _config.TileTypesCount, rng);
        InitializeBoard(levelConfig);
        _logger.LogInfo($"Match3Controller initialized with size {_config.Width}x{_config.Height}");
    }

    private void InitializeBoard(LevelConfig? levelConfig)
    {
        if (levelConfig != null)
        {
            // Load from config
            for (int i = 0; i < levelConfig.Grid.Length; i++)
            {
                int x = i % levelConfig.Width;
                int y = i / levelConfig.Width;
                
                // Ensure we don't go out of bounds if config doesn't match state dimensions exactly
                if (x < _state.Width && y < _state.Height)
                {
                    var type = levelConfig.Grid[i];
                    // If type is None (0), maybe we want to generate one? 
                    // Or maybe None is a valid hole? 
                    // For now, let's assume None means "Empty/Hole" or if the user wants random, they shouldn't set it in config?
                    // But usually level config specifies the initial layout.
                    
                    // If the editor saves 0 (None), it usually means empty space.
                    // Let's assign it directly.
                    _state.SetTile(x, y, new Tile(_state.NextTileId++, type, x, y));
                }
            }
        }
        else
        {
            // Default random generation
            for (int y = 0; y < _state.Height; y++)
            {
                for (int x = 0; x < _state.Width; x++)
                {
                    var type = _tileGenerator.GenerateNonMatchingTile(ref _state, x, y);
                    _state.SetTile(x, y, new Tile(_state.NextTileId++, type, x, y));
                }
            }
        }
    }

    public void OnTap(Position p)
    {
        if (!IsValidPosition(p)) return;

        // Interaction Check: Can only interact with Stable and Unlocked tiles
        if (!IsStable(p) || IsLocked(p)) 
        {
            return;
        }
        
        _logger.LogInfo($"OnTap: {p}");

        if (SelectedPosition == Position.Invalid)
        {
            SelectedPosition = p;
            StatusMessage = "Select destination";
        }
        else
        {
            if (SelectedPosition == p)
            {
                SelectedPosition = Position.Invalid;
                StatusMessage = "Selection Cleared";
            }
            else
            {
                if (IsNeighbor(SelectedPosition, p))
                {
                    bool success = TryStartSwap(SelectedPosition, p);
                    if (success)
                    {
                        SelectedPosition = Position.Invalid;
                        StatusMessage = "Swapping...";
                    }
                    else
                    {
                        StatusMessage = "Invalid Move";
                        SelectedPosition = Position.Invalid;
                    }
                }
                else
                {
                    SelectedPosition = p;
                    StatusMessage = "Select destination";
                }
            }
        }
    }

    public void OnSwipe(Position from, Direction direction)
    {
        if (!IsValidPosition(from)) return;
        if (!IsStable(from) || IsLocked(from)) return;

        Position to = GetNeighbor(from, direction);
        if (!IsValidPosition(to)) return;
        if (!IsStable(to) || IsLocked(to)) return;

        SelectedPosition = Position.Invalid;

        bool success = TryStartSwap(from, to);
        if (success)
        {
            StatusMessage = "Swapping...";
        }
        else
        {
            StatusMessage = "Invalid Move";
        }
    }

    public void Update(float dt)
    {
        // 1. Update Animations (Visuals)
        _isVisuallyStable = AnimateTiles(dt);

        // 2. Update Pending Swaps (Logic)
        UpdateSwapTasks();

        // 3. Resolve Board State (Matches, Gravity, Refill)
        ResolveMatches();
    }

    private bool TryStartSwap(Position a, Position b)
    {
        if (IsLocked(a) || IsLocked(b)) return false;

        var tA = _state.GetTile(a.X, a.Y);
        var tB = _state.GetTile(b.X, b.Y);

        // Lock tiles so they cannot be matched or moved again until done
        Lock(a);
        Lock(b);

        // Perform logical swap immediately
        Swap(ref _state, a, b);

        // Add task to track the animation and subsequent logic
        _activeSwaps.Add(new SwapTask { IdA = tA.Id, IdB = tB.Id, CheckMatch = true });
        
        // Notify View
        _view.ShowSwap(a, b, true); 
        
        return true;
    }

    private void UpdateSwapTasks()
    {
        for (int i = _activeSwaps.Count - 1; i >= 0; i--)
        {
            var task = _activeSwaps[i];
            
            var posA = FindTilePosition(task.IdA);
            var posB = FindTilePosition(task.IdB);

            // If tiles are missing (destroyed?), remove task
            if (!posA.IsValid || !posB.IsValid)
            {
                // Unlocking handles missing IDs gracefully (remove nothing)
                Unlock(task.IdA);
                Unlock(task.IdB);
                _activeSwaps.RemoveAt(i);
                continue;
            }

            // Check if visuals have arrived at the logical destination
            if (IsVisualAtTarget(posA) && IsVisualAtTarget(posB))
            {
                if (task.CheckMatch)
                {
                    var matchesA = _matchFinder.FindMatchGroups(in _state, posA);
                    var matchesB = _matchFinder.FindMatchGroups(in _state, posB);
                    bool hasMatch = matchesA.Count > 0 || matchesB.Count > 0;
                    bool isSpecial = IsSpecialMove(posA, posB);

                    if (hasMatch || isSpecial)
                    {
                        // Valid Move
                        if (isSpecial)
                        {
                            _powerUpHandler.ProcessSpecialMove(ref _state, posA, posB, out int points);
                            _state.Score += points;
                        }

                        // Unlock and finish
                        Unlock(task.IdA);
                        Unlock(task.IdB);
                        _activeSwaps.RemoveAt(i);
                    }
                    else
                    {
                        // Invalid Move -> Revert
                        Swap(ref _state, posA, posB); // Swap back logically
                        task.CheckMatch = false; // Next arrival means revert done
                        
                        _view.ShowSwap(posA, posB, false); // Visual feedback for revert
                    }
                }
                else
                {
                    // Revert Finished
                    Unlock(task.IdA);
                    Unlock(task.IdB);
                    _activeSwaps.RemoveAt(i);
                    StatusMessage = "Invalid Move";
                }
            }
        }
    }

    private void ResolveMatches()
    {
        // 1. Find all potential matches
        var allGroups = _matchFinder.FindMatchGroups(in _state);
        
        // 2. Filter matches: Keep only those where ALL tiles are Stable and Not Locked
        var validGroups = new List<MatchGroup>();
        foreach (var group in allGroups)
        {
            bool isGroupValid = true;
            foreach (var p in group.Positions)
            {
                if (IsLocked(p) || !IsStable(p))
                {
                    isGroupValid = false;
                    break;
                }
            }
            if (isGroupValid)
            {
                validGroups.Add(group);
            }
        }

        // 3. If no valid matches and no holes, nothing to do
        if (validGroups.Count == 0 && !HasHoles()) return;

        // 4. Process Matches
        if (validGroups.Count > 0)
        {
            var allPositions = new HashSet<Position>();
            foreach(var g in validGroups) 
                foreach(var p in g.Positions) allPositions.Add(p);
            
            _view.ShowMatches(allPositions);

            int points = _matchProcessor.ProcessMatches(ref _state, validGroups);
            _state.Score += points;
        }

        // 5. Apply Gravity
        var gravityMoves = _gravitySystem.ApplyGravity(ref _state);
        if (gravityMoves.Count > 0) _view.ShowGravity(gravityMoves);
        
        // 6. Refill
        var refillMoves = _gravitySystem.Refill(ref _state);
        if (refillMoves.Count > 0) _view.ShowRefill(refillMoves);
    }

    // Animation / Stability Helpers

    private bool AnimateTiles(float dt)
    {
        bool allStable = true;
        for (int i = 0; i < _state.Grid.Length; i++)
        {
            ref var tile = ref _state.Grid[i];
            if (tile.Type == TileType.None) continue;

            int x = i % _state.Width;
            int y = i / _state.Width;
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
        return allStable;
    }

    private bool IsStable(Position p)
    {
        return IsVisualAtTarget(p);
    }

    private bool IsVisualAtTarget(Position p)
    {
        var tile = _state.GetTile(p.X, p.Y);
        if (tile.Type == TileType.None) return true; // Empty is stable

        var target = new Vector2(p.X, p.Y);
        return Vector2.DistanceSquared(tile.Position, target) <= Epsilon * Epsilon;
    }

    // Locking Helpers (ID Based)

    private void Lock(Position p) => _lockedTileIds.Add(_state.GetTile(p.X, p.Y).Id);
    private void Unlock(long id) => _lockedTileIds.Remove(id);
    private bool IsLocked(Position p) => _lockedTileIds.Contains(_state.GetTile(p.X, p.Y).Id);

    private Position FindTilePosition(long id)
    {
        for (int i = 0; i < _state.Grid.Length; i++)
        {
            if (_state.Grid[i].Id == id)
            {
                return new Position(i % _state.Width, i / _state.Width);
            }
        }
        return Position.Invalid;
    }

    // Standard Helpers

    private bool IsValidPosition(Position p)
    {
        return p.X >= 0 && p.X < _state.Width && p.Y >= 0 && p.Y < _state.Height;
    }

    private bool IsNeighbor(Position a, Position b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
    }

    private Position GetNeighbor(Position p, Direction dir)
    {
        return dir switch
        {
            Direction.Up => new Position(p.X, p.Y - 1),
            Direction.Down => new Position(p.X, p.Y + 1),
            Direction.Left => new Position(p.X - 1, p.Y),
            Direction.Right => new Position(p.X + 1, p.Y),
            _ => p
        };
    }

    private bool IsSpecialMove(Position a, Position b)
    {
        var t1 = _state.GetTile(a.X, a.Y);
        var t2 = _state.GetTile(b.X, b.Y);
        
        if (t1.Type == TileType.Rainbow || t2.Type == TileType.Rainbow) return true;
        
        bool isBombCombo = t1.Bomb != BombType.None && t2.Bomb != BombType.None;
        
        return isBombCombo;
    }

    private void Swap(ref GameState state, Position a, Position b)
    {
        var idxA = state.Index(a.X, a.Y);
        var idxB = state.Index(b.X, b.Y);
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;
    }

    private bool HasHoles()
    {
        for (int i = 0; i < _state.Grid.Length; i++)
        {
            if (_state.Grid[i].Type == TileType.None) return true;
        }
        return false;
    }

    public void DebugSetTile(Position p, TileType t)
    {
        _state.SetTile(p.X, p.Y, new Tile(_state.NextTileId++, t, p.X, p.Y));
    }

    public bool TryMakeRandomMove()
    {
        for (int y = 0; y < _state.Height; y++)
        {
            for (int x = 0; x < _state.Width; x++)
            {
                var p = new Position(x, y);
                var right = new Position(x + 1, y);
                if (IsValidPosition(right))
                {
                    if (CheckAndStartMove(p, right)) return true;
                }
                var down = new Position(x, y + 1);
                if (IsValidPosition(down))
                {
                    if (CheckAndStartMove(p, down)) return true;
                }
            }
        }
        return false;
    }

    private bool CheckAndStartMove(Position a, Position b)
    {
        if (IsLocked(a) || IsLocked(b)) return false;

        Swap(ref _state, a, b);
        bool hasMatch = _matchFinder.HasMatches(in _state);
        bool isSpecial = IsSpecialMove(a, b);
        Swap(ref _state, a, b); // Swap back

        if (hasMatch || isSpecial)
        {
            TryStartSwap(a, b);
            StatusMessage = "Auto Move...";
            return true;
        }
        return false;
    }
}
