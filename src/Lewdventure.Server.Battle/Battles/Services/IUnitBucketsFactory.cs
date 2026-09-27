using Server.Entities;

namespace Server.Battles
{
    internal interface IUnitBucketsFactory
    {
        public CharacteristicBuckets BuildConstantsBuckets();

        public CharacteristicBuckets BuildEnemyBuckets(IEnemyMapper enemyMapper, int storyLevelId, int stageId);

        public CharacteristicBuckets BuildSummonBuckets(IUnitSnapshot unitSnapshot);

        public void ApplyBreakoutHook(IUnitSnapshot unitSnapshot, ISummonMapper summonMapper);

        public UnitFlags ResolveSummonMeleeFlags(IUnitSnapshot unitSnapshot);

        public int ResolveSummonAttackCooldown(IUnitSnapshot unitSnapshot);

        public bool TryResolveMastery(int masteryId, int masteryLevel, int summonId, out IMasteryMapper masteryMapper);
    }
}
