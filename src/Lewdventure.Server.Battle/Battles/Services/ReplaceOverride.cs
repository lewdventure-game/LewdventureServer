using Server.Bonuses;

namespace Server.Battles
{
    internal readonly struct ReplaceOverride
    {
        public BonusType BonusType { get; }

        public float Value { get; }

        public ReplaceOverride(BonusType bonusType, float value)
        {
            BonusType = bonusType;
            Value = value;
        }
    }
}
