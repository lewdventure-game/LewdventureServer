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

        [JsonProperty("promote_id")]
        public int PromoteId { get; init; }

        [JsonProperty("skill_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] SkillIds { get; init; } = Array.Empty<int>();

        [JsonProperty("promote_to_skill_levels")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] PromoteToSkillLevels { get; init; } = Array.Empty<int>();

        [JsonProperty("art_name")]
        public string ArtName { get; init; } = string.Empty;
    }
}
