using System.Collections.Generic;
using System.Text;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Editor.Models;

namespace Match3.Editor.Logic
{
    /// <summary>
    /// 从 LevelConfig 构建 LevelContext
    /// </summary>
    public static class LevelContextBuilder
    {
        public static LevelContext Build(LevelConfig config, float? winRate = null, string difficultyText = null)
        {
            return new LevelContext
            {
                Width = config.Width,
                Height = config.Height,
                MoveLimit = config.MoveLimit,
                Objectives = config.Objectives,
                GridSummary = BuildGridSummary(config),
                WinRate = winRate,
                DifficultyText = difficultyText
            };
        }

        private static string BuildGridSummary(LevelConfig config)
        {
            var tileCounts = new Dictionary<TileType, int>();
            var bombCounts = new Dictionary<BombType, int>();
            var coverCounts = new Dictionary<CoverType, int>();
            var groundCounts = new Dictionary<GroundType, int>();

            for (int i = 0; i < config.Grid.Length; i++)
            {
                var tile = config.Grid[i];
                if (tile != TileType.None)
                {
                    tileCounts.TryGetValue(tile, out var count);
                    tileCounts[tile] = count + 1;
                }

                if (config.Bombs != null && i < config.Bombs.Length)
                {
                    var bomb = config.Bombs[i];
                    if (bomb != BombType.None)
                    {
                        bombCounts.TryGetValue(bomb, out var count);
                        bombCounts[bomb] = count + 1;
                    }
                }

                if (config.Covers != null && i < config.Covers.Length)
                {
                    var cover = config.Covers[i];
                    if (cover != CoverType.None)
                    {
                        coverCounts.TryGetValue(cover, out var count);
                        coverCounts[cover] = count + 1;
                    }
                }

                if (config.Grounds != null && i < config.Grounds.Length)
                {
                    var ground = config.Grounds[i];
                    if (ground != GroundType.None)
                    {
                        groundCounts.TryGetValue(ground, out var count);
                        groundCounts[ground] = count + 1;
                    }
                }
            }

            var sb = new StringBuilder();

            if (tileCounts.Count > 0)
            {
                sb.Append("Tiles: ");
                foreach (var kv in tileCounts)
                    sb.Append($"{kv.Key}={kv.Value} ");
            }

            if (bombCounts.Count > 0)
            {
                sb.Append("Bombs: ");
                foreach (var kv in bombCounts)
                    sb.Append($"{kv.Key}={kv.Value} ");
            }

            if (coverCounts.Count > 0)
            {
                sb.Append("Covers: ");
                foreach (var kv in coverCounts)
                    sb.Append($"{kv.Key}={kv.Value} ");
            }

            if (groundCounts.Count > 0)
            {
                sb.Append("Grounds: ");
                foreach (var kv in groundCounts)
                    sb.Append($"{kv.Key}={kv.Value} ");
            }

            return sb.ToString().Trim();
        }
    }
}
