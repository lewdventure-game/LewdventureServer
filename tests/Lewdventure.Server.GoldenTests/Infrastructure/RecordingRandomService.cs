using System.Globalization;
using Server.Services;

namespace Tests.Golden.Infrastructure
{
    internal sealed class RecordingRandomService : ISeededRandomService
    {
        private readonly List<string> _rolls;
        private readonly ISeededRandomService _seededRandomService;

        public RecordingRandomService(ISeededRandomService seededRandomService, List<string> rolls)
        {
            _seededRandomService = seededRandomService;
            _rolls = rolls;
        }

        public ulong Seed => _seededRandomService.Seed;

        public void SetSeed(ulong seed)
        {
            _seededRandomService.SetSeed(seed);
        }

        public ulong NextULong()
        {
            return _seededRandomService.NextULong();
        }

        public uint NextUInt()
        {
            return _seededRandomService.NextUInt();
        }

        public float GetRandomValue(string rollName)
        {
            var value = _seededRandomService.GetRandomValue(rollName);

            _rolls.Add($"{_rolls.Count} {rollName} {value.ToString("R", CultureInfo.InvariantCulture)}");

            return value;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            return _seededRandomService.Range(minInclusive, maxExclusive);
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            return _seededRandomService.Range(minInclusive, maxInclusive);
        }

        public T Choice<T>(T[] options)
        {
            return _seededRandomService.Choice(options);
        }
    }
}
