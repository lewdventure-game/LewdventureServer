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

        public int SimulateSummonSkills(
            List<BattleStep> steps,
            IUnitState summon,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            return CastAllSkills(
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

            if (turnState.ShouldSkipRemainingActions(actor))
            {
                _coreLog.Debug($"[Story][Battle]: Energy skill skip after resurrection abort, unitId = {actor.Id}, turn = {currentTurn}");

                return;
            }

            if (TryFindEnergySkill(actor, out var energySkill) == false)
            {
                LogMissingEnergySkill(actor);

                return;
            }

            TryCastConfiguredEnergySkill(steps, actor, attacker, defender, energySkill, currentTurn, seededRandomService, turnState);
        }

        private void TryCastConfiguredEnergySkill(
            List<BattleStep> steps,
            IUnitState actor,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            ISkill energySkill,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            var activationContext = CreateActivationContext(actor, attacker, defender, energySkill, currentTurn, false);

            if (energySkill.CanActivate(activationContext) == false)
            {
                _coreLog.Debug($"[Story][Battle]: Energy skill triggers not satisfied, unitId = {actor.Id}, skillId = {energySkill.SkillKey}, energy = {actor.CharacteristicState.Energy}");

                return;
            }

            var targetIndex = _battleTeamQuery.FindTargetIndex(defender);

            if (targetIndex < 0)
                return;

            var target = defender.MainUnits[targetIndex];
            var cooldown = _battleConstantsReader.Get(ConstantKeys.UnitsCooldownKey);
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

            _coreLog.Information($"[Story][Battle]: Energy skill cast, unitId = {actor.Id}, targetId = {target.Id}, skillId = {energySkill.SkillKey}, energy = {actor.CharacteristicState.Energy}");

            if (0f < cooldown)
                _battleScriptBuilder.Add(steps, currentTurn, BattlePhaseType.EnergySkill, actor, new List<BattleCommand> { _battleCommandFactory.Wait(cooldown) });

            var aliveBefore = CollectAliveMainUnits(defender, attacker);

            energySkill.NotifyActivated(activationContext);
            energySkill.Execute(context);

            EmitSkillDeaths(context, aliveBefore);
        }

        private bool TryFindEnergySkill(IUnitState actor, out ISkill energySkill)
        {
            var skills = actor.Skills;

            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].RequiresEnergy == false)
                    continue;

                energySkill = skills[i];

                return true;
            }

            energySkill = null!;

            return false;
        }

        private void LogMissingEnergySkill(IUnitState actor)
        {
            var characteristics = actor.CharacteristicState;

            if (characteristics.MaxEnergy <= 0f || characteristics.Energy < characteristics.MaxEnergy)
            {
                _coreLog.Debug($"[Story][Battle]: Energy skill skip no skill, unitId = {actor.Id}, energy = {characteristics.Energy}, maxEnergy = {characteristics.MaxEnergy}");

                return;
            }

            _coreLog.Warning($"[Story][Battle]: Energy skill skip bar full without skill, unitId = {actor.Id}, energy = {characteristics.Energy}, maxEnergy = {characteristics.MaxEnergy}");
        }

        private int CastAllSkills(
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
                return 0;

            var cooldownKey = string.Empty;

            if (phase == BattlePhaseType.UnitSkill)
                cooldownKey = ConstantKeys.UnitsCooldownKey;
            else if (phase == BattlePhaseType.SummonSkill)
                cooldownKey = ConstantKeys.SummonsCooldownKey;

            if (string.IsNullOrEmpty(cooldownKey))
                return 0;

            var cooldown = _battleConstantsReader.Get(cooldownKey);
            var configuredCount = 0;

            for (int i = 0; i < skills.Count; i++)
            {
                if (actor.IsAlive() == false)
                    break;

                if (_battleTeamQuery.HasAliveMainUnits(defender) == false)
                    break;

                var skill = skills[i];

                if (skill.RequiresEnergy)
                {
                    _coreLog.Debug($"[Story][Battle]: Skill cast skip energy in unit phase, unitId = {actor.Id}, skillId = {skill.SkillKey}, turn = {currentTurn}");

                    continue;
                }

                var activationContext = CreateActivationContext(actor, attacker, defender, skill, currentTurn, false);

                if (skill.CanActivate(activationContext) == false)
                {
                    _coreLog.Debug($"[Story][Battle]: Skill triggers not satisfied, unitId = {actor.Id}, skillId = {skill.SkillKey}, turn = {currentTurn}");

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

                var aliveBefore = CollectAliveMainUnits(defender, attacker);

                skill.Execute(context);
                skill.NotifyActivated(activationContext);

                if (skill.SkillType == SkillType.Configured)
                    configuredCount += 1;

                EmitSkillDeaths(context, aliveBefore);

                if (turnState.ShouldSkipRemainingActions(actor))
                    break;
            }

            return configuredCount;
        }

        public void TrySimulateInstantSkills(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            SimulateInstantSkillsForTeam(steps, defender, attacker, currentTurn, seededRandomService, turnState);
            SimulateInstantSkillsForTeam(steps, attacker, defender, currentTurn, seededRandomService, turnState);
        }

        private void SimulateInstantSkillsForTeam(
            List<BattleStep> steps,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            SimulateInstantSkillsForUnits(steps, ownerTeam.MainUnits, ownerTeam, opponentTeam, BattlePhaseType.UnitSkill, currentTurn, seededRandomService, turnState);
            SimulateInstantSkillsForUnits(steps, ownerTeam.Summons, ownerTeam, opponentTeam, BattlePhaseType.SummonSkill, currentTurn, seededRandomService, turnState);
        }

        private void SimulateInstantSkillsForUnits(
            List<BattleStep> steps,
            IReadOnlyList<IUnitState> owners,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            BattlePhaseType phase,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            for (int unitIndex = 0; unitIndex < owners.Count; unitIndex++)
            {
                var owner = owners[unitIndex];

                if (owner.IsAlive() == false)
                    continue;

                var skills = owner.Skills;

                for (int skillIndex = 0; skillIndex < skills.Count; skillIndex++)
                {
                    var skill = skills[skillIndex];

                    if (skill.AllowsInstantActivation == false)
                        continue;

                    var activationContext = CreateActivationContext(owner, ownerTeam, opponentTeam, skill, currentTurn, true);

                    if (skill.CanActivate(activationContext) == false)
                        continue;

                    var targetIndex = _battleTeamQuery.FindTargetIndex(opponentTeam);
                    var target = targetIndex < 0 ? owner : opponentTeam.MainUnits[targetIndex];
                    var cooldown = _battleConstantsReader.Get(ConstantKeys.UnitsCooldownKey);
                    var context = CreateContext(
                        steps,
                        owner,
                        target,
                        ownerTeam,
                        opponentTeam,
                        currentTurn,
                        phase,
                        false,
                        cooldown,
                        seededRandomService,
                        turnState);

                    _coreLog.Information($"[Story][Battle]: Instant skill activation, unitId = {owner.Id}, skillId = {skill.SkillKey}, turn = {currentTurn}, phase = {phase}");

                    var aliveBefore = CollectAliveMainUnits(opponentTeam, ownerTeam);

                    skill.Execute(context);
                    skill.NotifyActivated(activationContext);

                    EmitSkillDeaths(context, aliveBefore);
                }
            }
        }

        private List<IUnitState> CollectAliveMainUnits(ITeamSimulationState firstTeam, ITeamSimulationState secondTeam)
        {
            var alive = new List<IUnitState>(firstTeam.MainUnits.Count + secondTeam.MainUnits.Count);

            AddAliveMainUnits(firstTeam, alive);
            AddAliveMainUnits(secondTeam, alive);

            return alive;
        }

        private void AddAliveMainUnits(ITeamSimulationState team, List<IUnitState> alive)
        {
            var mainUnits = team.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                if (mainUnits[i].IsAlive())
                    alive.Add(mainUnits[i]);
            }
        }

        private void EmitSkillDeaths(ISkillExecutionContext context, List<IUnitState> aliveBefore)
        {
            for (int i = 0; i < aliveBefore.Count; i++)
            {
                if (aliveBefore[i].IsAlive() == false)
                    context.EmitDeath(aliveBefore[i]);
            }
        }

        private SkillActivationContext CreateActivationContext(
            IUnitState owner,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            ISkill skill,
            int currentTurn,
            bool isInstantCheck)
        {
            return new SkillActivationContext(
                owner,
                ownerTeam,
                opponentTeam,
                owner.GetSkillState(skill.Id),
                owner.GetSkillLevel(skill.Id),
                currentTurn,
                isInstantCheck);
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
