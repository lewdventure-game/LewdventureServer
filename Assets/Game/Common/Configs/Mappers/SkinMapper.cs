using Newtonsoft.Json;

namespace Server.Configs
{
    public class SkinMapper : ISkinMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("art_name")]
        public string ArtName { get; init; } = string.Empty;
    }
}
