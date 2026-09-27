using Server.Perks;

namespace Server.Battles
{
    internal sealed class WaterAttackPerkCreator : BaseElementalPerkCreator
    {
        public WaterAttackPerkCreator(ICoreLog coreLog, PerkParameterReader perkParameterReader)
            : base(coreLog, perkParameterReader)
        { }

        public override PerkType PerkType => PerkType.WaterAttack;

        public override string TypeKey => "water_attack";

        protected override IPerk CreateElemental(
            IPerkMapper mapper,
            int projectileCount,
            float damageRatio,
            IReadOnlyList<BattleReward> hitRewards,
            float rewardsChance,
            float debuffRewardsChance,
            IReadOnlyList<int> procRounds)
        {
            return new WaterAttackPerk(mapper, projectileCount, damageRatio, hitRewards, rewardsChance, debuffRewardsChance, procRounds);
        }
    }
}
