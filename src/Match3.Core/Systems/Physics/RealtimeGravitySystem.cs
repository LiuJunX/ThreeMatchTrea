using System;
using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Physics;

public class RealtimeGravitySystem : IPhysicsSimulation
{
    private readonly Match3Config _config;
    private const float FloorThreshold = 0.05f; // Snap distance

    public RealtimeGravitySystem(Match3Config config)
    {
        _config = config;
    }

    private RealtimeGravitySystem()
    {
        throw new NotSupportedException("无参构造函数仅用于反射，禁止直接调用。请使用带 Match3Config 参数的构造函数。");
    }

    public void Update(ref GameState state, float deltaTime)
    {
        for (int x = 0; x < state.Width; x++)
        {
            ProcessColumn(ref state, x, deltaTime);
        }
    }

    public bool IsStable(in GameState state)
    {
        for (int x = 0; x < state.Width; x++)
        {
            for (int y = 0; y < state.Height; y++)
            {
                var tile = state.GetTile(x, y);
                if (tile.Type != TileType.None)
                {
                    // Check if moving significantly or not aligned
                    if (Math.Abs(tile.Velocity.Y) > 0.01f || Math.Abs(tile.Position.Y - y) > 0.01f)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private void ProcessColumn(ref GameState state, int x, float dt)
    {
        // Iterate from bottom to top so we can move tiles down into empty slots
        for (int y = state.Height - 1; y >= 0; y--)
        {
            var tile = state.GetTile(x, y);
            if (tile.Type == TileType.None || tile.IsSuspended) continue;

            // 1. Calculate the floor (stop target)
            int floorY = y;
            float targetY = y;
            bool foundTarget = false;
            float targetVelocityY = 0f;

            for (int checkY = y + 1; checkY < state.Height; checkY++)
            {
                var below = state.GetTile(x, checkY);
                if (below.Type == TileType.None && !below.IsSuspended)
                {
                    floorY = checkY;
                }
                else
                {
                    // Hit an obstacle
                    if (below.IsFalling)
                    {
                        // Follow the falling tile (maintain 1 unit distance)
                        targetY = below.Position.Y - 1.0f;
                        targetVelocityY = below.Velocity.Y;
                        foundTarget = true;
                    }
                    break;
                }
            }

            if (!foundTarget)
            {
                targetY = (float)floorY;
            }

            // 2. Physics Update
            if (tile.Position.Y < targetY - FloorThreshold)
            {
                // Falling
                tile.IsFalling = true;
                tile.Velocity.Y += _config.GravitySpeed * dt;
                if (tile.Velocity.Y > _config.MaxFallSpeed) tile.Velocity.Y = _config.MaxFallSpeed;
                
                tile.Position.Y += tile.Velocity.Y * dt;

                // Collision / Floor Check
                if (tile.Position.Y >= targetY)
                {
                    tile.Position.Y = targetY;
                    
                    if (foundTarget)
                    {
                        // Match velocity of the obstacle we hit and keep falling state
                        tile.Velocity.Y = targetVelocityY;
                        tile.IsFalling = true;
                    }
                    else
                    {
                        // Hit static floor
                        tile.Velocity.Y = 0;
                        tile.IsFalling = false;
                    }
                }
            }
            else
            {
                // Stable or near floor
                if (tile.IsFalling || Math.Abs(tile.Position.Y - targetY) > float.Epsilon)
                {
                    // Snap to target position
                    tile.Position.Y = targetY;
                    
                    if (foundTarget)
                    {
                        tile.Velocity.Y = targetVelocityY;
                        tile.IsFalling = true;
                    }
                    else
                    {
                        tile.Velocity.Y = 0;
                        tile.IsFalling = false;
                    }
                }
            }

            // 3. Grid Logic Update (Spatial Partitioning)
            // If the tile has visually moved past the midpoint of the current cell,
            // move it logically to the next cell.
            // Current logical row is 'y'.
            // If Position.Y > y + 1 (meaning it fully entered the next cell? No, usually > y + 0.5)
            // Let's be conservative: Only swap if target is truly empty.
            
            int visualRow = (int)Math.Floor(tile.Position.Y + 0.5f); // Round to nearest
            
            if (visualRow > y && visualRow < state.Height)
            {
                // Check if we can move ownership
                var targetSlot = state.GetTile(x, visualRow);
                if (targetSlot.Type == TileType.None)
                {
                    // Move Tile Logic
                    state.SetTile(x, visualRow, tile);
                    state.SetTile(x, y, new Tile(0, TileType.None, x, y));
                    // Note: We don't need to update 'tile' variable because we won't use it again in this loop
                    // (we are iterating backwards).
                    continue; // Done with this tile
                }
            }

            // Save state back if we didn't move logical slots
            state.SetTile(x, y, tile);
        }
    }
}
