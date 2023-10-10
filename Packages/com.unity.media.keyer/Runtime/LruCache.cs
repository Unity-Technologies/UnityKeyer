using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer
{
    abstract class LruCache<TKey, TValue> : IDisposable where TValue : class
    {
        // Using less then 2 instances means you should just cache one instance directly.
        // Least Recently Used needs at least 2 instances to make sense.
        const uint k_MinSize = 2;

        readonly uint m_Size;
        readonly List<TKey> m_List = new();
        readonly Dictionary<TKey, TValue> m_Map = new();

        protected LruCache(uint size)
        {
            if (size < k_MinSize)
            {
                throw new ArgumentException(
                    $"{nameof(size)} must be superior or equal to {k_MinSize}.");
            }

            m_Size = size;
        }

        public void Dispose()
        {
            foreach (var item in m_Map)
            {
                Destroy(item.Value);
            }

            m_Map.Clear();
            m_List.Clear();
        }

        public void Store(TKey key, TValue value)
        {
            if (m_Map.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"{GetType().FullName} already has entry for key {key}.");
            }

            if (m_Map.ContainsValue(value))
            {
                throw new InvalidOperationException(
                    $"{GetType().FullName} already has entry for value {value}.");
            }

            m_Map.Add(key, value);
            m_List.Add(key);

            if (m_List.Count == m_Size + 1)
            {
                var delKey = m_List[0];
                Assert.IsTrue(m_Map.ContainsKey(delKey));
                var delValue = m_Map[delKey];
                m_List.RemoveAt(0);
                m_Map.Remove(delKey);
                Destroy(delValue);
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var result = m_Map.TryGetValue(key, out value);

            // Bump up the list;
            if (result)
            {
                var index = m_List.IndexOf(key);

                // "slide" values down the list to insert Key at the end.
                for (var i = index; i != m_List.Count - 1; ++i)
                {
                    m_List[i] = m_List[i + 1];
                }

                m_List[^1] = key;
            }

            return result;
        }

        protected abstract void Destroy(TValue value);
    }

    class ObjectLruCache<TKey> : LruCache<TKey, Object>
    {
        public ObjectLruCache(uint size)
            : base(size) { }

        protected override void Destroy(Object value)
        {
            Utilities.Destroy(value);
        }
    }

    class ComputeBufferLruCache<TKey> : LruCache<TKey, ComputeBuffer>
    {
        public ComputeBufferLruCache(uint size)
            : base(size) { }

        protected override void Destroy(ComputeBuffer value)
        {
            value.Dispose();
        }
    }
}
