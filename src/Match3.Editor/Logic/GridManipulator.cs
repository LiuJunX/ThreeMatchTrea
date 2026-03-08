using System;
using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Editor.Logic
{
    public partial class GridManipulator
    {
        public LevelConfig ResizeGrid(LevelConfig oldConfig, int newWidth, int newHeight)
        {
            var newConfig = new LevelConfig(newWidth, newHeight);

            // Preserve scalar properties
            newConfig.MoveLimit = oldConfig.MoveLimit;
            newConfig.TargetDifficulty = oldConfig.TargetDifficulty;

            // Preserve objectives
            for (int i = 0; i < oldConfig.Objectives.Length && i < newConfig.Objectives.Length; i++)
                newConfig.Objectives[i] = oldConfig.Objectives[i];

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
                        // Copy Tile layer (Grid now contains both colors and bombs)
                        newConfig.Grid[newIdx] = oldConfig.Grid[oldIdx];

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
            var types = new[] { ElementType.Item1, ElementType.Item2, ElementType.Item3, ElementType.Item4, ElementType.Item5, ElementType.Item6 };

            for (int i = 0; i < config.Grid.Length; i++)
            {
                config.Grid[i] = types[rng.Next(0, types.Length)];
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

        public void PaintTile(LevelConfig config, int index, ElementType selectedType, ElementType selectedBomb)
        {
            if (index < 0 || index >= config.Grid.Length) return;

            if (selectedBomb.IsBomb())
            {
                // Painting a bomb: store the bomb ElementType directly in Grid
                config.Grid[index] = selectedBomb;
            }
            else
            {
                // Painting a color tile (or None)
                config.Grid[index] = selectedType;
            }
        }

        private bool IsColorTile(ElementType t)
        {
            return t.IsColor();
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
