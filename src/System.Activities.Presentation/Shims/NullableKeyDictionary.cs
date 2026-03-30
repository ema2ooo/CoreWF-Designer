namespace System.Runtime.Collections
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    // CoreWF's packaged NullableKeyDictionary is internal, but the designer model
    // still relies on a dictionary that can preserve a single null key entry.
    public class NullableKeyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> innerDictionary = new Dictionary<TKey, TValue>();
        private bool hasNullKey;
        private TValue nullValue;

        public TValue this[TKey key]
        {
            get
            {
                if (key is null)
                {
                    if (!this.hasNullKey)
                    {
                        throw new KeyNotFoundException();
                    }

                    return this.nullValue;
                }

                return this.innerDictionary[key];
            }
            set
            {
                if (key is null)
                {
                    this.hasNullKey = true;
                    this.nullValue = value;
                    return;
                }

                this.innerDictionary[key] = value;
            }
        }

        public ICollection<TKey> Keys
            => this.hasNullKey
                ? new[] { default(TKey) }.Concat(this.innerDictionary.Keys).ToArray()
                : this.innerDictionary.Keys;

        public ICollection<TValue> Values
            => this.hasNullKey
                ? new[] { this.nullValue }.Concat(this.innerDictionary.Values).ToArray()
                : this.innerDictionary.Values;

        public int Count => this.innerDictionary.Count + (this.hasNullKey ? 1 : 0);

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            if (key is null)
            {
                if (this.hasNullKey)
                {
                    throw new ArgumentException("An item with the same key has already been added.");
                }

                this.hasNullKey = true;
                this.nullValue = value;
                return;
            }

            this.innerDictionary.Add(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
            => this.Add(item.Key, item.Value);

        public void Clear()
        {
            this.innerDictionary.Clear();
            this.hasNullKey = false;
            this.nullValue = default;
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (!this.TryGetValue(item.Key, out var value))
            {
                return false;
            }

            return EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        public bool ContainsKey(TKey key)
            => key is null ? this.hasNullKey : this.innerDictionary.ContainsKey(key);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (this.hasNullKey)
            {
                array[arrayIndex++] = new KeyValuePair<TKey, TValue>(default, this.nullValue);
            }

            foreach (var entry in this.innerDictionary)
            {
                array[arrayIndex++] = entry;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            if (this.hasNullKey)
            {
                yield return new KeyValuePair<TKey, TValue>(default, this.nullValue);
            }

            foreach (var entry in this.innerDictionary)
            {
                yield return entry;
            }
        }

        public bool Remove(TKey key)
        {
            if (key is null)
            {
                var removed = this.hasNullKey;
                this.hasNullKey = false;
                this.nullValue = default;
                return removed;
            }

            return this.innerDictionary.Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (!this.Contains(item))
            {
                return false;
            }

            return this.Remove(item.Key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key is null)
            {
                value = this.nullValue;
                return this.hasNullKey;
            }

            return this.innerDictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
            => this.GetEnumerator();
    }
}
