using Newtonsoft.Json;

namespace Server.Entities
{
    internal sealed class MasteryMapper : IMasteryMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("mastery_level")]
        public int MasteryLevel { get; init; }

        [JsonProperty("copies_to_upgrade")]
        public int CopiesToUpgrade { get; init; }

        [JsonProperty("bonus_id")]
        public int BonusId { get; init; }

        [JsonProperty("art_name")]
        public string ArtName { get; init; } = string.Empty;

        [JsonProperty("dmg_multiplier")]
        public float DamageMultiplier { get; init; }
    }
}
