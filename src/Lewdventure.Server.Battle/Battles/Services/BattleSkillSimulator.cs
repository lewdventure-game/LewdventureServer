using Microsoft.Extensions.Logging;
using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleSkillSimulator : IBattleSkillSimulator
    {
        private readonly ILogger<BattleSkillSimulator> _logger;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleRewardService _battleRewardService;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly IConfigDistributor _configDistributor;
        private readonly float _battleFlytextTimer;

        public BattleSkillSimulator(
            ILogger<BattleSkillSimulator> logger,
            IBattleBonusService battleBonusService,
            IBattleCommandFactory battleCommandFactory,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleRewardService battleRewardService,
            IBattleScriptBuilder battleScriptBuilder,
            IConfigDistributor configDistributor)
        {
            _logger = logger;
            _battleBonusService = battleBonusService;
            _battleCommandFactory = battleCommandFactory;
            _battlePerkSimulator = battlePerkSimulator;
            _battleRewardService = battleRewardService;
            _battleScriptBuilder = battleScriptBuilder;
            _configDistributor = configDistributor;
            _battleFlytextTimer = GetConstant(ConstantKeys.BattleFlytextTimerKey);
        }

        public void SimulateUnitSkills(
            List<BattleStep> steps,
            IUnitState actor,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            CastAllSkills(
                steps,
                actor,
                attacker,
                defender,
                currentTurn,
                seededRandomService,
                BattlePhaseType.UnitSkill,
                true);
        }

        public void SimulateSummonSkills(
            List<BattleStep> steps,
            IUnitState summon,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            CastAllSkills(
                steps,
                summon,
                attacker,
                defender,
                currentTurn,
                seededRandomService,
                BattlePhaseType.SummonSkill,
                true);
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

            _logger.LogDebug($"[Story][Battle] energy gain unitId = {actor.Id}, gain = {characteristics.EnergyGain}, energy = {characteristics.Energy}, maxEnergy = {characteristics.MaxEnergy}");
        }

        public void TryCastEnergySkill(
            List<BattleStep> steps,
            IUnitState actor,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            var characteristics = actor.CharacteristicState;

            if (characteristics.MaxEnergy <= 0f)
                return;

            if (HasEnergySkill(actor) == false)
            {
                if (characteristics.Energy < characteristics.MaxEnergy)
                {
                    _logger.LogDebug($"[Story][Battle] energy skill skip no skill unitId = {actor.Id} energy = {characteristics.Energy} maxEnergy = {characteristics.MaxEnergy}");

                    return;
                }

                _logger.LogWarning($"[Story][Battle] energy skill skip bar full without skill unitId = {actor.Id} energy = {characteristics.Energy} maxEnergy = {characteristics.MaxEnergy}");

                return;
            }

            if (characteristics.Energy < characteristics.MaxEnergy)
            {
                _logger.LogDebug($"[Story][Battle] energy skill skip bar not full unitId = {actor.Id} energy = {characteristics.Energy} maxEnergy = {characteristics.MaxEnergy}");

                return;
            }

            var targetIndex = FindTargetIndex(defender);

            if (targetIndex < 0)
                return;

            var target = defender.MainUnits[targetIndex];
            var cooldown = GetUnitsCooldown();
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
                seededRandomService);

            _logger.LogInformation($"[Story][Battle] energy skill cast unitId = {actor.Id}, targetId = {target.Id}, energy = {characteristics.Energy}, maxEnergy = {characteristics.MaxEnergy}");

            var commands = new List<BattleCommand>
            {
                _battleCommandFactory.Wait(cooldown),
                _battleCommandFactory.PlayAnimation(actor.Id, actor.SlotIndex, "cast"),
                _battleCommandFactory.CastSkill(actor.Id, actor.SlotIndex, target.Id, target.SlotIndex, "energy"),
            };

            context.TryDealStrike(commands, characteristics.SkillMultiplier, out _, out _);

            characteristics.Energy = 0f;
            commands.Add(_battleCommandFactory.SetEnergy(actor.Id, actor.SlotIndex, 0f));
            context.AddStep(commands, target);

            if (target.IsAlive() == false)
                context.EmitDeath(target);
        }

        private static bool HasEnergySkill(IUnitState actor)
        {
            var skills = actor.Skills;

            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].SkillType == SkillType.Energy)
                    return true;
            }

            return false;
        }

        private void CastAllSkills(
            List<BattleStep> steps,
            IUnitState actor,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattlePhaseType phase,
            bool allowCritical)
        {
            var skills = actor.Skills;

            if (skills.Count == 0)
                return;

            var cooldown = GetUnitsCooldown();

            for (int i = 0; i < skills.Count; i++)
            {
                if (actor.IsAlive() == false)
                    return;

                if (HasAliveMainUnits(defender) == false)
                    return;

                var skill = skills[i];

                if (skill.SkillType == SkillType.Energy)
                {
                    _logger.LogDebug($"[Story][Battle] skill cast skip energy in unit phase unitId = {actor.Id} skillId = {skill.SkillKey} turn = {currentTurn}");

                    continue;
                }

                var targetIndex = FindTargetIndex(defender);

                if (targetIndex < 0)
                    return;

                var target = defender.MainUnits[targetIndex];

                _logger.LogDebug($"[Story][Battle] skill cast unitId = {actor.Id}, skillId = {skill.SkillKey}, type = {skill.SkillType}, targetId = {target.Id}, turn = {currentTurn}");

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
                    seededRandomService);

                skill.Execute(context);

                if (target.IsAlive() == false)
                    context.EmitDeath(target);
            }
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
            ISeededRandomService seededRandomService)
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
                _battleCommandFactory,
                _battleBonusService,
                _battlePerkSimulator,
                _battleRewardService,
                _battleScriptBuilder,
                _logger,
                _battleFlytextTimer);
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
                if (mainUnits[i].IsAlive())
                    return true;
            }

            return false;
        }

        private float GetUnitsCooldown()
        {
            if (_configDistributor.Constants.TryGet(ConstantKeys.UnitsCooldownKey, out var constant) == false)
            {
                _logger.LogWarning($"[Story][Battle] constant missing key = {ConstantKeys.UnitsCooldownKey}");

                return 0f;
            }

            return float.Parse(constant.ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
        }

        private float GetConstant(string constantKey)
        {
            if (_configDistributor.Constants.TryGet(constantKey, out var constant) == false)
            {
                _logger.LogWarning($"[Story][Battle] constant missing key = {constantKey}");

                return 0f;
            }

            return float.Parse(constant.ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
