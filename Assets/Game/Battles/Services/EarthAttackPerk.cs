using Server.Perks;

namespace Server.Battles
{
    internal sealed class EarthAttackPerk : ElementalAttackPerk
    {
        internal EarthAttackPerk(
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
            var burnStacks = CountStatusesOfTypes(context, target, true, false);
            var hasDebuff = 0 < burnStacks;
            var chance = RewardsChance;

            if (hasDebuff)
                chance = RewardsChance * DebuffRewardsChance;

            context.Logger.LogDebug($"[Story][Battle]: Earth hit rewards chance, perkId = {Id}, rewardsChance = {RewardsChance}, debuffRewardsChance = {DebuffRewardsChance}, burnStacks = {burnStacks}, hasDebuff = {hasDebuff},s effectiveChance = {chance}");

            return chance;
        }
    }
}
