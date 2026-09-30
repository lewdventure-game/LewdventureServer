using Server.Configs;

namespace Server.Artifacts
{
    public interface IArtifactMapper : IConfigMapper
    {
        public int Id { get; }

        public int[] BonusIds { get; }
    }
}
