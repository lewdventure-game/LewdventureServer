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
            Apply(rewards, source, target, null, null, commands, currentTurn);
        }

        public void Apply(
            IReadOnlyList<BattleReward> rewards,
            IUnitState source,
            IUnitState target,
            ITeamSimulationState? attacker,
            ITeamSimulationState? defender,
            List<BattleCommand> commands,
            int currentTurn)
        {
            if (rewards.Count == 0)
                return;

            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];

                _logger.LogDebug($"[Story][Battle]: Reward entry, type = {reward.Type}, id = {reward.Id}, rewardKey = {reward.RewardKey}, count = {reward.Count}, sourceId = {source.Id}, targetId = {target.Id}, turn = {currentTurn}");

                switch (reward.Type)
                {
                    case BattleRewardType.Bonus:
                        ApplyBonus(reward, target, commands, currentTurn);
                        break;
                    case BattleRewardType.Status:
                        ApplyStatus(reward, source, target, attacker, defender, commands, currentTurn);
                        break;
                    case BattleRewardType.Resource:
                    case BattleRewardType.Character:
                    case BattleRewardType.Summon:
                    case BattleRewardType.Equipment:
                        EmitMetaGrantReward(reward, target, commands);
                        break;
                    default:
                        _logger.LogWarning($"[Story][Battle]: Reward unknown type skipped, id = {reward.Id}, count = {reward.Count}, type = {reward.Type}");
                        break;
                }
            }
        }

        private void EmitMetaGrantReward(BattleReward reward, IUnitState target, List<BattleCommand> commands)
        {
            var rewardType = ResolveMetaRewardTypeName(reward.Type);

            if (string.IsNullOrEmpty(rewardType))
            {
                _logger.LogWarning($"[Story][Battle]: Grant meta reward unknown, type = {reward.Type}, id = {reward.Id}");

                return;
            }

            if (reward.HasStringRewardKey)
            {
                commands.Add(_battleCommandFactory.GrantReward(rewardType, reward.RewardKey, reward.Count, target.Id));
                _logger.LogDebug($"[Story][Battle]: Grant resource, key = {reward.RewardKey}, count = {reward.Count}, targetId = {target.Id}");
                _logger.LogInformation($"[Story][Battle]: Grant meta reward, type = {rewardType}, rewardId = {reward.RewardKey}, count = {reward.Count}, targetId = {target.Id}");

                return;
            }

            commands.Add(_battleCommandFactory.GrantReward(rewardType, reward.Id, reward.Count, target.Id));
            _logger.LogInformation($"[Story][Battle]: Grant meta reward, type = {rewardType}, rewardId = {reward.Id}, count = {reward.Count}, targetId = {target.Id}");
        }

        private string ResolveMetaRewardTypeName(BattleRewardType rewardType)
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
                _logger.LogWarning($"[Story][Battle]: Reward bonus missing, id = {reward.Id}");

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
            ITeamSimulationState? attacker,
            ITeamSimulationState? defender,
            List<BattleCommand> commands,
            int currentTurn)
        {
            if (_configDistributor.Statuses.TryGet(reward.Id, out var statusMapper) == false)
            {
                _logger.LogError($"[Story][Battle]: Reward status missing, id = {reward.Id}");

                return;
            }

            if (statusMapper.StatusType == StatusType.Unknown)
            {
                _logger.LogError($"[Config]: Status unknown type, id = {reward.Id}");

                return;
            }

            if (TryResolveStatusBearer(statusMapper.StatusTarget, source, target, attacker, defender, out var bearer) == false)
                return;

            if (TryResolveApplyingMain(source, attacker, defender, out var applyingMain) == false)
                return;

            _logger.LogDebug($"[Story][Battle]: status_target = {statusMapper.StatusTarget}, bearerId = {bearer.Id}, sourceId = {source.Id}, applyingMainId = {applyingMain.Id}, targetId = {target.Id}");

            if (_statusParametersParser.TryParse(statusMapper.Parameters, statusMapper.StatusType, out var parameters) == false)
                return;

            if (statusMapper.StatusType == StatusType.BonusChange)
            {
                ApplyBonusChange(reward, bearer, parameters, commands, currentTurn);

                return;
            }

            ApplyDamageOverTimeStatus(
                reward,
                bearer,
                applyingMain,
                statusMapper.StatusType,
                parameters,
                commands,
                currentTurn);
        }

        private void ApplyBonusChange(
            BattleReward reward,
            IUnitState bearer,
            StatusParameters parameters,
            List<BattleCommand> commands,
            int currentTurn)
        {
            var sourceKey = $"status:{reward.Id}:bonus_change";

            _battleBonusService.GrantRewardBonuses(
                bearer,
                parameters.Bonuses,
                reward.Count,
                sourceKey,
                commands,
                currentTurn);

            _logger.LogDebug($"[Story][Battle]: bonus_change granted, statusId = {reward.Id}, count = {reward.Count}, mainId = {bearer.Id}, bonuses = {parameters.Bonuses.Count}");
        }

        private void ApplyDamageOverTimeStatus(
            BattleReward reward,
            IUnitState bearer,
            IUnitState applyingMain,
            StatusType statusType,
            StatusParameters parameters,
            List<BattleCommand> commands,
            int currentTurn)
        {
            var activeStatuses = bearer.ActiveStatuses;

            for (int i = 0; i < reward.Count; i++)
            {
                var currentStacks = CountStatusStacks(activeStatuses, reward.Id);

                if (parameters.MaxStacks <= currentStacks)
                {
                    _logger.LogWarning($"[Story][Battle]: Reward status max stacks reached, id = {reward.Id}, maxStacks = {parameters.MaxStacks}");

                    break;
                }

                var appliesBonuses = IsStrongDamageOverTime(statusType);
                var sourceKey = $"status:{reward.Id}:{currentStacks}";
                var activeStatus = new ActiveStatus(
                    reward.Id,
                    parameters.DamageLength,
                    applyingMain.Id,
                    parameters.DamageRatio,
                    true,
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
                        applyingMain.Id,
                        applyingMain.SlotIndex,
                        bearer.Id,
                        bearer.SlotIndex,
                        reward.Id,
                        stacks,
                        parameters.DamageLength));

                _logger.LogDebug($"[Story][Battle]: Reward status applied, id = {reward.Id}, stacks = {stacks}, remainingTicks = {parameters.DamageLength}, damageRatio = {parameters.DamageRatio}, applyingMainId = {applyingMain.Id}, bearerId = {bearer.Id}");
            }
        }

        private bool TryResolveStatusBearer(
            StatusTargetType statusTarget,
            IUnitState source,
            IUnitState target,
            ITeamSimulationState? attacker,
            ITeamSimulationState? defender,
            out IUnitState bearer)
        {
            bearer = source;

            if (statusTarget == StatusTargetType.Unknown)
            {
                _logger.LogError($"[Story][Battle]: status_target unknown, sourceId = {source.Id}, targetId = {target.Id}");

                return false;
            }

            if (statusTarget == StatusTargetType.Caster)
                return TryResolveSideMain(source, attacker, defender, "caster", out bearer);

            if (statusTarget == StatusTargetType.Enemy)
            {
                if (attacker != null && defender != null)
                    return TryGetOpponentMain(source, attacker, defender, out bearer);

                if (IsSummon(target) || source.Side == target.Side)
                {
                    _logger.LogError($"[Story][Battle]: status_target enemy main missing, sourceId = {source.Id}, targetId = {target.Id}");

                    return false;
                }

                if (target.IsAlive() == false)
                {
                    _logger.LogError($"[Story][Battle]: status_target enemy main dead, targetId = {target.Id}");

                    return false;
                }

                bearer = target;

                return true;
            }

            _logger.LogError($"[Story][Battle]: status_target unsupported = {statusTarget}, sourceId = {source.Id}");

            return false;
        }

        private bool TryResolveApplyingMain(
            IUnitState source,
            ITeamSimulationState? attacker,
            ITeamSimulationState? defender,
            out IUnitState applyingMain)
        {
            return TryResolveSideMain(source, attacker, defender, "applying", out applyingMain);
        }

        private bool TryResolveSideMain(
            IUnitState unit,
            ITeamSimulationState? attacker,
            ITeamSimulationState? defender,
            string role,
            out IUnitState main)
        {
            if (attacker != null && defender != null)
            {
                if (TryGetTeam(unit, attacker, defender, out var team) == false)
                {
                    _logger.LogError($"[Story][Battle]: Status {role} team missing, unitId = {unit.Id}, side = {unit.Side}");

                    main = unit;

                    return false;
                }

                if (TryGetAliveMain(team, out main) == false)
                {
                    _logger.LogError($"[Story][Battle]: Status {role} main missing, unitId = {unit.Id}, side = {unit.Side}");

                    main = unit;

                    return false;
                }

                return true;
            }

            if (IsSummon(unit) || unit.IsAlive() == false)
            {
                _logger.LogError($"[Story][Battle]: Status {role} main missing without teams, unitId = {unit.Id}, summon = {IsSummon(unit)}");

                main = unit;

                return false;
            }

            main = unit;

            return true;
        }

        private bool TryGetOpponentMain(
            IUnitState source,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            out IUnitState main)
        {
            main = source;

            if (TryGetTeam(source, attacker, defender, out var sourceTeam) == false)
            {
                _logger.LogError($"[Story][Battle]: status_target enemy source team missing, sourceId = {source.Id}");

                return false;
            }

            var opponentTeam = sourceTeam.BattleSide == attacker.BattleSide ? defender : attacker;

            if (TryGetAliveMain(opponentTeam, out main) == false)
            {
                _logger.LogError($"[Story][Battle]: status_target enemy main missing, sourceId = {source.Id}, opponentSide = {opponentTeam.BattleSide}");

                main = source;

                return false;
            }

            return true;
        }

        private bool TryGetTeam(
            IUnitState unit,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            out ITeamSimulationState team)
        {
            if (unit.Side == attacker.BattleSide)
            {
                team = attacker;

                return true;
            }

            if (unit.Side == defender.BattleSide)
            {
                team = defender;

                return true;
            }

            team = attacker;

            return false;
        }

        private bool TryGetAliveMain(ITeamSimulationState team, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out IUnitState main)
        {
            var mainUnits = team.MainUnits;
            var selectedIndex = -1;
            var selectedSlot = int.MaxValue;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                var unit = mainUnits[i];

                if (unit.IsAlive() == false)
                    continue;

                if (selectedSlot <= unit.SlotIndex)
                    continue;

                selectedIndex = i;
                selectedSlot = unit.SlotIndex;
            }

            if (selectedIndex < 0)
            {
                main = null!;

                return false;
            }

            main = mainUnits[selectedIndex];

            return true;
        }

        private bool IsSummon(IUnitState unit)
        {
            return (unit.Flags & UnitFlags.Summon) == UnitFlags.Summon;
        }

        private bool IsStrongDamageOverTime(StatusType statusType)
        {
            return statusType == StatusType.BurningStrong
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
