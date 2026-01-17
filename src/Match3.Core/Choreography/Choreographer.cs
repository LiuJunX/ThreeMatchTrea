using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;

namespace Match3.Core.Choreography;

/// <summary>
/// Converts GameEvent sequences into RenderCommand sequences with pre-calculated timing.
/// This enables deterministic replay and easy serialization.
/// </summary>
public sealed class Choreographer : IEventVisitor
{
    private readonly List<RenderCommand> _commands = new();
    private float _baseTime;

    // Timing configuration
    /// <summary>Duration for tile movement animation.</summary>
    public float MoveDuration { get; set; } = 0.15f;

    /// <summary>Duration for tile destruction animation.</summary>
    public float DestroyDuration { get; set; } = 0.2f;

    /// <summary>Duration for match highlight before destruction.</summary>
    public float MatchHighlightDuration { get; set; } = 0.1f;

    /// <summary>Duration for swap animation.</summary>
    public float SwapDuration { get; set; } = 0.15f;

    /// <summary>Duration for projectile launch takeoff.</summary>
    public float ProjectileTakeoffDuration { get; set; } = 0.3f;

    /// <summary>Arc height for projectile launch.</summary>
    public float ProjectileArcHeight { get; set; } = 1.5f;

    // Timing tracking for cascade calculations
    private readonly Dictionary<int, float> _columnDestroyEndTimes = new();
    private readonly Dictionary<int, List<MoveRecord>> _columnMoves = new();

    private record struct MoveRecord(float StartTime, float EndTime, int TargetRow, Vector2 From, Vector2 To);

    /// <summary>
    /// Convert a sequence of game events into render commands.
    /// </summary>
    /// <param name="events">Events from simulation.</param>
    /// <param name="baseTime">Base timeline time for command scheduling.</param>
    /// <returns>List of render commands with pre-calculated timing.</returns>
    public IReadOnlyList<RenderCommand> Choreograph(IReadOnlyList<GameEvent> events, float baseTime = 0f)
    {
        _commands.Clear();
        _baseTime = baseTime;

        // Clear timing tracking
        _columnDestroyEndTimes.Clear();
        _columnMoves.Clear();

        // Process each event
        foreach (var evt in events)
        {
            evt.Accept(this);
        }

        return _commands;
    }

    private float GetStartTime(GameEvent evt)
    {
        return _baseTime + evt.SimulationTime;
    }

    #region IEventVisitor Implementation

    /// <inheritdoc />
    public void Visit(TileMovedEvent evt)
    {
        int column = (int)evt.ToPosition.X;
        int targetRow = (int)evt.ToPosition.Y;

        // Calculate start time considering cascading animations
        float startTime = CalculateMoveStartTime(column, targetRow, GetStartTime(evt));

        var command = new MoveTileCommand
        {
            TileId = evt.TileId,
            From = evt.FromPosition,
            To = evt.ToPosition,
            StartTime = startTime,
            Duration = MoveDuration,
            Easing = EasingType.OutCubic
        };

        _commands.Add(command);

        // Track this move for cascade timing
        TrackMove(column, startTime, startTime + MoveDuration, targetRow, evt.FromPosition, evt.ToPosition);
    }

    /// <inheritdoc />
    public void Visit(TileDestroyedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        // Destroy animation
        var destroyCommand = new DestroyTileCommand
        {
            TileId = evt.TileId,
            Position = position,
            Reason = evt.Reason,
            StartTime = startTime,
            Duration = DestroyDuration
        };
        _commands.Add(destroyCommand);

        // Track destroy end time for column
        int column = evt.GridPosition.X;
        float endTime = startTime + DestroyDuration;
        if (!_columnDestroyEndTimes.TryGetValue(column, out float existing) || endTime > existing)
        {
            _columnDestroyEndTimes[column] = endTime;
        }

        // Add visual effect
        string effectType = evt.Reason switch
        {
            DestroyReason.Match => "match_pop",
            DestroyReason.BombEffect => "explosion",
            DestroyReason.Projectile => "projectile_hit",
            DestroyReason.ChainReaction => "chain_pop",
            _ => "pop"
        };

        var effectCommand = new ShowEffectCommand
        {
            EffectType = effectType,
            Position = position,
            StartTime = startTime,
            Duration = DestroyDuration
        };
        _commands.Add(effectCommand);

        // Remove tile after destroy animation completes
        var removeCommand = new RemoveTileCommand
        {
            TileId = evt.TileId,
            StartTime = endTime,
            Duration = 0,
            Priority = 10 // Execute after effects
        };
        _commands.Add(removeCommand);
    }

