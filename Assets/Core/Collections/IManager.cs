namespace Core.Collections
{
    public interface IManager<T>
    {
        public IReadOnlyList<T> Collection { get; }

        public void Add(T value);

        public void Remove(T value);

        public void Clear();
    }
}
