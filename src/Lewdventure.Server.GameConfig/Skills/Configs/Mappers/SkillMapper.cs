using Newtonsoft.Json;

namespace Server.Skills
{
    internal sealed class SkillMapper : ISkillMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("type")]
        public string Type { get; init; } = string.Empty;

        [JsonProperty("proc_order")]
        public int ProcOrder { get; init; }

        [JsonProperty("parameters")]
        public string Parameters { get; init; } = string.Empty;
    }
}
