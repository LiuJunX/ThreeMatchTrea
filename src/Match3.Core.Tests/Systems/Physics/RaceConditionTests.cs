using System.Collections.Generic;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

public class RaceConditionTests
{
    private class ControlledRandom : IRandom
    {
        private readonly Queue<int> _nextValues = new Queue<int>();
        
        public void EnqueueNext(int value) => _nextValues.Enqueue(value);
        
        public float NextFloat() => 0f;
        public int Next(int max) => _nextValues.Count > 0 ? _nextValues.Dequeue() : 0;
        public int Next(int min, int max) => min + (_nextValues.Count > 0 ? _nextValues.Dequeue() : 0);
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
        public bool NextBool() => false;
        public T PickRandom<T>(IList<T> items) => items[0];
        public void Shuffle<T>(IList<T> list) { }
    }

    [Fact]
    public void VerticalAndDiagonal_ShouldNotClaimSameSlot()
    {
        // Scenario:
        // Grid 3x3
        // (1, 0) = Tile A (Vertical candidate)
        // (0, 0) = Tile B (Diagonal candidate)
        // (1, 1) = Empty (Target)
        // (0, 1) = Blocked (Force B to slide)
        // (2, 1) = Blocked (Force B to slide only right if valid)
        
        // Arrange
        var rng = new ControlledRandom();
        var state = new GameState(3, 3, 5, rng); // Fixed: Width=3, Height=3
        var gravity = new RealtimeGravitySystem(new Match3Config(), rng);
        
        // Setup Grid
        // Row 0
        state.SetTile(0, 0, new Tile(1, ElementType.Item3, 0, 0)); // B
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));  // A
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0)); // Blocker/Irrelevant
        
        // Row 1
        state.SetTile(0, 1, new Tile(4, ElementType.Item1, 0, 1)); // Block under B
        state.SetTile(1, 1, new Tile(0, ElementType.None, 1, 1)); // Empty Target
        state.SetTile(2, 1, new Tile(5, ElementType.Item1, 2, 1)); // Block right
        
        // Row 2 (Floor)
        state.SetTile(0, 2, new Tile(6, ElementType.Item1, 0, 2));
        state.SetTile(1, 2, new Tile(7, ElementType.Item1, 1, 2));
        state.SetTile(2, 2, new Tile(8, ElementType.Item1, 2, 2));
        
        // Controlling Shuffle:
        // Columns: [0, 1, 2]
        // Shuffle Loop:
        // n=3. k = Next(0, 3). Swap(k, 2).
        // n=2. k = Next(0, 2). Swap(k, 1).
        
        // We want final order: [1, 0, 2] (Process 1 first, then 0).
        // Initial: [0, 1, 2]
        
        // Step 1 (n=3): Want 2 at end? or we want 1 to be first.
        // Let's target final array: [1, 0, 2]
        
        // Reverse engineering Fisher-Yates is hard.
        // Let's just try to set RNG to produce [1, 0, 2]
        // 1. Swap(?, 2). 
        // 2. Swap(?, 1).
        
        // Actually, if we just run the test multiple times or rely on failure, it's flaky.
        // Let's try to set k=0, k=0...
        // [0, 1, 2] -> Swap(0, 2) -> [2, 1, 0]. -> Swap(0, 1) -> [1, 2, 0].
        // Result [1, 2, 0]. 1 is first. 
        // 0 is last. 
        // Order: 1, 2, 0.
        // 1 (A) processes.
        // 2 (Irrelevant).
        // 0 (B) processes.
        
        // So RNG: 0, 0.
        // Wait, RealtimeGravitySystem calls Next(0, n+1).
        // n=2 (indices 0..2). Next(0, 3).
        // n=1. Next(0, 2).
        
        rng.EnqueueNext(0); // Swap 0 and 2 -> [2, 1, 0]
        rng.EnqueueNext(0); // Swap 0 and 1 -> [1, 2, 0]
        
        // Also need RNG for Diagonal choice?
        // B at (0,0). Block at (0,1).
        // Left (x-1) invalid. Right (x+1) valid.
        // Code: if (canLeft && canRight) ... else if (canRight) ...
        // So no RNG needed for B if only Right is valid.
        
        // Act
        gravity.Update(ref state, 0.016f);
        
        // Assert
        var tileA = state.GetTile(1, 0); // Original pos of A
        var tileB = state.GetTile(0, 0); // Original pos of B
        
        // A should be falling to (1,1)
        // B should be blocked (because (1,1) is taken by A)
        
        // A state
        Assert.True(tileA.IsFalling, "Tile A should be falling vertically");
        Assert.True(tileA.Position.Y > 0.01f, "Tile A should move down");
        
        // B state
        // If B also moved, it means it claimed the slot -> RACE CONDITION
        Assert.False(tileB.IsFalling, "Tile B should NOT be falling (Slot reserved by A)");
        Assert.Equal(0f, tileB.Position.X, 0.01f); // Should stay at X=0
    }

    [Fact]
    public void DoubleUpdate_ShouldBePrevented()
    {
        // Scenario: Tile moves from Col 0 to Col 1.
        // If Col 0 is processed first, tile moves to Col 1.
        // Then Col 1 is processed. If no check, tile moves again (Double Gravity/Move).
        
        // Setup 2x5
        var rng = new ControlledRandom();
        var state = new GameState(2, 5, 3, rng);
        var gravity = new RealtimeGravitySystem(new Match3Config { GravitySpeed = 10f, InitialFallSpeed = 0f }, rng);
        
        // Setup stable column 0
        var blockFloor = new Tile(1, ElementType.Item1, 0, 4) { IsSuspended = true };
        state.SetTile(0, 4, blockFloor); // Floor Block
        var blockStack = new Tile(2, ElementType.Item1, 0, 3) { IsSuspended = true };
        state.SetTile(0, 3, blockStack); // Stacked Block
        
        // Tile A at (0, 2)
        var tileA = new Tile(3, ElementType.Item1, 0, 2);
        // Position it close to the border so it crosses into Col 1 in one frame
        tileA.Position = new System.Numerics.Vector2(0.49f, 2f); 
        state.SetTile(0, 2, tileA);
        
        // Column 1 is empty
        // Target is (1, 2) (Slide Right)
        // Below target is (1, 3) (Empty)
        
        // Ensure Col 0 processed before Col 1.
        rng.EnqueueNext(1); 
        
        // Act
        gravity.Update(ref state, 0.02f);
        
        // Assert
        // Tile should have moved to (1, 2) because X crossed 0.5 (0.49 + slide > 0.5)
        var tileAtTarget = state.GetTile(1, 2);
        
        // Should be at (1, 2)
        Assert.Equal(tileA.Id, tileAtTarget.Id);
        
        // Should NOT be at (1, 3) (which would mean it fell after sliding in same frame)
        Assert.Equal(ElementType.None, state.GetTile(1, 3).Type);
        
        // Velocity Check
        // If processed once: v = 0 + g*dt = 10*0.02 = 0.2.
        // But since we are sliding (Velocity.X > 0), gravity is reduced by SlideGravityFactor (0.6).
        // So v = 10 * 0.02 * 0.6 = 0.12.
        // If processed twice: v = 0.12 + 10 * 0.02 * 0.6 = 0.24 (or similar).
        Assert.Equal(0.12f, tileAtTarget.Velocity.Y, 0.001f);
    }
}


