using Newtonsoft.Json;

namespace Server.Stories
{
    internal sealed class StoryEventMapper : IStoryEventMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("event_type")]
        public StoryEventType EventType { get; init; }

        [JsonProperty("event_art_preset")]
        public string EventArtPreset { get; init; } = string.Empty;

        [JsonProperty("event_parameters")]
        public string EventParameters { get; init; } = string.Empty;

        [JsonProperty("level_exp")]
        public int RewardExperienceValue { get; init; }
    }
}
