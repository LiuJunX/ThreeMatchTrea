using System.Collections.Generic;
using Match3.Core.Utility.Pools;
using Xunit;

namespace Match3.Tests
{
    public class ObjectPoolTests
    {
        [Fact]
        public void Get_ReturnsNewObject_WhenPoolEmpty()
        {
            var pool = Pools.Create(() => new List<int>());
            var list = pool.Get();

            Assert.NotNull(list);
            Assert.Empty(list);
        }

        [Fact]
        public void Get_ReturnsPooledObject_AfterReturn()
        {
            var pool = Pools.Create(() => new List<int>());
            var list1 = pool.Get();
            pool.Return(list1);

            var list2 = pool.Get();
            Assert.Same(list1, list2);
        }

        [Fact]
        public void Return_ResetsObject()
        {
            var pool = Pools.Create(
                generator: () => new List<int>(),
                reset: l => l.Clear()
            );

            var list = pool.Get();
            list.Add(1);
            pool.Return(list);

            var list2 = pool.Get();
            Assert.Empty(list2);
        }

        [Fact]
        public void Pool_RespectsMaxSize()
        {
            var pool = Pools.Create(
                generator: () => new object(),
                maxSize: 1
            );

            var obj1 = pool.Get();
            var obj2 = pool.Get();

            pool.Return(obj1);
            pool.Return(obj2); // Should be dropped

            var obj3 = pool.Get(); // Should be obj1 (LIFO)
            var obj4 = pool.Get(); // Should be new object

            Assert.Same(obj1, obj3);
            Assert.NotSame(obj2, obj4);
        }

        [Fact]
        public void GlobalPools_ObtainList_WorksCorrectly()
        {
            var list1 = Pools.ObtainList<int>();
            list1.Add(123);
            
            Pools.Release(list1);

            var list2 = Pools.ObtainList<int>();
            
            Assert.Same(list1, list2);
            Assert.Empty(list2);
        }
        
        [Fact]
        public void GlobalPools_ObtainList_DifferentTypes_AreSeparate()
        {
            var listInt = Pools.ObtainList<int>();
            var listString = Pools.ObtainList<string>();
            
            Assert.NotSame((object)listInt, (object)listString);
            
            Pools.Release(listInt);
            Pools.Release(listString);
        }
    }
}
