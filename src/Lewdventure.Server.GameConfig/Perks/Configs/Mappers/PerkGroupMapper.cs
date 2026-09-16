using Newtonsoft.Json;
using Server.Configs;

namespace Server.Perks
{
    internal sealed class PerkGroupMapper : IPerkGroupMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("choice_type")]
        public PerkChoiceType ChoiceType { get; init; }

        [JsonProperty("perk_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] PerkIds { get; init; } = Array.Empty<int>();

        [JsonProperty("random_perks_count")]
        public int RandomPerksCount { get; init; }

        [JsonProperty("choice_count")]
        public int ChoiceCount { get; init; }
    }
}
