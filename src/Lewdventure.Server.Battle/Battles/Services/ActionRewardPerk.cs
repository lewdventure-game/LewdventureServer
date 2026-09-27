using Server.Perks;

using Server.Services;

namespace Server.Battles
{
    internal sealed class ActionRewardPerk : BasePerk
    {
        private readonly IReadOnlyList<BattleReward> _rewardsOnGrant;
        private readonly IReadOnlyList<BattleReward> _rewardsOnAction;
        private readonly IReadOnlyList<PerkActionThreshold> _actionThresholds;
        private readonly float _rewardsChance;
        private readonly IBattleRewardService _battleRewardService;
        private readonly ICoreLog _coreLog;
        private readonly Dictionary<BattlePerkActionType, int> _actionCounters = new();

        internal ActionRewardPerk(
            IPerkMapper mapper,
            IReadOnlyList<BattleReward> rewardsOnGrant,
            IReadOnlyList<BattleReward> rewardsOnAction,
            IReadOnlyList<PerkActionThreshold> actionThresholds,
            float rewardsChance,
            IBattleRewardService battleRewardService,
            ICoreLog coreLog)
            : base(mapper)
        {
            _rewardsOnGrant = rewardsOnGrant;
            _rewardsOnAction = rewardsOnAction;
            _actionThresholds = actionThresholds;
            _rewardsChance = rewardsChance;
            _battleRewardService = battleRewardService;
            _coreLog = coreLog;
        }

        public override void OnEquipped(IUnitState owner, List<BattleCommand> commands)
        {
            _coreLog.Debug($"[Story][Battle]: Perk action reward grant, perkId = {Id}, ownerId = {owner.Id}, rewards = {_rewardsOnGrant.Count}");

            _battleRewardService.Apply(_rewardsOnGrant, owner, owner, commands, 0);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override bool CanTrigger(int currentTurn)
        {
            return false;
        }

        public override void NotifyAction(BattlePerkActionType actionType, IPerkExecutionContext context)
        {
            if (context.Owner.IsAlive() == false)
                return;

            if (_actionThresholds.Count == 0 || _rewardsOnAction.Count == 0)
                return;

            ProcessAction(actionType, context);

            if (actionType != BattlePerkActionType.AnyDamage)
                ProcessAction(BattlePerkActionType.AnyDamage, context);
        }

        private void ProcessAction(BattlePerkActionType actionType, IPerkExecutionContext context)
        {
            var threshold = FindThreshold(actionType);

            if (threshold <= 0)
                return;

            if (_actionCounters.TryGetValue(actionType, out var current) == false)
                current = 0;

            current += 1;
            _actionCounters[actionType] = current;

            _coreLog.Debug($"[Story][Battle]: Perk action progress, perkId = {Id}, action = {actionType}, current = {current}, threshold = {threshold}");

            if (current < threshold)
                return;

            _actionCounters[actionType] = 0;

            var roll = context.SeededRandomService.GetRandomValue(RandomRollNames.ActionReward);

            _coreLog.Debug($"[Story][Battle]: Perk action reward roll, perkId = {Id}, action = {actionType}, roll = {roll}, rewardsChance = {_rewardsChance}");

            if (_rewardsChance <= roll)
                return;

            var owner = context.Owner;
            var commands = new List<BattleCommand>
            {
                context.BattleCommandFactory.TriggerPerk(owner.Id, owner.SlotIndex, owner.Id, owner.SlotIndex, Id),
            };

            _battleRewardService.Apply(_rewardsOnAction, owner, owner, context.Attacker, context.Defender, commands, context.CurrentTurn);

            context.BattleScriptBuilder.Add(
                context.Steps,
                context.CurrentTurn,
                BattlePhaseType.PerkTrigger,
                owner,
                commands,
                owner);
        }

        private int FindThreshold(BattlePerkActionType actionType)
        {
            for (int i = 0; i < _actionThresholds.Count; i++)
            {
                if (_actionThresholds[i].ActionType == actionType)
                    return _actionThresholds[i].Threshold;
            }

            return 0;
        }
    }
}
