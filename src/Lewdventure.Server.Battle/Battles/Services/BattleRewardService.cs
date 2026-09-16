using Microsoft.Extensions.Logging;
using Server.Services;
using Server.Statuses;

namespace Server.Battles
{
    internal sealed class BattleRewardService : IBattleRewardService
    {
        private readonly ILogger<BattleRewardService> _logger;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattleRewardParser _battleRewardParser;
        private readonly IConfigDistributor _configDistributor;
        private readonly IStatusParametersParser _statusParametersParser;

        public BattleRewardService(
            ILogger<BattleRewardService> logger,
            IBattleBonusService battleBonusService,
            IBattleCommandFactory battleCommandFactory,
            IBattleRewardParser battleRewardParser,
            IConfigDistributor configDistributor,
            IStatusParametersParser statusParametersParser)
        {
            _logger = logger;
            _battleBonusService = battleBonusService;
            _battleCommandFactory = battleCommandFactory;
            _battleRewardParser = battleRewardParser;
            _configDistributor = configDistributor;
            _statusParametersParser = statusParametersParser;
        }

        public IReadOnlyList<BattleReward> Parse(string value)
        {
            return _battleRewardParser.Parse(value);
        }

        public void Apply(
            IReadOnlyList<BattleReward> rewards,
            IUnitState source,
            IUnitState target,
            List<BattleCommand> commands,
            int currentTurn)
        {
            if (rewards.Count == 0)
                return;

            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];

                _logger.LogDebug($"[Story][Battle] reward entry type = {reward.Type}, id = {reward.Id}, rewardKey = {reward.RewardKey}, count = {reward.Count}, sourceId = {source.Id}, targetId = {target.Id}, turn = {currentTurn}");

