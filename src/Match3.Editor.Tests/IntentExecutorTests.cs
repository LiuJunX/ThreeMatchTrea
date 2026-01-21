using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Editor.Models;

namespace Match3.Editor.Tests
{
    /// <summary>
    /// Tests for LevelIntent model and its helper methods.
    /// These tests verify the parameter extraction logic used by IntentExecutor.
    /// </summary>
    public class IntentExecutorTests
    {
        #region LevelIntent.GetInt Tests

        [Fact]
        public void GetInt_WithIntValue_ReturnsValue()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.SetGridSize,
                Parameters = new Dictionary<string, object> { { "width", 10 } }
            };

            Assert.Equal(10, intent.GetInt("width"));
        }

        [Fact]
        public void GetInt_WithLongValue_ReturnsIntValue()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.SetMoveLimit,
                Parameters = new Dictionary<string, object> { { "moves", 25L } }
            };

            Assert.Equal(25, intent.GetInt("moves"));
        }

        [Fact]
        public void GetInt_WithDoubleValue_ReturnsIntValue()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.SetGridSize,
                Parameters = new Dictionary<string, object> { { "height", 8.0 } }
            };

            Assert.Equal(8, intent.GetInt("height"));
        }

        [Fact]
        public void GetInt_WithStringValue_ParsesAndReturns()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.SetObjective,
                Parameters = new Dictionary<string, object> { { "count", "30" } }
            };

            Assert.Equal(30, intent.GetInt("count"));
        }

        [Fact]
        public void GetInt_WithMissingKey_ReturnsDefault()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.SetGridSize,
                Parameters = new Dictionary<string, object>()
            };

            Assert.Equal(8, intent.GetInt("width", 8));
        }

        [Fact]
        public void GetInt_WithInvalidString_ReturnsDefault()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.PlaceBomb,
                Parameters = new Dictionary<string, object> { { "x", "center" } }
            };

            Assert.Equal(0, intent.GetInt("x", 0));
        }

        #endregion

        #region LevelIntent.GetString Tests

        [Fact]
        public void GetString_WithStringValue_ReturnsValue()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.PaintTile,
                Parameters = new Dictionary<string, object> { { "tileType", "Red" } }
            };

            Assert.Equal("Red", intent.GetString("tileType"));
        }

        [Fact]
        public void GetString_WithIntValue_ReturnsStringRepresentation()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.SetObjective,
                Parameters = new Dictionary<string, object> { { "elementType", 0 } }
            };

            Assert.Equal("0", intent.GetString("elementType"));
        }

        [Fact]
        public void GetString_WithMissingKey_ReturnsDefault()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.PaintTile,
                Parameters = new Dictionary<string, object>()
            };

            Assert.Equal("default", intent.GetString("missing", "default"));
        }

        #endregion

        #region LevelIntent.GetEnum Tests

        [Fact]
        public void GetEnum_WithValidString_ReturnsEnumValue()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.PaintTile,
                Parameters = new Dictionary<string, object> { { "tileType", "Blue" } }
            };

            Assert.Equal(TileType.Blue, intent.GetEnum<TileType>("tileType"));
        }

        [Fact]
        public void GetEnum_WithCaseInsensitiveString_ReturnsEnumValue()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.PlaceBomb,
                Parameters = new Dictionary<string, object> { { "bombType", "horizontal" } }
            };

            Assert.Equal(BombType.Horizontal, intent.GetEnum<BombType>("bombType"));
        }

        [Fact]
        public void GetEnum_WithInvalidString_ReturnsDefault()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.PaintCover,
                Parameters = new Dictionary<string, object> { { "coverType", "Invalid" } }
            };

            Assert.Equal(CoverType.None, intent.GetEnum("coverType", CoverType.None));
        }

        [Fact]
        public void GetEnum_WithMissingKey_ReturnsDefault()
        {
            var intent = new LevelIntent
            {
                Type = LevelIntentType.PaintGround,
                Parameters = new Dictionary<string, object>()
            };

            Assert.Equal(GroundType.Ice, intent.GetEnum("groundType", GroundType.Ice));
        }

        [Fact]
        public void GetEnum_AllTileTypes_ParseCorrectly()
        {
            var tileTypes = new[] { "Red", "Green", "Blue", "Yellow", "Purple", "Orange", "Rainbow", "None" };
            var expected = new[] { TileType.Red, TileType.Green, TileType.Blue, TileType.Yellow,
                                   TileType.Purple, TileType.Orange, TileType.Rainbow, TileType.None };

            for (int i = 0; i < tileTypes.Length; i++)
            {
                var intent = new LevelIntent
                {
                    Type = LevelIntentType.PaintTile,
                    Parameters = new Dictionary<string, object> { { "type", tileTypes[i] } }
                };
                Assert.Equal(expected[i], intent.GetEnum<TileType>("type"));
            }
        }

        [Fact]
        public void GetEnum_AllBombTypes_ParseCorrectly()
        {
            var bombTypes = new[] { "None", "Horizontal", "Vertical", "Color", "Ufo", "Square5x5" };
            var expected = new[] { BombType.None, BombType.Horizontal, BombType.Vertical,
                                   BombType.Color, BombType.Ufo, BombType.Square5x5 };

            for (int i = 0; i < bombTypes.Length; i++)
            {
                var intent = new LevelIntent
                {
                    Type = LevelIntentType.PlaceBomb,
                    Parameters = new Dictionary<string, object> { { "type", bombTypes[i] } }
                };
                Assert.Equal(expected[i], intent.GetEnum<BombType>("type"));
            }
        }

        #endregion

        #region LevelIntentType Tests

        [Fact]
        public void LevelIntentType_ContainsAllExpectedTypes()
        {
            var types = Enum.GetValues(typeof(LevelIntentType));

            Assert.Contains(LevelIntentType.SetGridSize, (LevelIntentType[])types);
            Assert.Contains(LevelIntentType.SetMoveLimit, (LevelIntentType[])types);
            Assert.Contains(LevelIntentType.SetObjective, (LevelIntentType[])types);
            Assert.Contains(LevelIntentType.PaintTile, (LevelIntentType[])types);
            Assert.Contains(LevelIntentType.PaintTileRegion, (LevelIntentType[])types);
            Assert.Contains(LevelIntentType.PlaceBomb, (LevelIntentType[])types);
            Assert.Contains(LevelIntentType.GenerateRandomLevel, (LevelIntentType[])types);
            Assert.Contains(LevelIntentType.ClearAll, (LevelIntentType[])types);
        }

        #endregion
    }
}
