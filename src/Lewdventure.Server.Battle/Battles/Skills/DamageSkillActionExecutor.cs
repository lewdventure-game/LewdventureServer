namespace Server.Battles
{
    internal sealed class DamageSkillActionExecutor : ISkillActionExecutor
    {
        private const string DamageParameter = "damage";
        private const string SpellAmplifierParameter = "is_spell_amp";

        private readonly ICoreLog _coreLog;
        private readonly ISkillArgumentReader _skillArgumentReader;

        public DamageSkillActionExecutor(ICoreLog coreLog, ISkillArgumentReader skillArgumentReader)
        {
            _coreLog = coreLog;
            _skillArgumentReader = skillArgumentReader;
        }

        public SkillActionType ActionType => SkillActionType.Damage;

        public string TypeKey => "damage";

        public void Execute(SkillActionContext context)
        {
            if (_skillArgumentReader.TryReadFloat(context.Component, DamageParameter, context.SkillLevel, out var damageRatio) == false)
            {
                _coreLog.Error("[Config]: Skill damage action has no readable damage parameter");

                return;
            }

            var executionContext = context.ExecutionContext;
            var multiplier = damageRatio;

            if (_skillArgumentReader.ReadFlag(context.Component, SpellAmplifierParameter, context.SkillLevel))
                multiplier *= executionContext.Actor.CharacteristicState.SkillMultiplier;

            executionContext.DealFixedDamage(context.Target, multiplier, context.Commands);
        }
    }
}