                switch (reward.Type)
                {
                    case BattleRewardType.Bonus:
                        ApplyBonus(reward, target, commands, currentTurn);
                        break;
                    case BattleRewardType.Status:
                        ApplyStatus(reward, source, target, commands, currentTurn);
                        break;
                    case BattleRewardType.Resource:
                    case BattleRewardType.Character:
                    case BattleRewardType.Summon:
                    case BattleRewardType.Equipment:
                        EmitMetaGrantReward(reward, target, commands);
                        break;
                    default:
                        _logger.LogWarning($"[Story][Battle] reward unknown type skipped id = {reward.Id}, count = {reward.Count}, type = {reward.Type}");
                        break;
                }
            }
        }

        private void EmitMetaGrantReward(BattleReward reward, IUnitState target, List<BattleCommand> commands)
        {
            var rewardType = ResolveMetaRewardTypeName(reward.Type);

            if (string.IsNullOrEmpty(rewardType))
            {
                _logger.LogWarning($"[Story][Battle] grant meta reward unknown type = {reward.Type} id = {reward.Id}");

                return;
            }

            if (reward.HasStringRewardKey)
            {
                commands.Add(_battleCommandFactory.GrantReward(rewardType, reward.RewardKey, reward.Count, target.Id));
                _logger.LogDebug($"[Story][Battle] grant resource key = {reward.RewardKey} count = {reward.Count} targetId = {target.Id}");
                _logger.LogInformation($"[Story][Battle] grant meta reward type = {rewardType} rewardId = {reward.RewardKey} count = {reward.Count} targetId = {target.Id}");

                return;
            }

            commands.Add(_battleCommandFactory.GrantReward(rewardType, reward.Id, reward.Count, target.Id));
            _logger.LogInformation($"[Story][Battle] grant meta reward type = {rewardType} rewardId = {reward.Id} count = {reward.Count} targetId = {target.Id}");
        }

        private static string ResolveMetaRewardTypeName(BattleRewardType rewardType)
        {
            if (rewardType == BattleRewardType.Resource)
                return "resource";

            if (rewardType == BattleRewardType.Character)
                return "character";

            if (rewardType == BattleRewardType.Summon)
                return "summon";

            if (rewardType == BattleRewardType.Equipment)
                return "equipment";

            return string.Empty;
        }

        private void ApplyBonus(BattleReward reward, IUnitState target, List<BattleCommand> commands, int currentTurn)
        {
            if (_configDistributor.Bonuses.TryGet(reward.Id, out _) == false)
            {
                _logger.LogWarning($"[Story][Battle] reward bonus missing id = {reward.Id}");

                return;
            }

            _battleBonusService.Grant(
                target,
                reward.Id,
                reward.Count,
                $"reward:bonus:{reward.Id}",
                commands,
                currentTurn);
        }

        private void ApplyStatus(
            BattleReward reward,
            IUnitState source,
            IUnitState target,
            List<BattleCommand> commands,
            int currentTurn)
        {
            if (_configDistributor.Statuses.TryGet(reward.Id, out var statusMapper) == false)
            {
                _logger.LogWarning($"[Story][Battle] reward status missing id = {reward.Id}");

                return;
            }

            if (statusMapper.StatusType == StatusType.Unknown)
            {
                _logger.LogError($"[Config] status unknown type id = {reward.Id}");

                return;
            }

            var bearer = ResolveStatusBearer(statusMapper.StatusTarget, source, target);

            _logger.LogDebug($"[Story][Battle] status_target = {statusMapper.StatusTarget}, bearerId = {bearer.Id}, sourceId = {source.Id}, targetId = {target.Id}");

            var parameters = _statusParametersParser.Parse(statusMapper.Parameters);
            var activeStatuses = bearer.ActiveStatuses;

            for (int i = 0; i < reward.Count; i++)
            {
                var currentStacks = CountStatusStacks(activeStatuses, reward.Id);

                if (parameters.MaxStacks <= currentStacks)
                {
                    _logger.LogWarning($"[Story][Battle] reward status max stacks reached id = {reward.Id}, maxStacks = {parameters.MaxStacks}");

                    break;
                }

                var appliesDamageOverTime = IsDamageOverTime(statusMapper.StatusType);
                var appliesBonuses = IsBonusStatus(statusMapper.StatusType);
                var remainingTicks = parameters.DamageLength;

                if (appliesDamageOverTime == false && remainingTicks <= 0)
                    remainingTicks = int.MaxValue;

                if (appliesDamageOverTime && remainingTicks <= 0)
                {
                    _logger.LogWarning($"[Story][Battle] reward status damage_length missing id = {reward.Id}");

                    break;
                }

                var sourceKey = $"status:{reward.Id}:{currentStacks}";
                var activeStatus = new ActiveStatus(
                    reward.Id,
                    remainingTicks,
                    source.Id,
                    parameters.DamageRatio,
                    appliesDamageOverTime,
                    appliesBonuses,
                    parameters.Bonuses,
                    sourceKey);

                if (appliesBonuses)
                    _battleBonusService.GrantRewardBonuses(
                        bearer,
                        parameters.Bonuses,
                        1,
                        sourceKey,
                        commands,
                        currentTurn);

                activeStatuses.Add(activeStatus);

                var stacks = currentStacks + 1;
                commands.Add(
                    _battleCommandFactory.ApplyStatus(
                        source.Id,
                        source.SlotIndex,
                        bearer.Id,
                        bearer.SlotIndex,
                        reward.Id,
                        stacks,
                        remainingTicks));

                _logger.LogDebug($"[Story][Battle] reward status applied id = {reward.Id}, stacks = {stacks}, remainingTicks = {remainingTicks}, damageRatio = {parameters.DamageRatio}, sourceId = {source.Id}, bearerId = {bearer.Id}");
            }
        }

        private IUnitState ResolveStatusBearer(StatusTargetType statusTarget, IUnitState source, IUnitState target)
        {
            if (statusTarget == StatusTargetType.Caster)
                return source;

            if (statusTarget == StatusTargetType.Enemy)
                return target;

            _logger.LogWarning($"[Story][Battle] status_target unknown = {statusTarget}; defaulting to enemy targetId = {target.Id}");

            return target;
        }

        private bool IsDamageOverTime(StatusType statusType)
        {
            return statusType == StatusType.Burning
                || statusType == StatusType.BurningStrong
                || statusType == StatusType.Poison
                || statusType == StatusType.PoisonStrong;
        }

        private bool IsBonusStatus(StatusType statusType)
        {
            return statusType == StatusType.BonusChange
                || statusType == StatusType.BurningStrong
                || statusType == StatusType.PoisonStrong;
        }

        private int CountStatusStacks(List<ActiveStatus> activeStatuses, int statusId)
        {
            var stacks = 0;

            for (int i = 0; i < activeStatuses.Count; i++)
            {
                if (activeStatuses[i].StatusId == statusId)
                    stacks += 1;
            }

            return stacks;
        }
    }
}
