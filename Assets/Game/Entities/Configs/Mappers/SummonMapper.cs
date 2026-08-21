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

        [JsonProperty("skill_id")]
        public string SkillId { get; init; } = string.Empty;

        [JsonProperty("mastery_id")]
        public int MasteryId { get; init; }

        [JsonProperty("level_upgrade_pattern")]
        public int LevelUpgradePattern { get; init; }

        [JsonProperty("bonus_mastery_levels")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] BonusMasteryLevels { get; init; } = Array.Empty<int>();

        [JsonProperty("bonus_types")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] BonusTypes { get; init; } = Array.Empty<int>();

        [JsonProperty("is_melee")]
        public string IsMelee { get; init; } = string.Empty;
    }
}
