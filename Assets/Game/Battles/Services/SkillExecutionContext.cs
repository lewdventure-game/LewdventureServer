using Microsoft.Extensions.Logging;
using Server.Services;

namespace Server.Battles
{
    internal sealed class SkillExecutionContext : ISkillExecutionContext
    {
        private int _pendingAnyDamageCount;

        private readonly List<BattleStep> _steps;
        private readonly IUnitState _actor;
        private readonly IUnitState _target;
        private readonly ITeamSimulationState _attacker;
        private readonly ITeamSimulationState _defender;
        private readonly int _currentTurn;
        private readonly BattlePhaseType _phase;
        private readonly bool _allowCritical;
        private readonly float _cooldown;
        private readonly ISeededRandomService _seededRandomService;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleRewardService _battleRewardService;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly ILogger _logger;
        private readonly float _battleFlytextTimer;

        public List<BattleStep> Steps => _steps;

        public IUnitState Actor => _actor;

        public IUnitState Target => _target;

        public ITeamSimulationState Attacker => _attacker;

        public ITeamSimulationState Defender => _defender;

        public int CurrentTurn => _currentTurn;

        public BattlePhaseType Phase => _phase;

        public bool AllowCritical => _allowCritical;

        public float Cooldown => _cooldown;

        public ISeededRandomService SeededRandomService => _seededRandomService;

        public IBattleCommandFactory BattleCommandFactory => _battleCommandFactory;

        public IBattleBonusService BattleBonusService => _battleBonusService;

        public IBattleRewardService BattleRewardService => _battleRewardService;

        public IBattleScriptBuilder BattleScriptBuilder => _battleScriptBuilder;

        public ILogger Logger => _logger;

        public SkillExecutionContext(
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
            IBattleCommandFactory battleCommandFactory,
            IBattleBonusService battleBonusService,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleRewardService battleRewardService,
            IBattleScriptBuilder battleScriptBuilder,
            ILogger logger,
            float battleFlytextTimer)
        {
            _steps = steps;
            _actor = actor;
            _target = target;
            _attacker = attacker;
            _defender = defender;
            _currentTurn = currentTurn;
            _phase = phase;
            _allowCritical = allowCritical;
            _cooldown = cooldown;
            _seededRandomService = seededRandomService;
            _battleCommandFactory = battleCommandFactory;
            _battleBonusService = battleBonusService;
            _battlePerkSimulator = battlePerkSimulator;
            _battleRewardService = battleRewardService;
            _battleScriptBuilder = battleScriptBuilder;
            _logger = logger;
            _battleFlytextTimer = battleFlytextTimer;
        }

