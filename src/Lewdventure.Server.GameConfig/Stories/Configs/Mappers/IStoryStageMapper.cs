using Server.Configs;

namespace Server.Stories
{
    internal interface IStoryStageMapper : IConfigMapper
    {
        public int Id { get; }

        public bool IsBossLevel { get; }

        public int[] EventIds { get; }

        public int[] EventChances { get; }

        public float EnemyStatsMultiplier { get; }
    }
}
