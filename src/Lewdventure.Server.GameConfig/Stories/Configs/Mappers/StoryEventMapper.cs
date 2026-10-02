using Newtonsoft.Json;
using Server.Configs;

namespace Server.Stories
{
    internal sealed class StoryEventMapper : IStoryEventMapper
    {
        private int _rewardExperienceValue;

        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("event_type")]
        public StoryEventType EventType { get; init; }

        [JsonProperty("event_art_preset")]
        public string EventArtPreset { get; init; } = string.Empty;

        [JsonProperty("event_parameters")]
        public string EventParameters { get; init; } = string.Empty;

        [OptionalColumn("колонка переименована из level_exp, в старых снапшотах её нет")]
        [JsonProperty("reward_xp_value")]
        private int RewardExperienceRaw
        {
            set { _rewardExperienceValue = value; }
        }

        [OptionalColumn("колонка переименована в reward_xp_value, читается для совместимости со старыми снапшотами")]
        [JsonProperty("level_exp")]
        private int LegacyRewardExperienceRaw
        {
            set { _rewardExperienceValue = value; }
        }

        public int RewardExperienceValue => _rewardExperienceValue;
    }
}
