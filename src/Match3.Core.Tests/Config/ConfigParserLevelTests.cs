using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Xunit;

namespace Match3.Core.Tests.Config;

/// <summary>
/// Tests for ConfigParser.ParseLevelConfig — verifying JSON deserialization
/// of LevelConfig with struct-field-based LevelObjective, enum conversion,
/// and case insensitivity.
/// </summary>
public class ConfigParserLevelTests
{
    #region Basic Parsing

    [Fact]
    public void ParseLevelConfig_MinimalJson_ReturnsDefaults()
    {
        var config = ConfigParser.ParseLevelConfig("{}");

        Assert.Equal(8, config.Width);
        Assert.Equal(8, config.Height);
        Assert.Equal(20, config.MoveLimit);
        // LevelConfig constructor creates default arrays, so Grid is not null
        Assert.NotNull(config.Grid);
    }

    [Fact]
    public void ParseLevelConfig_WithDimensions_ParsesCorrectly()
    {
        const string json = """
        {
            "width": 6,
            "height": 9,
            "moveLimit": 30,
            "targetDifficulty": 0.7
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(6, config.Width);
        Assert.Equal(9, config.Height);
        Assert.Equal(30, config.MoveLimit);
        Assert.Equal(0.7f, config.TargetDifficulty, precision: 2);
    }

    #endregion

    #region Objective Deserialization (struct fields + IncludeFields)

    [Fact]
    public void ParseLevelConfig_SingleObjective_DeserializesStructFields()
    {
        const string json = """
        {
            "objectives": [
                {
                    "targetLayer": "Tile",
                    "elementType": 1,
                    "targetCount": 10
                }
            ]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.NotNull(config.Objectives);
        Assert.Equal(ObjectiveTargetLayer.Tile, config.Objectives[0].TargetLayer);
        Assert.Equal(1, config.Objectives[0].ElementType); // ElementType.Item1
        Assert.Equal(10, config.Objectives[0].TargetCount);
    }

    [Fact]
    public void ParseLevelConfig_MultipleObjectives_AllDeserialized()
    {
        const string json = """
        {
            "objectives": [
                { "targetLayer": "Tile", "elementType": 1, "targetCount": 10 },
                { "targetLayer": "Tile", "elementType": 3, "targetCount": 10 },
                { "targetLayer": "Tile", "elementType": 4, "targetCount": 8 }
            ]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        // First 3 slots active, 4th slot should be None (fixed-size array)
        Assert.Equal(ObjectiveTargetLayer.Tile, config.Objectives[0].TargetLayer);
        Assert.Equal(1, config.Objectives[0].ElementType);
        Assert.Equal(10, config.Objectives[0].TargetCount);

        Assert.Equal(ObjectiveTargetLayer.Tile, config.Objectives[1].TargetLayer);
        Assert.Equal(3, config.Objectives[1].ElementType);
        Assert.Equal(10, config.Objectives[1].TargetCount);

        Assert.Equal(ObjectiveTargetLayer.Tile, config.Objectives[2].TargetLayer);
        Assert.Equal(4, config.Objectives[2].ElementType);
        Assert.Equal(8, config.Objectives[2].TargetCount);

        // 4th slot should be inactive (None)
        Assert.Equal(ObjectiveTargetLayer.None, config.Objectives[3].TargetLayer);
    }

    [Fact]
    public void ParseLevelConfig_CoverObjective_DeserializesCorrectly()
    {
        const string json = """
        {
            "objectives": [
                { "targetLayer": "Cover", "elementType": 1, "targetCount": 5 }
            ]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(ObjectiveTargetLayer.Cover, config.Objectives[0].TargetLayer);
        Assert.Equal(1, config.Objectives[0].ElementType);
        Assert.Equal(5, config.Objectives[0].TargetCount);
    }

    [Fact]
    public void ParseLevelConfig_GroundObjective_DeserializesCorrectly()
    {
        const string json = """
        {
            "objectives": [
                { "targetLayer": "Ground", "elementType": 2, "targetCount": 12 }
            ]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(ObjectiveTargetLayer.Ground, config.Objectives[0].TargetLayer);
        Assert.Equal(2, config.Objectives[0].ElementType);
        Assert.Equal(12, config.Objectives[0].TargetCount);
    }

    #endregion

    #region Enum String Conversion

    [Theory]
    [InlineData("\"None\"", ObjectiveTargetLayer.None)]
    [InlineData("\"Tile\"", ObjectiveTargetLayer.Tile)]
    [InlineData("\"Cover\"", ObjectiveTargetLayer.Cover)]
    [InlineData("\"Ground\"", ObjectiveTargetLayer.Ground)]
    [InlineData("\"Obstacle\"", ObjectiveTargetLayer.Obstacle)]
    public void ParseLevelConfig_EnumStrings_ConvertCorrectly(string enumStr, ObjectiveTargetLayer expected)
    {
        var json = $$"""
        {
            "objectives": [
                { "targetLayer": {{enumStr}}, "elementType": 1, "targetCount": 1 }
            ]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(expected, config.Objectives[0].TargetLayer);
    }

    #endregion

    #region Case Insensitivity

    [Fact]
    public void ParseLevelConfig_CaseInsensitiveFieldNames_ParsesCorrectly()
    {
        const string json = """
        {
            "Width": 5,
            "HEIGHT": 7,
            "moveLimit": 15,
            "Objectives": [
                { "TargetLayer": "Tile", "ElementType": 2, "TargetCount": 6 }
            ]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(5, config.Width);
        Assert.Equal(7, config.Height);
        Assert.Equal(15, config.MoveLimit);
        Assert.Equal(ObjectiveTargetLayer.Tile, config.Objectives[0].TargetLayer);
        Assert.Equal(2, config.Objectives[0].ElementType);
        Assert.Equal(6, config.Objectives[0].TargetCount);
    }

    #endregion

    #region Cells Array Deserialization

    [Fact]
    public void ParseLevelConfig_WithCellsArray_DeserializesVoidAndSlot()
    {
        const string json = """
        {
            "width": 3,
            "height": 3,
            "grid": null,
            "cells": [0, 1, 1, 1, 1, 1, 1, 1, 0]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Null(config.Grid);
        Assert.NotNull(config.Cells);
        Assert.Equal(9, config.Cells.Length);
        Assert.Equal(CellKind.Void, config.Cells[0]);
        Assert.Equal(CellKind.Slot, config.Cells[1]);
        Assert.Equal(CellKind.Void, config.Cells[8]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ParseLevelConfig_NoObjectives_DefaultsToInactive()
    {
        const string json = """{ "width": 8, "height": 8 }""";

        var config = ConfigParser.ParseLevelConfig(json);

        // All 4 slots should be inactive (default)
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(ObjectiveTargetLayer.None, config.Objectives[i].TargetLayer);
            Assert.Equal(0, config.Objectives[i].TargetCount);
        }
    }

    [Fact]
    public void ParseLevelConfig_NullGrid_ParsesAsNull()
    {
        const string json = """{ "grid": null }""";

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Null(config.Grid);
    }

    [Fact]
    public void ParseLevelConfig_CommentsAndTrailingCommas_Tolerated()
    {
        const string json = """
        {
            // This is a comment
            "width": 6,
            "objectives": [
                { "targetLayer": "Tile", "elementType": 1, "targetCount": 5, },
            ],
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(6, config.Width);
        Assert.Equal(1, config.Objectives[0].ElementType);
    }

    [Fact]
    public void ParseLevelConfig_FullLevel001Format_ParsesCorrectly()
    {
        // Mirrors the actual level_001.json format
        const string json = """
        {
            "id": "level_001",
            "name": "Tutorial 1",
            "width": 8,
            "height": 8,
            "moveLimit": 20,
            "targetDifficulty": 0.3,
            "objectives": [
                { "targetLayer": "Tile", "elementType": 1, "targetCount": 10 },
                { "targetLayer": "Tile", "elementType": 3, "targetCount": 10 },
                { "targetLayer": "Tile", "elementType": 4, "targetCount": 8 }
            ],
            "grid": null
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(8, config.Width);
        Assert.Equal(8, config.Height);
        Assert.Equal(20, config.MoveLimit);
        Assert.Equal(0.3f, config.TargetDifficulty, precision: 2);

        // Verify all 3 objectives
        Assert.Equal(ObjectiveTargetLayer.Tile, config.Objectives[0].TargetLayer);
        Assert.Equal(1, config.Objectives[0].ElementType);
        Assert.Equal(10, config.Objectives[0].TargetCount);

        Assert.Equal(ObjectiveTargetLayer.Tile, config.Objectives[1].TargetLayer);
        Assert.Equal(3, config.Objectives[1].ElementType);
        Assert.Equal(10, config.Objectives[1].TargetCount);

        Assert.Equal(ObjectiveTargetLayer.Tile, config.Objectives[2].TargetLayer);
        Assert.Equal(4, config.Objectives[2].ElementType);
        Assert.Equal(8, config.Objectives[2].TargetCount);

        // 4th slot inactive
        Assert.Equal(ObjectiveTargetLayer.None, config.Objectives[3].TargetLayer);

        Assert.Null(config.Grid);
    }

    #endregion

    #region Round-trip Serialization

    [Fact]
    public void Serialize_ThenParse_PreservesObjectives()
    {
        var original = new LevelConfig
        {
            Width = 6,
            Height = 6,
            MoveLimit = 15,
        };
        original.Objectives = new[]
        {
            new Match3.Core.Models.Gameplay.LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item1,
                TargetCount = 5
            },
            new Match3.Core.Models.Gameplay.LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Cover,
                ElementType = 1,
                TargetCount = 3
            }
        };

        var json = ConfigParser.Serialize(original);
        var parsed = ConfigParser.ParseLevelConfig(json);

        Assert.Equal(original.Width, parsed.Width);
        Assert.Equal(original.Height, parsed.Height);
        Assert.Equal(original.MoveLimit, parsed.MoveLimit);
        Assert.Equal(original.Objectives[0].TargetLayer, parsed.Objectives[0].TargetLayer);
        Assert.Equal(original.Objectives[0].ElementType, parsed.Objectives[0].ElementType);
        Assert.Equal(original.Objectives[0].TargetCount, parsed.Objectives[0].TargetCount);
        Assert.Equal(original.Objectives[1].TargetLayer, parsed.Objectives[1].TargetLayer);
        Assert.Equal(original.Objectives[1].ElementType, parsed.Objectives[1].ElementType);
        Assert.Equal(original.Objectives[1].TargetCount, parsed.Objectives[1].TargetCount);
    }

    #endregion

    #region Spawner DeepCopy

    [Fact]
    public void DeepCopy_Spawners_MutatingCopyDoesNotAffectOriginal()
    {
        var original = new LevelConfig(4, 4)
        {
            Spawners = new[]
            {
                new SpawnerConfig
                {
                    Id = 0,
                    Columns = new[] { 0, 1 },
                    Weights = new Dictionary<ElementType, int>
                    {
                        { ElementType.Item1, 80 },
                        { ElementType.Item3, 20 }
                    },
                    Preset = new PresetQueueConfig
                    {
                        Sequence = new[] { ElementType.ColorBomb, ElementType.Item2 },
                        Cycles = 2
                    }
                }
            }
        };

        var copy = original.DeepCopy();

        // Basic values preserved
        Assert.NotNull(copy.Spawners);
        Assert.Single(copy.Spawners);
        Assert.Equal(0, copy.Spawners[0].Id);
        Assert.Equal(new[] { 0, 1 }, copy.Spawners[0].Columns);
        Assert.Equal(80, copy.Spawners[0].Weights![ElementType.Item1]);
        Assert.Equal(ElementType.ColorBomb, copy.Spawners[0].Preset!.Sequence[0]);
        Assert.Equal(2, copy.Spawners[0].Preset.Cycles);

        // Mutate copy — original must not change
        copy.Spawners[0].Columns[0] = 99;
        Assert.Equal(0, original.Spawners[0].Columns[0]);

        copy.Spawners[0].Weights[ElementType.Item1] = 999;
        Assert.Equal(80, original.Spawners[0].Weights![ElementType.Item1]);

        copy.Spawners[0].Preset.Sequence[0] = ElementType.Bird;
        Assert.Equal(ElementType.ColorBomb, original.Spawners[0].Preset!.Sequence[0]);
    }

    [Fact]
    public void DeepCopy_NullSpawners_RemainsNull()
    {
        var original = new LevelConfig(4, 4);
        Assert.Null(original.Spawners);

        var copy = original.DeepCopy();
        Assert.Null(copy.Spawners);
    }

    #endregion

    #region Spawner JSON Round-Trip

    [Fact]
    public void ParseLevelConfig_WithSpawners_DeserializesCorrectly()
    {
        const string json = """
        {
            "width": 8,
            "height": 8,
            "spawners": [
                {
                    "id": 0,
                    "columns": [0, 1, 2],
                    "weights": { "Item1": 80, "Item3": 20 },
                    "preset": {
                        "sequence": ["ColorBomb", "Item2"],
                        "cycles": 2
                    }
                },
                {
                    "id": 1,
                    "columns": [6, 7]
                }
            ]
        }
        """;

        var config = ConfigParser.ParseLevelConfig(json);

        Assert.NotNull(config.Spawners);
        Assert.Equal(2, config.Spawners.Length);

        // Spawner 0
        Assert.Equal(0, config.Spawners[0].Id);
        Assert.Equal(new[] { 0, 1, 2 }, config.Spawners[0].Columns);
        Assert.Equal(80, config.Spawners[0].Weights![ElementType.Item1]);
        Assert.Equal(20, config.Spawners[0].Weights[ElementType.Item3]);
        Assert.Equal(new[] { ElementType.ColorBomb, ElementType.Item2 }, config.Spawners[0].Preset!.Sequence);
        Assert.Equal(2, config.Spawners[0].Preset.Cycles);

        // Spawner 1 — no weights or preset
        Assert.Equal(1, config.Spawners[1].Id);
        Assert.Equal(new[] { 6, 7 }, config.Spawners[1].Columns);
        Assert.Null(config.Spawners[1].Weights);
        Assert.Null(config.Spawners[1].Preset);
    }

    [Fact]
    public void Serialize_ThenParse_PreservesSpawners()
    {
        var original = new LevelConfig(6, 6)
        {
            Spawners = new[]
            {
                new SpawnerConfig
                {
                    Id = 0,
                    Columns = new[] { 0, 1 },
                    Weights = new Dictionary<ElementType, int>
                    {
                        { ElementType.Item4, 60 },
                        { ElementType.Bird, 10 }
                    },
                    Preset = new PresetQueueConfig
                    {
                        Sequence = new[] { ElementType.Item1 },
                        Cycles = 3
                    }
                }
            }
        };

        var json = ConfigParser.Serialize(original);
        var parsed = ConfigParser.ParseLevelConfig(json);

        Assert.NotNull(parsed.Spawners);
        Assert.Single(parsed.Spawners);
        Assert.Equal(0, parsed.Spawners[0].Id);
        Assert.Equal(new[] { 0, 1 }, parsed.Spawners[0].Columns);
        Assert.Equal(60, parsed.Spawners[0].Weights![ElementType.Item4]);
        Assert.Equal(10, parsed.Spawners[0].Weights[ElementType.Bird]);
        Assert.Equal(new[] { ElementType.Item1 }, parsed.Spawners[0].Preset!.Sequence);
        Assert.Equal(3, parsed.Spawners[0].Preset.Cycles);
    }

    #endregion
}
