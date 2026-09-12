using Newtonsoft.Json;
using Server.Common;
using Server.Configs;

namespace Server.Entities
{
    internal sealed class SummonMapper : ISummonMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("art_name")]
        public string ArtName { get; init; } = string.Empty;

        [JsonProperty("rarity")]
        public RarityType Rarity { get; init; }

        [JsonProperty("dmg_on_lvls")]
        [JsonConverter(typeof(DelimitedFloatArrayConverter), ';')]
        public float[] DamageOnLevels { get; init; } = Array.Empty<float>();

        [JsonProperty("breakout_multis")]
        [JsonConverter(typeof(DelimitedFloatArrayConverter), ';')]
        public float[] BreakoutMultipliers { get; init; } = Array.Empty<float>();

        [JsonProperty("attack_speed")]
        public float AttackSpeed { get; init; }

        [JsonProperty("attack_cooldown")]
        public int AttackCooldown { get; init; }

        [JsonProperty("is_melee")]
        public bool IsMelee { get; init; }

        [JsonProperty("skill_id")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';')]
        public string[] SkillIds { get; init; } = Array.Empty<string>();

        [JsonProperty("mastery_id")]
        public int MasteryId { get; init; }

        [JsonProperty("level_upgrade_pattern")]
        public int LevelUpgradePattern { get; init; }
    }
}
