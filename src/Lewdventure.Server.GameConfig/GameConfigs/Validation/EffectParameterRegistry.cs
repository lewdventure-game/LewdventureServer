namespace Server.GameConfigs
{
    internal sealed class EffectParameterRegistry
    {
        private readonly Dictionary<string, EffectParameterDescriptor> _perks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["reward"] = new EffectParameterDescriptor("reward", new[] { "rewards" }, Array.Empty<string>()),
            ["fire_attack"] = new EffectParameterDescriptor(
                "fire_attack",
                new[] { "damage_ratio", "proc_rounds" },
                new[] { "projectile_count", "hit_rewards", "rewards_chance", "debuff_rewards_chance" }),
            ["earth_attack"] = new EffectParameterDescriptor(
                "earth_attack",
                new[] { "damage_ratio", "proc_rounds" },
                new[] { "projectile_count", "hit_rewards", "rewards_chance", "debuff_rewards_chance" }),
            ["air_attack"] = new EffectParameterDescriptor(
                "air_attack",
                new[] { "damage_ratio", "proc_rounds" },
                new[] { "projectile_count", "hit_rewards", "rewards_chance", "debuff_rewards_chance" }),
            ["water_attack"] = new EffectParameterDescriptor(
                "water_attack",
                new[] { "damage_ratio", "proc_rounds" },
                new[] { "projectile_count", "hit_rewards", "rewards_chance", "debuff_rewards_chance" }),
            ["action_reward"] = new EffectParameterDescriptor(
                "action_reward",
                new[] { "rewards_on_action", "actions" },
                new[] { "rewards", "rewards_chance" }),
            ["resurrection"] = new EffectParameterDescriptor(
                "resurrection",
                new[] { "health_ratio", "resurrections_count" },
                Array.Empty<string>()),
        };

        private readonly Dictionary<string, EffectParameterDescriptor> _statuses = new(StringComparer.OrdinalIgnoreCase)
        {
            ["burning"] = new EffectParameterDescriptor("burning", new[] { "damage_ratio", "damage_length", "max_stacks" }, Array.Empty<string>()),
            ["burning_strong"] = new EffectParameterDescriptor("burning_strong", new[] { "damage_ratio", "damage_length", "max_stacks" }, new[] { "bonuses" }),
            ["poison"] = new EffectParameterDescriptor("poison", new[] { "damage_ratio", "damage_length", "max_stacks" }, Array.Empty<string>()),
            ["poison_strong"] = new EffectParameterDescriptor("poison_strong", new[] { "damage_ratio", "damage_length", "max_stacks" }, new[] { "bonuses" }),
            ["bonus_change"] = new EffectParameterDescriptor("bonus_change", new[] { "bonuses" }, Array.Empty<string>()),
        };

        private readonly Dictionary<string, EffectParameterDescriptor> _skills = new(StringComparer.OrdinalIgnoreCase)
        {
            ["fireball"] = new EffectParameterDescriptor("fireball", new[] { "damage_ratio" }, new[] { "projectile_count", "duration" }),
            ["energy"] = new EffectParameterDescriptor("energy", Array.Empty<string>(), new[] { "duration" }),
            ["summon_1_skill_1"] = new EffectParameterDescriptor(
                "summon_1_skill_1",
                new[] { "damage_ratio" },
                new[] { "hit_rewards", "duration" }),
            ["summon_2_skill_1"] = new EffectParameterDescriptor(
                "summon_2_skill_1",
                new[] { "damage_ratio" },
                new[] { "heal_from_max_ratio", "ally_rewards", "duration" }),
            ["summon_3_skill_1"] = new EffectParameterDescriptor(
                "summon_3_skill_1",
                new[] { "damage_ratio" },
                new[] { "life_steal_ratio", "hit_rewards", "ally_rewards", "duration" }),
        };

        public IReadOnlyCollection<string> PerkTypeKeys => _perks.Keys;

        public IReadOnlyCollection<string> StatusTypeKeys => _statuses.Keys;

        public IReadOnlyCollection<string> SkillTypeKeys => _skills.Keys;

        public bool TryGetPerk(string effectType, out EffectParameterDescriptor descriptor)
        {
            return _perks.TryGetValue(effectType, out descriptor!);
        }

        public bool TryGetStatus(string effectType, out EffectParameterDescriptor descriptor)
        {
            return _statuses.TryGetValue(effectType, out descriptor!);
        }

        public bool TryGetSkill(string effectType, out EffectParameterDescriptor descriptor)
        {
            return _skills.TryGetValue(effectType, out descriptor!);
        }
    }
}
