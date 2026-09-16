namespace Core.Collections
{
    public abstract class BaseManager<T> : IManager<T>
    {
        private readonly List<T> _collection = new();

        public IReadOnlyList<T> Collection => _collection;

        public void Add(T value)
        {
            if (value == null)
                return;

            if (_collection.Contains(value))
                return;

            _collection.Add(value);
        }

        public void Remove(T value)
        {
            if (value == null)
                return;

            _collection.Remove(value);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public virtual void Clear()
        {
            _collection.Clear();
        }
    }
}
