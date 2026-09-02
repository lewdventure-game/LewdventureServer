using System.Diagnostics.CodeAnalysis;

namespace Core.Collections
{
    public abstract class BaseDictionaryManager<TKey, TValue> : IDictionaryManager<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new();

        public int Count => _dictionary.Count;

        public IEnumerable<TValue> Values => _dictionary.Values;

        public bool Add(TKey key, TValue value)
        {
            if (value == null)
                return false;

            if (_dictionary.ContainsKey(key))
                return false;

            _dictionary[key] = value;

            return true;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _dictionary.Clear();
        }
    }
}
