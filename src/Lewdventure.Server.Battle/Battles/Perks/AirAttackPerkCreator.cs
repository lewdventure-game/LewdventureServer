using Server.Perks;

namespace Server.Battles
{
    internal sealed class AirAttackPerkCreator : BaseElementalPerkCreator
    {
        public AirAttackPerkCreator(ICoreLog coreLog, PerkParameterReader perkParameterReader)
            : base(coreLog, perkParameterReader)
        { }

        public override PerkType PerkType => PerkType.AirAttack;

        public override string TypeKey => "air_attack";

        protected override IPerk CreateElemental(
            IPerkMapper mapper,
            int projectileCount,
            float damageRatio,
            IReadOnlyList<BattleReward> hitRewards,
            float rewardsChance,
            float debuffRewardsChance,
            IReadOnlyList<int> procRounds)
        {
            return new AirAttackPerk(mapper, projectileCount, damageRatio, hitRewards, rewardsChance, debuffRewardsChance, procRounds);
        }
    }
}
