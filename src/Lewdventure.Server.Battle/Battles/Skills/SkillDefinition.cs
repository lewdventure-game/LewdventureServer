namespace Server.Battles
{
    internal sealed class SkillDefinition
    {
        private readonly int _id;
        private readonly IReadOnlyList<SkillTriggerDefinition> _triggers;
        private readonly IReadOnlyList<SkillActionDefinition> _actions;

        public SkillDefinition(int id, IReadOnlyList<SkillTriggerDefinition> triggers, IReadOnlyList<SkillActionDefinition> actions)
        {
            _id = id;
            _triggers = triggers;
            _actions = actions;
        }

        public int Id => _id;

        public IReadOnlyList<SkillTriggerDefinition> Triggers => _triggers;

        public IReadOnlyList<SkillActionDefinition> Actions => _actions;

        public bool HasTrigger(SkillTriggerType triggerType)
        {
            for (int i = 0; i < _triggers.Count; i++)
            {
                if (_triggers[i].TriggerType == triggerType)
                    return true;
            }

            return false;
        }

        public bool TryGetTrigger(SkillTriggerType triggerType, out SkillTriggerDefinition trigger)
        {
            for (int i = 0; i < _triggers.Count; i++)
            {
                if (_triggers[i].TriggerType != triggerType)
                    continue;

                trigger = _triggers[i];

                return true;
            }

            trigger = null!;

            return false;
        }
    }
}
