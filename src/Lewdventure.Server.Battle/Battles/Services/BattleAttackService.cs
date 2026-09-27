using System;
using Microsoft.Extensions.Logging;
using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleAttackService : IBattleAttackService
    {
        private readonly ILogger<BattleAttackService> _logger;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattleDamageMath _battleDamageMath;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly IBattleSkillSimulator _battleSkillSimulator;
        private readonly float _counterAttackCooldown;
        private readonly float _comboAttackCooldown;
        private readonly float _unitsCooldown;
        private readonly float _battleFlytextTimer;

        public BattleAttackService(
            ILogger<BattleAttackService> logger,
            IBattleCommandFactory battleCommandFactory,
            IBattleDamageMath battleDamageMath,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleScriptBuilder battleScriptBuilder,
            IBattleSkillSimulator battleSkillSimulator,
            IConfigDistributor configDistributor)
        {
            _logger = logger;
            _battleCommandFactory = battleCommandFactory;
            _battleDamageMath = battleDamageMath;
            _battlePerkSimulator = battlePerkSimulator;
            _battleScriptBuilder = battleScriptBuilder;
            _battleSkillSimulator = battleSkillSimulator;
            _counterAttackCooldown = GetConstant(configDistributor, ConstantKeys.CounterAttackCooldownKey);
            _comboAttackCooldown = GetConstant(configDistributor, ConstantKeys.ComboAttackCooldownKey);
            _unitsCooldown = GetConstant(configDistributor, ConstantKeys.UnitsCooldownKey);
            _battleFlytextTimer = GetConstant(configDistributor, ConstantKeys.BattleFlytextTimerKey);
        }

        public void SimulateMainUnits(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            var attackers = attacker.MainUnits;

            for (int i = 0; i < attackers.Count; i++)
            {
                var actor = attackers[i];

                turnState.SetActingUnit(actor);

                if (turnState.ShouldSkipRemainingActions(actor))
                {
                    _logger.LogDebug($"[Story][Battle]: Main attack skip aborted unit, unitId = {actor.Id}, turn = {currentTurn}");

                    continue;
                }

                if (actor.IsAlive() == false)
                    continue;

                var targetIndex = FindTargetIndex(defender);

                if (targetIndex < 0)
                    return;

                _battleSkillSimulator.SimulateUnitSkills(
                    steps,
                    actor,
                    attacker,
                    defender,
                    currentTurn,
                    seededRandomService,
                    turnState);

                if (actor.IsAlive() == false)
                    continue;

                if (HasAliveMainUnits(defender) == false)
                    return;

                var skipRemaining = turnState.ShouldSkipRemainingActions(actor);

                if (skipRemaining || actor.CanUseNormalAttack(currentTurn) == false)
                {
                    _logger.LogDebug($"[Story][Battle]: Main skip attack, unitId = {actor.Id}, turn = {currentTurn}, aborted = {skipRemaining}");

                    _battleSkillSimulator.TryCastEnergySkill(steps, actor, attacker, defender, currentTurn, seededRandomService, turnState);

                    continue;
                }

                targetIndex = FindTargetIndex(defender);

                if (targetIndex < 0)
                    return;

                ExecuteNormalAttack(
                    steps,
                    attacker,
                    defender,
                    actor,
                    defender.MainUnits[targetIndex],
                    currentTurn,
                    seededRandomService,
                    turnState);

                if (HasAliveMainUnits(defender) == false)
                    return;
            }
        }

        private void ExecuteNormalAttack(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            IUnitState actor,
            IUnitState target,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            var isMelee = IsMeleeUnit(actor);
            var commands = new List<BattleCommand>();

            if (isMelee)
                commands.Add(_battleCommandFactory.Approach(actor.Id, actor.SlotIndex, target.Id, target.SlotIndex, true));

            AppendWait(commands, _unitsCooldown);

            commands.Add(_battleCommandFactory.PlayAnimation(actor.Id, actor.SlotIndex, "attack"));

            if (isMelee == false)
                commands.Add(_battleCommandFactory.Approach(actor.Id, actor.SlotIndex, target.Id, target.SlotIndex, false));

            var hit = TryResolveStrike(
                commands,
                actor,
                target,
                seededRandomService,
                BattlePhaseType.NormalAttack,
                actor.CharacteristicState.AttackMultiplier,
                turnState);

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.NormalAttack,
                actor,
                commands,
                target);

            _logger.LogDebug($"[Story][Battle]: Normal attack chain decision, actorId = {actor.Id}, targetId = {target.Id}, hit = {hit}");

            if (hit)
            {
                NotifyPerkAction(
                    BattlePerkActionType.Attack,
                    actor,
                    attacker,
                    defender,
                    steps,
                    currentTurn,
                    seededRandomService,
                    turnState);

                var targetDied = target.IsAlive() == false;

                if (targetDied)
                    EmitDeath(steps, currentTurn, target, turnState);

                _battleSkillSimulator.ApplyEnergyGain(steps, actor, currentTurn);

                if (turnState.ShouldSkipRemainingActions(actor))
                {
                    EmitReturnToPosition(steps, actor, currentTurn, isMelee);
                    _battleSkillSimulator.TryCastEnergySkill(steps, actor, attacker, defender, currentTurn, seededRandomService, turnState);

                    return;
                }

                if (targetDied)
                {
                    EmitReturnToPosition(steps, actor, currentTurn, isMelee);
                    _battleSkillSimulator.TryCastEnergySkill(steps, actor, attacker, defender, currentTurn, seededRandomService, turnState);

                    return;
                }

                if (actor.IsAlive() && target.IsAlive())
                {
                    _logger.LogDebug($"[Story][Battle]: Post-attack chain start, actorId = {actor.Id}, targetId = {target.Id}, turn = {currentTurn}");

                    ExecutePostAttackChain(steps, attacker, defender, actor, target, currentTurn, seededRandomService, turnState);
                }
            }
            else
            {
                _logger.LogDebug($"[Story][Battle]: Post-attack chain skip miss, actorId = {actor.Id}, targetId = {target.Id}, turn = {currentTurn}");
            }

            EmitReturnToPosition(steps, actor, currentTurn, isMelee);

            _battleSkillSimulator.TryCastEnergySkill(steps, actor, attacker, defender, currentTurn, seededRandomService, turnState);
        }

        private void ExecutePostAttackChain(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            IUnitState actor,
            IUnitState target,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            TryCounterAttack(steps, attacker, defender, target, actor, currentTurn, seededRandomService, turnState);

            if (turnState.ShouldSkipRemainingActions(actor))
                return;

            if (actor.IsAlive() == false || target.IsAlive() == false)
                return;

            var combo1Hit = TryComboAttack(steps, attacker, defender, actor, target, currentTurn, seededRandomService, 1, turnState);

            _logger.LogDebug($"[Story][Battle]: Combo1 gate, actorId = {actor.Id}, targetId = {target.Id}, combo1Hit = {combo1Hit}");

            if (turnState.ShouldSkipRemainingActions(actor))
                return;

            if (combo1Hit == false)
            {
                _logger.LogDebug($"[Story][Battle]: Combo2 skip, actorId = {actor.Id}, targetId = {target.Id}, reason = combo1MissOrNoRoll");

                return;
            }

            TryCounterAttack(steps, attacker, defender, target, actor, currentTurn, seededRandomService, turnState);

            if (turnState.ShouldSkipRemainingActions(actor))
                return;

            if (actor.IsAlive() == false || target.IsAlive() == false)
                return;

            if (TryComboAttack(steps, attacker, defender, actor, target, currentTurn, seededRandomService, 2, turnState) == false)
            {
                _logger.LogDebug($"[Story][Battle]: Combo2 skip, actorId = {actor.Id}, targetId = {target.Id}, reason = combo2MissOrNoRoll");

                return;
            }

            TryCounterAttack(steps, attacker, defender, target, actor, currentTurn, seededRandomService, turnState);
        }

        private void TryCounterAttack(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            IUnitState counterActor,
            IUnitState counterTarget,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            if (counterActor.IsAlive() == false || counterTarget.IsAlive() == false)
                return;

            var counterChance = counterActor.CharacteristicState.CounterChance;
            var counterRoll = seededRandomService.GetRandomValue();

            _logger.LogDebug($"[Story][Battle]: Counter roll, actorId = {counterActor.Id}, targetId = {counterTarget.Id}, counterRoll = {counterRoll}, counterChance = {counterChance}");

            if (counterChance <= counterRoll)
                return;

            var commands = new List<BattleCommand>();
            var isMelee = IsMeleeUnit(counterActor);

            AppendWait(commands, _counterAttackCooldown);

            if (isMelee)
            {
                commands.Add(_battleCommandFactory.Approach(counterActor.Id, counterActor.SlotIndex, counterTarget.Id, counterTarget.SlotIndex, true));

                _logger.LogDebug($"[Story][Battle]: Counter approach, actorId = {counterActor.Id}, targetId = {counterTarget.Id}");
            }

            commands.Add(_battleCommandFactory.PlayAnimation(counterActor.Id, counterActor.SlotIndex, "attack"));

            if (isMelee == false)
                commands.Add(_battleCommandFactory.Approach(counterActor.Id, counterActor.SlotIndex, counterTarget.Id, counterTarget.SlotIndex, false));

            var hit = TryResolveStrike(
                commands,
                counterActor,
                counterTarget,
                seededRandomService,
                BattlePhaseType.CounterAttack,
                counterActor.CharacteristicState.CounterMultiplier,
                turnState);

            if (isMelee)
                commands.Add(_battleCommandFactory.ReturnToPosition(counterActor.Id, counterActor.SlotIndex));

            AppendIdleIfAlive(commands, counterActor);

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.CounterAttack,
                counterActor,
                commands,
                counterTarget);

            if (hit)
            {
                NotifyPerkAction(
                    BattlePerkActionType.CounterAttack,
                    counterActor,
                    defender,
                    attacker,
                    steps,
                    currentTurn,
                    seededRandomService,
                    turnState);
            }

            if (hit && counterTarget.IsAlive() == false)
                EmitDeath(steps, currentTurn, counterTarget, turnState);
        }

        private bool TryComboAttack(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            IUnitState actor,
            IUnitState target,
            int currentTurn,
            ISeededRandomService seededRandomService,
            int comboNumber,
            BattleTurnState turnState)
        {
            if (actor.IsAlive() == false || target.IsAlive() == false)
                return false;

            var actorCharacteristics = actor.CharacteristicState;
            var comboChance = comboNumber == 1 ? actorCharacteristics.Combo1Chance : actorCharacteristics.Combo2Chance;
            var comboMultiplier = actorCharacteristics.ComboMultiplier;
            var phase = comboNumber == 1 ? BattlePhaseType.Combo1Attack : BattlePhaseType.Combo2Attack;
            var comboRoll = seededRandomService.GetRandomValue();

            _logger.LogDebug($"[Story][Battle]: Combo{comboNumber} roll, actorId = {actor.Id}, targetId = {target.Id}, comboRoll = {comboRoll}, comboChance = {comboChance}");

            if (comboChance <= comboRoll)
                return false;

            var commands = new List<BattleCommand>();
            var isMelee = IsMeleeUnit(actor);

            AppendWait(commands, _comboAttackCooldown);

            commands.Add(_battleCommandFactory.PlayAnimation(actor.Id, actor.SlotIndex, "attack"));

            if (isMelee == false)
                commands.Add(_battleCommandFactory.Approach(actor.Id, actor.SlotIndex, target.Id, target.SlotIndex, false));

            var hit = TryResolveStrike(
                commands,
                actor,
                target,
                seededRandomService,
                phase,
                comboMultiplier,
                turnState);

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                phase,
                actor,
                commands,
                target);

            if (hit)
            {
                NotifyPerkAction(
                    BattlePerkActionType.ComboAttack,
                    actor,
                    attacker,
                    defender,
                    steps,
                    currentTurn,
                    seededRandomService,
                    turnState);
            }
            else
            {
                _logger.LogDebug($"[Story][Battle]: Combo{comboNumber} miss, actorId = {actor.Id}, targetId = {target.Id}");
            }

            if (hit && target.IsAlive() == false)
                EmitDeath(steps, currentTurn, target, turnState);

            return hit;
        }

        private void NotifyPerkAction(
            BattlePerkActionType actionType,
            IUnitState actor,
            ITeamSimulationState actorTeam,
            ITeamSimulationState opponentTeam,
            List<BattleStep> steps,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            _battlePerkSimulator.NotifyAction(
                actionType,
                actor,
                actorTeam,
                opponentTeam,
                steps,
                currentTurn,
                seededRandomService,
                turnState);
        }

        private bool TryResolveStrike(
            List<BattleCommand> commands,
            IUnitState actor,
            IUnitState target,
            ISeededRandomService seededRandomService,
            BattlePhaseType phase,
            float damageMultiplier,
            BattleTurnState turnState)
        {
            var actorCharacteristics = actor.CharacteristicState;
            var targetCharacteristics = target.CharacteristicState;
            var evasionRoll = seededRandomService.GetRandomValue();
            var isEvaded = evasionRoll < targetCharacteristics.Evasion;

            _logger.LogDebug($"[Story][Battle]: Strike, phase = {phase}, actorId = {actor.Id}, targetId = {target.Id}, evasionRoll = {evasionRoll}, evasion = {targetCharacteristics.Evasion}, isEvaded = {isEvaded}");

            if (isEvaded)
            {
                commands.Add(_battleCommandFactory.ShowMiss(actor.Id, actor.SlotIndex, target.Id, target.SlotIndex));

                AppendMissWait(commands);

                return false;
            }

            var criticalRoll = seededRandomService.GetRandomValue();
            var isCritical = criticalRoll < actorCharacteristics.CriticalChance;

            _logger.LogDebug($"[Story][Battle]: Strike, phase = {phase}, actorId = {actor.Id}, criticalRoll = {criticalRoll}, criticalChance = {actorCharacteristics.CriticalChance}, isCritical = {isCritical}");

            var damage = CalculateStrikeDamage(actorCharacteristics, targetCharacteristics, damageMultiplier, isCritical);
            var healthBefore = targetCharacteristics.Health;
            var healthAfter = healthBefore - damage;

            if (healthAfter < 0f)
                healthAfter = 0f;

            targetCharacteristics.Health = healthAfter;

            commands.Add(_battleCommandFactory.ShowDamage(actor.Id, actor.SlotIndex, target.Id, target.SlotIndex, damage, isCritical, false));
            commands.Add(_battleCommandFactory.SetHp(target.Id, target.SlotIndex, healthAfter));

            ApplyVampyrism(commands, actor, damage);

            _logger.LogDebug($"[Story][Battle]: Damage applied, phase = {phase}, actorId = {actor.Id}, targetId = {target.Id}, damage = {damage}, damageMultiplier = {damageMultiplier}, defence = {targetCharacteristics.Defence}, isCritical = {isCritical}, health = {healthAfter}");

            return true;
        }

        private void ApplyVampyrism(List<BattleCommand> commands, IUnitState actor, float dealtDamage)
        {
            var actorCharacteristics = actor.CharacteristicState;

            if (dealtDamage <= 0f || actorCharacteristics.Vampyrism <= 0f)
                return;

            var heal = MathF.Ceiling(dealtDamage * actorCharacteristics.Vampyrism * actorCharacteristics.HealingBoost);

            if (heal <= 0f)
                return;

            actorCharacteristics.Health += heal;

            if (actorCharacteristics.MaxHealth < actorCharacteristics.Health)
                actorCharacteristics.Health = actorCharacteristics.MaxHealth;

            commands.Add(_battleCommandFactory.ShowHeal(actor.Id, actor.SlotIndex, actor.Id, actor.SlotIndex, heal));
            commands.Add(_battleCommandFactory.SetHp(actor.Id, actor.SlotIndex, actorCharacteristics.Health));

            _logger.LogDebug($"[Story][Battle]: Vampyrism, actorId = {actor.Id}, dealt = {dealtDamage}, vampyrism = {actorCharacteristics.Vampyrism}, healingBoost = {actorCharacteristics.HealingBoost}, heal = {heal}, health = {actorCharacteristics.Health}");
        }

        private float CalculateStrikeDamage(
            ICharacteristicState actor,
            ICharacteristicState target,
            float damageMultiplier,
            bool isCritical)
        {
            var defenceFactor = 1f - target.Defence;

            if (defenceFactor < 0f)
                defenceFactor = 0f;

            var damage = actor.Damage * damageMultiplier;

            if (isCritical)
                damage *= actor.CriticalMultiplier;

            return _battleDamageMath.RoundDamage(damage * defenceFactor);
        }

        private void AppendMissWait(List<BattleCommand> commands)
        {
            if (_battleFlytextTimer <= 0f)
                return;

            commands.Add(_battleCommandFactory.Wait(_battleFlytextTimer));

            _logger.LogDebug($"[Story][Battle]: Miss, wait seconds = {_battleFlytextTimer}");
        }

        private void EmitReturnToPosition(
            List<BattleStep> steps,
            IUnitState actor,
            int currentTurn,
            bool isMelee)
        {
            var commands = new List<BattleCommand>();

            if (isMelee)
            {
                commands.Add(_battleCommandFactory.ReturnToPosition(actor.Id, actor.SlotIndex));

                _logger.LogDebug($"[Story][Battle]: Return to position unitId = {actor.Id}");
            }

            AppendIdleIfAlive(commands, actor);

            if (commands.Count == 0)
                return;

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.ReturnToPosition,
                actor,
                commands);
        }

        private void AppendIdleIfAlive(List<BattleCommand> commands, IUnitState unit)
        {
            if (unit.IsAlive() == false)
                return;

            commands.Add(_battleCommandFactory.PlayAnimation(unit.Id, unit.SlotIndex, "idle"));

            _logger.LogDebug($"[Story][Battle]: Idle after action unitId = {unit.Id}");
        }

        private void AppendWait(List<BattleCommand> commands, float seconds)
        {
            if (seconds <= 0f)
                return;

            commands.Add(_battleCommandFactory.Wait(seconds));
        }

        private bool IsMeleeUnit(IUnitState unit)
        {
            return (unit.Flags & UnitFlags.Melee) != 0;
        }

        private void EmitDeath(List<BattleStep> steps, int currentTurn, IUnitState unit, BattleTurnState turnState)
        {
            if (_battlePerkSimulator.TryResurrectOnDeath(unit, steps, currentTurn, turnState))
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

        private float GetConstant(IConfigDistributor configDistributor, string constantKey)
        {
            if (configDistributor.Constants.TryGet(constantKey, out var constant) == false)
            {
                _logger.LogError($"[Error][Story][Battle]: Constant missing key = {constantKey}");

                throw new InvalidOperationException($"[Error][Story][Battle]: Constant missing key = {constantKey}");
            }

            return float.Parse(constant.ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
