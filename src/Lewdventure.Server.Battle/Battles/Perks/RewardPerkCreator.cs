using Server.Perks;

namespace Server.Battles
{
    internal sealed class RewardPerkCreator : IPerkCreator
    {
        private readonly IBattleRewardService _battleRewardService;
        private readonly ICoreLog _coreLog;
        private readonly PerkParameterReader _perkParameterReader;

        public RewardPerkCreator(IBattleRewardService battleRewardService, ICoreLog coreLog, PerkParameterReader perkParameterReader)
        {
            _battleRewardService = battleRewardService;
            _coreLog = coreLog;
            _perkParameterReader = perkParameterReader;
        }

        public PerkType PerkType => PerkType.Reward;

        public string TypeKey => "reward";

        public IPerk Create(IPerkMapper mapper)
        {
            _perkParameterReader.Read(mapper);

            var rewards = _perkParameterReader.Rewards("rewards");

            return new RewardPerk(mapper, rewards, _battleRewardService, _coreLog);
        }
    }
}
