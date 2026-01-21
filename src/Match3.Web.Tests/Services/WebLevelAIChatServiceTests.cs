using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Editor.Interfaces;
using Match3.Editor.Models;
using Match3.Web.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Match3.Web.Tests.Services
{
    /// <summary>
    /// Tests for WebLevelAIChatService, focusing on AI response parsing.
    /// </summary>
    public class WebLevelAIChatServiceTests
    {
        private readonly ILogger<WebLevelAIChatService> _logger = NullLogger<WebLevelAIChatService>.Instance;

        #region Response Parsing Tests

        [Fact]
        public async Task SendMessageAsync_WithValidJsonResponse_ParsesIntents()
        {
            var mockClient = new MockLLMClient(
                """
                {
                    "message": "好的，我来帮你设置网格大小。",
                    "intents": [
                        {"type": "SetGridSize", "parameters": {"width": 10, "height": 10}}
                    ]
                }
                """);

            var service = new WebLevelAIChatService(mockClient, _logger);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("把网格改成 10x10", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Equal("好的，我来帮你设置网格大小。", response.Message);
            Assert.Single(response.Intents);
            Assert.Equal(LevelIntentType.SetGridSize, response.Intents[0].Type);
            Assert.Equal(10, response.Intents[0].GetInt("width"));
            Assert.Equal(10, response.Intents[0].GetInt("height"));
        }

        [Fact]
        public async Task SendMessageAsync_WithMultipleIntents_ParsesAll()
        {
            var mockClient = new MockLLMClient(
                """
                {
                    "message": "已设置网格和步数。",
                    "intents": [
                        {"type": "SetGridSize", "parameters": {"width": 6, "height": 6}},
                        {"type": "SetMoveLimit", "parameters": {"moves": 15}}
                    ]
                }
                """);

            var service = new WebLevelAIChatService(mockClient, _logger);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("6x6，15步", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Equal(2, response.Intents.Count);
            Assert.Equal(LevelIntentType.SetGridSize, response.Intents[0].Type);
            Assert.Equal(LevelIntentType.SetMoveLimit, response.Intents[1].Type);
        }

        [Fact]
        public async Task SendMessageAsync_WithEmptyIntents_ReturnsEmptyList()
        {
            var mockClient = new MockLLMClient(
                """
                {
                    "message": "你好！有什么可以帮你的？",
                    "intents": []
                }
                """);

            var service = new WebLevelAIChatService(mockClient, _logger);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("你好", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Equal("你好！有什么可以帮你的？", response.Message);
            Assert.Empty(response.Intents);
        }

        [Fact]
        public async Task SendMessageAsync_WithPlainTextResponse_ReturnsMessageOnly()
        {
            var mockClient = new MockLLMClient("这是一个普通的文本回复，没有JSON。");

            var service = new WebLevelAIChatService(mockClient, _logger);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("说点什么", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Equal("这是一个普通的文本回复，没有JSON。", response.Message);
            Assert.Empty(response.Intents);
        }

        [Fact]
        public async Task SendMessageAsync_WithJsonInMarkdownBlock_ExtractsJson()
        {
            var mockClient = new MockLLMClient(
                """
                我来帮你设置：
                ```json
                {
                    "message": "已设置",
                    "intents": [{"type": "SetMoveLimit", "parameters": {"moves": 25}}]
                }
                ```
                """);

            var service = new WebLevelAIChatService(mockClient, _logger);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("步数25", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Single(response.Intents);
            Assert.Equal(LevelIntentType.SetMoveLimit, response.Intents[0].Type);
        }

        [Fact]
        public async Task SendMessageAsync_WithInvalidJson_ReturnsOriginalContent()
        {
            var mockClient = new MockLLMClient("{invalid json content");

            var service = new WebLevelAIChatService(mockClient, _logger);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Equal("{invalid json content", response.Message);
            Assert.Empty(response.Intents);
        }

        [Fact]
        public async Task SendMessageAsync_WithApiError_ReturnsError()
        {
            var mockClient = new MockLLMClient(success: false, error: "API rate limit exceeded");

            var service = new WebLevelAIChatService(mockClient, _logger);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.False(response.Success);
            Assert.Equal("API rate limit exceeded", response.Error);
        }

        #endregion

        #region Intent Type Parsing Tests

        [Theory]
        [InlineData("SetGridSize")]
        [InlineData("SetMoveLimit")]
        [InlineData("SetObjective")]
        [InlineData("PaintTile")]
        [InlineData("PaintTileRegion")]
        [InlineData("PaintCover")]
        [InlineData("PaintCoverRegion")]
        [InlineData("PaintGround")]
        [InlineData("PaintGroundRegion")]
        [InlineData("PlaceBomb")]
        [InlineData("GenerateRandomLevel")]
        [InlineData("ClearRegion")]
        [InlineData("ClearAll")]
        public async Task SendMessageAsync_AllIntentTypes_ParseCorrectly(string intentType)
        {
            var jsonResponse = $@"{{
                ""message"": ""Done"",
                ""intents"": [{{""type"": ""{intentType}"", ""parameters"": {{}}}}]
            }}";
            var mockClient = new MockLLMClient(jsonResponse);

            var service = new WebLevelAIChatService(mockClient, _logger);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.True(response.Success);
            Assert.Single(response.Intents);
            Assert.True(System.Enum.TryParse<LevelIntentType>(intentType, out var expected));
            Assert.Equal(expected, response.Intents[0].Type);
        }

        [Fact]
        public async Task SendMessageAsync_WithUnknownIntentType_SkipsInvalidIntent()
        {
            var mockClient = new MockLLMClient(
                """
                {
                    "message": "Done",
                    "intents": [
                        {"type": "UnknownType", "parameters": {}},
                        {"type": "SetGridSize", "parameters": {"width": 8, "height": 8}}
                    ]
                }
                """);

            var service = new WebLevelAIChatService(mockClient, _logger);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.True(response.Success);
            // Only valid intent should be parsed
            Assert.Single(response.Intents);
            Assert.Equal(LevelIntentType.SetGridSize, response.Intents[0].Type);
        }

        #endregion

        #region Parameter Parsing Tests

        [Fact]
        public async Task SendMessageAsync_WithStringEnumParameter_ParsesCorrectly()
        {
            var mockClient = new MockLLMClient(
                """
                {
                    "message": "放置红色方块",
                    "intents": [{
                        "type": "PaintTile",
                        "parameters": {"x": 3, "y": 4, "tileType": "Blue", "bombType": "Horizontal"}
                    }]
                }
                """);

            var service = new WebLevelAIChatService(mockClient, _logger);
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
            var mockClient = new MockLLMClient(
                """
                {
                    "message": "设置目标",
                    "intents": [{
                        "type": "SetObjective",
                        "parameters": {"layer": "Tile", "elementType": 0, "count": 30}
                    }]
                }
                """);

            var service = new WebLevelAIChatService(mockClient, _logger);
            var context = new LevelContext { Width = 8, Height = 8, MoveLimit = 20 };

            var response = await service.SendMessageAsync("test", context, new List<ChatMessage>());

            Assert.True(response.Success);
            var intent = response.Intents[0];
            Assert.Equal("Tile", intent.GetString("layer"));
            Assert.Equal(0, intent.GetInt("elementType"));
            Assert.Equal(30, intent.GetInt("count"));
        }

        #endregion

        #region Mock LLM Client

        private class MockLLMClient : ILLMClient
        {
            private readonly string _responseContent;
            private readonly bool _success;
            private readonly string? _error;

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

            public bool IsAvailable => true;

            public Task<LLMResponse> SendAsync(IReadOnlyList<LLMMessage> messages, CancellationToken cancellationToken = default)
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
        }

        #endregion
    }
}
