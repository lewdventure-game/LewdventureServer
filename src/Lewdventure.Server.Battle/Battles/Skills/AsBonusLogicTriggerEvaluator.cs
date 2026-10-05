namespace Server.Battles
{
    internal sealed class AsBonusLogicTriggerEvaluator : ISkillTriggerEvaluator
    {
        public SkillTriggerType TriggerType => SkillTriggerType.AsBonusLogic;

        public string TypeKey => "as_bonus_logic";

        public bool CanActivate(SkillComponent component, SkillActivationContext context)
        {
            return false;
        }

        public void OnActivated(SkillComponent component, SkillActivationContext context)
        {
        }
    }
}