    /// <inheritdoc />
    public void Visit(TileSpawnedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var gridPos = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        // Calculate move start time for cascade
        int column = evt.GridPosition.X;
        int targetRow = evt.GridPosition.Y;
        float moveStartTime = CalculateMoveStartTime(column, targetRow, startTime);

        // Spawn command
        var spawnCommand = new SpawnTileCommand
        {
            TileId = evt.TileId,
            Type = evt.Type,
            Bomb = evt.Bomb,
            GridPos = evt.GridPosition,
            SpawnPos = evt.SpawnPosition,
            StartTime = moveStartTime,
            Duration = 0
        };
        _commands.Add(spawnCommand);

        // Move from spawn position to grid position
        var moveCommand = new MoveTileCommand
        {
            TileId = evt.TileId,
            From = evt.SpawnPosition,
            To = gridPos,
            StartTime = moveStartTime,
            Duration = MoveDuration,
            Easing = EasingType.OutCubic
        };
        _commands.Add(moveCommand);

        // Track this move
        TrackMove(column, moveStartTime, moveStartTime + MoveDuration, targetRow, evt.SpawnPosition, gridPos);
    }

    /// <inheritdoc />
    public void Visit(TilesSwappedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var posA = new Vector2(evt.PositionA.X, evt.PositionA.Y);
        var posB = new Vector2(evt.PositionB.X, evt.PositionB.Y);

        var swapCommand = new SwapTilesCommand
        {
            TileAId = evt.TileAId,
            TileBId = evt.TileBId,
            PosA = posA,
            PosB = posB,
            IsRevert = evt.IsRevert,
            StartTime = startTime,
            Duration = SwapDuration,
            Easing = EasingType.OutCubic
        };
        _commands.Add(swapCommand);
    }

    /// <inheritdoc />
    public void Visit(MatchDetectedEvent evt)
    {
        float startTime = GetStartTime(evt);

        // Convert to array for the command
        var positions = new Models.Grid.Position[evt.Positions.Count];
        int i = 0;
        foreach (var pos in evt.Positions)
        {
            positions[i++] = pos;
        }

        var highlightCommand = new ShowMatchHighlightCommand
        {
            Positions = positions,
            StartTime = startTime,
            Duration = MatchHighlightDuration
        };
        _commands.Add(highlightCommand);
    }

    /// <inheritdoc />
    public void Visit(BombCreatedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var position = new Vector2(evt.Position.X, evt.Position.Y);

        // Update tile bomb type
        var updateCommand = new UpdateTileBombCommand
        {
            TileId = evt.TileId,
            Position = evt.Position,
            BombType = evt.BombType,
            StartTime = startTime,
            Duration = 0
        };
        _commands.Add(updateCommand);

        // Visual effect
        var effectCommand = new ShowEffectCommand
        {
            EffectType = "bomb_created",
            Position = position,
            StartTime = startTime,
            Duration = 0.3f
        };
        _commands.Add(effectCommand);
    }

    /// <inheritdoc />
    public void Visit(BombActivatedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var position = new Vector2(evt.Position.X, evt.Position.Y);

        var effectCommand = new ShowEffectCommand
        {
            EffectType = "bomb_explosion",
            Position = position,
            StartTime = startTime,
            Duration = 0.4f
        };
        _commands.Add(effectCommand);
    }

    /// <inheritdoc />
    public void Visit(BombComboEvent evt)
    {
        float startTime = GetStartTime(evt);
        var posA = new Vector2(evt.PositionA.X, evt.PositionA.Y);
        var posB = new Vector2(evt.PositionB.X, evt.PositionB.Y);

        var effectA = new ShowEffectCommand
        {
            EffectType = "bomb_combo",
            Position = posA,
            StartTime = startTime,
            Duration = 0.5f
        };
        _commands.Add(effectA);

        var effectB = new ShowEffectCommand
        {
            EffectType = "bomb_combo",
            Position = posB,
            StartTime = startTime,
            Duration = 0.5f
        };
        _commands.Add(effectB);
    }

    /// <inheritdoc />
    public void Visit(ScoreAddedEvent evt)
    {
        // Score events don't generate render commands
        // UI handles score display separately
    }

    /// <inheritdoc />
    public void Visit(ComboChangedEvent evt)
    {
        // Combo events don't generate render commands
        // UI handles combo display separately
    }

    /// <inheritdoc />
    public void Visit(MoveCompletedEvent evt)
    {
        // Move completed events don't generate render commands
    }

