namespace Server.Battles
{
    internal sealed class SkillTriggerDefinition
    {
        private readonly SkillTriggerType _triggerType;
        private readonly SkillComponent _component;

        public SkillTriggerDefinition(SkillTriggerType triggerType, SkillComponent component)
        {
            _triggerType = triggerType;
            _component = component;
        }

        public SkillTriggerType TriggerType => _triggerType;

        public SkillComponent Component => _component;
    }
}
