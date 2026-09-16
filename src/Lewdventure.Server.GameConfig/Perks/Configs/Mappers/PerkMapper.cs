using Newtonsoft.Json;
using Server.Common;

namespace Server.Perks
{
    internal sealed class PerkMapper : IPerkMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("icon_art")]
        public string IconArt { get; init; } = string.Empty;

        [JsonProperty("rarity")]
        public RarityType Rarity { get; init; }

        [JsonProperty("perk_type")]
        public PerkType PerkType { get; init; }

        [JsonProperty("perk_parameters")]
        public string PerkParameters { get; init; } = string.Empty;

        [JsonProperty("proc_order")]
        public int TriggerOrder { get; init; }

        [JsonProperty("name_loc")]
        public string NameLocalizationKey { get; init; } = string.Empty;

        [JsonProperty("desc_loc")]
        public string DescriptionLocalizationKey { get; init; } = string.Empty;
    }
}
