using Match3.Core.Config;
using Match3.Core.Models.Enums;

namespace Match3.Editor.Tests
{
    public class UndoHistoryTests
    {
        private static LevelConfig MakeLevel(TileType fill = TileType.Red)
        {
            var config = new LevelConfig(3, 3);
            for (int i = 0; i < config.Grid.Length; i++)
                config.Grid[i] = fill;
            return config;
        }

        [Fact]
        public void Initial_state_cannot_undo_or_redo()
        {
            var undo = new UndoHistory();
            Assert.False(undo.CanUndo);
            Assert.False(undo.CanRedo);
        }

        [Fact]
        public void Single_record_cannot_undo()
        {
            var undo = new UndoHistory();
            undo.Record(MakeLevel(TileType.Red));
            Assert.False(undo.CanUndo);
            Assert.False(undo.CanRedo);
        }

        [Fact]
        public void Two_records_can_undo()
        {
            var undo = new UndoHistory();
            undo.Record(MakeLevel(TileType.Red));
            undo.Record(MakeLevel(TileType.Blue));

            Assert.True(undo.CanUndo);
            Assert.False(undo.CanRedo);
        }

        [Fact]
        public void Undo_restores_previous_state()
        {
            var undo = new UndoHistory();
            undo.Record(MakeLevel(TileType.Red));
            undo.Record(MakeLevel(TileType.Blue));

            var restored = undo.Undo()!;
            Assert.Equal(TileType.Red, restored.Grid[0]);
        }

        [Fact]
        public void Redo_restores_next_state()
        {
            var undo = new UndoHistory();
            undo.Record(MakeLevel(TileType.Red));
            undo.Record(MakeLevel(TileType.Blue));

            undo.Undo();
            Assert.True(undo.CanRedo);

            var restored = undo.Redo()!;
            Assert.Equal(TileType.Blue, restored.Grid[0]);
        }

        [Fact]
        public void New_record_after_undo_truncates_redo_history()
        {
            var undo = new UndoHistory();
            undo.Record(MakeLevel(TileType.Red));
            undo.Record(MakeLevel(TileType.Blue));
            undo.Record(MakeLevel(TileType.Green));

            undo.Undo(); // back to Blue
            undo.Record(MakeLevel(TileType.Yellow)); // branch from Blue

            Assert.False(undo.CanRedo); // Green is gone
            var restored = undo.Undo()!;
            Assert.Equal(TileType.Blue, restored.Grid[0]);
        }

        [Fact]
        public void Max_snapshots_drops_oldest()
        {
            var undo = new UndoHistory();

            // Record 21 snapshots (max is 20)
            for (int i = 0; i < 21; i++)
            {
                var level = MakeLevel((TileType)(i + 1)); // TileType values
                undo.Record(level);
            }

            Assert.Equal(20, undo.Count);

            // Undo all the way back — should reach snapshot index 1 (0 was dropped)
            int undoCount = 0;
            while (undo.CanUndo)
            {
                undo.Undo();
                undoCount++;
            }
            Assert.Equal(19, undoCount); // 20 snapshots → 19 undo steps
        }

        [Fact]
        public void Record_makes_deep_copy()
        {
            var undo = new UndoHistory();
            var level = MakeLevel(TileType.Red);
            undo.Record(level);

            // Mutate original
            level.Grid[0] = TileType.Blue;
            undo.Record(level);

            // Undo should give the original Red, not the mutated Blue
            var restored = undo.Undo()!;
            Assert.Equal(TileType.Red, restored.Grid[0]);
        }

        [Fact]
        public void Undo_returns_deep_copy()
        {
            var undo = new UndoHistory();
            undo.Record(MakeLevel(TileType.Red));
            undo.Record(MakeLevel(TileType.Blue));

            var restored = undo.Undo()!;
            restored.Grid[0] = TileType.Green; // mutate the returned copy

            // Redo then undo again — should still be Red, not Green
            undo.Redo();
            var restored2 = undo.Undo()!;
            Assert.Equal(TileType.Red, restored2.Grid[0]);
        }

        [Fact]
        public void Clear_resets_state()
        {
            var undo = new UndoHistory();
            undo.Record(MakeLevel(TileType.Red));
            undo.Record(MakeLevel(TileType.Blue));

            undo.Clear();
            Assert.False(undo.CanUndo);
            Assert.False(undo.CanRedo);
            Assert.Equal(0, undo.Count);
        }
    }
}
