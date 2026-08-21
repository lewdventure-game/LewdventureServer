using Server.Configs;

namespace Server.Stories
{
    internal interface IStoryEventMapper : IConfigMapper
    {
        public int Id { get; }

        public StoryEventType EventType { get; }

        public string EventArtPreset { get; }

        public string EventParameters { get; }

        public int RewardExperienceValue { get; }
    }
}
