using Server.Configs;

namespace Server.Entities
{
    internal interface ISummonLevelMapper : IConfigMapper
    {
        public int Id { get; }

        public int PatternId { get; }

        public int Level { get; }

        public int MasteryRequirement { get; }

        public string[] ResourceTypes { get; }

        public string[] ResourceIds { get; }

        public int[] ResourceValues { get; }
    }
}
