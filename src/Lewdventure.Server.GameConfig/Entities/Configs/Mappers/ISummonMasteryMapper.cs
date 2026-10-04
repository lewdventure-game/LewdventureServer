using Server.Configs;

namespace Server.Entities
{
    public interface ISummonMasteryMapper : IConfigMapper
    {
        public int Id { get; }

        public int MasteryLevel { get; }

        public int CopiesToUpgrade { get; }

        public float DamageMultiplier { get; }

        public string[] RewardTypes { get; }

        public string[] RewardIds { get; }

        public int[] RewardValues { get; }

        public int FrameId { get; }
    }
}
