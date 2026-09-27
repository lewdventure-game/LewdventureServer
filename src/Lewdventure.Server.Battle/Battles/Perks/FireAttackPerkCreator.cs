using Server.Perks;

namespace Server.Battles
{
    internal sealed class FireAttackPerkCreator : BaseElementalPerkCreator
    {
        public FireAttackPerkCreator(ICoreLog coreLog, PerkParameterReader perkParameterReader)
            : base(coreLog, perkParameterReader)
        { }

        public override PerkType PerkType => PerkType.FireAttack;

        public override string TypeKey => "fire_attack";

        protected override IPerk CreateElemental(
            IPerkMapper mapper,
            int projectileCount,
            float damageRatio,
            IReadOnlyList<BattleReward> hitRewards,
            float rewardsChance,
            float debuffRewardsChance,
            IReadOnlyList<int> procRounds)
        {
            return new FireAttackPerk(mapper, projectileCount, damageRatio, hitRewards, rewardsChance, debuffRewardsChance, procRounds);
        }
    }
}
