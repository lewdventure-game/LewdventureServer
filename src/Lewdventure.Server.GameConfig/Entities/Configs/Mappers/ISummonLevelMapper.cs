using Server.Configs;

namespace Server.Entities
{
    public interface ISummonLevelMapper : IConfigMapper
    {
        public int Id { get; }

        public int PatternId { get; }

        public int Level { get; }

        public string[] ResourceTypes { get; }

        public string[] ResourceIds { get; }

        public int[] ResourceValues { get; }
    }
}
