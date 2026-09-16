using Newtonsoft.Json;

namespace Server.Equipments
{
    internal sealed class EquipmentMapper : IEquipmentMapper
    {
        [JsonProperty("id")]
        public string Id { get; init; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; init; } = string.Empty;

        [JsonProperty("rarity")]
        public string Rarity { get; init; } = string.Empty;

        [JsonProperty("lvlup_types")]
        public string LevelUpTypes { get; init; } = string.Empty;

        [JsonProperty("lvlup_values")]
        public string LevelUpValues { get; init; } = string.Empty;

        [JsonProperty("equip_bonus_type_1")]
        public string EquipmentBonusTypeOne { get; init; } = string.Empty;

        [JsonProperty("equip_bonus_values_1")]
        public string EquipmentBonusValuesOne { get; init; } = string.Empty;

        [JsonProperty("equip_bonus_type_2")]
        public string EquipmentBonusTypeTwo { get; init; } = string.Empty;

        [JsonProperty("equip_bonus_values_2")]
        public string EquipmentBonusValuesTwo { get; init; } = string.Empty;

        [JsonProperty("equip_bonus_type_3")]
        public string EquipmentBonusTypeThree { get; init; } = string.Empty;

        [JsonProperty("equip_bonus_values_3")]
        public string EquipmentBonusValuesThree { get; init; } = string.Empty;

        [JsonProperty("merge_group")]
        public string MergeGroup { get; init; } = string.Empty;

        [JsonProperty("merge_number")]
        public string MergeNumber { get; init; } = string.Empty;

        [JsonProperty("merge_requirements")]
        public string MergeRequirements { get; init; } = string.Empty;

        [JsonProperty("art_name")]
        public string ArtName { get; init; } = string.Empty;

        [JsonProperty("is_melee")]
        public string IsMelee { get; init; } = string.Empty;

        [JsonProperty("skill_id")]
        public string SkillId { get; init; } = string.Empty;
    }
}
