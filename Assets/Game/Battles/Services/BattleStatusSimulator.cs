using System;
using System.Diagnostics.CodeAnalysis;
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
        private readonly List<IUnitState> _mainsInSlotOrder = new();
        private readonly List<int> _processedStatusIds = new();
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
            FillMainsInSlotOrder(ownerTeam.MainUnits);

            var emittedWaits = 0;
            var cooldownLoaded = false;
            var cooldown = 0f;

            for (int mainIndex = 0; mainIndex < _mainsInSlotOrder.Count; mainIndex++)
            {
                var unit = _mainsInSlotOrder[mainIndex];

                if (unit.IsAlive() == false)
                    continue;

                CollectDamageOverTimeTicks(unit);
                SortTickQueueByTriggerOrder();

                if (_tickQueue.Count == 0)
                    continue;

                if (cooldownLoaded == false)
                {
                    cooldown = GetStatusesCooldown();
                    cooldownLoaded = true;
                }

                _logger.LogDebug($"[Story][Battle]: Status unit queue, side = {ownerTeam.BattleSide}, turn = {currentTurn}, unitId = {unit.Id}, slotIndex = {unit.SlotIndex}, queueSize = {_tickQueue.Count}");

                for (int i = 0; i < _tickQueue.Count; i++)
                {
                    var entry = _tickQueue[i];

                    _battlePerkSimulator.SetActingUnit(entry.Unit);

                    if (entry.Unit.IsAlive() == false || _battlePerkSimulator.ShouldSkipRemainingActions(entry.Unit))
                    {
                        _logger.LogDebug($"[Story][Battle]: Status queue stop remaining, unitId = {entry.Unit.Id}, statusId = {entry.StatusId}, turn = {currentTurn}, remaining = {_tickQueue.Count - i}");

                        break;
                    }

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
                    ++emittedWaits;
                    DecrementAndExpireStatusId(entry.Unit, steps, currentTurn, entry.StatusId);

                    if (entry.Unit.IsAlive() == false || _battlePerkSimulator.ShouldSkipRemainingActions(entry.Unit))
                    {
                        _logger.LogDebug($"[Story][Battle]: Status queue killed unit, unitId = {entry.Unit.Id}, statusId = {entry.StatusId}, turn = {currentTurn}, remainingSkipped = {_tickQueue.Count - i - 1}");

                        break;
                    }
                }
            }

            if (emittedWaits == 0)
            {
                _logger.LogDebug($"[Story][Battle]: Phase wait skippedEmpty phase = statuses, side = {ownerTeam.BattleSide}, turn = {currentTurn}, queueSize = 0, emittedWaits = 0");

                return;
            }

            _logger.LogDebug($"[Story][Battle]: Phase wait phase = statuses, side = {ownerTeam.BattleSide}, turn = {currentTurn}, emittedWaits = {emittedWaits}");
        }

        private void FillMainsInSlotOrder(IReadOnlyList<IUnitState> mains)
        {
            _mainsInSlotOrder.Clear();

            for (int i = 0; i < mains.Count; i++)
                _mainsInSlotOrder.Add(mains[i]);

            var count = _mainsInSlotOrder.Count;

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count - 1 - i; j++)
                {
                    if (_mainsInSlotOrder[j].SlotIndex <= _mainsInSlotOrder[j + 1].SlotIndex)
                        continue;

                    (_mainsInSlotOrder[j + 1], _mainsInSlotOrder[j]) = (_mainsInSlotOrder[j], _mainsInSlotOrder[j + 1]);
                }
            }
        }

        private void CollectDamageOverTimeTicks(IUnitState unit)
        {
            _tickQueue.Clear();
            _processedStatusIds.Clear();

            var activeStatuses = unit.ActiveStatuses;

            for (int j = 0; j < activeStatuses.Count; j++)
            {
                var statusId = activeStatuses[j].StatusId;

                if (ContainsStatusId(_processedStatusIds, statusId))
                    continue;

                if (_configDistributor.Statuses.TryGet(statusId, out var mapper) == false)
                {
                    _logger.LogError($"[Error][Story][Battle]: Status missing in tick queue, statusId = {statusId}, unitId = {unit.Id}");

                    throw new InvalidOperationException($"[Error][Story][Battle]: Status missing in tick queue, statusId = {statusId}, unitId = {unit.Id}");
                }

                if (IsDamageOverTime(mapper.StatusType) == false)
                    continue;

                _processedStatusIds.Add(statusId);
                _tickQueue.Add(new StatusTickEntry(unit, statusId, mapper.TriggerOrder));
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

        private void DecrementAndExpireStatusId(
            IUnitState unitState,
            List<BattleStep> steps,
            int currentTurn,
            int statusId)
        {
            var activeStatuses = unitState.ActiveStatuses;

            for (int j = 0; j < activeStatuses.Count; j++)
            {
                if (activeStatuses[j].StatusId != statusId)
                    continue;

                var activeStatus = activeStatuses[j];
                activeStatus.DecrementRemainingTicks();
                activeStatuses[j] = activeStatus;
            }

            for (int j = activeStatuses.Count - 1; 0 <= j; j--)
            {
                if (activeStatuses[j].StatusId != statusId)
                    continue;

                if (0 < activeStatuses[j].RemainingTicks)
                    continue;

                ExpireStatus(unitState, steps, currentTurn, activeStatuses[j]);
                activeStatuses.RemoveAt(j);
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

            var healthAfter = healthBefore - totalDamage;

            if (healthAfter < 0f)
                healthAfter = 0f;

            characteristics.Health = healthAfter;

            var commands = new List<BattleCommand>
            {
                _battleCommandFactory.TickStatus(unitState.Id, unitState.SlotIndex, statusId, totalDamage),
            };

            if (0f < totalDamage)
                commands.Add(_battleCommandFactory.ShowDamage(unitState.Id, unitState.SlotIndex, unitState.Id, unitState.SlotIndex, totalDamage, anyCritical, false));

            if (healthAfter != healthBefore)
                commands.Add(_battleCommandFactory.SetHp(unitState.Id, unitState.SlotIndex, healthAfter));

            commands.Add(_battleCommandFactory.Wait(cooldown));

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.StatusTrigger,
                unitState,
                commands,
                unitState);

            _logger.LogDebug($"[Story][Battle]: Status aggregate tick, unitId = {unitState.Id}, statusId = {statusId}, damage = {totalDamage}, isCritical = {anyCritical}, health = {healthAfter}, wait = {cooldown}");

            if (0f < totalDamage)
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

            if (TryFindMain(opponentTeam, sourceUnitId, out var sourceUnit))
            {
                _battlePerkSimulator.NotifyAnyDamage(sourceUnit, opponentTeam, ownerTeam, steps, currentTurn, seededRandomService);

                return;
            }

            if (TryFindMain(ownerTeam, sourceUnitId, out sourceUnit))
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

        private bool TryFindMain(ITeamSimulationState team, int unitId, [MaybeNullWhen(false)] out IUnitState unit)
        {
            var mainUnits = team.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                if (mainUnits[i].Id != unitId)
                    continue;

                unit = mainUnits[i];

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
            criticalMultiplier = 0f;

            if (activeStatus.SourceUnitId < 0)
            {
                _logger.LogError($"[Story][Battle]: Damage over time source unset, bearerId = {bearer.Id}, statusId = {activeStatus.StatusId}");

                return;
            }

            if (TryFindMain(ownerTeam, activeStatus.SourceUnitId, out var sourceUnit)
                || TryFindMain(opponentTeam, activeStatus.SourceUnitId, out sourceUnit))
            {
                var sourceCharacteristics = sourceUnit.CharacteristicState;
                sourceDamage = sourceCharacteristics.Damage;
                criticalChance = sourceCharacteristics.CriticalChance;
                criticalMultiplier = sourceCharacteristics.CriticalMultiplier;

                return;
            }

            _logger.LogError($"[Story][Battle]: Damage over time source main missing, sourceId = {activeStatus.SourceUnitId}, bearerId = {bearer.Id}, statusId = {activeStatus.StatusId}");
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
                _logger.LogError($"[Error][Story][Battle]: Constant missing key = {ConstantKeys.StatusesCooldownKey}");

                throw new InvalidOperationException($"[Error][Story][Battle]: Constant missing key = {ConstantKeys.StatusesCooldownKey}");
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
