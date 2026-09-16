using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Server.Services;
using Server.Statuses;

namespace Server.Battles
{
    internal sealed class BattleStatusSimulator : IBattleStatusSimulator
    {
        private readonly ILogger<BattleStatusSimulator> _logger;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly IConfigDistributor _configDistributor;
        private readonly List<StatusTickEntry> _tickQueue = new();

        public BattleStatusSimulator(
            ILogger<BattleStatusSimulator> logger,
            IBattleBonusService battleBonusService,
            IBattleCommandFactory battleCommandFactory,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleScriptBuilder battleScriptBuilder,
            IConfigDistributor configDistributor)
        {
            _logger = logger;
            _battleBonusService = battleBonusService;
            _battleCommandFactory = battleCommandFactory;
            _battlePerkSimulator = battlePerkSimulator;
            _battleScriptBuilder = battleScriptBuilder;
            _configDistributor = configDistributor;
        }

        public void EmitInitialStatuses(IUnitState unitState, List<BattleStep> steps, int currentTurn)
        {
            var activeStatuses = unitState.ActiveStatuses;

            if (activeStatuses.Count == 0)
                return;

            for (int i = 0; i < activeStatuses.Count; i++)
            {
                var activeStatus = activeStatuses[i];

                if (HasStatusBefore(activeStatuses, activeStatus.StatusId, i))
                    continue;

                var stacks = CountStacks(activeStatuses, activeStatus.StatusId);
                var commands = new List<BattleCommand>
                {
                    _battleCommandFactory.ApplyStatus(
                        unitState.Id,
                        unitState.SlotIndex,
                        unitState.Id,
                        unitState.SlotIndex,
                        activeStatus.StatusId,
                        stacks,
                        activeStatus.RemainingTicks),
                };

                _battleScriptBuilder.Add(
                    steps,
                    currentTurn,
                    BattlePhaseType.StatusTrigger,
                    unitState,
                    commands,
                    unitState);

                _logger.LogDebug($"[Story][Battle]: Apply status, unitId = {unitState.Id}, statusId = {activeStatus.StatusId}, stacks = {stacks}, remainingTicks = {activeStatus.RemainingTicks}");
            }
        }

        public void SimulateSide(
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            List<BattleStep> steps,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            _tickQueue.Clear();

            CollectDamageOverTimeTicks(ownerTeam.MainUnits);
            SortTickQueueByTriggerOrder();

            var cooldown = GetStatusesCooldown();

            _logger.LogDebug($"[Story][Battle]: Status side, queue side = {ownerTeam.BattleSide}, turn = {currentTurn}, mainsOnly = true, summonsSkipped = true, queueSize = {_tickQueue.Count}");

            for (int i = 0; i < _tickQueue.Count; i++)
            {
                var entry = _tickQueue[i];

                _logger.LogDebug($"[Story][Battle]: Status queue entry, index = {i}, unitId = {entry.Unit.Id}, statusId = {entry.StatusId}, triggerOrder = {entry.TriggerOrder}");

                TickDamageOverTimeGroup(
                    entry.Unit,
                    ownerTeam,
                    opponentTeam,
                    steps,
                    currentTurn,
                    seededRandomService,
                    cooldown,
                    entry.StatusId);
            }

            DecrementAndExpireStatuses(ownerTeam.MainUnits, steps, currentTurn);
            DecrementAndExpireStatuses(ownerTeam.Summons, steps, currentTurn);
        }

        public void ExpireNonDamageOverTimeStatusesAtBattleEnd(
            IUnitState unitState,
            List<BattleStep> steps,
            int currentTurn)
        {
            var activeStatuses = unitState.ActiveStatuses;

            for (int i = activeStatuses.Count - 1; 0 <= i; i--)
            {
                var activeStatus = activeStatuses[i];

                if (activeStatus.AppliesDamageOverTime)
                    continue;

                ExpireStatus(unitState, steps, currentTurn, activeStatus);

                activeStatuses.RemoveAt(i);

                _logger.LogDebug($"[Story][Battle]: Battle end, expire non damage over time status, unitId = {unitState.Id}, statusId = {activeStatus.StatusId}, sourceKey = {activeStatus.BonusSourceKey}");
            }
        }

