using Server.Configs;

namespace Server.Entities
{
    public interface ICharacterPromoteMapper : IConfigMapper
    {
        public int Id { get; }

        public int PromoteLevel { get; }

        public int FrameId { get; }

        public int CopiesToUpgrade { get; }

        public string[] RewardTypes { get; }

        public string[] RewardIds { get; }

        public int[] RewardValues { get; }
    }
}
