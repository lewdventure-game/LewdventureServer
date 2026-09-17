using Newtonsoft.Json;

namespace Server.Bonuses
{
    internal sealed class BonusMapper : IBonusMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("operator")]
        public BonusOperatorType OperatorType { get; init; }

        [JsonProperty("work_mode")]
        public string WorkModeParameters { get; init; } = string.Empty;

        [JsonProperty("bonus_type")]
        public BonusType BonusType { get; init; }

        [JsonProperty("bonus_value")]
        public float BonusValue { get; init; }
    }
}