        private void CollectDamageOverTimeTicks(IReadOnlyList<IUnitState> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                if (unit.IsAlive() == false)
                    continue;

                var activeStatuses = unit.ActiveStatuses;
                var processedStatusIds = new List<int>();

                for (int j = 0; j < activeStatuses.Count; j++)
                {
                    var statusId = activeStatuses[j].StatusId;

                    if (ContainsStatusId(processedStatusIds, statusId))
                        continue;

                    if (_configDistributor.Statuses.TryGet(statusId, out var mapper) == false)
                        continue;

                    if (IsDamageOverTime(mapper.StatusType) == false)
                        continue;

                    processedStatusIds.Add(statusId);

                    _tickQueue.Add(new StatusTickEntry(unit, statusId, mapper.TriggerOrder));
                }
            }
        }

        private void SortTickQueueByTriggerOrder()
        {
            var count = _tickQueue.Count;

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count - 1 - i; j++)
                {
                    if (_tickQueue[j].TriggerOrder <= _tickQueue[j + 1].TriggerOrder)
                        continue;

                    (_tickQueue[j + 1], _tickQueue[j]) = (_tickQueue[j], _tickQueue[j + 1]);
                }
            }
        }

        private void DecrementAndExpireStatuses(IReadOnlyList<IUnitState> units, List<BattleStep> steps, int currentTurn)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var unitState = units[i];
                var activeStatuses = unitState.ActiveStatuses;

                for (int j = 0; j < activeStatuses.Count; j++)
                {
                    var activeStatus = activeStatuses[j];
                    activeStatus.DecrementRemainingTicks();
                    activeStatuses[j] = activeStatus;
                }

                for (int j = activeStatuses.Count - 1; 0 <= j; j--)
                {
                    if (0 < activeStatuses[j].RemainingTicks)
                        continue;

                    ExpireStatus(unitState, steps, currentTurn, activeStatuses[j]);

                    activeStatuses.RemoveAt(j);
                }
            }
        }

        private void TickDamageOverTimeGroup(
            IUnitState unitState,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            List<BattleStep> steps,
            int currentTurn,
            ISeededRandomService seededRandomService,
            float cooldown,
            int statusId)
        {
            var characteristics = unitState.CharacteristicState;
            var healthBefore = characteristics.Health;
            var wasAlive = 0f < healthBefore;
            var totalDamage = 0f;
            var anyCritical = false;
            var activeStatuses = unitState.ActiveStatuses;

            for (int i = 0; i < activeStatuses.Count; i++)
            {
                var activeStatus = activeStatuses[i];

                if (activeStatus.StatusId != statusId)
                    continue;

                if (activeStatus.AppliesDamageOverTime == false)
                    continue;

                var stackDamage = CalculateStackDamage(
                    unitState,
                    ownerTeam,
                    opponentTeam,
                    activeStatus,
                    seededRandomService,
                    out var isCritical);
                totalDamage += stackDamage;

                if (isCritical)
                    anyCritical = true;
            }

            if (totalDamage <= 0f)
                return;

            var healthAfter = healthBefore - totalDamage;

            if (healthAfter < 0f)
                healthAfter = 0f;

            characteristics.Health = healthAfter;

            var commands = new List<BattleCommand>
            {
                _battleCommandFactory.TickStatus(unitState.Id, unitState.SlotIndex, statusId, totalDamage),
                _battleCommandFactory.ShowDamage(unitState.Id, unitState.SlotIndex, unitState.Id, unitState.SlotIndex, totalDamage, anyCritical, false),
                _battleCommandFactory.SetHp(unitState.Id, unitState.SlotIndex, healthAfter),
                _battleCommandFactory.Wait(cooldown),
            };

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.StatusTrigger,
                unitState,
                commands,
                unitState);

            _logger.LogDebug($"[Story][Battle]: Status aggregate tick, unitId = {unitState.Id}, statusId = {statusId}, damage = {totalDamage}, isCritical = {anyCritical}, health = {healthAfter}");

            NotifyDamageOverTimeSource(unitState, ownerTeam, opponentTeam, steps, currentTurn, seededRandomService, statusId);

            if (wasAlive == false || 0f < healthAfter)
                return;

            EmitDeath(steps, currentTurn, unitState);
        }

        private void NotifyDamageOverTimeSource(
            IUnitState unitState,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            List<BattleStep> steps,
            int currentTurn,
            ISeededRandomService seededRandomService,
            int statusId)
        {
            var sourceUnitId = FindDamageOverTimeSourceUnitId(unitState, statusId);

            if (sourceUnitId < 0)
            {
                _logger.LogDebug($"[Story][Battle]: Any_damage skip status tick; source missing statusId = {statusId}, targetId = {unitState.Id}");

                return;
            }

            if (TryFindUnit(opponentTeam, sourceUnitId, out var sourceUnit))
            {
                _battlePerkSimulator.NotifyAnyDamage(sourceUnit, opponentTeam, ownerTeam, steps, currentTurn, seededRandomService);

                return;
            }

            if (TryFindUnit(ownerTeam, sourceUnitId, out sourceUnit))
                _battlePerkSimulator.NotifyAnyDamage(sourceUnit, ownerTeam, opponentTeam, steps, currentTurn, seededRandomService);
        }

        private int FindDamageOverTimeSourceUnitId(IUnitState unitState, int statusId)
        {
            var activeStatuses = unitState.ActiveStatuses;

            for (int i = 0; i < activeStatuses.Count; i++)
            {
                var activeStatus = activeStatuses[i];

                if (activeStatus.StatusId != statusId)
                    continue;

                if (activeStatus.AppliesDamageOverTime == false)
                    continue;

                if (activeStatus.SourceUnitId < 0)
                    continue;

                return activeStatus.SourceUnitId;
            }

            return -1;
        }

        private static bool TryFindUnit(ITeamSimulationState team, int unitId, [MaybeNullWhen(false)] out IUnitState unit)
        {
            var mainUnits = team.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                if (mainUnits[i].Id != unitId)
                    continue;

                unit = mainUnits[i];

                return true;
            }

            var summons = team.Summons;

            for (int i = 0; i < summons.Count; i++)
            {
                if (summons[i].Id != unitId)
                    continue;

                unit = summons[i];

                return true;
            }

            unit = null;

            return false;
        }

        private float CalculateStackDamage(
            IUnitState unitState,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            ActiveStatus activeStatus,
            ISeededRandomService seededRandomService,
            out bool isCritical)
        {
            isCritical = false;

            ResolveLiveSourceStats(
                unitState,
                ownerTeam,
                opponentTeam,
                activeStatus,
                out var sourceDamage,
                out var criticalChance,
                out var criticalMultiplier);

            var tickDamageBase = sourceDamage * activeStatus.DamageRatio;

            if (tickDamageBase <= 0f)
                return 0f;

            var defenceFactor = 1f - unitState.CharacteristicState.Defence;

            if (defenceFactor < 0f)
                defenceFactor = 0f;

            var damage = tickDamageBase * defenceFactor;
            var criticalRoll = seededRandomService.GetRandomValue();
            isCritical = criticalRoll < criticalChance;

            if (isCritical)
                damage *= criticalMultiplier;

            _logger.LogDebug($"[Story][Battle]: Damage over time stack, unitId = {unitState.Id}, statusId = {activeStatus.StatusId}, sourceId = {activeStatus.SourceUnitId}, sourceDamage = {sourceDamage}, damageRatio = {activeStatus.DamageRatio}, tickDamageBase = {tickDamageBase}, criticalRoll = {criticalRoll}, isCritical = {isCritical}, damage = {damage}");

            return damage;
        }

        private void ResolveLiveSourceStats(
            IUnitState bearer,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            ActiveStatus activeStatus,
            out float sourceDamage,
            out float criticalChance,
            out float criticalMultiplier)
        {
            sourceDamage = 0f;
            criticalChance = 0f;
            criticalMultiplier = 1f;

            if (0 <= activeStatus.SourceUnitId)
            {
                if (TryFindUnit(ownerTeam, activeStatus.SourceUnitId, out var sourceUnit)
                    || TryFindUnit(opponentTeam, activeStatus.SourceUnitId, out sourceUnit))
                {
                    var sourceCharacteristics = sourceUnit.CharacteristicState;
                    sourceDamage = sourceCharacteristics.Damage;
                    criticalChance = sourceCharacteristics.CriticalChance;
                    criticalMultiplier = sourceCharacteristics.CriticalMultiplier;

                    return;
                }

                _logger.LogWarning($"[Story][Battle]: Damage over time source missing, sourceId = {activeStatus.SourceUnitId}, bearerId = {bearer.Id}, statusId = {activeStatus.StatusId}; fallback bearer");
            }
            else
            {
                _logger.LogWarning($"[Story][Battle]: Damage over time source unset, bearerId = {bearer.Id}, statusId = {activeStatus.StatusId}; fallback bearer");
            }

            var bearerCharacteristics = bearer.CharacteristicState;
            sourceDamage = bearerCharacteristics.Damage;
            criticalChance = bearerCharacteristics.CriticalChance;
            criticalMultiplier = bearerCharacteristics.CriticalMultiplier;
        }

        private void ExpireStatus(
            IUnitState unitState,
            List<BattleStep> steps,
            int currentTurn,
            ActiveStatus activeStatus)
        {
            var remainingStacks = CountStacks(unitState.ActiveStatuses, activeStatus.StatusId) - 1;

            if (remainingStacks < 0)
                remainingStacks = 0;

            var commands = new List<BattleCommand>
            {
                _battleCommandFactory.RemoveStatus(unitState.Id, unitState.SlotIndex, activeStatus.StatusId),
            };

            if (activeStatus.AppliesBonuses)
                _battleBonusService.RemoveBySourceKey(unitState, activeStatus.BonusSourceKey, commands, currentTurn);

            if (0 < remainingStacks)
            {
                var remainingDurationTurns = FindMaxRemainingTicks(unitState.ActiveStatuses, activeStatus.StatusId);

                commands.Add(
                    _battleCommandFactory.ApplyStatus(
                        unitState.Id,
                        unitState.SlotIndex,
                        unitState.Id,
                        unitState.SlotIndex,
                        activeStatus.StatusId,
                        remainingStacks,
                        remainingDurationTurns));
            }

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.StatusTrigger,
                unitState,
                commands,
                unitState);

            _logger.LogDebug($"[Story][Battle]: Expire status, unitId = {unitState.Id}, statusId = {activeStatus.StatusId}, remainingStacks = {remainingStacks}");
        }

        private void EmitDeath(List<BattleStep> steps, int currentTurn, IUnitState unit)
        {
            if (_battlePerkSimulator.TryResurrectOnDeath(unit, steps, currentTurn))
                return;

            _logger.LogDebug($"[Story][Battle]: Death unitId = {unit.Id}, turn = {currentTurn}");

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.Death,
                unit,
                new List<BattleCommand>
                {
                    _battleCommandFactory.KillUnit(unit.Id, unit.SlotIndex),
                    _battleCommandFactory.SetHp(unit.Id, unit.SlotIndex, 0f),
                });
        }

        private float GetStatusesCooldown()
        {
            if (_configDistributor.Constants.TryGet(ConstantKeys.StatusesCooldownKey, out var constant) == false)
            {
                _logger.LogError($"[Story][Battle]: Constant missing key = {ConstantKeys.StatusesCooldownKey}");

                return 0f;
            }

            return float.Parse(constant.ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
        }

        private bool IsDamageOverTime(StatusType statusType)
        {
            return statusType == StatusType.Burning
                || statusType == StatusType.BurningStrong
                || statusType == StatusType.Poison
                || statusType == StatusType.PoisonStrong;
        }

        private bool HasStatusBefore(List<ActiveStatus> activeStatuses, int statusId, int index)
        {
            for (int i = 0; i < index; i++)
            {
                var activeStatus = activeStatuses[i];

                if (activeStatus.StatusId == statusId)
                    return true;
            }

            return false;
        }

        private bool ContainsStatusId(List<int> statusIds, int statusId)
        {
            for (int i = 0; i < statusIds.Count; i++)
                if (statusIds[i] == statusId)
                    return true;

            return false;
        }

        private int CountStacks(List<ActiveStatus> activeStatuses, int statusId)
        {
            var stacks = 0;

            for (int i = 0; i < activeStatuses.Count; i++)
            {
                var activeStatus = activeStatuses[i];

                if (activeStatus.StatusId == statusId)
                    stacks += 1;
            }

            return stacks;
        }

        private int FindMaxRemainingTicks(List<ActiveStatus> activeStatuses, int statusId)
        {
            var maxTicks = 0;

            for (int i = 0; i < activeStatuses.Count; i++)
            {
                var activeStatus = activeStatuses[i];

                if (activeStatus.StatusId != statusId)
                    continue;

                var remainingTicks = activeStatus.RemainingTicks;

                if (remainingTicks <= maxTicks)
                    continue;

                maxTicks = remainingTicks;
            }

            return maxTicks;
        }

        private readonly struct StatusTickEntry
        {
            private readonly IUnitState _unit;
            private readonly int _statusId;
            private readonly int _triggerOrder;

            internal IUnitState Unit => _unit;

            internal int StatusId => _statusId;

            internal int TriggerOrder => _triggerOrder;

            internal StatusTickEntry(IUnitState unit, int statusId, int triggerOrder)
            {
                _unit = unit;
                _statusId = statusId;
                _triggerOrder = triggerOrder;
            }
        }
    }
}
