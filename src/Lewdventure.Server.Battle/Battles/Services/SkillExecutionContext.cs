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
        private readonly BattleTurnState _turnState;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattleDamageMath _battleDamageMath;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleRewardService _battleRewardService;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly ICoreLog _coreLog;
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

        public float SpellMultiplier => ResolveSpellMultiplier();

        public ISeededRandomService SeededRandomService => _seededRandomService;

        public IBattleCommandFactory BattleCommandFactory => _battleCommandFactory;

        public IBattleBonusService BattleBonusService => _battleBonusService;

        public IBattleRewardService BattleRewardService => _battleRewardService;

        public IBattleScriptBuilder BattleScriptBuilder => _battleScriptBuilder;

        public ICoreLog CoreLog => _coreLog;

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
            BattleTurnState turnState,
            IBattleCommandFactory battleCommandFactory,
            IBattleDamageMath battleDamageMath,
            IBattleBonusService battleBonusService,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleRewardService battleRewardService,
            IBattleScriptBuilder battleScriptBuilder,
            ICoreLog coreLog,
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
            _turnState = turnState;
            _battleCommandFactory = battleCommandFactory;
            _battleDamageMath = battleDamageMath;
            _battleBonusService = battleBonusService;
            _battlePerkSimulator = battlePerkSimulator;
            _battleRewardService = battleRewardService;
            _battleScriptBuilder = battleScriptBuilder;
            _coreLog = coreLog;
            _battleFlytextTimer = battleFlytextTimer;
        }

        public float DealFixedDamage(IUnitState target, float damageMultiplier, List<BattleCommand> commands)
        {
            var actorCharacteristics = _actor.CharacteristicState;
            var targetCharacteristics = target.CharacteristicState;
            var rawDamage = actorCharacteristics.Damage * damageMultiplier;

            if (_actor.StrikeDamageMultiplier != 1f)
                rawDamage *= _actor.StrikeDamageMultiplier;

            var damage = _battleDamageMath.RoundDamage(rawDamage);

            if (damage <= 0f)
            {
                _coreLog.Debug($"[Story][Battle]: Skill action damage is zero, actorId = {_actor.Id}, targetId = {target.Id}, multiplier = {damageMultiplier}");

                return 0f;
            }

            var healthAfter = targetCharacteristics.Health - damage;

            if (healthAfter < 0f)
                healthAfter = 0f;

            targetCharacteristics.Health = healthAfter;

            if (_battleDamageMath.IsRecordingFormulas)
            {
                _battleDamageMath.RecordFormula(new BattleFormula
                {
                    Kind = BattleFormulaKinds.Skill,
                    Phase = _phase,
                    ActorId = _actor.Id,
                    ActorSlot = _actor.SlotIndex,
                    TargetId = target.Id,
                    TargetSlot = target.SlotIndex,
                    BaseDamage = actorCharacteristics.Damage,
                    DamageMultiplier = damageMultiplier,
                    QaMultiplier = _actor.StrikeDamageMultiplier,
                    Result = damage,
                });
            }

            commands.Add(_battleCommandFactory.ShowDamage(_actor.Id, _actor.SlotIndex, target.Id, target.SlotIndex, damage, false, false));
            commands.Add(_battleCommandFactory.SetHp(target.Id, target.SlotIndex, healthAfter));

            _pendingAnyDamageCount += 1;

            _coreLog.Information($"[Story][Battle]: Skill action damage, actorId = {_actor.Id}, targetId = {target.Id}, damage = {damage}, health = {healthAfter}");

            return damage;
        }

        private float ResolveSpellMultiplier()
        {
            if ((_actor.Flags & UnitFlags.Summon) == 0)
                return _actor.CharacteristicState.SkillMultiplier;

            var allyIndex = FindAllyMainIndex();

            if (allyIndex < 0)
                return _actor.CharacteristicState.SkillMultiplier;

            return _attacker.MainUnits[allyIndex].CharacteristicState.SkillMultiplier;
        }

        private int FindAllyMainIndex()
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
            if (_battlePerkSimulator.TryResurrectOnDeath(unit, _steps, _currentTurn, _turnState))
                return;

            _coreLog.Debug($"[Story][Battle]: Death, unitId = {unit.Id}, turn = {_currentTurn}");

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
            AppendIdleIfAlive(commands);

            _battleScriptBuilder.Add(
                _steps,
                _currentTurn,
                _phase,
                _actor,
                commands,
                target);

            FlushPendingAnyDamage();
        }

        private void AppendIdleIfAlive(List<BattleCommand> commands)
        {
            if (_actor.IsAlive() == false)
                return;

            commands.Add(_battleCommandFactory.PlayAnimation(_actor.Id, _actor.SlotIndex, "idle"));

            _coreLog.Debug($"[Story][Battle]: Idle after skill, actorId = {_actor.Id}, phase = {_phase}");
        }

        private void FlushPendingAnyDamage()
        {
            if (_pendingAnyDamageCount <= 0)
                return;

            var notifyCount = _pendingAnyDamageCount;
            _pendingAnyDamageCount = 0;

            _coreLog.Debug($"[Story][Battle]: any_damage flush after skill step, actorId = {_actor.Id}, count = {notifyCount}, turn = {_currentTurn}");

            for (int i = 0; i < notifyCount; i++)
                _battlePerkSimulator.NotifyAnyDamage(_actor, _attacker, _defender, _steps, _currentTurn, _seededRandomService, _turnState);
        }
    }
}
