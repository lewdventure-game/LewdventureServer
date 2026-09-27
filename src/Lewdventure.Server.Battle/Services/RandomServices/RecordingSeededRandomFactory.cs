using Server.Battles;

namespace Server.Services
{
    internal sealed class RecordingSeededRandomFactory : ISeededRandomFactory
    {
        private readonly IBattleRollRecorder _battleRollRecorder;
        private readonly SeededRandomFactory _seededRandomFactory = new();

        public RecordingSeededRandomFactory(IBattleRollRecorder battleRollRecorder)
        {
            _battleRollRecorder = battleRollRecorder;
        }

        public ISeededRandomService Create()
        {
            return new RecordingSeededRandomService(_battleRollRecorder, _seededRandomFactory.Create());
        }

        public ISeededRandomService Create(ulong seed)
        {
            return new RecordingSeededRandomService(_battleRollRecorder, _seededRandomFactory.Create(seed));
        }
    }
}
