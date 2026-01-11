using System;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
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

            // Ensure bombs array exists
            if (newConfig.Bombs == null || newConfig.Bombs.Length != newConfig.Grid.Length)
            {
                newConfig.Bombs = new BombType[newConfig.Grid.Length];
            }

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
                        newConfig.Grid[newIdx] = oldConfig.Grid[oldIdx];
                        if (oldConfig.Bombs != null && oldIdx < oldConfig.Bombs.Length)
                        {
                            newConfig.Bombs[newIdx] = oldConfig.Bombs[oldIdx];
                        }
                    }
                }
            }

            return newConfig;
        }

        public void GenerateRandomLevel(LevelConfig config, int seed)
        {
            if (config.Bombs == null || config.Bombs.Length != config.Grid.Length)
            {
                config.Bombs = new BombType[config.Grid.Length];
            }

            var rng = new SeedManager(seed).GetRandom(RandomDomain.Refill);
            var types = new[] { TileType.Red, TileType.Green, TileType.Blue, TileType.Yellow, TileType.Purple, TileType.Orange };

            for (int i = 0; i < config.Grid.Length; i++)
            {
                config.Grid[i] = types[rng.Next(0, types.Length)];
            }
            
            Array.Clear(config.Bombs, 0, config.Bombs.Length);
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
                    var current = config.Grid[index];
                    if (current == TileType.None || current == TileType.Bomb || current == TileType.Rainbow)
                    {
                        var defaultColor = (selectedType >= TileType.Red && selectedType <= TileType.Orange) 
                            ? selectedType 
                            : TileType.Red;
                        config.Grid[index] = defaultColor;
                    }
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
    }
}
