namespace Server.Battles
{
    internal sealed class OnCooldownTriggerEvaluator : ISkillTriggerEvaluator
    {
        private const string CooldownParameter = "cooldown";
        private const string StartAvailableParameter = "is_start_availiable";

        private readonly ICoreLog _coreLog;
        private readonly ISkillArgumentReader _skillArgumentReader;

        public OnCooldownTriggerEvaluator(ICoreLog coreLog, ISkillArgumentReader skillArgumentReader)
        {
            _coreLog = coreLog;
            _skillArgumentReader = skillArgumentReader;
        }

        public SkillTriggerType TriggerType => SkillTriggerType.OnCooldown;

        public string TypeKey => "on_cooldown";

        public bool CanActivate(SkillComponent component, SkillActivationContext context)
        {
            if (_skillArgumentReader.TryReadInt(component, CooldownParameter, context.SkillLevel, out var cooldownTurns) == false)
            {
                _coreLog.Error($"[Config]: Skill on_cooldown has no readable cooldown parameter, unitId = {context.Owner.Id}");

                return false;
            }

            if (cooldownTurns < 0)
                return false;

            var runtimeState = context.RuntimeState;

            if (0 <= runtimeState.LastActivationTurn)
                return cooldownTurns <= context.CurrentTurn - runtimeState.LastActivationTurn;

            if (_skillArgumentReader.ReadFlag(component, StartAvailableParameter, context.SkillLevel))
                return true;

            return cooldownTurns <= context.CurrentTurn;
        }

        public void OnActivated(SkillComponent component, SkillActivationContext context)
        {
            context.RuntimeState.LastActivationTurn = context.CurrentTurn;
        }
    }
}
