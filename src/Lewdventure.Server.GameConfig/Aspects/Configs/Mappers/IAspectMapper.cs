using Server.Configs;

namespace Server.Aspects
{
    internal interface IAspectMapper : IConfigMapper
    {
        public int Id { get; }

        public int[] BonusIds { get; }

        public float[] BonusValues { get; }
    }
}