    /// <inheritdoc />
    public void Visit(ProjectileLaunchedEvent evt)
    {
        float startTime = GetStartTime(evt);

        var spawnCommand = new SpawnProjectileCommand
        {
            ProjectileId = evt.ProjectileId,
            Origin = evt.Origin,
            ArcHeight = ProjectileArcHeight,
            Type = evt.Type,
            StartTime = startTime,
            Duration = ProjectileTakeoffDuration
        };
        _commands.Add(spawnCommand);
    }

    /// <inheritdoc />
    public void Visit(ProjectileMovedEvent evt)
    {
        float startTime = GetStartTime(evt);

        // Calculate duration based on velocity
        float distance = Vector2.Distance(evt.FromPosition, evt.ToPosition);
        float velocity = evt.Velocity.Length();
        float duration = velocity > 0 ? distance / velocity : 0.016f;

        var moveCommand = new MoveProjectileCommand
        {
            ProjectileId = evt.ProjectileId,
            From = evt.FromPosition,
            To = evt.ToPosition,
            StartTime = startTime,
            Duration = duration
        };
        _commands.Add(moveCommand);
    }

    /// <inheritdoc />
    public void Visit(ProjectileRetargetedEvent evt)
    {
        // Retarget events don't generate render commands
        // Could add visual indicator if needed
    }

    /// <inheritdoc />
    public void Visit(ProjectileImpactEvent evt)
    {
        float startTime = GetStartTime(evt);
        var position = new Vector2(evt.ImpactPosition.X, evt.ImpactPosition.Y);

        var impactCommand = new ImpactProjectileCommand
        {
            ProjectileId = evt.ProjectileId,
            Position = position,
            EffectType = "projectile_explosion",
            StartTime = startTime,
            Duration = 0.3f
        };
        _commands.Add(impactCommand);

        // Remove projectile after impact
        var removeCommand = new RemoveProjectileCommand
        {
            ProjectileId = evt.ProjectileId,
            StartTime = startTime + 0.3f,
            Duration = 0
        };
        _commands.Add(removeCommand);
    }

    /// <inheritdoc />
    public void Visit(CoverDestroyedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        var destroyCommand = new DestroyCoverCommand
        {
            GridPos = evt.GridPosition,
            CoverType = evt.Type,
            StartTime = startTime,
            Duration = 0.25f
        };
        _commands.Add(destroyCommand);

        var effectCommand = new ShowEffectCommand
        {
            EffectType = "cover_destroyed",
            Position = position,
            StartTime = startTime,
            Duration = 0.25f
        };
        _commands.Add(effectCommand);
    }

    /// <inheritdoc />
    public void Visit(GroundDestroyedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        var destroyCommand = new DestroyGroundCommand
        {
            GridPos = evt.GridPosition,
            GroundType = evt.Type,
            StartTime = startTime,
            Duration = 0.25f
        };
        _commands.Add(destroyCommand);

        var effectCommand = new ShowEffectCommand
        {
            EffectType = "ground_destroyed",
            Position = position,
            StartTime = startTime,
            Duration = 0.25f
        };
        _commands.Add(effectCommand);
    }

    #endregion

    #region Cascade Timing

    /// <summary>
    /// Calculate the start time for a move animation considering cascading.
    /// Tiles should wait for destroyed tiles above them and for tiles below them to clear space.
    /// </summary>
    private float CalculateMoveStartTime(int column, int targetRow, float eventTime)
    {
        float startTime = eventTime;

        // Wait for destroy animations in this column at or above the target row
        if (_columnDestroyEndTimes.TryGetValue(column, out float destroyEndTime))
        {
            startTime = Math.Max(startTime, destroyEndTime);
        }

        // Wait for tiles below to clear 0.5 cells of space
        if (_columnMoves.TryGetValue(column, out var moves))
        {
            foreach (var move in moves)
            {
                // Check if this existing move ends at or below our target row
                if (move.TargetRow >= targetRow)
                {
                    // Calculate when this tile clears half a cell from its start
                    float totalDistance = move.To.Y - move.From.Y;
                    if (totalDistance > 0)
                    {
                        float halfCellRatio = Math.Min(0.5f / totalDistance, 1.0f);
                        float halfCellTime = move.StartTime + (move.EndTime - move.StartTime) * halfCellRatio;
                        startTime = Math.Max(startTime, halfCellTime);
                    }
                }
            }
        }

        return startTime;
    }

    private void TrackMove(int column, float startTime, float endTime, int targetRow, Vector2 from, Vector2 to)
    {
        if (!_columnMoves.TryGetValue(column, out var moves))
        {
            moves = new List<MoveRecord>();
            _columnMoves[column] = moves;
        }
        moves.Add(new MoveRecord(startTime, endTime, targetRow, from, to));
    }

    #endregion
}
