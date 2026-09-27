using Server.Perks;

namespace Server.Battles
{
    internal sealed class ResurrectionPerkCreator : IPerkCreator
    {
        private readonly ICoreLog _coreLog;
        private readonly PerkParameterReader _perkParameterReader;

        public ResurrectionPerkCreator(ICoreLog coreLog, PerkParameterReader perkParameterReader)
        {
            _coreLog = coreLog;
            _perkParameterReader = perkParameterReader;
        }

        public PerkType PerkType => PerkType.Resurrection;

        public string TypeKey => "resurrection";

        public IPerk Create(IPerkMapper mapper)
        {
            _perkParameterReader.Read(mapper);

            var healthRatio = _perkParameterReader.Float("health_ratio", 0f);
            var resurrectionsCount = _perkParameterReader.Int("resurrections_count", 0);

            if (healthRatio <= 0f || resurrectionsCount <= 0)
                _coreLog.Error($"[Story][Battle]: Perk resurrection invalid params, id = {mapper.Id}, healthRatio = {healthRatio}, count = {resurrectionsCount}");

            return new ResurrectionPerk(mapper, healthRatio, resurrectionsCount, _coreLog);
        }
    }
}
