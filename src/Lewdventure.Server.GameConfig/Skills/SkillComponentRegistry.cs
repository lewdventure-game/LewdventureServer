namespace Server.Skills
{
    internal sealed class SkillComponentRegistry
    {
        private const string Target = "target";
        private const string Timing = "timing";
        private const string InstantActivation = "is_instant_activation";

        private readonly List<SkillComponentDefinition> _triggers;
        private readonly List<SkillComponentDefinition> _actions;

        public SkillComponentRegistry()
        {
            _triggers = new List<SkillComponentDefinition>
            {
                new("energy_needed", new[] { "energy", InstantActivation }),
                new("ally_health_lower", new[] { "health", InstantActivation }),
                new("on_cooldown", new[] { "cooldown", "is_start_availiable", InstantActivation }),
            };

            _actions = new List<SkillComponentDefinition>
            {
                new("damage", new[] { "damage", "is_spell_amp", Target, Timing }),
                new("set_status", new[] { "status_id", Target, Timing }),
                new("set_bonus", new[] { "bonus_id", Target, Timing }),
            };
        }

        public IReadOnlyList<SkillComponentDefinition> Triggers => _triggers;

        public IReadOnlyList<SkillComponentDefinition> Actions => _actions;

        public bool TryGetTrigger(string name, out SkillComponentDefinition definition)
        {
            return TryGet(_triggers, name, out definition);
        }

        public bool TryGetAction(string name, out SkillComponentDefinition definition)
        {
            return TryGet(_actions, name, out definition);
        }

        private bool TryGet(List<SkillComponentDefinition> source, string name, out SkillComponentDefinition definition)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (string.Equals(source[i].Name, name, StringComparison.Ordinal) == false)
                    continue;

                definition = source[i];

                return true;
            }

            definition = null!;

            return false;
        }
    }
}
