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

        [JsonProperty("upgrade_costs")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] UpgradeCosts { get; init; } = Array.Empty<int>();

        [JsonProperty("start_bonus")]
        [OptionalColumn("пока лист не переименован, читается устаревшая колонка start_bonus_type")]
        public int StartBonus { get; init; }

        [JsonProperty("start_bonus_type")]
        [OptionalColumn("устаревшая колонка, переименуйте в start_bonus")]
        public int LegacyStartBonus { get; init; }

        [JsonProperty("upgrade_bonuses")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        [OptionalColumn("пока лист не переименован, читается устаревшая колонка upgrade_bonus_types")]
        public int[] UpgradeBonuses { get; init; } = Array.Empty<int>();

        [JsonProperty("upgrade_bonus_types")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        [OptionalColumn("устаревшая колонка, переименуйте в upgrade_bonuses")]
        public int[] LegacyUpgradeBonuses { get; init; } = Array.Empty<int>();

        [JsonProperty("art_name")]
        public string ArtName { get; init; } = string.Empty;

        [JsonProperty("skill_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] SkillIds { get; init; } = Array.Empty<int>();

        public int StartBonusId => 0 < StartBonus ? StartBonus : LegacyStartBonus;

        public int[] UpgradeBonusIds => 0 < UpgradeBonuses.Length ? UpgradeBonuses : LegacyUpgradeBonuses;
    }
}
