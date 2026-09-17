using Server.Configs;

namespace Server.Bonuses
{
    internal interface IBonusMapper : IConfigMapper
    {
        public int Id { get; }

        public BonusOperatorType OperatorType { get; }

        public string WorkModeParameters { get; }

        public BonusType BonusType { get; }

        public float BonusValue { get; }
    }
}
