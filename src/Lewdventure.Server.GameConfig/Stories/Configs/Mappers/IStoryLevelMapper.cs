using Server.Configs;

namespace Server.Stories
{
    public interface IStoryLevelMapper : IConfigMapper
    {
        public int Id { get; }

        public string Background { get; }

        public string StartPhraseLocKey { get; }

        public int[] StageIds { get; }

        public int ExperienceLevelId { get; }

        public int MaxBattleTurns { get; }

        public StoryLevelTriggerType[] TriggerTypes { get; }

        public int[] TriggerValues { get; }
    }
}
