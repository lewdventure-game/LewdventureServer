using Server.Perks;

namespace Server.Battles
{
    internal sealed class RewardPerk : BasePerk
    {
        private readonly IReadOnlyList<BattleReward> _rewards;
        private readonly IBattleRewardService _battleRewardService;
        private readonly ILogger _logger;

        internal RewardPerk(
            IPerkMapper mapper,
            IReadOnlyList<BattleReward> rewards,
            IBattleRewardService battleRewardService,
            ILogger logger)
            : base(mapper)
        {
            _rewards = rewards;
            _battleRewardService = battleRewardService;
            _logger = logger;
        }

        public override void OnEquipped(IUnitState owner, List<BattleCommand> commands)
        {
            _logger.LogDebug($"[Story][Battle]: Perk reward grant, perkId = {Id}, ownerId = {owner.Id}, rewards = {_rewards.Count}");

            _battleRewardService.Apply(_rewards, owner, owner, commands, 0);
        }

        public override bool CanTrigger(int currentTurn)
        {
            return false;
        }
    }
}
