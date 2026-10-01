namespace Server.Battles
{
    internal sealed class SkillActionDefinition
    {
        private readonly SkillActionType _actionType;
        private readonly SkillComponent _component;

        public SkillActionDefinition(SkillActionType actionType, SkillComponent component)
        {
            _actionType = actionType;
            _component = component;
        }

        public SkillActionType ActionType => _actionType;

        public SkillComponent Component => _component;
    }
}
