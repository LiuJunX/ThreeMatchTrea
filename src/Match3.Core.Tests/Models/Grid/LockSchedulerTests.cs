using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Models.Grid;

public class LockSchedulerTests
{
    private static GameState CreateState(int width = 3, int height = 3)
    {
        return new GameState(width, height, 6, new StubRandom());
    }

    #region Basic Acquire/Release

    [Fact]
    public void Acquire_Manual_LocksCellWithRefCount()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var token = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);

        Assert.True(token.IsValid);
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));
    }

    [Fact]
    public void Acquire_Timed_LocksCellWithRefCount()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var token = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, duration: 1.0f);

        Assert.True(token.IsValid);
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));
    }

    [Fact]
    public void Release_DecrementsRefCount()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var token = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);
        scheduler.Release(ref state, token);

        Assert.False(state.IsLocked(1, 1, CellLockType.Drop));
        Assert.Equal(0, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));
    }

    [Fact]
    public void Release_Idempotent_SecondCallIsNoop()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        // Acquire two tokens to set ref-count to 2
        var tokenA = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);
        var tokenB = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);
        Assert.Equal(2, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));

        // Release A twice — second call should be a no-op
        scheduler.Release(ref state, tokenA);
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));

        scheduler.Release(ref state, tokenA); // idempotent
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));

        // TokenB's lock is still intact
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));
    }

    [Fact]
    public void Release_InvalidToken_IsNoop()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        // Lock a cell manually via state so there's a ref-count to observe
        state.Lock(1, 1, CellLockType.Drop);

        // Release a default token (Id=0, IsValid=false) — should be a no-op
        var defaultToken = default(LockToken);
        Assert.False(defaultToken.IsValid);

        scheduler.Release(ref state, defaultToken);

        // Ref-count should remain unchanged
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));
    }

    #endregion

    #region Ref-count Stacking

    [Fact]
    public void TwoTokens_SameCell_SameType_StackRefCount()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var tokenA = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);
        var tokenB = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);

        Assert.Equal(2, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));

        // Release one — still locked
        scheduler.Release(ref state, tokenA);
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));

        // Release the other — now unlocked
        scheduler.Release(ref state, tokenB);
        Assert.False(state.IsLocked(1, 1, CellLockType.Drop));
    }

    [Fact]
    public void TwoTokens_SameCell_DifferentTypes_IndependentRefCount()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var tokenDrop = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);
        var tokenSwap = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Swap);

        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Swap));

        // Release Drop — Swap still locked
        scheduler.Release(ref state, tokenDrop);
        Assert.False(state.IsLocked(1, 1, CellLockType.Drop));
        Assert.True(state.IsLocked(1, 1, CellLockType.Swap));
    }

    [Fact]
    public void Release_OnlyDecrementsSelf_NotOtherTokens()
    {
        // Bug case: Token A and B both lock Drop on same cell.
        // Releasing A twice should NOT affect B's count.
        var state = CreateState();
        var scheduler = new LockScheduler();

        var tokenA = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);
        var tokenB = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);
        Assert.Equal(2, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));

        // Release A once (valid)
        scheduler.Release(ref state, tokenA);
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));

        // Release A again (idempotent — should be a no-op)
        scheduler.Release(ref state, tokenA);
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));

        // B's lock is still intact — releasing B should bring count to 0
        scheduler.Release(ref state, tokenB);
        Assert.Equal(0, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));
        Assert.False(state.IsLocked(1, 1, CellLockType.Drop));
    }

    #endregion

    #region Timed Locks

    [Fact]
    public void Tick_ExpiresTimedLock_AfterDuration()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, duration: 0.5f);
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));

        // Tick past the duration
        scheduler.Tick(ref state, 0.6f);

        Assert.False(state.IsLocked(1, 1, CellLockType.Drop));
    }

    [Fact]
    public void Tick_DoesNotExpire_BeforeDuration()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, duration: 1.0f);

        // Tick less than the duration
        scheduler.Tick(ref state, 0.5f);

        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));
    }

    [Fact]
    public void Tick_MultipleTimedLocks_ExpireIndependently()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        scheduler.Acquire(ref state, new Position(0, 0), CellLockType.Drop, duration: 0.3f);
        scheduler.Acquire(ref state, new Position(2, 2), CellLockType.Swap, duration: 0.8f);

        // After 0.4s: first should expire, second should remain
        scheduler.Tick(ref state, 0.4f);

        Assert.False(state.IsLocked(0, 0, CellLockType.Drop));
        Assert.True(state.IsLocked(2, 2, CellLockType.Swap));

        // After another 0.5s (total 0.9s): second should also expire
        scheduler.Tick(ref state, 0.5f);

        Assert.False(state.IsLocked(2, 2, CellLockType.Swap));
    }

    [Fact]
    public void TimedLock_ManualReleaseEarly_ClearsBeforeTimer()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var token = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, duration: 2.0f);
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));

        // Release manually before timer expires
        scheduler.Release(ref state, token);

        Assert.False(state.IsLocked(1, 1, CellLockType.Drop));
    }

    [Fact]
    public void TimedLock_ManualReleaseEarly_TimerExpiryIsNoop()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var token = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, duration: 0.5f);

        // Release manually first
        scheduler.Release(ref state, token);
        Assert.False(state.IsLocked(1, 1, CellLockType.Drop));

        // Acquire a new lock on the same cell (simulating another system)
        var tokenB = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));

        // Tick past the original timer — should NOT double-release and affect tokenB
        scheduler.Tick(ref state, 1.0f);

        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));
    }

    #endregion

    #region Mixed Manual + Timed

    [Fact]
    public void ManualAndTimed_SameCell_StackCorrectly()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var manualToken = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);
        var timedToken = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, duration: 0.5f);

        Assert.Equal(2, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));

        // Let timer expire
        scheduler.Tick(ref state, 0.6f);

        // Still locked because manual token is held
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));
        Assert.Equal(1, CellLockOps.GetCount(state.CellLocks[state.Index(1, 1)], CellLockType.Drop));
    }

    [Fact]
    public void ManualAndTimed_TimedExpires_ManualStillHeld()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var manualToken = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Receive);
        scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Receive, duration: 0.3f);

        // Expire the timed lock
        scheduler.Tick(ref state, 0.4f);

        // Manual lock still held
        Assert.True(state.IsLocked(1, 1, CellLockType.Receive));

        // Release manual
        scheduler.Release(ref state, manualToken);
        Assert.False(state.IsLocked(1, 1, CellLockType.Receive));
    }

    #endregion

    #region Clone

    [Fact]
    public void Clone_CopiesTimedEntries()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, duration: 1.0f);

        var clonedState = state.Clone();
        var clonedScheduler = scheduler.Clone();

        // Cloned scheduler should have the timed entry — tick to expire it
        clonedScheduler.Tick(ref clonedState, 1.1f);
        Assert.False(clonedState.IsLocked(1, 1, CellLockType.Drop));
    }

    [Fact]
    public void Clone_IndependentFromOriginal()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var token = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, duration: 1.0f);

        var clonedState = state.Clone();
        var clonedScheduler = scheduler.Clone();

        // Release on clone
        clonedScheduler.Tick(ref clonedState, 1.1f);
        Assert.False(clonedState.IsLocked(1, 1, CellLockType.Drop));

        // Original should still be locked
        Assert.True(state.IsLocked(1, 1, CellLockType.Drop));
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_ReleasesAllTimedLocks()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        scheduler.Acquire(ref state, new Position(0, 0), CellLockType.Drop, duration: 5.0f);
        scheduler.Acquire(ref state, new Position(2, 2), CellLockType.Swap, duration: 10.0f);

        Assert.True(state.IsLocked(0, 0, CellLockType.Drop));
        Assert.True(state.IsLocked(2, 2, CellLockType.Swap));

        scheduler.Reset(ref state);

        Assert.False(state.IsLocked(0, 0, CellLockType.Drop));
        Assert.False(state.IsLocked(2, 2, CellLockType.Swap));
    }

    [Fact]
    public void Reset_ClearsCellLocks()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        // Mix of manual and timed locks
        scheduler.Acquire(ref state, new Position(0, 0), CellLockType.Drop);
        scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Swap, duration: 1.0f);

        scheduler.Reset(ref state);

        // All cell locks should be cleared
        for (int i = 0; i < state.CellLocks.Length; i++)
        {
            Assert.Equal(0u, state.CellLocks[i]);
        }
    }

    [Fact]
    public void Reset_AfterReset_CanAcquireNewLocks()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop, duration: 1.0f);
        scheduler.Reset(ref state);

        // Should be able to acquire new locks after reset
        var token = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Swap);
        Assert.True(token.IsValid);
        Assert.True(state.IsLocked(1, 1, CellLockType.Swap));

        // Release should work correctly
        scheduler.Release(ref state, token);
        Assert.False(state.IsLocked(1, 1, CellLockType.Swap));
    }

    #endregion

    #region LockToken.Id

    [Fact]
    public void LockToken_HasUniqueId()
    {
        var state = CreateState();
        var scheduler = new LockScheduler();

        var tokenA = scheduler.Acquire(ref state, new Position(0, 0), CellLockType.Drop);
        var tokenB = scheduler.Acquire(ref state, new Position(1, 1), CellLockType.Drop);
        var tokenC = scheduler.Acquire(ref state, new Position(0, 0), CellLockType.Swap);

        Assert.NotEqual(tokenA.Id, tokenB.Id);
        Assert.NotEqual(tokenA.Id, tokenC.Id);
        Assert.NotEqual(tokenB.Id, tokenC.Id);
    }

    [Fact]
    public void DefaultLockToken_IsNotValid()
    {
        var token = default(LockToken);

        Assert.Equal(0, token.Id);
        Assert.False(token.IsValid);
    }

    #endregion
}
