using Microsoft.Extensions.Logging;
using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleSummonSimulator : IBattleSummonSimulator
    {
        private readonly ILogger<BattleSummonSimulator> _logger;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly IBattleSkillSimulator _battleSkillSimulator;
        private readonly IConfigDistributor _configDistributor;
        private readonly List<IUnitState> _summonOrderBuffer = new();
        private readonly float _battleFlytextTimer;

        public BattleSummonSimulator(
            ILogger<BattleSummonSimulator> logger,
            IBattleCommandFactory battleCommandFactory,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleScriptBuilder battleScriptBuilder,
            IBattleSkillSimulator battleSkillSimulator,
            IConfigDistributor configDistributor)
        {
            _logger = logger;
            _battleCommandFactory = battleCommandFactory;
            _battlePerkSimulator = battlePerkSimulator;
            _battleScriptBuilder = battleScriptBuilder;
            _battleSkillSimulator = battleSkillSimulator;
            _configDistributor = configDistributor;
            _battleFlytextTimer = GetConstant(ConstantKeys.BattleFlytextTimerKey);
        }

        public void EmitInitialSpawns(List<BattleStep> steps, ITeamSimulationState team, int currentTurn)
        {
            var summons = team.Summons;

            for (int i = 0; i < summons.Count; i++)
            {
                var summon = summons[i];

                _battleScriptBuilder.Add(
                    steps,
                    currentTurn,
                    BattlePhaseType.Unknown,
                    summon,
                    new List<BattleCommand>
                    {
                        _battleCommandFactory.SpawnUnit(summon.Id, summon.SlotIndex),
                    });

                _logger.LogDebug($"[Story][Battle]: Spawn summon, unitId = {summon.Id}, slot = {summon.SlotIndex}, side = {team.BattleSide}");
            }
        }

        public void Simulate(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            BuildOrderedLivingSummons(attacker.Summons);

            if (_summonOrderBuffer.Count == 0)
                return;

            var critSourceIndex = FindCritSourceMainIndex(attacker);

            if (critSourceIndex < 0)
            {
                _logger.LogWarning($"[Story][Battle]: Summon phase skipped no living main, side = {attacker.BattleSide}, turn = {currentTurn}");

                return;
            }

            var critSource = attacker.MainUnits[critSourceIndex];
            var cooldown = GetConstant(ConstantKeys.SummonsCooldownKey);

            _logger.LogDebug($"[Story][Battle]: Summon phase, side = {attacker.BattleSide}, turn = {currentTurn}, summons = {_summonOrderBuffer.Count}, critSourceId = {critSource.Id}");

            for (int i = 0; i < _summonOrderBuffer.Count; i++)
            {
                if (_battlePerkSimulator.ShouldAbortRemainingTurn)
                {
                    _logger.LogDebug($"[Story][Battle]: Summon phase abort remaining turn, side = {attacker.BattleSide}, turn = {currentTurn}");

                    return;
                }

                if (HasAliveMainUnits(defender) == false)
                    return;

                var summon = _summonOrderBuffer[i];

                if (summon.IsAlive() == false)
                    continue;

                _battleSkillSimulator.SimulateSummonSkills(
                    steps,
                    summon,
                    attacker,
                    defender,
                    currentTurn,
                    seededRandomService);

                if (summon.IsAlive() == false || HasAliveMainUnits(defender) == false)
                    return;

                var targetIndex = FindTargetIndex(defender);

                if (targetIndex < 0)
                    return;

                var target = defender.MainUnits[targetIndex];

                ExecuteSummonAttack(
                    steps,
                    attacker,
                    defender,
                    summon,
                    target,
                    critSource,
                    currentTurn,
                    seededRandomService);

                if (target.IsAlive() == false)
                    EmitDeath(steps, currentTurn, target);

                EmitSummonCooldown(steps, summon, currentTurn, cooldown);
            }
        }

        private void ExecuteSummonAttack(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            IUnitState summon,
            IUnitState target,
            IUnitState critSource,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            var isRanged = (summon.Flags & UnitFlags.Range) != 0;
            var commands = new List<BattleCommand>();

            if (isRanged == false)
                commands.Add(_battleCommandFactory.Approach(summon.Id, summon.SlotIndex, target.Id, target.SlotIndex, true));

            commands.Add(_battleCommandFactory.PlayAnimation(summon.Id, summon.SlotIndex, "attack"));

            var targetCharacteristics = target.CharacteristicState;
            var evasionRoll = seededRandomService.GetRandomValue();
            var isEvaded = evasionRoll < targetCharacteristics.Evasion;

            _logger.LogDebug($"[Story][Battle]: Summon attack, unitId = {summon.Id}, slot = {summon.SlotIndex}, targetId = {target.Id}, evasionRoll = {evasionRoll}, evasion = {targetCharacteristics.Evasion}, isEvaded = {isEvaded}, critSourceId = {critSource.Id}");

            var dealtAnyDamage = false;

            if (isEvaded)
            {
                commands.Add(_battleCommandFactory.ShowMiss(summon.Id, summon.SlotIndex, target.Id, target.SlotIndex));

                if (0f < _battleFlytextTimer)
                {
                    commands.Add(_battleCommandFactory.Wait(_battleFlytextTimer));

                    _logger.LogDebug($"[Story][Battle]: Summon miss wait, unitId = {summon.Id}, seconds = {_battleFlytextTimer}");
                }
            }
            else
            {
                var critSourceCharacteristics = critSource.CharacteristicState;
                var criticalRoll = seededRandomService.GetRandomValue();
                var isCritical = criticalRoll < critSourceCharacteristics.CriticalChance;

                _logger.LogDebug($"[Story][Battle]: Summon crit roll, unitId = {summon.Id}, criticalRoll = {criticalRoll}, criticalChance = {critSourceCharacteristics.CriticalChance}, isCritical = {isCritical}, critSourceId = {critSource.Id}");

                var defenceFactor = 1f - targetCharacteristics.Defence;

                if (defenceFactor < 0f)
                    defenceFactor = 0f;

                var damage = summon.CharacteristicState.Damage;

                if (isCritical)
                    damage *= critSourceCharacteristics.CriticalMultiplier;

                damage *= defenceFactor;

                var healthAfter = targetCharacteristics.Health - damage;

                if (healthAfter < 0f)
                    healthAfter = 0f;

                targetCharacteristics.Health = healthAfter;

                commands.Add(_battleCommandFactory.ShowDamage(summon.Id, summon.SlotIndex, target.Id, target.SlotIndex, damage, isCritical, false));
                commands.Add(_battleCommandFactory.SetHp(target.Id, target.SlotIndex, healthAfter));

                _logger.LogInformation($"[Story][Battle]: Summon damage, unitId = {summon.Id}, slot = {summon.SlotIndex}, targetId = {target.Id}, damage = {damage}, isCritical = {isCritical}, health = {healthAfter}, critSourceId = {critSource.Id}");

                dealtAnyDamage = true;

                _logger.LogDebug($"[Story][Battle]: Any_damage deferred until summon step commit, unitId = {summon.Id}, targetId = {target.Id}, turn = {currentTurn}");
            }

            if (isRanged == false)
                commands.Add(_battleCommandFactory.ReturnToPosition(summon.Id, summon.SlotIndex));

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.SummonAttack,
                summon,
                commands,
                target);

            if (dealtAnyDamage)
            {
                _logger.LogDebug($"[Story][Battle]: Any_damage flush after summon step, unitId = {summon.Id}, targetId = {target.Id}, turn = {currentTurn}");

                _battlePerkSimulator.NotifyAnyDamage(summon, attacker, defender, steps, currentTurn, seededRandomService);
            }
        }

        private void EmitSummonCooldown(List<BattleStep> steps, IUnitState summon, int currentTurn, float cooldown)
        {
            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.SummonCooldown,
                summon,
                new List<BattleCommand>
                {
                    _battleCommandFactory.Wait(cooldown),
                });
        }

        private void EmitDeath(List<BattleStep> steps, int currentTurn, IUnitState unit)
        {
            if (_battlePerkSimulator.TryResurrectOnDeath(unit, steps, currentTurn))
                return;

            _logger.LogDebug($"[Story][Battle]: Death, unitId = {unit.Id}, turn = {currentTurn}");

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

        private void BuildOrderedLivingSummons(IReadOnlyList<IUnitState> summons)
        {
            _summonOrderBuffer.Clear();

            for (int i = 0; i < summons.Count; i++)
            {
                var summon = summons[i];

                if (summon.IsAlive() == false)
                    continue;

                _summonOrderBuffer.Add(summon);
            }

            var count = _summonOrderBuffer.Count;

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count - 1 - i; j++)
                {
                    if (_summonOrderBuffer[j].SlotIndex <= _summonOrderBuffer[j + 1].SlotIndex)
                        continue;

                    (_summonOrderBuffer[j + 1], _summonOrderBuffer[j]) = (_summonOrderBuffer[j], _summonOrderBuffer[j + 1]);
                }
            }
        }

        private int FindCritSourceMainIndex(ITeamSimulationState attacker)
        {
            var mainUnits = attacker.MainUnits;
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

            return selectedIndex;
        }

        private int FindTargetIndex(ITeamSimulationState defender)
        {
            var mainUnits = defender.MainUnits;
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

            return selectedIndex;
        }

        private bool HasAliveMainUnits(ITeamSimulationState state)
        {
            var mainUnits = state.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                var mainUnit = mainUnits[i];

                if (mainUnit.IsAlive())
                    return true;
            }

            return false;
        }

        private float GetConstant(string constantKey)
        {
            if (_configDistributor.Constants.TryGet(constantKey, out var constant) == false)
            {
                _logger.LogError($"[Error][Story][Battle]: Constant missing key = {constantKey}");

                throw new InvalidOperationException($"[Error][Story][Battle]: Constant missing key = {constantKey}");
            }

            return float.Parse(constant.ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
