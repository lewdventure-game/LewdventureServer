using System.Diagnostics.CodeAnalysis;

namespace Core.Collections
{
    public interface IDictionaryManager<TKey, TValue>
        where TKey : notnull
    {
        public int Count { get; }

        public IEnumerable<TValue> Values { get; }

        public bool Add(TKey key, TValue value);

        public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value);

        public void Clear();
    }
}
