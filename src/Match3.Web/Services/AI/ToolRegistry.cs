using System.Collections.Generic;
using Match3.Editor.Models;

namespace Match3.Web.Services.AI
{
    /// <summary>
    /// 工具注册表 - 定义所有可用的 Function Calling 工具
    /// </summary>
    public static class ToolRegistry
    {
        /// <summary>
        /// 工具名到 LevelIntentType 的映射
        /// </summary>
        public static readonly Dictionary<string, LevelIntentType> ToolNameToIntentType = new()
        {
            ["set_grid_size"] = LevelIntentType.SetGridSize,
            ["set_move_limit"] = LevelIntentType.SetMoveLimit,
            ["set_objective"] = LevelIntentType.SetObjective,
            ["add_objective"] = LevelIntentType.AddObjective,
            ["remove_objective"] = LevelIntentType.RemoveObjective,
            ["paint_tile"] = LevelIntentType.PaintTile,
            ["paint_tile_region"] = LevelIntentType.PaintTileRegion,
            ["paint_cover"] = LevelIntentType.PaintCover,
            ["paint_cover_region"] = LevelIntentType.PaintCoverRegion,
            ["paint_ground"] = LevelIntentType.PaintGround,
            ["paint_ground_region"] = LevelIntentType.PaintGroundRegion,
            ["place_bomb"] = LevelIntentType.PlaceBomb,
            ["generate_random_level"] = LevelIntentType.GenerateRandomLevel,
            ["clear_region"] = LevelIntentType.ClearRegion,
            ["clear_all"] = LevelIntentType.ClearAll
        };

        /// <summary>
        /// 分析工具名称集合
        /// </summary>
        public static readonly HashSet<string> AnalysisToolNames = new()
        {
            "analyze_level",
            "deep_analyze",
            "get_bottleneck"
        };

        /// <summary>
        /// 路由工具名称（触发深度思考）
        /// </summary>
        public const string NeedDeepThinkingTool = "need_deep_thinking";

        /// <summary>
        /// 获取所有工具定义
        /// </summary>
        public static List<ToolDefinition> GetAllTools()
        {
            return new List<ToolDefinition>
            {
                // === 编辑工具 (15个) ===
                CreateSetGridSizeTool(),
                CreateSetMoveLimitTool(),
                CreateSetObjectiveTool(),
                CreateAddObjectiveTool(),
                CreateRemoveObjectiveTool(),
                CreatePaintTileTool(),
                CreatePaintTileRegionTool(),
                CreatePaintCoverTool(),
                CreatePaintCoverRegionTool(),
                CreatePaintGroundTool(),
                CreatePaintGroundRegionTool(),
                CreatePlaceBombTool(),
                CreateGenerateRandomLevelTool(),
                CreateClearRegionTool(),
                CreateClearAllTool(),

                // === 分析工具 (3个) ===
                CreateAnalyzeLevelTool(),
                CreateDeepAnalyzeTool(),
                CreateGetBottleneckTool(),

                // === 路由工具 (1个) ===
                CreateNeedDeepThinkingTool()
            };
        }

        /// <summary>
        /// 获取仅编辑工具（排除分析和路由，用于 R1 后的执行阶段）
        /// </summary>
        public static List<ToolDefinition> GetEditToolsOnly()
        {
            return new List<ToolDefinition>
            {
                // === 仅编辑工具 (15个) ===
                CreateSetGridSizeTool(),
                CreateSetMoveLimitTool(),
                CreateSetObjectiveTool(),
                CreateAddObjectiveTool(),
                CreateRemoveObjectiveTool(),
                CreatePaintTileTool(),
                CreatePaintTileRegionTool(),
                CreatePaintCoverTool(),
                CreatePaintCoverRegionTool(),
                CreatePaintGroundTool(),
                CreatePaintGroundRegionTool(),
                CreatePlaceBombTool(),
                CreateGenerateRandomLevelTool(),
                CreateClearRegionTool(),
                CreateClearAllTool()
                // 不包含分析工具，防止执行阶段调用
            };
        }

        // === 编辑工具定义 ===

