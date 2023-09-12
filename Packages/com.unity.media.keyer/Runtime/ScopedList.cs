using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace Unity.Media.Keyer
{
    // Note that have no disposal mechanism on the collection values themselves.
    // Not needed as we restrict to value types at the moment.
    readonly struct ScopedList<T> : IDisposable where T : struct
    {
        static readonly ObjectPool<List<T>> s_Pool = new(() => new List<T>(), x => x.Clear());

        public static ScopedList<T> Create() => new(s_Pool);

        readonly List<T> m_List;
        public List<T> List => m_List;
        ScopedList(ObjectPool<List<T>> pool) => m_List = pool.Get();
        public void Dispose() => s_Pool.Release(m_List);
    }
}
