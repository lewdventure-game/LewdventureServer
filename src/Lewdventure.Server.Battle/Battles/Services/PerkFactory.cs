using Server.Perks;

namespace Server.Battles
{
    internal sealed class PerkFactory : IPerkFactory
    {
        private readonly ICoreLog _coreLog;
        private readonly Dictionary<PerkType, IPerkCreator> _creatorsByType = new();
        private readonly Dictionary<string, IPerkCreator> _creatorsByTypeKey = new(StringComparer.OrdinalIgnoreCase);

        public PerkFactory(ICoreLog coreLog, IReadOnlyList<IPerkCreator> perkCreators)
        {
            _coreLog = coreLog;

            for (int i = 0; i < perkCreators.Count; i++)
            {
                var creator = perkCreators[i];

                _creatorsByType[creator.PerkType] = creator;
                _creatorsByTypeKey[creator.TypeKey] = creator;
            }
        }

        public IReadOnlyCollection<string> KnownTypeKeys => _creatorsByTypeKey.Keys;

        public IPerk Create(IPerkMapper mapper)
        {
            if (_creatorsByType.TryGetValue(mapper.PerkType, out var creator) == false)
            {
                _coreLog.Error($"[Story][Battle]: Perk unknown type, id = {mapper.Id}, type = {mapper.PerkType}");

                return new UnknownPerk(mapper, _coreLog);
            }

            return creator.Create(mapper);
        }
    }
}
