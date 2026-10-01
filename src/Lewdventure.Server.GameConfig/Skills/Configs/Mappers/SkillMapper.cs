using Newtonsoft.Json;
using Server.Configs;

namespace Server.Skills
{
    internal sealed class SkillMapper : ISkillMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [OptionalColumn("устаревшая колонка, механика скилла собирается из triggers и actions")]
        [JsonProperty("type")]
        public string Type { get; init; } = string.Empty;

        [OptionalColumn("устаревшая колонка, порядок скиллов задаёт skill_ids носителя")]
        [JsonProperty("proc_order")]
        public int ProcOrder { get; init; }

        [OptionalColumn("устаревшая колонка, параметры лежат внутри triggers и actions")]
        [JsonProperty("parameters")]
        public string Parameters { get; init; } = string.Empty;

        [OptionalColumn("колонка нового формата скиллов")]
        [JsonProperty("triggers")]
        public string Triggers { get; init; } = string.Empty;

        [OptionalColumn("колонка нового формата скиллов")]
        [JsonProperty("actions")]
        public string Actions { get; init; } = string.Empty;
    }
}
