using Server.Perks;

namespace Server.Battles
{
    internal sealed class FireAttackPerk : ElementalAttackPerk
    {
        internal FireAttackPerk(
            IPerkMapper mapper,
            int projectileCount,
            float damageRatio,
            IReadOnlyList<BattleReward> hitRewards,
            float rewardsChance,
            float debuffRewardsChance,
            IReadOnlyList<int> procRounds)
            : base(mapper, projectileCount, damageRatio, hitRewards, rewardsChance, debuffRewardsChance, procRounds)
        {
        }

        protected override float ResolveFlatHitRewardsChance(IPerkExecutionContext context, IUnitState target)
        {
            var poisonStacks = CountStatusesOfTypes(context, target, false, true);
            var hasDebuff = 0 < poisonStacks;
            var chance = RewardsChance;

            if (hasDebuff)
                chance = RewardsChance * DebuffRewardsChance;

            context.Logger.LogDebug($"[Story][Battle] fire hit rewards chance perkId = {Id} rewardsChance = {RewardsChance} debuffRewardsChance = {DebuffRewardsChance} poisonStacks = {poisonStacks} hasDebuff = {hasDebuff} effectiveChance = {chance}");

            return chance;
        }
    }
}
