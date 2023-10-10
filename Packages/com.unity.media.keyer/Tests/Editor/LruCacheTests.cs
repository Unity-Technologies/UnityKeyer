using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class LruCacheTests
    {
        class DummyObject
        {
            static int s_InstancesCount;
            static int s_DestroyedInstancesCount;

            public static int InstancesCount => s_InstancesCount;
            public static int DestroyedInstancesCount => s_DestroyedInstancesCount;

            bool m_Destroyed;

            public bool Destroyed => m_Destroyed;

            public DummyObject()
            {
                ++s_InstancesCount;
                m_Destroyed = false;
            }

            public void Destroy()
            {
                m_Destroyed = true;
                ++s_DestroyedInstancesCount;
            }

            ~DummyObject()
            {
                --s_InstancesCount;
            }
        }

        class DummyLruCache<TKey> : LruCache<TKey, DummyObject>
        {
            public DummyLruCache(uint size)
                : base(size) { }

            protected override void Destroy(DummyObject obj)
            {
                obj.Destroy();
            }
        }

        [Test]
        public void InvalidSizeThrows()
        {
            Assert.Throws<ArgumentException>(
                () => new DummyLruCache<int>(1));
        }

        [Test]
        public void CanStoreAndRetrieveValue()
        {
            var cache = new DummyLruCache<int>(3);
            var obj0 = new DummyObject();
            cache.Store(0, obj0);
            Assert.IsTrue(cache.TryGetValue(0, out var retrieved));
            Assert.AreEqual(obj0, retrieved);
        }

        [Test]
        public void StoringSameObjectTwiceThrows()
        {
            var cache = new DummyLruCache<int>(3);
            var obj = new DummyObject();
            cache.Store(0, obj);
            Assert.Throws<InvalidOperationException>(
                () => cache.Store(0, obj));
        }

        [Test]
        public void StoringSameObjectTwiceWithDifferentKeysThrows()
        {
            var cache = new DummyLruCache<int>(3);
            var obj = new DummyObject();
            cache.Store(0, obj);
            Assert.Throws<InvalidOperationException>(
                () => cache.Store(1, obj));
        }

        [Test]
        public void DropsLastRecentlyUsed()
        {
            var cache = new DummyLruCache<int>(2);
            var obj0 = new DummyObject();
            var obj1 = new DummyObject();
            var obj2 = new DummyObject();
            cache.Store(0, obj0);
            cache.Store(1, obj1);
            cache.Store(2, obj2);

            Assert.IsTrue(cache.TryGetValue(2, out var retrieved2));
            Assert.AreEqual(obj2, retrieved2);
            Assert.IsTrue(cache.TryGetValue(1, out var retrieved1));
            Assert.AreEqual(obj1, retrieved1);
            Assert.IsFalse(cache.TryGetValue(0, out var _));

            Assert.IsFalse(obj2.Destroyed);
            Assert.IsFalse(obj1.Destroyed);
            Assert.IsTrue(obj0.Destroyed);
        }

        [Test]
        public void AccessingValueBumpsItUpTheList()
        {
            var cache = new DummyLruCache<int>(2);
            var obj0 = new DummyObject();
            var obj1 = new DummyObject();
            var obj2 = new DummyObject();
            cache.Store(0, obj0);
            cache.Store(1, obj1);

            {
                Assert.IsTrue(cache.TryGetValue(1, out var retrieved1));
                Assert.AreEqual(obj1, retrieved1);
                Assert.IsTrue(cache.TryGetValue(0, out var retrieved0));
                Assert.AreEqual(obj0, retrieved0);
            }

            cache.Store(2, obj2);

            {
                // O was last accessed so 1 should've been deleted.
                Assert.IsFalse(cache.TryGetValue(1, out var _));
                Assert.IsTrue(cache.TryGetValue(0, out var retrieved0));
                Assert.AreEqual(obj0, retrieved0);
                Assert.IsTrue(cache.TryGetValue(2, out var retrieved2));
                Assert.AreEqual(obj2, retrieved2);
            }
        }

        [UnityTest]
        public IEnumerator DestroyedObjectsAreGarbageCollected()
        {
            const int capacity = 12;
            const int totalInstances = 48;

            var initInstancesCount = DummyObject.InstancesCount;
            var initDestroyedCount = DummyObject.DestroyedInstancesCount;

            var cache = new DummyLruCache<int>(capacity);

            for (var i = 0; i != totalInstances; ++i)
            {
                cache.Store(i, new DummyObject());
            }

            Assert.IsTrue(DummyObject.InstancesCount - initInstancesCount == totalInstances);
            Assert.IsTrue(DummyObject.DestroyedInstancesCount - initDestroyedCount == totalInstances - capacity);

            // Let's verify destroyed objects are garbage collected.
            // Perform garbage collection, non deterministic,
            // so give it a couple tries, and margin.
            // The point is not that all objects are garbage collected,
            // but that the GC starts collecting them.
            const int maxTry = 24;
            const int tolerance = capacity / 3;
            var index = 0;
            for (; index != maxTry; ++index)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                yield return null;

                if (DummyObject.InstancesCount - tolerance < capacity)
                {
                    break;
                }
            }

            Assert.IsTrue(index < maxTry - 1);
        }
    }
}
