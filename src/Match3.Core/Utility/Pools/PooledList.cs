using System;
using System.Collections.Generic;

namespace Match3.Core.Utility.Pools;

/// <summary>
/// A wrapper struct for a pooled List&lt;T&gt; that returns the list to the pool when disposed.
/// Use with the 'using' statement.
/// </summary>
public readonly struct PooledList<T> : IDisposable
{
    public readonly List<T> List;

    public PooledList(List<T> list)
    {
        List = list;
    }

    public void Dispose()
    {
        if (List != null)
        {
            Pools.Release(List);
        }
    }

    // Implicit conversion to List<T> for easier usage? 
    // No, explicit access via .List property is clearer and avoids confusion about ownership.
}
