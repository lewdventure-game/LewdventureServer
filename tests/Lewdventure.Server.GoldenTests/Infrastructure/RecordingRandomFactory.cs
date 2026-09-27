using Server.Services;

namespace Tests.Golden.Infrastructure
{
    internal sealed class RecordingRandomFactory : ISeededRandomFactory
    {
        private readonly List<string> _rolls = new();
        private readonly SeededRandomFactory _seededRandomFactory = new();

        public IReadOnlyList<string> Rolls => _rolls;

        public void Reset()
        {
            _rolls.Clear();
        }

        public ISeededRandomService Create()
        {
            return new RecordingRandomService(_seededRandomFactory.Create(), _rolls);
        }

        public ISeededRandomService Create(ulong seed)
        {
            return new RecordingRandomService(_seededRandomFactory.Create(seed), _rolls);
        }
    }
}