        private static ToolDefinition CreateSetGridSizeTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "set_grid_size",
                Description = "设置关卡网格大小",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["width"] = new() { Type = "integer", Description = "网格宽度", Minimum = 3, Maximum = 12 },
                        ["height"] = new() { Type = "integer", Description = "网格高度", Minimum = 3, Maximum = 12 }
                    },
                    Required = new List<string> { "width", "height" }
                }
            }
        };

        private static ToolDefinition CreateSetMoveLimitTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "set_move_limit",
                Description = "设置步数限制",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["moves"] = new() { Type = "integer", Description = "步数限制", Minimum = 1, Maximum = 99 }
                    },
                    Required = new List<string> { "moves" }
                }
            }
        };

        private static ToolDefinition CreateSetObjectiveTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "set_objective",
                Description = "设置指定索引的目标（如果索引不存在则添加）",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["index"] = new() { Type = "integer", Description = "目标索引 (0-3)，-1表示添加新目标", Minimum = -1, Maximum = 3 },
                        ["layer"] = new() { Type = "string", Description = "目标层级", Enum = new List<string> { "Tile", "Cover", "Ground" } },
                        ["element_type"] = new() { Type = "integer", Description = "元素类型索引: Tile层(0=Red,1=Green,2=Blue,3=Yellow,4=Purple,5=Orange,6=Rainbow), Cover层(0=Cage,1=Chain,2=Bubble), Ground层(0=Ice)", Minimum = 0, Maximum = 6 },
                        ["count"] = new() { Type = "integer", Description = "目标数量", Minimum = 1, Maximum = 999 }
                    },
                    Required = new List<string> { "layer", "element_type", "count" }
                }
            }
        };

        private static ToolDefinition CreateAddObjectiveTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "add_objective",
                Description = "添加新目标",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["layer"] = new() { Type = "string", Description = "目标层级", Enum = new List<string> { "Tile", "Cover", "Ground" } },
                        ["element_type"] = new() { Type = "integer", Description = "元素类型索引: Tile层(0=Red,1=Green,2=Blue,3=Yellow,4=Purple,5=Orange,6=Rainbow), Cover层(0=Cage,1=Chain,2=Bubble), Ground层(0=Ice)", Minimum = 0, Maximum = 6 },
                        ["count"] = new() { Type = "integer", Description = "目标数量", Minimum = 1, Maximum = 999 }
                    },
                    Required = new List<string> { "layer", "element_type", "count" }
                }
            }
        };

        private static ToolDefinition CreateRemoveObjectiveTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "remove_objective",
                Description = "移除指定索引的目标",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["index"] = new() { Type = "integer", Description = "要移除的目标索引 (0-3)", Minimum = 0, Maximum = 3 }
                    },
                    Required = new List<string> { "index" }
                }
            }
        };

        private static ToolDefinition CreatePaintTileTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "paint_tile",
                Description = "在指定坐标绘制方块",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["x"] = new() { Type = "integer", Description = "X坐标（列）", Minimum = 0 },
                        ["y"] = new() { Type = "integer", Description = "Y坐标（行）", Minimum = 0 },
                        ["tile_type"] = new() { Type = "string", Description = "方块类型", Enum = new List<string> { "Red", "Green", "Blue", "Yellow", "Purple", "Orange", "Rainbow", "None" } },
                        ["bomb_type"] = new() { Type = "string", Description = "炸弹类型（可选）", Enum = new List<string> { "None", "Horizontal", "Vertical", "Color", "Ufo", "Square5x5" } }
                    },
                    Required = new List<string> { "x", "y", "tile_type" }
                }
            }
        };

        private static ToolDefinition CreatePaintTileRegionTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "paint_tile_region",
                Description = "在指定区域绘制方块",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["x1"] = new() { Type = "integer", Description = "起始X坐标", Minimum = 0 },
                        ["y1"] = new() { Type = "integer", Description = "起始Y坐标", Minimum = 0 },
                        ["x2"] = new() { Type = "integer", Description = "结束X坐标", Minimum = 0 },
                        ["y2"] = new() { Type = "integer", Description = "结束Y坐标", Minimum = 0 },
                        ["tile_type"] = new() { Type = "string", Description = "方块类型", Enum = new List<string> { "Red", "Green", "Blue", "Yellow", "Purple", "Orange", "Rainbow", "None" } },
                        ["bomb_type"] = new() { Type = "string", Description = "炸弹类型（可选）", Enum = new List<string> { "None", "Horizontal", "Vertical", "Color", "Ufo", "Square5x5" } }
                    },
                    Required = new List<string> { "x1", "y1", "x2", "y2", "tile_type" }
                }
            }
        };

        private static ToolDefinition CreatePaintCoverTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "paint_cover",
                Description = "在指定坐标放置覆盖物",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["x"] = new() { Type = "integer", Description = "X坐标（列）", Minimum = 0 },
                        ["y"] = new() { Type = "integer", Description = "Y坐标（行）", Minimum = 0 },
                        ["cover_type"] = new() { Type = "string", Description = "覆盖物类型", Enum = new List<string> { "None", "Cage", "Chain", "Bubble" } }
                    },
                    Required = new List<string> { "x", "y", "cover_type" }
                }
            }
        };

        private static ToolDefinition CreatePaintCoverRegionTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "paint_cover_region",
                Description = "在指定区域放置覆盖物",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["x1"] = new() { Type = "integer", Description = "起始X坐标", Minimum = 0 },
                        ["y1"] = new() { Type = "integer", Description = "起始Y坐标", Minimum = 0 },
                        ["x2"] = new() { Type = "integer", Description = "结束X坐标", Minimum = 0 },
                        ["y2"] = new() { Type = "integer", Description = "结束Y坐标", Minimum = 0 },
                        ["cover_type"] = new() { Type = "string", Description = "覆盖物类型", Enum = new List<string> { "None", "Cage", "Chain", "Bubble" } }
                    },
                    Required = new List<string> { "x1", "y1", "x2", "y2", "cover_type" }
                }
            }
        };

        private static ToolDefinition CreatePaintGroundTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "paint_ground",
                Description = "在指定坐标放置地面",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["x"] = new() { Type = "integer", Description = "X坐标（列）", Minimum = 0 },
                        ["y"] = new() { Type = "integer", Description = "Y坐标（行）", Minimum = 0 },
                        ["ground_type"] = new() { Type = "string", Description = "地面类型", Enum = new List<string> { "None", "Ice" } }
                    },
                    Required = new List<string> { "x", "y", "ground_type" }
                }
            }
        };

        private static ToolDefinition CreatePaintGroundRegionTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "paint_ground_region",
                Description = "在指定区域放置地面",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["x1"] = new() { Type = "integer", Description = "起始X坐标", Minimum = 0 },
                        ["y1"] = new() { Type = "integer", Description = "起始Y坐标", Minimum = 0 },
                        ["x2"] = new() { Type = "integer", Description = "结束X坐标", Minimum = 0 },
                        ["y2"] = new() { Type = "integer", Description = "结束Y坐标", Minimum = 0 },
                        ["ground_type"] = new() { Type = "string", Description = "地面类型", Enum = new List<string> { "None", "Ice" } }
                    },
                    Required = new List<string> { "x1", "y1", "x2", "y2", "ground_type" }
                }
            }
        };

        private static ToolDefinition CreatePlaceBombTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "place_bomb",
                Description = "在指定位置放置炸弹",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["x"] = new() { Type = "integer", Description = "X坐标（列），或使用 -1 表示中心", Minimum = -1 },
                        ["y"] = new() { Type = "integer", Description = "Y坐标（行），或使用 -1 表示中心", Minimum = -1 },
                        ["bomb_type"] = new() { Type = "string", Description = "炸弹类型", Enum = new List<string> { "Horizontal", "Vertical", "Color", "Ufo", "Square5x5" } },
                        ["tile_type"] = new() { Type = "string", Description = "携带炸弹的方块颜色（可选，默认Red）", Enum = new List<string> { "Red", "Green", "Blue", "Yellow", "Purple", "Orange", "Rainbow" } }
                    },
                    Required = new List<string> { "x", "y", "bomb_type" }
                }
            }
        };

        private static ToolDefinition CreateGenerateRandomLevelTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "generate_random_level",
                Description = "随机生成关卡布局",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>(),
                    Required = new List<string>()
                }
            }
        };

        private static ToolDefinition CreateClearRegionTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "clear_region",
                Description = "清空指定区域的所有元素",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["x1"] = new() { Type = "integer", Description = "起始X坐标", Minimum = 0 },
                        ["y1"] = new() { Type = "integer", Description = "起始Y坐标", Minimum = 0 },
                        ["x2"] = new() { Type = "integer", Description = "结束X坐标", Minimum = 0 },
                        ["y2"] = new() { Type = "integer", Description = "结束Y坐标", Minimum = 0 }
                    },
                    Required = new List<string> { "x1", "y1", "x2", "y2" }
                }
            }
        };

        private static ToolDefinition CreateClearAllTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "clear_all",
                Description = "清空整个网格的所有元素",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>(),
                    Required = new List<string>()
                }
            }
        };

        // === 分析工具定义 ===

        private static ToolDefinition CreateAnalyzeLevelTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "analyze_level",
                Description = "快速分析当前关卡，返回胜率、死锁率等基础指标",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["simulation_count"] = new() { Type = "integer", Description = "模拟次数（可选，默认500）", Minimum = 100, Maximum = 2000 }
                    },
                    Required = new List<string>()
                }
            }
        };

        private static ToolDefinition CreateDeepAnalyzeTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "deep_analyze",
                Description = "深度分析当前关卡，返回7项高级指标：心流曲线、分层胜率、瓶颈目标、技能敏感度、挫败风险、运气依赖度、P95通关次数",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["simulations_per_tier"] = new() { Type = "integer", Description = "每个玩家分层的模拟次数（可选，默认250）", Minimum = 50, Maximum = 500 }
                    },
                    Required = new List<string>()
                }
            }
        };

        private static ToolDefinition CreateGetBottleneckTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "get_bottleneck",
                Description = "分析关卡瓶颈，找出最难完成的目标和导致失败的主要原因",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>(),
                    Required = new List<string>()
                }
            }
        };

        // === 路由工具定义 ===

        private static ToolDefinition CreateNeedDeepThinkingTool() => new()
        {
            Function = new FunctionDefinition
            {
                Name = "need_deep_thinking",
                Description = "当用户的请求需要深度创意设计、复杂分析、问题诊断或开放式建议时，调用此工具触发深度思考模式。适用场景：设计有趣的关卡、分析为什么关卡太难/太简单、给出优化建议、创意性任务。不适用于简单的参数修改或直接编辑操作。",
                Parameters = new FunctionParameters
                {
                    Properties = new Dictionary<string, ParameterProperty>
                    {
                        ["reason"] = new() { Type = "string", Description = "为什么这个任务需要深度思考" },
                        ["task_summary"] = new() { Type = "string", Description = "用户任务的简要总结" }
                    },
                    Required = new List<string> { "reason", "task_summary" }
                }
            }
        };
    }
}
