using Server.Perks;

namespace Server.Battles
{
    internal sealed class ActionRewardPerkCreator : IPerkCreator
    {
        private readonly IBattleRewardService _battleRewardService;
        private readonly ICoreLog _coreLog;
        private readonly PerkParameterReader _perkParameterReader;

        public ActionRewardPerkCreator(IBattleRewardService battleRewardService, ICoreLog coreLog, PerkParameterReader perkParameterReader)
        {
            _battleRewardService = battleRewardService;
            _coreLog = coreLog;
            _perkParameterReader = perkParameterReader;
        }

        public PerkType PerkType => PerkType.ActionReward;

        public string TypeKey => "action_reward";

        public IPerk Create(IPerkMapper mapper)
        {
            _perkParameterReader.Read(mapper);

            var rewardsOnGrant = _perkParameterReader.Rewards("rewards");
            var rewardsOnAction = _perkParameterReader.Rewards("rewards_on_action");
            var thresholds = _perkParameterReader.ActionThresholds();
            var rewardsChance = _perkParameterReader.Float("rewards_chance", 0f);

            return new ActionRewardPerk(
                mapper,
                rewardsOnGrant,
                rewardsOnAction,
                thresholds,
                rewardsChance,
                _battleRewardService,
                _coreLog);
        }
    }
}
