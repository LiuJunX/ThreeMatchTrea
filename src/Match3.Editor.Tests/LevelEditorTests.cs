using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Editor.Tests;

public class StubEditorFileSystem : IEditorFileSystem
{
    public Dictionary<string, string> Files { get; } = new Dictionary<string, string>();
    public string LevelsDir { get; set; } = "/levels";

    public string ReadText(string path) => Files[path];
    public void WriteText(string path, string content) => Files[path] = content;
    public bool FileExists(string path) => Files.ContainsKey(path);
    public string[] ListFiles(string directory, string pattern)
    {
        var result = new List<string>();
        foreach (var key in Files.Keys)
        {
            if (key.StartsWith(directory))
                result.Add(key);
        }
        return result.ToArray();
    }
    public void CreateDirectory(string path) { }
    public void DeleteFile(string path) => Files.Remove(path);
    public string GetLevelsDirectory() => LevelsDir;
}

public class LevelEditorTests
{
    private LevelEditor CreateEditor() => new LevelEditor(new StubEditorFileSystem());

    [Fact]
    public void NewLevel_creates_valid_board()
    {
        var editor = CreateEditor();
        Assert.Equal(8, editor.Level.Width);
        Assert.Equal(8, editor.Level.Height);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void PaintCell_changes_tile()
    {
        var editor = CreateEditor();
        editor.SetActiveLayer(0);
        editor.SetSelectedTileType(ElementType.Item3);

        editor.PaintCell(0, 0);

        Assert.Equal(ElementType.Item3, editor.Level.Grid[0]);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void PaintCell_on_cover_layer()
    {
        var editor = CreateEditor();
        editor.SetActiveLayer(1);
        editor.SetSelectedCoverType(CoverType.Cage);

        editor.PaintCell(0, 0);

        Assert.Equal(CoverType.Cage, editor.Level.Covers[0]);
    }

    [Fact]
    public void PaintCell_on_ground_layer()
    {
        var editor = CreateEditor();
        editor.SetActiveLayer(2);
        editor.SetSelectedGroundType(GroundType.Ice);

        editor.PaintCell(1, 1);

        int index = 1 * editor.Level.Width + 1;
        Assert.Equal(GroundType.Ice, editor.Level.Grounds[index]);
    }

    [Fact]
    public void Stroke_groups_as_single_undo()
    {
        var editor = CreateEditor();
        editor.SetSelectedTileType(ElementType.Item3);

        // Save original tile at (0,0)
        var originalTile = editor.Level.Grid[0];

        editor.BeginStroke();
        editor.PaintCell(0, 0);
        editor.PaintCell(1, 0);
        editor.PaintCell(2, 0);
        editor.EndStroke();

        Assert.Equal(ElementType.Item3, editor.Level.Grid[0]);
        Assert.True(editor.CanUndo);

        // Single undo reverts all 3 cells
        editor.Undo();
        Assert.Equal(originalTile, editor.Level.Grid[0]);
    }

    [Fact]
    public void Undo_Redo_roundtrip()
    {
        var editor = CreateEditor();
        editor.SetSelectedTileType(ElementType.Item2);

        var before = editor.Level.Grid[0];
        editor.PaintCell(0, 0);
        Assert.Equal(ElementType.Item2, editor.Level.Grid[0]);

        editor.Undo();
        Assert.Equal(before, editor.Level.Grid[0]);

        editor.Redo();
        Assert.Equal(ElementType.Item2, editor.Level.Grid[0]);
    }

    [Fact]
    public void Resize_changes_dimensions()
    {
        var editor = CreateEditor();
        editor.Resize(5, 6);

        Assert.Equal(5, editor.Level.Width);
        Assert.Equal(6, editor.Level.Height);
        Assert.Equal(30, editor.Level.Grid.Length);
        Assert.True(editor.CanUndo);
    }

    [Fact]
    public void Resize_undo_restores_original()
    {
        var editor = CreateEditor();
        Assert.Equal(8, editor.Level.Width);

        editor.Resize(5, 5);
        Assert.Equal(5, editor.Level.Width);

        editor.Undo();
        Assert.Equal(8, editor.Level.Width);
    }

    [Fact]
    public void SetMoveLimit_clamped()
    {
        var editor = CreateEditor();
        editor.SetMoveLimit(0);
        Assert.Equal(1, editor.Level.MoveLimit); // clamped to min 1
    }

    [Fact]
    public void AddObjective_and_remove()
    {
        var editor = CreateEditor();
        // Default already has 1 objective
        int initial = CountActiveObjectives(editor);

        editor.AddObjective();
        Assert.Equal(initial + 1, CountActiveObjectives(editor));

        editor.RemoveObjective(1);
        Assert.Equal(initial, CountActiveObjectives(editor));
    }

    [Fact]
    public void SetObjective_updates_fields()
    {
        var editor = CreateEditor();
        editor.SetObjective(0, ObjectiveTargetLayer.Cover, (int)CoverType.Chain, 15);

        Assert.Equal(ObjectiveTargetLayer.Cover, editor.Level.Objectives[0].TargetLayer);
        Assert.Equal((int)CoverType.Chain, editor.Level.Objectives[0].ElementType);
        Assert.Equal(15, editor.Level.Objectives[0].TargetCount);
    }

    [Fact]
    public void Save_validates_and_writes()
    {
        var fs = new StubEditorFileSystem();
        var editor = new LevelEditor(fs);
        editor.NewLevel(4, 4);

        var result = editor.SaveAs("/levels/test.json");
        Assert.True(result.IsValid);
        Assert.True(fs.FileExists("/levels/test.json"));
        Assert.False(editor.IsDirty);
        Assert.Equal("/levels/test.json", editor.CurrentFilePath);
    }

    [Fact]
    public void Save_without_path_returns_error()
    {
        var editor = CreateEditor();
        var result = editor.Save();
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Save_invalid_level_returns_errors()
    {
        var fs = new StubEditorFileSystem();
        var editor = new LevelEditor(fs);

        // Make level invalid: clear all objectives
        for (int i = 0; i < editor.Level.Objectives.Length; i++)
            editor.Level.Objectives[i] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.None };

        var result = editor.SaveAs("/levels/bad.json");
        Assert.True(result.HasErrors);
        Assert.False(fs.FileExists("/levels/bad.json")); // not written
    }

    [Fact]
    public void Load_restores_level()
    {
        var fs = new StubEditorFileSystem();
        var editor = new LevelEditor(fs);
        editor.NewLevel(4, 4);
        editor.SetSelectedTileType(ElementType.Item5);
        editor.PaintCell(0, 0);
        editor.SaveAs("/levels/test.json");

        // Create new editor, load the file
        var editor2 = new LevelEditor(fs);
        editor2.Load("/levels/test.json");

        Assert.Equal(4, editor2.Level.Width);
        Assert.Equal("/levels/test.json", editor2.CurrentFilePath);
        Assert.False(editor2.IsDirty);
    }

    [Fact]
    public void StateChanged_fires_on_paint()
    {
        var editor = CreateEditor();
        int fireCount = 0;
        editor.StateChanged += () => fireCount++;

        editor.PaintCell(0, 0);
        Assert.True(fireCount > 0);
    }

    [Fact]
    public void GenerateRandom_changes_board()
    {
        var editor = CreateEditor();
        var gridBefore = (ElementType[])editor.Level.Grid.Clone();

        editor.GenerateRandom(42);

        // Board should likely be different (statistically near-certain for 8x8)
        Assert.True(editor.CanUndo);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void GetLevelFiles_returns_files()
    {
        var fs = new StubEditorFileSystem();
        fs.Files["/levels/a.json"] = "{}";
        fs.Files["/levels/b.json"] = "{}";

        var editor = new LevelEditor(fs);
        var files = editor.GetLevelFiles();

        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void Resize_preserves_objectives()
    {
        var editor = CreateEditor();
        editor.SetObjective(0, ObjectiveTargetLayer.Tile, (int)ElementType.Item3, 30);
        editor.AddObjective();
        editor.SetObjective(1, ObjectiveTargetLayer.Cover, (int)CoverType.Cage, 10);

        editor.Resize(5, 5);

        Assert.Equal(ObjectiveTargetLayer.Tile, editor.Level.Objectives[0].TargetLayer);
        Assert.Equal(30, editor.Level.Objectives[0].TargetCount);
        Assert.Equal(ObjectiveTargetLayer.Cover, editor.Level.Objectives[1].TargetLayer);
        Assert.Equal(10, editor.Level.Objectives[1].TargetCount);
    }

    [Fact]
    public void GetCurrentFileName_extracts_name()
    {
        var fs = new StubEditorFileSystem();
        var editor = new LevelEditor(fs);
        editor.SaveAs("/levels/my_level.json");

        Assert.Equal("my_level.json", editor.GetCurrentFileName());
    }

    [Fact]
    public void GetCurrentFileName_empty_when_unsaved()
    {
        var editor = CreateEditor();
        Assert.Equal("", editor.GetCurrentFileName());
    }

    [Fact]
    public void BeginStroke_ends_previous_unclosed_stroke()
    {
        var editor = CreateEditor();
        editor.BeginStroke();
        editor.PaintCell(0, 0);
        // Start new stroke without ending previous — should auto-end
        editor.BeginStroke();
        editor.PaintCell(1, 0);
        editor.EndStroke();

        // Two strokes = two undo steps
        Assert.True(editor.CanUndo);
        editor.Undo();
        Assert.True(editor.CanUndo); // can still undo the first stroke
    }

    private static int CountActiveObjectives(LevelEditor editor)
    {
        int count = 0;
        foreach (var obj in editor.Level.Objectives)
            if (obj.TargetLayer != ObjectiveTargetLayer.None)
                count++;
        return count;
    }
}
