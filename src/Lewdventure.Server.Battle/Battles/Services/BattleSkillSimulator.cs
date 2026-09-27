using System;
using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleSkillSimulator : IBattleSkillSimulator
    {
        private readonly IBattleConstantsReader _battleConstantsReader;
        private readonly IBattleTeamQuery _battleTeamQuery;
        private readonly ICoreLog _coreLog;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattleDamageMath _battleDamageMath;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleRewardService _battleRewardService;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly IConfigDistributor _configDistributor;
        private readonly float _battleFlytextTimer;

        public BattleSkillSimulator(
            IBattleConstantsReader battleConstantsReader,
            IBattleTeamQuery battleTeamQuery,
            ICoreLog coreLog,
            IBattleBonusService battleBonusService,
            IBattleCommandFactory battleCommandFactory,
            IBattleDamageMath battleDamageMath,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleRewardService battleRewardService,
            IBattleScriptBuilder battleScriptBuilder,
            IConfigDistributor configDistributor)
        {
            _battleConstantsReader = battleConstantsReader;
            _battleTeamQuery = battleTeamQuery;
            _coreLog = coreLog;
            _battleBonusService = battleBonusService;
            _battleCommandFactory = battleCommandFactory;
            _battleDamageMath = battleDamageMath;
            _battlePerkSimulator = battlePerkSimulator;
            _battleRewardService = battleRewardService;
            _battleScriptBuilder = battleScriptBuilder;
            _configDistributor = configDistributor;
            _battleFlytextTimer = _battleConstantsReader.Get(ConstantKeys.BattleFlytextTimerKey);
        }

        public void SimulateUnitSkills(
            List<BattleStep> steps,
            IUnitState actor,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            CastAllSkills(
                steps,
                actor,
                attacker,
                defender,
                currentTurn,
                seededRandomService,
                BattlePhaseType.UnitSkill,
                true,
                turnState);
        }

        public void SimulateSummonSkills(
            List<BattleStep> steps,
            IUnitState summon,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            CastAllSkills(
                steps,
                summon,
                attacker,
                defender,
                currentTurn,
                seededRandomService,
                BattlePhaseType.SummonSkill,
                true,
                turnState);
        }

        public void ApplyEnergyGain(
            List<BattleStep> steps,
            IUnitState actor,
            int currentTurn)
        {
            var characteristics = actor.CharacteristicState;

            if (characteristics.EnergyGain <= 0f || characteristics.MaxEnergy <= 0f)
                return;

            characteristics.Energy += characteristics.EnergyGain;

            if (characteristics.MaxEnergy < characteristics.Energy)
                characteristics.Energy = characteristics.MaxEnergy;

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.UnitCooldown,
                actor,
                new List<BattleCommand>
                {
                    _battleCommandFactory.SetEnergy(actor.Id, actor.SlotIndex, characteristics.Energy),
                });

            _coreLog.Debug($"[Story][Battle]: Energy gain unitId = {actor.Id}, gain = {characteristics.EnergyGain}, energy = {characteristics.Energy}, maxEnergy = {characteristics.MaxEnergy}");
        }

        public void TryCastEnergySkill(
            List<BattleStep> steps,
            IUnitState actor,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            if (actor.IsAlive() == false)
                return;

            var characteristics = actor.CharacteristicState;

            if (characteristics.MaxEnergy <= 0f)
                return;

            if (HasEnergySkill(actor) == false)
            {
                if (characteristics.Energy < characteristics.MaxEnergy)
                {
                    _coreLog.Debug($"[Story][Battle]: Energy skill skip no skill, unitId = {actor.Id}, energy = {characteristics.Energy}, maxEnergy = {characteristics.MaxEnergy}");

                    return;
                }

                _coreLog.Warning($"[Story][Battle]: Energy skill skip bar full without skill, unitId = {actor.Id}, energy = {characteristics.Energy}, maxEnergy = {characteristics.MaxEnergy}");

                return;
            }

            if (characteristics.Energy < characteristics.MaxEnergy)
            {
                _coreLog.Debug($"[Story][Battle]: Energy skill skip bar not full, unitId = {actor.Id}, energy = {characteristics.Energy}, maxEnergy = {characteristics.MaxEnergy}");

                return;
            }

            var targetIndex = _battleTeamQuery.FindTargetIndex(defender);

            if (targetIndex < 0)
                return;

            var target = defender.MainUnits[targetIndex];
            var cooldown = _battleConstantsReader.Get(ConstantKeys.UnitsCooldownKey);
            var castDuration = GetEnergyCastDuration(actor);
            var context = CreateContext(
                steps,
                actor,
                target,
                attacker,
                defender,
                currentTurn,
                BattlePhaseType.EnergySkill,
                false,
                cooldown,
                seededRandomService,
                turnState);

            _coreLog.Information($"[Story][Battle]: Energy skill cast, unitId = {actor.Id}, targetId = {target.Id}, energy = {characteristics.Energy}, maxEnergy = {characteristics.MaxEnergy}, duration = {castDuration}");

            var commands = new List<BattleCommand>();

            if (0f < cooldown)
                commands.Add(_battleCommandFactory.Wait(cooldown));

            commands.Add(_battleCommandFactory.CastSkill(actor.Id, actor.SlotIndex, target.Id, target.SlotIndex, "energy"));
            commands.Add(_battleCommandFactory.PlayAnimation(actor.Id, actor.SlotIndex, "cast"));
            commands.Add(_battleCommandFactory.Wait(castDuration));

            context.TryDealStrike(commands, characteristics.SkillMultiplier, out _, out _);

            characteristics.Energy = 0f;
            commands.Add(_battleCommandFactory.SetEnergy(actor.Id, actor.SlotIndex, 0f));
            context.AddStep(commands, target);

            if (target.IsAlive() == false)
                context.EmitDeath(target);
        }

        private bool HasEnergySkill(IUnitState actor)
        {
            var skills = actor.Skills;

            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].SkillType == SkillType.Energy)
                    return true;
            }

            return false;
        }

        private float GetEnergyCastDuration(IUnitState actor)
        {
            var skills = actor.Skills;

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];

                if (skill.SkillType != SkillType.Energy)
                    continue;

                if (skill.CastDurationSeconds <= 0f)
                {
                    _coreLog.Error($"[Error][Story][Battle]: Energy skill duration missing or zero, unitId = {actor.Id}, skillId = {skill.SkillKey}");

                    throw new InvalidOperationException($"[Error][Story][Battle]: Energy skill duration missing or zero, unitId = {actor.Id}, skillId = {skill.SkillKey}");
                }

                return skill.CastDurationSeconds;
            }

            _coreLog.Error($"[Error][Story][Battle]: Energy skill duration missing, unitId = {actor.Id}");

            throw new InvalidOperationException($"[Error][Story][Battle]: Energy skill duration missing, unitId = {actor.Id}");
        }

        private void CastAllSkills(
            List<BattleStep> steps,
            IUnitState actor,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattlePhaseType phase,
            bool allowCritical,
            BattleTurnState turnState)
        {
            var skills = actor.Skills;

            if (skills.Count == 0)
                return;

            var cooldownKey = string.Empty;

            if (phase == BattlePhaseType.UnitSkill)
                cooldownKey = ConstantKeys.UnitsCooldownKey;
            else if (phase == BattlePhaseType.SummonSkill)
                cooldownKey = ConstantKeys.SummonsCooldownKey;

            if (string.IsNullOrEmpty(cooldownKey))
                return;

            var cooldown = _battleConstantsReader.Get(cooldownKey);
            var executedCount = 0;

            for (int i = 0; i < skills.Count; i++)
            {
                if (actor.IsAlive() == false)
                    break;

                if (_battleTeamQuery.HasAliveMainUnits(defender) == false)
                    break;

                var skill = skills[i];

                if (skill.SkillType == SkillType.Energy)
                {
                    _coreLog.Debug($"[Story][Battle]: Skill cast skip energy in unit phase, unitId = {actor.Id}, skillId = {skill.SkillKey}, turn = {currentTurn}");

                    continue;
                }

                var targetIndex = _battleTeamQuery.FindTargetIndex(defender);

                if (targetIndex < 0)
                    break;

                var target = defender.MainUnits[targetIndex];

                _coreLog.Debug($"[Story][Battle]: Skill cast, unitId = {actor.Id}, skillId = {skill.SkillKey}, type = {skill.SkillType}, targetId = {target.Id}, turn = {currentTurn}, cooldownKey = {cooldownKey}");

                var context = CreateContext(
                    steps,
                    actor,
                    target,
                    attacker,
                    defender,
                    currentTurn,
                    phase,
                    allowCritical,
                    cooldown,
                    seededRandomService,
                    turnState);

                skill.Execute(context);
                executedCount += 1;

                if (target.IsAlive() == false)
                    context.EmitDeath(target);

                if (turnState.ShouldSkipRemainingActions(actor))
                    break;
            }

            TryEmitUnitSkillPhaseWait(steps, actor, currentTurn, cooldown, phase, executedCount);
        }

        private void TryEmitUnitSkillPhaseWait(
            List<BattleStep> steps,
            IUnitState actor,
            int currentTurn,
            float cooldown,
            BattlePhaseType phase,
            int executedCount)
        {
            if (phase != BattlePhaseType.UnitSkill)
                return;

            if (executedCount <= 0)
                return;

            if (actor.IsAlive() == false)
                return;

            if (cooldown <= 0f)
                return;

            var commands = new List<BattleCommand>
            {
                _battleCommandFactory.Wait(cooldown),
            };

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                BattlePhaseType.UnitSkill,
                actor,
                commands);

            _coreLog.Debug($"[Story][Battle]: Unit skill phase wait, unitId = {actor.Id}, wait = {cooldown}, executed = {executedCount}");
        }

        private SkillExecutionContext CreateContext(
            List<BattleStep> steps,
            IUnitState actor,
            IUnitState target,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            BattlePhaseType phase,
            bool allowCritical,
            float cooldown,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            return new SkillExecutionContext(
                steps,
                actor,
                target,
                attacker,
                defender,
                currentTurn,
                phase,
                allowCritical,
                cooldown,
                seededRandomService,
                turnState,
                _battleCommandFactory,
                _battleDamageMath,
                _battleBonusService,
                _battlePerkSimulator,
                _battleRewardService,
                _battleScriptBuilder,
                _coreLog,
                _battleFlytextTimer);
        }

    }
}