        public bool TryDealStrike(List<BattleCommand> commands, float damageMultiplier, out float dealtDamage, out bool isCritical)
        {
            dealtDamage = 0f;
            isCritical = false;

            var actorCharacteristics = _actor.CharacteristicState;
            var targetCharacteristics = _target.CharacteristicState;
            var evasionRoll = _seededRandomService.GetRandomValue();
            var isEvaded = evasionRoll < targetCharacteristics.Evasion;

            _logger.LogDebug($"[Story][Battle]: Skill strike, actorId = {_actor.Id}, targetId = {_target.Id}, evasionRoll = {evasionRoll}, evasion = {targetCharacteristics.Evasion}, isEvaded = {isEvaded}, allowCritical = {_allowCritical}");

            if (isEvaded)
            {
                commands.Add(_battleCommandFactory.ShowMiss(_actor.Id, _actor.SlotIndex, _target.Id, _target.SlotIndex));

                if (0f < _battleFlytextTimer)
                {
                    commands.Add(_battleCommandFactory.Wait(_battleFlytextTimer));
                    _logger.LogDebug($"[Story][Battle]: Skill miss wait, actorId = {_actor.Id} seconds = {_battleFlytextTimer}");
                }

                return false;
            }

            if (_allowCritical)
            {
                var criticalRoll = _seededRandomService.GetRandomValue();
                isCritical = criticalRoll < actorCharacteristics.CriticalChance;

                _logger.LogDebug($"[Story][Battle]: Skill crit roll, actorId = {_actor.Id}, criticalRoll = {criticalRoll}, criticalChance = {actorCharacteristics.CriticalChance}, isCritical = {isCritical}");
            }

            var defenceFactor = 1f - targetCharacteristics.Defence;

            if (defenceFactor < 0f)
                defenceFactor = 0f;

            var damage = actorCharacteristics.Damage * damageMultiplier;

            if (isCritical)
                damage *= actorCharacteristics.CriticalMultiplier;

            damage *= defenceFactor;

            var healthAfter = targetCharacteristics.Health - damage;

            if (healthAfter < 0f)
                healthAfter = 0f;

            targetCharacteristics.Health = healthAfter;
            dealtDamage = damage;

            commands.Add(_battleCommandFactory.ShowDamage(_actor.Id, _actor.SlotIndex, _target.Id, _target.SlotIndex, damage, isCritical, false));
            commands.Add(_battleCommandFactory.SetHp(_target.Id, _target.SlotIndex, healthAfter));

            _logger.LogInformation($"[Story][Battle]: Skill damage, actorId = {_actor.Id}, targetId = {_target.Id}, damage = {damage}, isCritical = {isCritical}, health = {healthAfter}");

            _pendingAnyDamageCount += 1;
            _logger.LogDebug($"[Story][Battle]: any_damage deferred until step commit, actorId = {_actor.Id} targetId = {_target.Id} pending = {_pendingAnyDamageCount} turn = {_currentTurn}");

            return true;
        }

        public void Heal(IUnitState unit, float amount, List<BattleCommand> commands)
        {
            if (amount <= 0f)
                return;

            var characteristics = unit.CharacteristicState;
            var heal = MathF.Ceiling(amount);

            if (heal <= 0f)
                return;

            characteristics.Health += heal;

            if (characteristics.MaxHealth < characteristics.Health)
                characteristics.Health = characteristics.MaxHealth;

            commands.Add(_battleCommandFactory.ShowHeal(_actor.Id, _actor.SlotIndex, unit.Id, unit.SlotIndex, heal));
            commands.Add(_battleCommandFactory.SetHp(unit.Id, unit.SlotIndex, characteristics.Health));

            _logger.LogDebug($"[Story][Battle]: Skill heal, actorId = {_actor.Id}, unitId = {unit.Id}, heal = {heal}, health = {characteristics.Health}");
        }

        public int FindAllyMainIndex()
        {
            var mainUnits = _attacker.MainUnits;
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

        public void EmitDeath(IUnitState unit)
        {
            if (_battlePerkSimulator.TryResurrectOnDeath(unit, _steps, _currentTurn))
                return;

            _logger.LogDebug($"[Story][Battle]: Death, unitId = {unit.Id}, turn = {_currentTurn}");

            _battleScriptBuilder.Add(
                _steps,
                _currentTurn,
                BattlePhaseType.Death,
                unit,
                new List<BattleCommand>
                {
                    _battleCommandFactory.KillUnit(unit.Id, unit.SlotIndex),
                    _battleCommandFactory.SetHp(unit.Id, unit.SlotIndex, 0f),
                });
        }

        public void AddStep(List<BattleCommand> commands, IUnitState target)
        {
            _battleScriptBuilder.Add(
                _steps,
                _currentTurn,
                _phase,
                _actor,
                commands,
                target);

            FlushPendingAnyDamage();
        }

        private void FlushPendingAnyDamage()
        {
            if (_pendingAnyDamageCount <= 0)
                return;

            var notifyCount = _pendingAnyDamageCount;
            _pendingAnyDamageCount = 0;

            _logger.LogDebug($"[Story][Battle]: any_damage flush after skill step, actorId = {_actor.Id}, count = {notifyCount}, turn = {_currentTurn}");

            for (int i = 0; i < notifyCount; i++)
                _battlePerkSimulator.NotifyAnyDamage(_actor, _attacker, _defender, _steps, _currentTurn, _seededRandomService);
        }
    }
}
