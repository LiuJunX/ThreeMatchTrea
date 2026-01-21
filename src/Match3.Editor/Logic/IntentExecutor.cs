using System;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Editor.Models;
using Match3.Editor.ViewModels;

namespace Match3.Editor.Logic
{
    /// <summary>
    /// 执行 AI 生成的关卡操作意图
    /// </summary>
    public class IntentExecutor
    {
        private readonly LevelEditorViewModel _viewModel;
        private readonly GridManipulator _gridManipulator;

        public IntentExecutor(LevelEditorViewModel viewModel, GridManipulator gridManipulator)
        {
            _viewModel = viewModel;
            _gridManipulator = gridManipulator;
        }

        public void Execute(LevelIntent intent)
        {
            var config = _viewModel.ActiveLevelConfig;

            switch (intent.Type)
            {
                case LevelIntentType.SetGridSize:
                    ExecuteSetGridSize(intent);
                    break;

                case LevelIntentType.SetMoveLimit:
                    ExecuteSetMoveLimit(intent, config);
                    break;

                case LevelIntentType.SetObjective:
                case LevelIntentType.AddObjective:
                    ExecuteSetObjective(intent);
                    break;

                case LevelIntentType.RemoveObjective:
                    ExecuteRemoveObjective(intent);
                    break;

                case LevelIntentType.PaintTile:
                    ExecutePaintTile(intent, config);
                    break;

                case LevelIntentType.PaintTileRegion:
                    ExecutePaintTileRegion(intent, config);
                    break;

                case LevelIntentType.PaintCover:
                    ExecutePaintCover(intent, config);
                    break;

                case LevelIntentType.PaintCoverRegion:
                    ExecutePaintCoverRegion(intent, config);
                    break;

                case LevelIntentType.PaintGround:
                    ExecutePaintGround(intent, config);
                    break;

                case LevelIntentType.PaintGroundRegion:
                    ExecutePaintGroundRegion(intent, config);
                    break;

                case LevelIntentType.PlaceBomb:
                    ExecutePlaceBomb(intent, config);
                    break;

                case LevelIntentType.GenerateRandomLevel:
                    _viewModel.GenerateRandomLevel();
                    break;

                case LevelIntentType.ClearRegion:
                    ExecuteClearRegion(intent, config);
                    break;

                case LevelIntentType.ClearAll:
                    ExecuteClearAll(config);
                    break;
            }

            _viewModel.IsDirty = true;
            _viewModel.NotifyGridChanged();
        }

        private void ExecuteSetGridSize(LevelIntent intent)
        {
            var width = intent.GetInt("width", _viewModel.EditorWidth);
            var height = intent.GetInt("height", _viewModel.EditorHeight);

            width = Math.Clamp(width, 3, 12);
            height = Math.Clamp(height, 3, 12);

            _viewModel.EditorWidth = width;
            _viewModel.EditorHeight = height;
            _viewModel.ResizeGrid();
        }

        private void ExecuteSetMoveLimit(LevelIntent intent, LevelConfig config)
        {
            var moves = intent.GetInt("moves", config.MoveLimit);
            moves = Math.Clamp(moves, 1, 99);
            config.MoveLimit = moves;
        }

        private void ExecuteSetObjective(LevelIntent intent)
        {
            var index = intent.GetInt("index", -1);
            var layer = intent.GetEnum("layer", ObjectiveTargetLayer.Tile);
            var aiIndex = intent.GetInt("elementType", 0);
            var count = intent.GetInt("count", 10);

            // Convert AI index to actual enum value
            var elementType = AITypeRegistry.FromAIIndex(layer, aiIndex);

            // 如果没有指定 index，添加新目标
            if (index < 0)
            {
                _viewModel.AddObjective();
                index = _viewModel.ActiveObjectiveCount - 1;
            }

            if (index >= 0 && index < 4)
            {
                _viewModel.SetObjectiveLayer(index, layer);
                _viewModel.SetObjectiveElementType(index, elementType);
                _viewModel.SetObjectiveTargetCount(index, Math.Clamp(count, 1, 999));
            }
        }

        private void ExecuteRemoveObjective(LevelIntent intent)
        {
            var index = intent.GetInt("index", -1);
            if (index >= 0 && index < 4)
            {
                _viewModel.RemoveObjective(index);
            }
        }

        private void ExecutePaintTile(LevelIntent intent, LevelConfig config)
        {
            var x = intent.GetInt("x");
            var y = intent.GetInt("y");
            var tileType = intent.GetEnum("tileType", TileType.Red);
            var bombType = intent.GetEnum("bombType", BombType.None);

            if (IsValidPosition(x, y, config))
            {
                var index = y * config.Width + x;
                _gridManipulator.PaintTile(config, index, tileType, bombType);
            }
        }

