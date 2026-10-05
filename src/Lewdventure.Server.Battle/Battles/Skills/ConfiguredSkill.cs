namespace Server.Battles
{
    internal sealed class ConfiguredSkill : ISkill
    {
        private const string TimingParameter = "timing";
        private const string TargetParameter = "target";
        private const string BonusParameter = "bonus_id";

        private readonly SkillDefinition _definition;
        private readonly string _skillKey;
        private readonly ICoreLog _coreLog;
        private readonly ISkillActionRegistry _skillActionRegistry;
        private readonly ISkillArgumentReader _skillArgumentReader;
        private readonly ISkillTargetResolver _skillTargetResolver;
        private readonly ISkillTriggerRegistry _skillTriggerRegistry;
        private readonly List<IUnitState> _targetsBuffer = new();

        public ConfiguredSkill(
            SkillDefinition definition,
            string skillKey,
            ICoreLog coreLog,
            ISkillActionRegistry skillActionRegistry,
            ISkillArgumentReader skillArgumentReader,
            ISkillTargetResolver skillTargetResolver,
            ISkillTriggerRegistry skillTriggerRegistry)
        {
            _definition = definition;
            _skillKey = skillKey;
            _coreLog = coreLog;
            _skillActionRegistry = skillActionRegistry;
            _skillArgumentReader = skillArgumentReader;
            _skillTargetResolver = skillTargetResolver;
            _skillTriggerRegistry = skillTriggerRegistry;
        }

        public int Id => _definition.Id;

        public string SkillKey => _skillKey;

        public SkillType SkillType => SkillType.Configured;

        public float CastDurationSeconds => ResolveCastDuration(0);

        public bool RequiresEnergy => _definition.HasTrigger(SkillTriggerType.EnergyNeeded);

        public bool AllowsInstantActivation => ResolveInstantActivation();

        public bool IsPassiveBonus => _definition.HasTrigger(SkillTriggerType.AsBonusLogic);

        public bool CanActivate(SkillActivationContext context)
        {
            var triggers = _definition.Triggers;

            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];

                if (_skillTriggerRegistry.TryResolve(trigger.TriggerType, out var evaluator) == false)
                {
                    _coreLog.Error($"[Story][Battle]: Skill trigger has no evaluator, skillId = {Id}, triggerType = {trigger.TriggerType}");

                    return false;
                }

                if (evaluator.CanActivate(trigger.Component, context) == false)
                    return false;
            }

            return 0 < triggers.Count;
        }

        public void NotifyActivated(SkillActivationContext context)
        {
            var triggers = _definition.Triggers;

            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];

                if (_skillTriggerRegistry.TryResolve(trigger.TriggerType, out var evaluator) == false)
                    continue;

                evaluator.OnActivated(trigger.Component, context);
            }

            context.RuntimeState.ActivationCount += 1;
        }

        public void Execute(ISkillExecutionContext context)
        {
            var skillLevel = context.Actor.GetSkillLevel(Id);
            var commands = new List<BattleCommand>
            {
                context.BattleCommandFactory.CastSkill(context.Actor.Id, context.Actor.SlotIndex, context.Target.Id, context.Target.SlotIndex, SkillKey),
                context.BattleCommandFactory.PlayAnimation(context.Actor.Id, context.Actor.SlotIndex, "cast"),
            };
            var actions = SortActionsByTiming(skillLevel);
            var elapsed = 0f;

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var timing = ReadTiming(action.Component, skillLevel);

                if (elapsed < timing)
                {
                    commands.Add(context.BattleCommandFactory.Wait(timing - elapsed));
                    elapsed = timing;
                }

                ExecuteAction(context, action, skillLevel, commands);
            }

            if (context.Actor.CharacteristicState.Energy <= 0f && RequiresEnergy)
                commands.Add(context.BattleCommandFactory.SetEnergy(context.Actor.Id, context.Actor.SlotIndex, 0f));

            context.AddStep(commands, context.Target);

            _coreLog.Information($"[Story][Battle]: Skill executed, skillId = {Id}, skillKey = {SkillKey}, actorId = {context.Actor.Id}, level = {skillLevel}, actions = {actions.Count}");
        }

        public void CollectPassiveBonusIds(int skillLevel, List<int> bonusIds)
        {
            if (IsPassiveBonus == false)
                return;

            var actions = _definition.Actions;

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];

                if (action.ActionType != SkillActionType.SetBonus)
                {
                    _coreLog.Warning($"[Config]: Skill {Id} with as_bonus_logic ignores action {action.ActionType}, only set_bonus works without activation");

                    continue;
                }

                var targetType = ReadTargetType(action.Component, skillLevel, SkillTargetType.Ally);

                if (targetType != SkillTargetType.Ally)
                {
                    _coreLog.Warning($"[Config]: Skill {Id} with as_bonus_logic has set_bonus target {targetType}, only 0 (owner) is supported");

                    continue;
                }

                if (_skillArgumentReader.TryReadInt(action.Component, BonusParameter, skillLevel, out var bonusId) == false || bonusId <= 0)
                {
                    _coreLog.Warning($"[Config]: Skill {Id} with as_bonus_logic has no readable bonus_id at level {skillLevel}");

                    continue;
                }

                bonusIds.Add(bonusId);
            }
        }

        private void ExecuteAction(ISkillExecutionContext context, SkillActionDefinition action, int skillLevel, List<BattleCommand> commands)
        {
            if (_skillActionRegistry.TryResolve(action.ActionType, out var executor) == false)
            {
                _coreLog.Error($"[Story][Battle]: Skill action has no executor, skillId = {Id}, actionType = {action.ActionType}");

                return;
            }

            var targetType = ReadTargetType(action.Component, skillLevel, SkillTargetType.EnemyFirstSlot);

            _skillTargetResolver.Resolve(targetType, context.Actor, context.Attacker, context.Defender, _targetsBuffer);

            if (_targetsBuffer.Count == 0)
            {
                _coreLog.Debug($"[Story][Battle]: Skill action without target, skillId = {Id}, actionType = {action.ActionType}, targetType = {targetType}");

                return;
            }

            for (int i = 0; i < _targetsBuffer.Count; i++)
                executor.Execute(new SkillActionContext(context, action.Component, _targetsBuffer[i], commands, skillLevel));
        }

        private List<SkillActionDefinition> SortActionsByTiming(int skillLevel)
        {
            var actions = new List<SkillActionDefinition>(_definition.Actions);

            for (int i = 0; i < actions.Count; i++)
            {
                for (int j = 0; j < actions.Count - 1 - i; j++)
                {
                    if (ReadTiming(actions[j].Component, skillLevel) <= ReadTiming(actions[j + 1].Component, skillLevel))
                        continue;

                    (actions[j + 1], actions[j]) = (actions[j], actions[j + 1]);
                }
            }

            return actions;
        }

        private float ReadTiming(SkillComponent component, int skillLevel)
        {
            if (_skillArgumentReader.TryReadFloat(component, TimingParameter, skillLevel, out var timing) == false)
                return 0f;

            if (timing < 0f)
                return 0f;

            return timing;
        }

        private SkillTargetType ReadTargetType(SkillComponent component, int skillLevel, SkillTargetType fallback)
        {
            if (_skillArgumentReader.TryReadInt(component, TargetParameter, skillLevel, out var rawTarget) == false)
                return fallback;

            if (rawTarget == (int)SkillTargetType.Ally)
                return SkillTargetType.Ally;

            if (rawTarget == (int)SkillTargetType.EnemySecondSlot)
                return SkillTargetType.EnemySecondSlot;

            if (rawTarget == (int)SkillTargetType.AllEnemies)
                return SkillTargetType.AllEnemies;

            return SkillTargetType.EnemyFirstSlot;
        }

        private float ResolveCastDuration(int skillLevel)
        {
            var duration = 0f;
            var actions = _definition.Actions;

            for (int i = 0; i < actions.Count; i++)
            {
                var timing = ReadTiming(actions[i].Component, skillLevel);

                if (duration < timing)
                    duration = timing;
            }

            return duration;
        }

        private bool ResolveInstantActivation()
        {
            if (_definition.TryGetTrigger(SkillTriggerType.AllyHealthLower, out var trigger) == false)
                return false;

            return _skillArgumentReader.ReadFlag(trigger.Component, "is_instant_activation", 0);
        }
    }
}
