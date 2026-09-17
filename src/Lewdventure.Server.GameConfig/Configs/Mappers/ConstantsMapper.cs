using Newtonsoft.Json;

namespace Server.Configs
{
    internal class ConstantsMapper : IConstantsMapper
    {
        [JsonProperty("constant_name")]
        public string ConstantName { get; init; } = string.Empty;

        [JsonProperty("constant_value")]
        public string ConstantValue { get; init; } = string.Empty;

        [JsonProperty("constant_type")]
        public ValueType ConstantType { get; init; }
    }
}