        private void ExecutePaintTileRegion(LevelIntent intent, LevelConfig config)
        {
            var x1 = intent.GetInt("x1");
            var y1 = intent.GetInt("y1");
            var x2 = intent.GetInt("x2");
            var y2 = intent.GetInt("y2");
            var tileType = intent.GetEnum("tileType", TileType.Red);
            var bombType = intent.GetEnum("bombType", BombType.None);

            NormalizeRegion(ref x1, ref y1, ref x2, ref y2);

            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    if (IsValidPosition(x, y, config))
                    {
                        var index = y * config.Width + x;
                        _gridManipulator.PaintTile(config, index, tileType, bombType);
                    }
                }
            }
        }

        private void ExecutePaintCover(LevelIntent intent, LevelConfig config)
        {
            var x = intent.GetInt("x");
            var y = intent.GetInt("y");
            var coverType = intent.GetEnum("coverType", CoverType.Cage);

            if (IsValidPosition(x, y, config))
            {
                var index = y * config.Width + x;
                _gridManipulator.PaintCover(config, index, coverType);
            }
        }

        private void ExecutePaintCoverRegion(LevelIntent intent, LevelConfig config)
        {
            var x1 = intent.GetInt("x1");
            var y1 = intent.GetInt("y1");
            var x2 = intent.GetInt("x2");
            var y2 = intent.GetInt("y2");
            var coverType = intent.GetEnum("coverType", CoverType.Cage);

            NormalizeRegion(ref x1, ref y1, ref x2, ref y2);

            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    if (IsValidPosition(x, y, config))
                    {
                        var index = y * config.Width + x;
                        _gridManipulator.PaintCover(config, index, coverType);
                    }
                }
            }
        }

        private void ExecutePaintGround(LevelIntent intent, LevelConfig config)
        {
            var x = intent.GetInt("x");
            var y = intent.GetInt("y");
            var groundType = intent.GetEnum("groundType", GroundType.Ice);

            if (IsValidPosition(x, y, config))
            {
                var index = y * config.Width + x;
                _gridManipulator.PaintGround(config, index, groundType);
            }
        }

        private void ExecutePaintGroundRegion(LevelIntent intent, LevelConfig config)
        {
            var x1 = intent.GetInt("x1");
            var y1 = intent.GetInt("y1");
            var x2 = intent.GetInt("x2");
            var y2 = intent.GetInt("y2");
            var groundType = intent.GetEnum("groundType", GroundType.Ice);

            NormalizeRegion(ref x1, ref y1, ref x2, ref y2);

            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    if (IsValidPosition(x, y, config))
                    {
                        var index = y * config.Width + x;
                        _gridManipulator.PaintGround(config, index, groundType);
                    }
                }
            }
        }

        private void ExecutePlaceBomb(LevelIntent intent, LevelConfig config)
        {
            var x = intent.GetInt("x");
            var y = intent.GetInt("y");
            var bombType = intent.GetEnum("bombType", BombType.Horizontal);
            var tileType = intent.GetEnum("tileType", TileType.Red);

            // 特殊处理 center
            if (intent.Parameters.ContainsKey("center") ||
                intent.GetString("x") == "center" ||
                intent.GetString("y") == "center")
            {
                x = config.Width / 2;
                y = config.Height / 2;
            }

            if (IsValidPosition(x, y, config))
            {
                var index = y * config.Width + x;
                _gridManipulator.PaintTile(config, index, tileType, bombType);
            }
        }

        private void ExecuteClearRegion(LevelIntent intent, LevelConfig config)
        {
            var x1 = intent.GetInt("x1");
            var y1 = intent.GetInt("y1");
            var x2 = intent.GetInt("x2", config.Width - 1);
            var y2 = intent.GetInt("y2", config.Height - 1);

            NormalizeRegion(ref x1, ref y1, ref x2, ref y2);

            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    if (IsValidPosition(x, y, config))
                    {
                        var index = y * config.Width + x;
                        _gridManipulator.PaintTile(config, index, TileType.None, BombType.None);
                        _gridManipulator.ClearCover(config, index);
                        _gridManipulator.ClearGround(config, index);
                    }
                }
            }
        }

        private void ExecuteClearAll(LevelConfig config)
        {
            for (int i = 0; i < config.Grid.Length; i++)
            {
                _gridManipulator.PaintTile(config, i, TileType.None, BombType.None);
                _gridManipulator.ClearCover(config, i);
                _gridManipulator.ClearGround(config, i);
            }
        }

        private bool IsValidPosition(int x, int y, LevelConfig config)
        {
            return x >= 0 && x < config.Width && y >= 0 && y < config.Height;
        }

        private void NormalizeRegion(ref int x1, ref int y1, ref int x2, ref int y2)
        {
            if (x1 > x2) (x1, x2) = (x2, x1);
            if (y1 > y2) (y1, y2) = (y2, y1);
        }
    }
}
