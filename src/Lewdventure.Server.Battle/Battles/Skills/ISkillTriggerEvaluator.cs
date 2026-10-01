namespace Server.Battles
{
    internal interface ISkillTriggerEvaluator
    {
        public SkillTriggerType TriggerType { get; }

        public string TypeKey { get; }

        public bool CanActivate(SkillComponent component, SkillActivationContext context);

        public void OnActivated(SkillComponent component, SkillActivationContext context);
    }
}
