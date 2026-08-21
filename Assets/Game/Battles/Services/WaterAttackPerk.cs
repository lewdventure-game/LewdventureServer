using Server.Perks;

namespace Server.Battles
{
    internal sealed class WaterAttackPerk : ElementalAttackPerk
    {
        internal WaterAttackPerk(
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

        protected override bool UsesFlatHitRewards => false;

        protected override void OnProjectileHit(
            IPerkExecutionContext context,
            IUnitState owner,
            IUnitState target,
            List<BattleCommand> commands)
        {
            var stacksBefore = CountStatusesOfTypes(context, target, false, true);

            if (stacksBefore <= 0)
            {
                context.Logger.LogDebug($"[Story][Battle] water cleanse skip no poison perkId = {Id} targetId = {target.Id}");

                return;
            }

            if (TryRollRewardsChance(context, target, RewardsChance, "water_cleanse") == false)
            {
                context.Logger.LogDebug($"[Story][Battle] water cleanse roll failed perkId = {Id} stacksBefore = {stacksBefore} targetId = {target.Id}");

                return;
            }

            var cleansed = CleanseStatusesOfTypes(context, target, commands, false, true);
            ApplyHitRewardsTimes(context, owner, target, commands, cleansed);

            if (0 < cleansed && HitRewards.Count == 0)
                context.Logger.LogWarning($"[Story][Battle] water cleanse without hit_rewards perkId = {Id} cleansed = {cleansed}");

            context.Logger.LogDebug($"[Story][Battle] water cleanse poison perkId = {Id} stacksBefore = {stacksBefore} cleansed = {cleansed} rewardsTimes = {cleansed}");
        }
    }
}
