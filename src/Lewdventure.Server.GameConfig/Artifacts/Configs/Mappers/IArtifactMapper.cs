using Server.Configs;

namespace Server.Artifacts
{
    internal interface IArtifactMapper : IConfigMapper
    {
        public int Id { get; }

        public int[] BonusIds { get; }

        public float[] BonusValues { get; }
    }
}
