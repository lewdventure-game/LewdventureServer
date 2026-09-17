using Server.Bonuses;

namespace Server.Battles
{
    internal interface ICharacteristicBucketApplicator
    {
        public void Apply(CharacteristicBuckets buckets, BonusType bonusType, float value);
    }
}
