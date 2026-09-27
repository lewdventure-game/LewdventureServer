using Server.Perks;

namespace Server.Battles
{
    internal sealed class RewardPerk : BasePerk
    {
        private readonly IReadOnlyList<BattleReward> _rewards;
        private readonly IBattleRewardService _battleRewardService;
        private readonly ICoreLog _coreLog;

        internal RewardPerk(
            IPerkMapper mapper,
            IReadOnlyList<BattleReward> rewards,
            IBattleRewardService battleRewardService,
            ICoreLog coreLog)
            : base(mapper)
        {
            _rewards = rewards;
            _battleRewardService = battleRewardService;
            _coreLog = coreLog;
        }

        public override void OnEquipped(IUnitState owner, List<BattleCommand> commands)
        {
            _coreLog.Debug($"[Story][Battle]: Perk reward grant, perkId = {Id}, ownerId = {owner.Id}, rewards = {_rewards.Count}");

            _battleRewardService.Apply(_rewards, owner, owner, commands, 0);
        }

        public override bool CanTrigger(int currentTurn)
        {
            return false;
        }
    }
}
