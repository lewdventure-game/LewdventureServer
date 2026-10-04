using Server.Configs;

namespace Server.Skills
{
    public interface ISkillPromoteMapper : IConfigMapper
    {
        public int Id { get; }

        public int PatternId { get; }

        public int Level { get; }

        public string[] ResourceTypes { get; }

        public string[] ResourceIds { get; }

        public int[] ResourceValues { get; }

        public int LevelToUnlock { get; }
    }
}
