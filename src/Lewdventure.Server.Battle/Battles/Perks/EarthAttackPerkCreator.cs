using Server.Perks;

namespace Server.Battles
{
    internal sealed class EarthAttackPerkCreator : BaseElementalPerkCreator
    {
        public EarthAttackPerkCreator(ICoreLog coreLog, PerkParameterReader perkParameterReader)
            : base(coreLog, perkParameterReader)
        { }

        public override PerkType PerkType => PerkType.EarthAttack;

        public override string TypeKey => "earth_attack";

        protected override IPerk CreateElemental(
            IPerkMapper mapper,
            int projectileCount,
            float damageRatio,
            IReadOnlyList<BattleReward> hitRewards,
            float rewardsChance,
            float debuffRewardsChance,
            IReadOnlyList<int> procRounds)
        {
            return new EarthAttackPerk(mapper, projectileCount, damageRatio, hitRewards, rewardsChance, debuffRewardsChance, procRounds);
        }
    }
}
