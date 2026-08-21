using Newtonsoft.Json;
using Server.Configs;

namespace Server.Trainings
{
    internal sealed class TrainingMapper : ITrainingMapper
    {
        [JsonProperty("level")]
        public int Level { get; init; }

        [JsonProperty("bonus_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] BonusIds { get; init; } = Array.Empty<int>();

        [JsonProperty("bonus_values")]
        [JsonConverter(typeof(DelimitedFloatArrayConverter), ';')]
        public float[] BonusValues { get; init; } = Array.Empty<float>();
    }
}
