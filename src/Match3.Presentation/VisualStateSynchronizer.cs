using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation.Animations;

namespace Match3.Presentation;

/// <summary>
/// Synchronizes visual state with game state, creating animations as needed.
/// This is the bridge between Core simulation and Presentation rendering.
/// </summary>
public sealed class VisualStateSynchronizer
{
    private readonly VisualState _visualState;
    private readonly AnimationTimeline _timeline;

    // Reusable collections to avoid per-frame allocations
    private readonly HashSet<long> _gameStateTileIds = new();
    private readonly List<long> _tileIdsToRemove = new();

    /// <summary>
    /// Duration for tile movement animations.
    /// </summary>
    public float MoveDuration { get; set; } = 0.15f;

    /// <summary>
    /// Duration for tile destroy animations.
    /// </summary>
    public float DestroyDuration { get; set; } = 0.15f;

    public VisualStateSynchronizer(VisualState visualState, AnimationTimeline timeline)
    {
        _visualState = visualState ?? throw new ArgumentNullException(nameof(visualState));
        _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
    }

    /// <summary>
    /// Synchronize visual state with current game state.
    /// Creates animations for new tiles, falling tiles, and removed tiles.
    /// </summary>
    public void SyncFromGameState(in GameState state)
    {
        // IMPORTANT: Iterate from bottom to top so lower tiles create animations first,
        // allowing upper tiles to correctly wait for them
        _gameStateTileIds.Clear();

        for (int y = state.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                if (tile.Type == TileType.None) continue;

                _gameStateTileIds.Add(tile.Id);
                SyncTile(tile, x, y);
            }
        }

        // Handle tiles that are no longer in game state
        CleanupRemovedTiles();
    }

    private void SyncTile(in Tile tile, int x, int y)
    {
        var existingVisual = _visualState.GetTile(tile.Id);

        if (existingVisual == null)
        {
            // New tile - spawn from above with animation
            SpawnNewTile(tile, x, y);
        }
        else
        {
            // Existing tile - check if needs movement animation
            UpdateExistingTile(tile.Id, existingVisual, x, y);
        }
    }

    private void SpawnNewTile(in Tile tile, int x, int y)
    {
        var startPos = new Vector2(x, y - 1);
        var endPos = new Vector2(x, y);

        _visualState.AddTile(
            tile.Id,
            tile.Type,
            tile.Bomb,
            new Position(x, y),
            startPos
        );

        // Wait for destroy animations and for tiles below to clear 0.5 cell
        float destroyEndTime = _timeline.GetDestroyEndTimeForColumn(x, y);
        float moveHalfTime = _timeline.GetMoveHalfCellTimeForColumn(x, y + 1);
        float startTime = Math.Max(destroyEndTime, moveHalfTime);

        var animation = new TileMoveAnimation(
            _timeline.GenerateAnimationId(),
            tile.Id,
            startPos,
            endPos,
            startTime,
            MoveDuration
        );
        _timeline.AddAnimation(animation);
    }

    private void UpdateExistingTile(long tileId, TileVisual visual, int x, int y)
    {
        var currentPos = visual.Position;
        var targetPos = new Vector2(x, y);

        // Check if there's an active animation for this tile
        var existingTarget = _timeline.GetMoveTargetForTile(tileId);

        if (existingTarget.HasValue)
        {
            // Tile has active animation - check if target needs updating
            if ((int)existingTarget.Value.Y != y)
            {
                // Target has changed (tile fell further), update animation
                _timeline.RemoveAnimationsForTile(tileId);

                // Create new animation from current visual position to new target
                float startTime = _timeline.CurrentTime;
                var animation = new TileMoveAnimation(
                    _timeline.GenerateAnimationId(),
                    tileId,
                    currentPos,
                    targetPos,
                    startTime,
                    MoveDuration
                );
                _timeline.AddAnimation(animation);
            }
            // else: target is same, let existing animation continue
        }
        else if ((int)currentPos.Y != y)
        {
            // No active animation, but position changed - create new animation
            // Wait for destroy animations and for tiles below to clear 0.5 cell
            float destroyEndTime = _timeline.GetDestroyEndTimeForColumn(x, y);
            float moveHalfTime = _timeline.GetMoveHalfCellTimeForColumn(x, y + 1);
            float startTime = Math.Max(destroyEndTime, moveHalfTime);

            var animation = new TileMoveAnimation(
                _timeline.GenerateAnimationId(),
                tileId,
                currentPos,
                targetPos,
                startTime,
                MoveDuration
            );
            _timeline.AddAnimation(animation);
        }
        else
        {
            // No movement needed, just sync position
            _visualState.SetTilePosition(tileId, targetPos);
        }
    }

    private void CleanupRemovedTiles()
    {
        _tileIdsToRemove.Clear();

        foreach (var kvp in _visualState.Tiles)
        {
            if (!_gameStateTileIds.Contains(kvp.Key))
            {
                if (kvp.Value.Alpha < 0.01f)
                {
                    // Animation complete, remove tile
                    _tileIdsToRemove.Add(kvp.Key);
                }
                else if (kvp.Value.Alpha >= 0.99f && !_timeline.HasAnimationForTile(kvp.Key))
                {
                    // Tile was replaced without destroy event (e.g., bomb spawn)
                    // Start destroy animation
                    var pos = kvp.Value.Position;
                    var animation = new TileDestroyAnimation(
                        _timeline.GenerateAnimationId(),
                        kvp.Key,
                        pos,
                        _timeline.CurrentTime,
                        DestroyDuration
                    );
                    _timeline.AddAnimation(animation);
                }
            }
        }

        foreach (var id in _tileIdsToRemove)
        {
            _visualState.RemoveTile(id);
        }
    }
}
