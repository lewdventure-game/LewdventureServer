using Newtonsoft.Json;
using Server.Configs;

namespace Server.Entities
{
    internal sealed class CharacterMapper : ICharacterMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("is_melee")]
        public bool IsMelee { get; init; }

        // GDD: length = max upgrades; index i = cost of upgrade i + 1. Scalar sheet value → one-element array.
        [JsonProperty("upgrade_costs")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] UpgradeCosts { get; init; } = Array.Empty<int>();

        // Sheet column name is start_bonus_type; value is bonus id from Bonuses table.
        [JsonProperty("start_bonus_type")]
        public int StartBonusId { get; init; }

        [JsonProperty("start_bonus_value")]
        public int StartBonusValue { get; init; }

        // GDD: index i = bonus id for upgrade i + 1 (paired with upgrade_costs / upgrade_bonus_values).
        [JsonProperty("upgrade_bonus_types")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] UpgradeBonusTypes { get; init; } = Array.Empty<int>();

        [JsonProperty("upgrade_bonus_values")]
        [JsonConverter(typeof(DelimitedFloatArrayConverter), ';')]
        public float[] UpgradeBonusValues { get; init; } = Array.Empty<float>();

        [JsonProperty("art_name")]
        public string ArtName { get; init; } = string.Empty;

        [JsonProperty("skill_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] SkillIds { get; init; } = Array.Empty<int>();
    }
}
