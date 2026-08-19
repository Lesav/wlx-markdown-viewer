#if NET48
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace System.Collections.Frozen
{
    public sealed class FrozenDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> values;

        internal FrozenDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source, IEqualityComparer<TKey>? comparer)
        {
            values = new Dictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default);
            foreach (KeyValuePair<TKey, TValue> item in source)
                values[item.Key] = item.Value;
        }

        public TValue this[TKey key] => values[key];
        public IEnumerable<TKey> Keys => values.Keys;
        public IEnumerable<TValue> Values => values.Values;
        public int Count => values.Count;
        public bool ContainsKey(TKey key) => values.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => values.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class FrozenDictionary
    {
        public static FrozenDictionary<TKey, TValue> ToFrozenDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source,
            IEqualityComparer<TKey>? comparer = null) => new(source, comparer);
    }

    public sealed class FrozenSet<T> : IReadOnlyCollection<T>
    {
        private readonly HashSet<T> values;

        internal FrozenSet(IEnumerable<T> source, IEqualityComparer<T>? comparer)
        {
            values = new HashSet<T>(source, comparer ?? EqualityComparer<T>.Default);
        }

        public int Count => values.Count;
        public bool Contains(T value) => values.Contains(value);
        public IEnumerator<T> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class FrozenSet
    {
        public static FrozenSet<T> ToFrozenSet<T>(
            this IEnumerable<T> source,
            IEqualityComparer<T>? comparer = null) => new(source, comparer);
    }
}
#endif
