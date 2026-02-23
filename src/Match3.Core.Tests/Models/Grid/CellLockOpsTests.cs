using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Xunit;

namespace Match3.Core.Tests.Models.Grid;

public class CellLockOpsTests
{
    #region Lock Tests

    [Fact]
    public void Lock_SingleType_SetsCount1()
    {
        uint packed = CellLockOps.Lock(0u, CellLockType.Drop);

        Assert.True(CellLockOps.IsLocked(packed, CellLockType.Drop));
        Assert.Equal(1, CellLockOps.GetCount(packed, CellLockType.Drop));
    }

    [Fact]
    public void Lock_MultipleTypes_SetsEachCount1()
    {
        uint packed = CellLockOps.Lock(0u, CellLockType.Drop | CellLockType.Receive);

        Assert.Equal(1, CellLockOps.GetCount(packed, CellLockType.Drop));
        Assert.Equal(1, CellLockOps.GetCount(packed, CellLockType.Receive));
        Assert.Equal(0, CellLockOps.GetCount(packed, CellLockType.Swap));
    }

    [Fact]
    public void Lock_SameTypeTwice_IncrementsRefCount()
    {
        uint packed = CellLockOps.Lock(0u, CellLockType.Matching);
        packed = CellLockOps.Lock(packed, CellLockType.Matching);

        Assert.Equal(2, CellLockOps.GetCount(packed, CellLockType.Matching));
    }

    [Fact]
    public void Lock_SaturatesAt15()
    {
        uint packed = 0u;
        for (int i = 0; i < 20; i++)
        {
            packed = CellLockOps.Lock(packed, CellLockType.Drop);
        }

        Assert.Equal(15, CellLockOps.GetCount(packed, CellLockType.Drop));
    }

    #endregion

    #region Unlock Tests

    [Fact]
    public void Unlock_DecrementsRefCount()
    {
        uint packed = CellLockOps.Lock(0u, CellLockType.Swap);
        packed = CellLockOps.Lock(packed, CellLockType.Swap);
        packed = CellLockOps.Unlock(packed, CellLockType.Swap);

        Assert.Equal(1, CellLockOps.GetCount(packed, CellLockType.Swap));
        Assert.True(CellLockOps.IsLocked(packed, CellLockType.Swap));
    }

    [Fact]
    public void Unlock_ToZero_NoLongerLocked()
    {
        uint packed = CellLockOps.Lock(0u, CellLockType.Drop);
        packed = CellLockOps.Unlock(packed, CellLockType.Drop);

        Assert.False(CellLockOps.IsLocked(packed, CellLockType.Drop));
        Assert.Equal(0, CellLockOps.GetCount(packed, CellLockType.Drop));
    }

    [Fact]
    public void Unlock_BelowZero_ClampsAt0()
    {
        uint packed = CellLockOps.Unlock(0u, CellLockType.Receive);

        Assert.Equal(0, CellLockOps.GetCount(packed, CellLockType.Receive));
    }

    [Fact]
    public void Unlock_MultipleTypes_DecrementsEach()
    {
        uint packed = CellLockOps.Lock(0u, CellLockType.Drop | CellLockType.Receive);
        packed = CellLockOps.Lock(packed, CellLockType.Drop); // Drop=2, Receive=1
        packed = CellLockOps.Unlock(packed, CellLockType.Drop | CellLockType.Receive);

        Assert.Equal(1, CellLockOps.GetCount(packed, CellLockType.Drop));
        Assert.Equal(0, CellLockOps.GetCount(packed, CellLockType.Receive));
    }

    #endregion

    #region IsLocked Tests

    [Fact]
    public void IsLocked_ZeroPacked_ReturnsFalse()
    {
        Assert.False(CellLockOps.IsLocked(0u, CellLockType.Drop));
        Assert.False(CellLockOps.IsLocked(0u, CellLockType.Targeting));
    }

    [Fact]
    public void IsLocked_IndependentTypes()
    {
        uint packed = CellLockOps.Lock(0u, CellLockType.Matching);

        Assert.True(CellLockOps.IsLocked(packed, CellLockType.Matching));
        Assert.False(CellLockOps.IsLocked(packed, CellLockType.Drop));
        Assert.False(CellLockOps.IsLocked(packed, CellLockType.Swap));
    }

    #endregion

    #region GetCount Tests

    [Fact]
    public void GetCount_AllTypes_Independent()
    {
        uint packed = 0u;
        packed = CellLockOps.Lock(packed, CellLockType.Drop);
        packed = CellLockOps.Lock(packed, CellLockType.Drop);
        packed = CellLockOps.Lock(packed, CellLockType.Receive);
        packed = CellLockOps.Lock(packed, CellLockType.Targeting);
        packed = CellLockOps.Lock(packed, CellLockType.Targeting);
        packed = CellLockOps.Lock(packed, CellLockType.Targeting);

        Assert.Equal(2, CellLockOps.GetCount(packed, CellLockType.Drop));
        Assert.Equal(1, CellLockOps.GetCount(packed, CellLockType.Receive));
        Assert.Equal(0, CellLockOps.GetCount(packed, CellLockType.Swap));
        Assert.Equal(0, CellLockOps.GetCount(packed, CellLockType.Matching));
        Assert.Equal(0, CellLockOps.GetCount(packed, CellLockType.Indestructible));
        Assert.Equal(3, CellLockOps.GetCount(packed, CellLockType.Targeting));
    }

    #endregion

    #region Preset Tests

    [Fact]
    public void LockPreset_Frozen_LocksFourTypes()
    {
        uint packed = CellLockOps.Lock(0u, LockPreset.Frozen);

        Assert.True(CellLockOps.IsLocked(packed, CellLockType.Drop));
        Assert.True(CellLockOps.IsLocked(packed, CellLockType.Receive));
        Assert.True(CellLockOps.IsLocked(packed, CellLockType.Swap));
        Assert.True(CellLockOps.IsLocked(packed, CellLockType.Matching));
        Assert.False(CellLockOps.IsLocked(packed, CellLockType.Indestructible));
        Assert.False(CellLockOps.IsLocked(packed, CellLockType.Targeting));
    }

    [Fact]
    public void LockPreset_Protected_LocksTwoTypes()
    {
        uint packed = CellLockOps.Lock(0u, LockPreset.Protected);

        Assert.True(CellLockOps.IsLocked(packed, CellLockType.Indestructible));
        Assert.True(CellLockOps.IsLocked(packed, CellLockType.Targeting));
        Assert.False(CellLockOps.IsLocked(packed, CellLockType.Drop));
    }

    #endregion

    #region AllLockedAboveZero Tests

    [Fact]
    public void AllLockedAboveZero_AllLocked_ReturnsTrue()
    {
        uint packed = CellLockOps.Lock(0u, CellLockType.Drop | CellLockType.Receive);

        Assert.True(CellLockOps.AllLockedAboveZero(packed, CellLockType.Drop | CellLockType.Receive));
        Assert.True(CellLockOps.AllLockedAboveZero(packed, CellLockType.Drop));
    }

    [Fact]
    public void AllLockedAboveZero_OneMissing_ReturnsFalse()
    {
        uint packed = CellLockOps.Lock(0u, CellLockType.Drop);

        Assert.False(CellLockOps.AllLockedAboveZero(packed, CellLockType.Drop | CellLockType.Receive));
    }

    [Fact]
    public void AllLockedAboveZero_ZeroPacked_ReturnsFalse()
    {
        Assert.False(CellLockOps.AllLockedAboveZero(0u, CellLockType.Swap));
    }

    #endregion
}
