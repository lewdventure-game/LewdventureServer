using Newtonsoft.Json;
using Server.Common;
using Server.Configs;

namespace Server.Equipments
{
    internal sealed class EquipmentMapper : IEquipmentMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("type")]
        public EquipmentType Type { get; init; }

        [JsonProperty("rarity")]
        public RarityType Rarity { get; init; }

        [OptionalColumn("колонка появилась вместе с Equipment_promotes, в старых снапшотах её нет")]
        [JsonProperty("promote_id")]
        public int PromoteId { get; init; }

        [OptionalColumn("колонка заменила equip_bonus_type_1..3, в старых снапшотах её нет")]
        [JsonProperty("bonus_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] BonusIds { get; init; } = Array.Empty<int>();

        [OptionalColumn("колонка заменила skill_id, в старых снапшотах её нет")]
        [JsonProperty("skill_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] SkillIds { get; init; } = Array.Empty<int>();

        [JsonProperty("merge_group")]
        public int MergeGroup { get; init; }

        [JsonProperty("merge_number")]
        public int MergeNumber { get; init; }

        [JsonProperty("merge_requirements")]
        public string MergeRequirements { get; init; } = string.Empty;

        [JsonProperty("art_name")]
        public string ArtName { get; init; } = string.Empty;
    }
}
