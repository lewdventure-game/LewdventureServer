using Server.Battles;

namespace Server.Services
{
    internal sealed class RecordingSeededRandomService : ISeededRandomService
    {
        private readonly IBattleRollRecorder _battleRollRecorder;
        private readonly ISeededRandomService _seededRandomService;

        private int _rollIndex;

        public RecordingSeededRandomService(IBattleRollRecorder battleRollRecorder, ISeededRandomService seededRandomService)
        {
            _battleRollRecorder = battleRollRecorder;
            _seededRandomService = seededRandomService;
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

            _battleRollRecorder.Record(_rollIndex, rollName, value);
            _rollIndex += 1;

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
