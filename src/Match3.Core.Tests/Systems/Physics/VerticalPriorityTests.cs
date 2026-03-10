using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics
{
    public class VerticalPriorityTests
    {
        private class ControlledRandom : Match3.Random.IRandom
        {
            private readonly Queue<int> _nextResults = new Queue<int>();
            
            public void EnqueueNext(int value) => _nextResults.Enqueue(value);

            public float NextFloat() => 0.5f;
            public int Next(int max) => _nextResults.Count > 0 ? _nextResults.Dequeue() : 0;
            public int Next(int min, int max) => min + (_nextResults.Count > 0 ? _nextResults.Dequeue() : 0);
            public void Shuffle<T>(IList<T> list) { }
        }

        [Fact]
        public void VerticalFall_ShouldPrevent_DiagonalSlideIntoSameSlot()
        {
            // Scenario:
            // Row 0: TileA(0,0), TileB(1,0), TileC(2,0)
            // Row 1: Block(0,1), Empty(1,1), Block(2,1)
            //
            // TileB should fall into (1,1).
            // TileA should NOT slide into (1,1).
            // TileC should NOT slide into (1,1).
            //
            // We force the processing order to favor the sliders (Col 0 first),
            // to ensure the logic actively prevents the slide even if it gets "first dibs".
            
            var config = new Match3Config { GravitySpeed = 10f };
            var rng = new ControlledRandom();
            var state = new GameState(3, 2, 5, rng);
            var gravity = new RealtimeGravitySystem(config, rng);

            // Setup
            state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
            state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0)); // The falling tile
            state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

            state.SetTile(0, 1, new Tile(4, ElementType.Item1, 0, 1)); // Block
            state.Lock(0, 1, CellLockType.Drop);
            state.SetTile(1, 1, new Tile(0, ElementType.None, 1, 1)); // Empty Target
            state.SetTile(2, 1, new Tile(5, ElementType.Item1, 2, 1)); // Block
            state.Lock(2, 1, CellLockType.Drop);

            // Force Shuffle Order: 0, 1, 2
            // Shuffle Logic: 
            // n=3. Swap(k, 2).
            // n=2. Swap(k, 1).
            // We want final: [0, 1, 2]
            // Start: [0, 1, 2]
            // k must be 2 -> Swap(2, 2) -> [0, 1, 2]
            // k must be 1 -> Swap(1, 1) -> [0, 1, 2]
            rng.EnqueueNext(2);
            rng.EnqueueNext(1);

            // Also diagonal slide choice (left vs right) requires random
            // If TileA(0,0) tries to slide, it checks (1,1).
            // If TileC(2,0) tries to slide, it checks (1,1).

            // Act
            gravity.Update(ref state, 0.02f);

            // Assert
            var tileAtTarget = state.GetTile(1, 1);
            var tileAt10 = state.GetTile(1, 0);
            var tileAt00 = state.GetTile(0, 0);

            // 1. TileB (ID 2) should be the one falling into (1,1)
            // It might not have fully arrived, but it should be moving down.
            // Or, if it hasn't moved enough to claim the cell yet, the cell should still be None.
            // BUT, TileA (ID 1) should DEFINITELY NOT be moving to x=1.
            
            Assert.Equal(0, tileAt00.Position.X); // Tile A stays at x=0
            Assert.True(tileAt00.Velocity.X == 0, "Tile A should not have horizontal velocity");

            Assert.True(tileAt10.Velocity.Y > 0 || tileAt10.Position.Y > 0, "Tile B should be falling");
            
            // If the bug exists, TileA might have claimed (1,1) or started moving towards it.
        }
    }
}


