using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Editor.Interfaces;
using Match3.Editor.Models;
using Match3.Web.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Match3.Web.Tests.Services
{
    /// <summary>
    /// Tests for WebLevelAIChatService with Function Calling support.
    /// </summary>
    public class WebLevelAIChatServiceTests
    {
        private readonly ILogger<WebLevelAIChatService> _logger = NullLogger<WebLevelAIChatService>.Instance;
        private readonly IOptions<LLMOptions> _options = Options.Create(new LLMOptions());

        private WebLevelAIChatService CreateService(ILLMClient mockClient)
        {
            return new WebLevelAIChatService(mockClient, _options, _logger);
        }

        #region Tool Call Response Tests

        [Fact]
        public async Task SendMessageAsync_WithSetGridSizeToolCall_ParsesIntent()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "set_grid_size",
                        Arguments = """{"width": 10, "height": 10}"""
                    }
                }
            };
            var mockClient = new MockLLMClient(toolCalls, "好的，我来帮你设置网格大小。");

            var service = CreateService(mockClient);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("把网格改成 10x10", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Single(response.Intents);
            Assert.Equal(LevelIntentType.SetGridSize, response.Intents[0].Type);
            Assert.Equal(10, response.Intents[0].GetInt("width"));
            Assert.Equal(10, response.Intents[0].GetInt("height"));
        }

        [Fact]
        public async Task SendMessageAsync_WithMultipleToolCalls_ParsesAll()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "set_grid_size",
                        Arguments = """{"width": 6, "height": 6}"""
                    }
                },
                new ToolCall
                {
                    Id = "call_2",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "set_move_limit",
                        Arguments = """{"moves": 15}"""
                    }
                }
            };
            var mockClient = new MockLLMClient(toolCalls, "已设置网格和步数。");

            var service = CreateService(mockClient);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("6x6，15步", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Equal(2, response.Intents.Count);
            Assert.Equal(LevelIntentType.SetGridSize, response.Intents[0].Type);
            Assert.Equal(LevelIntentType.SetMoveLimit, response.Intents[1].Type);
        }

        [Fact]
        public async Task SendMessageAsync_WithNoToolCalls_ReturnsMessageOnly()
        {
            var mockClient = new MockLLMClient(null, "你好！有什么可以帮你的？");

            var service = CreateService(mockClient);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("你好", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Equal("你好！有什么可以帮你的？", response.Message);
            Assert.Empty(response.Intents);
        }

        [Fact]
        public async Task SendMessageAsync_WithApiError_ReturnsError()
        {
            var mockClient = new MockLLMClient(success: false, error: "API rate limit exceeded");

            var service = CreateService(mockClient);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.False(response.Success);
            Assert.Equal("API rate limit exceeded", response.Error);
        }

        #endregion

        #region Tool Type Tests

        [Theory]
        [InlineData("set_grid_size", LevelIntentType.SetGridSize)]
        [InlineData("set_move_limit", LevelIntentType.SetMoveLimit)]
        [InlineData("set_objective", LevelIntentType.SetObjective)]
        [InlineData("add_objective", LevelIntentType.AddObjective)]
        [InlineData("remove_objective", LevelIntentType.RemoveObjective)]
        [InlineData("paint_tile", LevelIntentType.PaintTile)]
        [InlineData("paint_tile_region", LevelIntentType.PaintTileRegion)]
        [InlineData("paint_cover", LevelIntentType.PaintCover)]
        [InlineData("paint_cover_region", LevelIntentType.PaintCoverRegion)]
        [InlineData("paint_ground", LevelIntentType.PaintGround)]
        [InlineData("paint_ground_region", LevelIntentType.PaintGroundRegion)]
        [InlineData("place_bomb", LevelIntentType.PlaceBomb)]
        [InlineData("generate_random_level", LevelIntentType.GenerateRandomLevel)]
        [InlineData("clear_region", LevelIntentType.ClearRegion)]
        [InlineData("clear_all", LevelIntentType.ClearAll)]
        public async Task SendMessageAsync_AllToolTypes_MapToCorrectIntents(string toolName, LevelIntentType expectedType)
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = toolName,
                        Arguments = "{}"
                    }
                }
            };
            var mockClient = new MockLLMClient(toolCalls, "Done");

            var service = CreateService(mockClient);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Single(response.Intents);
            Assert.Equal(expectedType, response.Intents[0].Type);
        }

        [Fact]
        public async Task SendMessageAsync_WithUnknownTool_SkipsInvalidTool()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "unknown_tool",
                        Arguments = "{}"
                    }
                },
                new ToolCall
                {
                    Id = "call_2",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "set_grid_size",
                        Arguments = """{"width": 8, "height": 8}"""
                    }
                }
            };
            var mockClient = new MockLLMClient(toolCalls, "Done");

            var service = CreateService(mockClient);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.True(response.Success);
            // Only valid tool should create intent
            Assert.Single(response.Intents);
            Assert.Equal(LevelIntentType.SetGridSize, response.Intents[0].Type);
        }

        #endregion

        #region Parameter Conversion Tests

        [Fact]
        public async Task SendMessageAsync_ConvertsSnakeCaseToCamelCase()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "paint_tile",
                        Arguments = """{"x": 3, "y": 4, "tile_type": "Blue", "bomb_type": "Horizontal"}"""
                    }
                }
            };
            var mockClient = new MockLLMClient(toolCalls, "放置蓝色方块");

            var service = CreateService(mockClient);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.True(response.Success);
            var intent = response.Intents[0];
            Assert.Equal(3, intent.GetInt("x"));
            Assert.Equal(4, intent.GetInt("y"));
            Assert.Equal("Blue", intent.GetString("tileType"));
            Assert.Equal("Horizontal", intent.GetString("bombType"));
        }

        [Fact]
        public async Task SendMessageAsync_WithObjectiveParameters_ParsesCorrectly()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "set_objective",
                        Arguments = """{"layer": "Tile", "element_type": 0, "count": 30}"""
                    }
                }
            };
            var mockClient = new MockLLMClient(toolCalls, "设置目标");

            var service = CreateService(mockClient);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.True(response.Success);
            var intent = response.Intents[0];
            Assert.Equal("Tile", intent.GetString("layer"));
            Assert.Equal(0, intent.GetInt("elementType")); // 0 = Red
            Assert.Equal(30, intent.GetInt("count"));
        }

        [Fact]
        public async Task SendMessageAsync_WithRegionParameters_ParsesCorrectly()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "paint_tile_region",
                        Arguments = """{"x1": 0, "y1": 0, "x2": 3, "y2": 3, "tile_type": "None"}"""
                    }
                }
            };
            var mockClient = new MockLLMClient(toolCalls, "清空左上角区域");

            var service = CreateService(mockClient);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.True(response.Success);
            var intent = response.Intents[0];
            Assert.Equal(0, intent.GetInt("x1"));
            Assert.Equal(0, intent.GetInt("y1"));
            Assert.Equal(3, intent.GetInt("x2"));
            Assert.Equal(3, intent.GetInt("y2"));
            Assert.Equal("None", intent.GetString("tileType"));
        }

        [Fact]
        public async Task SendMessageAsync_WithPlaceBombCenter_AddsCenterParameter()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "place_bomb",
                        Arguments = """{"x": -1, "y": -1, "bomb_type": "Color"}"""
                    }
                }
            };
            var mockClient = new MockLLMClient(toolCalls, "在中心放置彩虹炸弹");

            var service = CreateService(mockClient);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.True(response.Success);
            var intent = response.Intents[0];
            Assert.Equal(LevelIntentType.PlaceBomb, intent.Type);
            Assert.True(intent.Parameters.ContainsKey("center"));
            Assert.Equal(true, intent.Parameters["center"]);
        }

        #endregion

        #region ToolRegistry Tests

        [Fact]
        public void ToolRegistry_GetAllTools_Returns19Tools()
        {
            var tools = ToolRegistry.GetAllTools();

            // 15 edit tools + 3 analysis tools + 1 routing tool = 19 total
            Assert.Equal(19, tools.Count);
        }

        [Fact]
        public void ToolRegistry_AllEditToolsHaveIntentMapping()
        {
            var tools = ToolRegistry.GetAllTools();

            foreach (var tool in tools)
            {
                var name = tool.Function.Name;
                // Skip analysis tools and routing tools
                if (ToolRegistry.AnalysisToolNames.Contains(name))
                    continue;
                if (name == ToolRegistry.NeedDeepThinkingTool)
                    continue;

                Assert.True(ToolRegistry.ToolNameToIntentType.ContainsKey(name),
                    $"Tool '{name}' should have an intent type mapping");
            }
        }

        [Fact]
        public void ToolRegistry_AnalysisToolsAreIdentified()
        {
            Assert.Contains("analyze_level", ToolRegistry.AnalysisToolNames);
            Assert.Contains("deep_analyze", ToolRegistry.AnalysisToolNames);
            Assert.Contains("get_bottleneck", ToolRegistry.AnalysisToolNames);
            Assert.Equal(3, ToolRegistry.AnalysisToolNames.Count);
        }

        [Fact]
        public void ToolRegistry_GetEditToolsOnly_ExcludesAnalysisAndRoutingTools()
        {
            var editTools = ToolRegistry.GetEditToolsOnly();

            // Should have 15 edit tools only
            Assert.Equal(15, editTools.Count);

            // Should not contain any analysis tools
            foreach (var tool in editTools)
            {
                var name = tool.Function.Name;
                Assert.DoesNotContain(name, ToolRegistry.AnalysisToolNames);
                Assert.NotEqual(ToolRegistry.NeedDeepThinkingTool, name);
            }

            // Should contain all edit tools
            foreach (var toolName in ToolRegistry.ToolNameToIntentType.Keys)
            {
                Assert.Contains(editTools, t => t.Function.Name == toolName);
            }
        }

        #endregion

        #region Mock LLM Client

        private class MockLLMClient : ILLMClient
        {
            private readonly string _responseContent;
            private readonly bool _success;
            private readonly string? _error;
            private readonly List<ToolCall>? _toolCalls;

            public MockLLMClient(string responseContent)
            {
                _responseContent = responseContent;
                _success = true;
            }

            public MockLLMClient(bool success, string? error)
            {
                _responseContent = "";
                _success = success;
                _error = error;
            }

            public MockLLMClient(List<ToolCall>? toolCalls, string? finalMessage = null)
            {
                _toolCalls = toolCalls;
                _responseContent = finalMessage ?? "";
                _success = true;
            }

            public bool IsAvailable => true;

            public Task<LLMResponse> SendAsync(IReadOnlyList<LLMMessage> messages, CancellationToken cancellationToken = default)
            {
                return SendAsync(messages, "default", 2048, cancellationToken);
            }

            public Task<LLMResponse> SendAsync(IReadOnlyList<LLMMessage> messages, string model, CancellationToken cancellationToken = default)
            {
                return SendAsync(messages, model, 2048, cancellationToken);
            }

            public Task<LLMResponse> SendAsync(IReadOnlyList<LLMMessage> messages, string model, int maxTokens, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new LLMResponse
                {
                    Success = _success,
                    Content = _responseContent,
                    Error = _error
                });
            }

            public async IAsyncEnumerable<string> SendStreamAsync(
                IReadOnlyList<LLMMessage> messages,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                yield return _responseContent;
                await Task.CompletedTask;
            }

            public Task<LLMResponse> SendWithToolsAsync(
                IReadOnlyList<LLMMessage> messages,
                IReadOnlyList<ToolDefinition> tools,
                CancellationToken cancellationToken = default)
            {
                // Check if this is a follow-up call after tool results
                bool hasToolResults = false;
                foreach (var msg in messages)
                {
                    if (msg.Role == "tool")
                    {
                        hasToolResults = true;
                        break;
                    }
                }

                if (hasToolResults || _toolCalls == null)
                {
                    // Return final response without tool calls
                    return Task.FromResult(new LLMResponse
                    {
                        Success = _success,
                        Content = _responseContent,
                        Error = _error,
                        ToolCalls = null,
                        FinishReason = "stop"
                    });
                }

                // First call - return tool calls
                return Task.FromResult(new LLMResponse
                {
                    Success = _success,
                    Content = null,
                    Error = _error,
                    ToolCalls = _toolCalls,
                    FinishReason = "tool_calls"
                });
            }
        }

        #endregion
    }
}
