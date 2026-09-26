using Microsoft.Extensions.Logging;
using Server.Bonuses;
using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleBonusService : IBattleBonusService
    {
        private readonly ILogger<BattleBonusService> _logger;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly ICharacteristicBucketApplicator _characteristicBucketApplicator;
        private readonly ICharacteristicCalculator _characteristicCalculator;
        private readonly IConfigDistributor _configDistributor;

        public BattleBonusService(
            ILogger<BattleBonusService> logger,
            IBattleCommandFactory battleCommandFactory,
            IBonusWorkModeParser bonusWorkModeParser,
            ICharacteristicBucketApplicator characteristicBucketApplicator,
            ICharacteristicCalculator characteristicCalculator,
            IConfigDistributor configDistributor)
        {
            _logger = logger;
            _battleCommandFactory = battleCommandFactory;
            _bonusWorkModeParser = bonusWorkModeParser;
            _characteristicBucketApplicator = characteristicBucketApplicator;
            _characteristicCalculator = characteristicCalculator;
            _configDistributor = configDistributor;
        }

        public void Grant(
            IUnitState unit,
            int bonusId,
            int count,
            string sourceKey,
            List<BattleCommand> commands,
            int currentTurn)
        {
            if (count == 0 || bonusId <= 0)
                return;

            if (_configDistributor.Bonuses.TryGet(bonusId, out var bonusMapper) == false)
            {
                _logger.LogError($"[Story][Battle]: Bonus missing, id = {bonusId}");

                throw new InvalidOperationException($"[Story][Battle]: Bonus missing, id = {bonusId}");
            }

            if (bonusMapper.BonusType == BonusType.Healing || bonusMapper.BonusType == BonusType.HealingFromMax)
            {
                ApplyHealingEffect(unit, bonusMapper, count, commands);

                return;
            }

            if (bonusMapper.BonusType == BonusType.CurrentHealthLocal)
            {
                ApplyCurrentHealthLocal(unit, bonusMapper.BonusValue * count, bonusMapper.OperatorType, commands);

                return;
            }

            if (_bonusWorkModeParser.TryParse(bonusMapper.WorkModeParameters, out var workMode) == false)
            {
                _logger.LogError($"[Story][Battle]: work_mode parse failed, bonusId = {bonusId}");

                return;
            }

            RemoveBonusesWithExactSourceKey(unit, sourceKey);

            var activeBonus = new ActiveBattleBonus(
                bonusId,
                count,
                bonusMapper.BonusType,
                bonusMapper.BonusValue,
                bonusMapper.OperatorType,
                workMode,
                sourceKey);

            unit.ActiveBonuses.Add(activeBonus);

            if (workMode.Contains(BonusWorkModeKind.NextBattles))
                _logger.LogDebug($"[Story][Battle]: Bonus next_battles grant, unitId = {unit.Id}, bonusId = {bonusId}, remainingBattles = {activeBonus.RemainingBattles}");

            _logger.LogDebug($"[Story][Battle]: Bonus grant, unitId = {unit.Id}, bonusId = {bonusId}, count = {count}, type = {bonusMapper.BonusType}, operator = {bonusMapper.OperatorType}, workMode = {workMode.Format()}, sourceKey = {sourceKey}");

            Rebuild(unit, currentTurn, commands, true);
        }

        public void GrantRewardBonuses(
            IUnitState unit,
            IReadOnlyList<RewardBonus> bonuses,
            int stacksMultiplier,
            string sourceKey,
            List<BattleCommand> commands,
            int currentTurn)
        {
            if (bonuses.Count == 0 || stacksMultiplier <= 0)
                return;

            for (int i = 0; i < bonuses.Count; i++)
            {
                var rewardBonus = bonuses[i];
                var layeredSourceKey = $"{sourceKey}:{rewardBonus.BonusId}:{i}";

                Grant(unit, rewardBonus.BonusId, rewardBonus.Count * stacksMultiplier, layeredSourceKey, commands, currentTurn);
            }
        }

        public void RemoveBySourceKey(
            IUnitState unit,
            string sourceKey,
            List<BattleCommand> commands,
            int currentTurn)
        {
            var activeBonuses = unit.ActiveBonuses;
            var removedBonusIds = new List<int>();
            var removed = 0;

            for (int i = activeBonuses.Count - 1; 0 <= i; i--)
            {
                if (string.Equals(activeBonuses[i].SourceKey, sourceKey, StringComparison.Ordinal) == false
                    && activeBonuses[i].SourceKey.StartsWith(sourceKey + ":", StringComparison.Ordinal) == false)
                    continue;

                var bonusId = activeBonuses[i].BonusId;

                _logger.LogDebug($"[Story][Battle]: Bonus remove, unitId = {unit.Id}, bonusId = {bonusId}, sourceKey = {activeBonuses[i].SourceKey}");

                if (ContainsBonusId(removedBonusIds, bonusId) == false)
                    removedBonusIds.Add(bonusId);

                activeBonuses.RemoveAt(i);

                ++removed;
            }

            if (removed == 0)
                return;

            Rebuild(unit, currentTurn, commands, true);
            EmitClearedBonusCommands(unit, removedBonusIds, currentTurn, commands);
        }

        public void OnTurnStart(IUnitState unit, int currentTurn, List<BattleCommand> commands)
        {
            var activeBonuses = unit.ActiveBonuses;
            var changed = false;

            for (int i = 0; i < activeBonuses.Count; i++)
            {
                var activeBonus = activeBonuses[i];

                if (activeBonus.WorkMode.Contains(BonusWorkModeKind.EveryTurn) == false)
                    continue;

                ++activeBonus.EveryTurnStacks;

                changed = true;

                _logger.LogDebug($"[Story][Battle]: Bonus every_turn stack, unitId = {unit.Id}, bonusId = {activeBonus.BonusId}, everyTurnStacks = {activeBonus.EveryTurnStacks}, turn = {currentTurn}");
            }

            if (changed == false && HasTurnScopedBonus(activeBonuses) == false)
                return;

            Rebuild(unit, currentTurn, commands, true);
        }

        public void OnBattleEnd(IUnitState unit, List<BattleCommand> commands)
        {
            var activeBonuses = unit.ActiveBonuses;
            var removed = 0;
            var nextBattlesChanged = false;

            for (int i = activeBonuses.Count - 1; 0 <= i; i--)
            {
                var activeBonus = activeBonuses[i];
                var workMode = activeBonus.WorkMode;

                if (workMode.Contains(BonusWorkModeKind.EndOfBattle))
                {
                    _logger.LogDebug($"[Story][Battle]: Bonus battle-end remove, unitId = {unit.Id}, bonusId = {activeBonus.BonusId}, workMode = {workMode.Format()}");

                    commands.Add(_battleCommandFactory.SetBonus(unit.Id, unit.SlotIndex, activeBonus.BonusId, 0f, unit.Id));
                    activeBonuses.RemoveAt(i);

                    ++removed;

                    continue;
                }

                if (workMode.Contains(BonusWorkModeKind.NextBattles))
                {
                    activeBonus.RemainingBattles -= 1;
                    nextBattlesChanged = true;

                    _logger.LogDebug($"[Story][Battle]: Bonus next_battles decrement, unitId = {unit.Id}, bonusId = {activeBonus.BonusId}, remainingBattles = {activeBonus.RemainingBattles}");

                    if (0 < activeBonus.RemainingBattles)
                    {
                        if (workMode.Contains(BonusWorkModeKind.EveryTurn))
                            activeBonus.EveryTurnStacks = 0;

                        continue;
                    }

                    commands.Add(_battleCommandFactory.SetBonus(unit.Id, unit.SlotIndex, activeBonus.BonusId, 0f, unit.Id));
                    activeBonuses.RemoveAt(i);

                    ++removed;

                    continue;
                }

                if (workMode.Contains(BonusWorkModeKind.EveryTurn) == false)
                    continue;

                activeBonus.EveryTurnStacks = 0;
                nextBattlesChanged = true;
            }

            if (removed == 0 && nextBattlesChanged == false)
                return;

            Rebuild(unit, int.MaxValue, commands, true);
        }

        public void Rebuild(IUnitState unit, int currentTurn, List<BattleCommand> commands, bool emitSetBonusCommands)
        {
            var buckets = unit.BaseBuckets.Clone();
            var replaceOverrides = new List<ReplaceOverride>();
            var activeBonuses = unit.ActiveBonuses;

            for (int i = 0; i < activeBonuses.Count; i++)
            {
                var activeBonus = activeBonuses[i];

                if (IsActiveForTurn(unit, activeBonus, currentTurn) == false)
                    continue;

                var totalCount = activeBonus.Count;

                if (activeBonus.WorkMode.Contains(BonusWorkModeKind.EveryTurn))
                    totalCount = activeBonus.Count * Math.Max(activeBonus.EveryTurnStacks, 1);

                var totalValue = activeBonus.BonusValue * totalCount;

                if (activeBonus.OperatorType == BonusOperatorType.Replace)
                {
                    replaceOverrides.Add(new ReplaceOverride(activeBonus.BonusType, totalValue));

                    continue;
                }

                _characteristicBucketApplicator.Apply(buckets, activeBonus.BonusType, totalValue);
            }

            var healthBeforeRebuild = unit.CharacteristicState.Health;

            _characteristicCalculator.ApplyToState(buckets, unit.CharacteristicState, replaceOverrides, true, false);

            if (emitSetBonusCommands == false)
                return;

            EmitHealthSyncIfChanged(unit, healthBeforeRebuild, commands);
            EmitAggregatedSetBonusCommands(unit, currentTurn, commands, activeBonuses);
        }

        private void EmitHealthSyncIfChanged(IUnitState unit, float healthBeforeRebuild, List<BattleCommand> commands)
        {
            var healthAfterRebuild = unit.CharacteristicState.Health;

            if (MathF.Abs(healthAfterRebuild - healthBeforeRebuild) <= 0.0001f)
                return;

            commands.Add(_battleCommandFactory.SetHp(unit.Id, unit.SlotIndex, healthAfterRebuild));
            _logger.LogDebug($"[Story][Battle]: Bonus rebuild health sync, unitId = {unit.Id}, healthBefore = {healthBeforeRebuild}, healthAfter = {healthAfterRebuild}, maxHealth = {unit.CharacteristicState.MaxHealth}");
        }

        private static void RemoveBonusesWithExactSourceKey(IUnitState unit, string sourceKey)
        {
            if (string.IsNullOrEmpty(sourceKey))
                return;

            var activeBonuses = unit.ActiveBonuses;

            for (int i = activeBonuses.Count - 1; 0 <= i; i--)
            {
                if (string.Equals(activeBonuses[i].SourceKey, sourceKey, StringComparison.Ordinal) == false)
                    continue;

                activeBonuses.RemoveAt(i);
            }
        }

        private void EmitAggregatedSetBonusCommands(
            IUnitState unit,
            int currentTurn,
            List<BattleCommand> commands,
            List<ActiveBattleBonus> activeBonuses)
        {
            var emittedBonusIds = new List<int>();

            for (int i = 0; i < activeBonuses.Count; i++)
            {
                var bonusId = activeBonuses[i].BonusId;

                if (ContainsBonusId(emittedBonusIds, bonusId))
                    continue;

                emittedBonusIds.Add(bonusId);

                var appliedValue = 0f;
                var hasActiveEntry = false;
                var hasInactivePresentationClear = false;

                for (int j = 0; j < activeBonuses.Count; j++)
                {
                    var activeBonus = activeBonuses[j];

                    if (activeBonus.BonusId != bonusId)
                        continue;

                    if (IsActiveForTurn(unit, activeBonus, currentTurn) == false)
                    {
                        if (activeBonus.WorkMode.Contains(BonusWorkModeKind.FirstTurns)
                            || activeBonus.WorkMode.Contains(BonusWorkModeKind.IfEquipped))
                            hasInactivePresentationClear = true;

                        continue;
                    }

                    hasActiveEntry = true;
                    var totalCount = activeBonus.Count;

                    if (activeBonus.WorkMode.Contains(BonusWorkModeKind.EveryTurn))
                        totalCount = activeBonus.Count * Math.Max(activeBonus.EveryTurnStacks, 1);

                    appliedValue += activeBonus.BonusValue * totalCount;
                }

                if (hasActiveEntry == false)
                {
                    if (hasInactivePresentationClear)
                        commands.Add(_battleCommandFactory.SetBonus(unit.Id, unit.SlotIndex, bonusId, 0f, unit.Id));

                    continue;
                }

                commands.Add(_battleCommandFactory.SetBonus(unit.Id, unit.SlotIndex, bonusId, appliedValue, unit.Id));
            }
        }

        private void ApplyHealingEffect(IUnitState unit, IBonusMapper bonusMapper, int count, List<BattleCommand> commands)
        {
            var characteristics = unit.CharacteristicState;
            var value = bonusMapper.BonusValue * count;
            float healDelta;

            if (bonusMapper.BonusType == BonusType.HealingFromMax)
                healDelta = _characteristicCalculator.CalculateHealingFromMax(characteristics.MaxHealth, value, characteristics.HealingBoost);
            else
                healDelta = _characteristicCalculator.CalculateHealingFromCurrent(characteristics.Health, value, characteristics.HealingBoost);

            characteristics.Health += healDelta;

            if (characteristics.Health < 0f)
                characteristics.Health = 0f;

            if (characteristics.MaxHealth < characteristics.Health)
                characteristics.Health = characteristics.MaxHealth;

            commands.Add(_battleCommandFactory.SetBonus(unit.Id, unit.SlotIndex, bonusMapper.Id, value, unit.Id));
            commands.Add(_battleCommandFactory.ShowHeal(unit.Id, unit.SlotIndex, unit.Id, unit.SlotIndex, healDelta));
            commands.Add(_battleCommandFactory.SetHp(unit.Id, unit.SlotIndex, characteristics.Health));

            _logger.LogDebug($"[Story][Battle]: Healing effect, unitId = {unit.Id}, bonusId = {bonusMapper.Id}, type = {bonusMapper.BonusType}, healDelta = {healDelta}, health = {characteristics.Health}");
        }

        private void ApplyCurrentHealthLocal(IUnitState unit, float value, BonusOperatorType operatorType, List<BattleCommand> commands)
        {
            var characteristics = unit.CharacteristicState;

            if (operatorType == BonusOperatorType.Replace)
            {
                characteristics.Health = value;
                _logger.LogDebug($"[Story][Battle]: Current health local replace, unitId = {unit.Id}, value = {value}");
            }
            else
            {
                characteristics.Health += value;
                _logger.LogDebug($"[Story][Battle]: Current health local, unitId = {unit.Id}, delta = {value}, health = {characteristics.Health}");
            }

            if (characteristics.Health < 0f)
                characteristics.Health = 0f;

            if (characteristics.MaxHealth < characteristics.Health)
                characteristics.Health = characteristics.MaxHealth;

            commands.Add(_battleCommandFactory.SetHp(unit.Id, unit.SlotIndex, characteristics.Health));
        }

        private bool IsActiveForTurn(IUnitState unit, ActiveBattleBonus activeBonus, int currentTurn)
        {
            var parts = activeBonus.WorkMode.Parts;

            if (parts.Count == 0)
                return false;

            for (int i = 0; i < parts.Count; i++)
            {
                if (IsPartActive(unit, activeBonus, currentTurn, parts[i]) == false)
                    return false;
            }

            return true;
        }

        private bool IsPartActive(IUnitState unit, ActiveBattleBonus activeBonus, int currentTurn, BonusWorkModePart part)
        {
            var kind = part.Kind;

            if (kind == BonusWorkModeKind.IfEquipped)
            {
                var isEquipped = unit.HasEquippedEntity(part.EquippedEntityType, part.EquippedEntityId);

                if (isEquipped == false)
                    _logger.LogWarning($"[Story][Battle]: if_equipped skip, unitId = {unit.Id}, bonusId = {activeBonus.BonusId}, entityType = {part.EquippedEntityType}, entityId = {part.EquippedEntityId}");

                return isEquipped;
            }

            if (kind == BonusWorkModeKind.NextBattles)
                return 0 < activeBonus.RemainingBattles;

            if (kind == BonusWorkModeKind.FirstTurns)
            {
                var turnLimit = part.Count;

                if (turnLimit <= 0)
                {
                    _logger.LogError($"[Story][Battle]: first_turns count <= 0, bonusId = {activeBonus.BonusId}, turnLimit = {turnLimit}");

                    throw new InvalidOperationException($"[Story][Battle]: first_turns count <= 0, bonusId = {activeBonus.BonusId}");
                }

                return currentTurn < turnLimit;
            }

            if (kind == BonusWorkModeKind.Permanent
                || kind == BonusWorkModeKind.EndOfGame
                || kind == BonusWorkModeKind.EndOfBattle
                || kind == BonusWorkModeKind.EveryTurn)
                return true;

            _logger.LogError($"[Story][Battle]: work_mode kind unhandled, bonusId = {activeBonus.BonusId}, kind = {kind}");

            throw new InvalidOperationException($"[Story][Battle]: work_mode kind unhandled, bonusId = {activeBonus.BonusId}, kind = {kind}");
        }

        private bool HasTurnScopedBonus(List<ActiveBattleBonus> activeBonuses)
        {
            for (int i = 0; i < activeBonuses.Count; i++)
            {
                if (activeBonuses[i].WorkMode.Contains(BonusWorkModeKind.FirstTurns))
                    return true;
            }

            return false;
        }

        private void EmitClearedBonusCommands(
            IUnitState unit,
            List<int> removedBonusIds,
            int currentTurn,
            List<BattleCommand> commands)
        {
            var activeBonuses = unit.ActiveBonuses;

            for (int i = 0; i < removedBonusIds.Count; i++)
            {
                var bonusId = removedBonusIds[i];

                if (HasRemainingActiveBonusId(unit, activeBonuses, bonusId, currentTurn))
                    continue;

                commands.Add(_battleCommandFactory.SetBonus(unit.Id, unit.SlotIndex, bonusId, 0f, unit.Id));

                _logger.LogDebug($"[Story][Battle]: Bonus cleared presentation, unitId = {unit.Id}, bonusId = {bonusId}");
            }
        }

        private bool HasRemainingActiveBonusId(
            IUnitState unit,
            List<ActiveBattleBonus> activeBonuses,
            int bonusId,
            int currentTurn)
        {
            for (int i = 0; i < activeBonuses.Count; i++)
            {
                if (activeBonuses[i].BonusId != bonusId)
                    continue;

                if (IsActiveForTurn(unit, activeBonuses[i], currentTurn) == false)
                    continue;

                return true;
            }

            return false;
        }

        private bool ContainsBonusId(List<int> bonusIds, int bonusId)
        {
            for (int i = 0; i < bonusIds.Count; i++)
                if (bonusIds[i] == bonusId)
                    return true;

            return false;
        }
    }
}
