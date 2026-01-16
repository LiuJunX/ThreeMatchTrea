using System;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Editor.Logic
{
    public class GridManipulator
    {
        public LevelConfig ResizeGrid(LevelConfig oldConfig, int newWidth, int newHeight)
        {
            var newConfig = new LevelConfig(newWidth, newHeight);

            // Preserve move limit
            newConfig.MoveLimit = oldConfig.MoveLimit;
            newConfig.TargetDifficulty = oldConfig.TargetDifficulty;

            int w = Math.Min(oldConfig.Width, newConfig.Width);
            int h = Math.Min(oldConfig.Height, newConfig.Height);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int oldIdx = y * oldConfig.Width + x;
                    int newIdx = y * newConfig.Width + x;

                    if (oldIdx < oldConfig.Grid.Length && newIdx < newConfig.Grid.Length)
                    {
                        // Copy Tile layer
                        newConfig.Grid[newIdx] = oldConfig.Grid[oldIdx];
                        if (oldConfig.Bombs != null && oldIdx < oldConfig.Bombs.Length)
                        {
                            newConfig.Bombs[newIdx] = oldConfig.Bombs[oldIdx];
                        }

                        // Copy Ground layer
                        if (oldConfig.Grounds != null && oldIdx < oldConfig.Grounds.Length)
                        {
                            newConfig.Grounds[newIdx] = oldConfig.Grounds[oldIdx];
                        }
                        if (oldConfig.GroundHealths != null && oldIdx < oldConfig.GroundHealths.Length)
                        {
                            newConfig.GroundHealths[newIdx] = oldConfig.GroundHealths[oldIdx];
                        }

                        // Copy Cover layer
                        if (oldConfig.Covers != null && oldIdx < oldConfig.Covers.Length)
                        {
                            newConfig.Covers[newIdx] = oldConfig.Covers[oldIdx];
                        }
                        if (oldConfig.CoverHealths != null && oldIdx < oldConfig.CoverHealths.Length)
                        {
                            newConfig.CoverHealths[newIdx] = oldConfig.CoverHealths[oldIdx];
                        }
                    }
                }
            }

            return newConfig;
        }

        public void GenerateRandomLevel(LevelConfig config, int seed)
        {
            var rng = new SeedManager(seed).GetRandom(RandomDomain.Refill);
            var types = new[] { TileType.Red, TileType.Green, TileType.Blue, TileType.Yellow, TileType.Purple, TileType.Orange };

            for (int i = 0; i < config.Grid.Length; i++)
            {
                config.Grid[i] = types[rng.Next(0, types.Length)];
            }

            // Clear bombs
            if (config.Bombs != null)
            {
                Array.Clear(config.Bombs, 0, config.Bombs.Length);
            }

            // Clear ground and cover layers
            if (config.Grounds != null)
            {
                Array.Clear(config.Grounds, 0, config.Grounds.Length);
            }
            if (config.GroundHealths != null)
            {
                Array.Clear(config.GroundHealths, 0, config.GroundHealths.Length);
            }
            if (config.Covers != null)
            {
                Array.Clear(config.Covers, 0, config.Covers.Length);
            }
            if (config.CoverHealths != null)
            {
                Array.Clear(config.CoverHealths, 0, config.CoverHealths.Length);
            }
        }

        public void PaintTile(LevelConfig config, int index, TileType selectedType, BombType selectedBomb)
        {
            if (index < 0 || index >= config.Grid.Length) return;

            if (selectedBomb != BombType.None)
            {
                config.Bombs[index] = selectedBomb;
                if (selectedBomb == BombType.Color)
                {
                    config.Grid[index] = TileType.Rainbow;
                }
                else
                {
                    // Always use selectedType for bomb placement; fallback to Red if invalid
                    var newColor = (selectedType >= TileType.Red && selectedType <= TileType.Orange)
                        ? selectedType
                        : TileType.Red;
                    config.Grid[index] = newColor;
                }
            }
            else
            {
                config.Grid[index] = selectedType;
                if (selectedType == TileType.Rainbow)
                {
                    config.Bombs[index] = BombType.Color;
                }
                else
                {
                    config.Bombs[index] = BombType.None;
                }
            }
        }

        /// <summary>
        /// Paint a ground element at the specified position.
        /// </summary>
        public void PaintGround(LevelConfig config, int index, GroundType groundType, byte health = 0)
        {
            if (index < 0 || index >= config.Grounds.Length) return;

            config.Grounds[index] = groundType;
            config.GroundHealths[index] = health > 0 ? health : GroundRules.GetDefaultHealth(groundType);
        }

        /// <summary>
        /// Paint a cover element at the specified position.
        /// </summary>
        public void PaintCover(LevelConfig config, int index, CoverType coverType, byte health = 0)
        {
            if (index < 0 || index >= config.Covers.Length) return;

            config.Covers[index] = coverType;
            config.CoverHealths[index] = health > 0 ? health : CoverRules.GetDefaultHealth(coverType);
        }

        /// <summary>
        /// Clear ground element at the specified position.
        /// </summary>
        public void ClearGround(LevelConfig config, int index)
        {
            if (index < 0 || index >= config.Grounds.Length) return;

            config.Grounds[index] = GroundType.None;
            config.GroundHealths[index] = 0;
        }

        /// <summary>
        /// Clear cover element at the specified position.
        /// </summary>
        public void ClearCover(LevelConfig config, int index)
        {
            if (index < 0 || index >= config.Covers.Length) return;

            config.Covers[index] = CoverType.None;
            config.CoverHealths[index] = 0;
        }
    }
}
