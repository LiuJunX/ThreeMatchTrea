using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Models.Grid;

public class LockTokenTests
{
    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    [Fact]
    public void AcquireLock_ReturnsTokenWithCorrectFields()
    {
        var state = new GameState(3, 3, 6, new StubRandom());

        var token = state.AcquireLock(1, 2, CellLockType.Drop | CellLockType.Matching);

        Assert.Equal(2 * 3 + 1, token.CellIndex); // y * Width + x
        Assert.Equal(CellLockType.Drop | CellLockType.Matching, token.Types);
    }

    [Fact]
    public void AcquireLock_LocksTheCell()
    {
        var state = new GameState(3, 3, 6, new StubRandom());

        state.AcquireLock(1, 1, CellLockType.Swap);

        Assert.True(state.IsLocked(1, 1, CellLockType.Swap));
    }

    [Fact]
    public void ReleaseLock_UnlocksTheCell()
    {
        var state = new GameState(3, 3, 6, new StubRandom());
        var token = state.AcquireLock(0, 0, CellLockType.Receive);

        state.ReleaseLock(token);

        Assert.False(state.IsLocked(0, 0, CellLockType.Receive));
    }

    [Fact]
    public void MultipleTokens_IndependentLifecycles()
    {
        var state = new GameState(3, 3, 6, new StubRandom());

        var tokenA = state.AcquireLock(1, 1, CellLockType.Drop);
        var tokenB = state.AcquireLock(1, 1, CellLockType.Drop);

        // Both acquired: ref-count = 2
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));

        state.ReleaseLock(tokenA);
        // One released: ref-count = 1, still locked
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));

        state.ReleaseLock(tokenB);
        // Both released: ref-count = 0
        Assert.False(state.IsLocked(1, 1, CellLockType.Drop));
    }

    [Fact]
    public void MultipleTokens_DifferentCells()
    {
        var state = new GameState(3, 3, 6, new StubRandom());

        var tokenA = state.AcquireLock(0, 0, CellLockType.Matching);
        var tokenB = state.AcquireLock(2, 2, CellLockType.Matching);

        Assert.True(state.IsLocked(0, 0, CellLockType.Matching));
        Assert.True(state.IsLocked(2, 2, CellLockType.Matching));

        state.ReleaseLock(tokenA);
        Assert.False(state.IsLocked(0, 0, CellLockType.Matching));
        Assert.True(state.IsLocked(2, 2, CellLockType.Matching));
    }

    [Fact]
    public void DoubleRelease_ClampsAtZero_DoesNotCorruptOtherLocks()
    {
        var state = new GameState(3, 3, 6, new StubRandom());

        // Source A and B both lock Swap
        var tokenA = state.AcquireLock(1, 1, CellLockType.Swap);
        var tokenB = state.AcquireLock(1, 1, CellLockType.Swap);

        state.ReleaseLock(tokenA); // count=1
        state.ReleaseLock(tokenB); // count=0

        // Simulating double-release of tokenA: Unlock clamps at 0, does not go negative
        // (Debug.Assert would fire in Debug builds, but the clamp is still safe)
        state.Unlock(1, 1, CellLockType.Swap);

        // Verify count stays at 0 and doesn't wrap around
        Assert.False(state.IsLocked(1, 1, CellLockType.Swap));
        Assert.Equal(0, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Swap));
    }
}
