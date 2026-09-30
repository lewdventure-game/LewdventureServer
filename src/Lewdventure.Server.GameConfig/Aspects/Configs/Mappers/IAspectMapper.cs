using Server.Configs;

namespace Server.Aspects
{
    public interface IAspectMapper : IConfigMapper
    {
        public int Id { get; }

        public int[] BonusIds { get; }
    }
}
