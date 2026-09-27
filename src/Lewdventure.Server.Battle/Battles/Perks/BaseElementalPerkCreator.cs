using Server.Perks;

namespace Server.Battles
{
    internal abstract class BaseElementalPerkCreator : IPerkCreator
    {
        private readonly ICoreLog _coreLog;
        private readonly PerkParameterReader _perkParameterReader;

        protected BaseElementalPerkCreator(ICoreLog coreLog, PerkParameterReader perkParameterReader)
        {
            _coreLog = coreLog;
            _perkParameterReader = perkParameterReader;
        }

        public abstract PerkType PerkType { get; }

        public abstract string TypeKey { get; }

        public IPerk Create(IPerkMapper mapper)
        {
            _perkParameterReader.Read(mapper);

            var projectileCount = _perkParameterReader.Int("projectile_count", 1);
            var damageRatio = _perkParameterReader.Float("damage_ratio", 0f);
            var hitRewards = _perkParameterReader.Rewards("hit_rewards");
            var rewardsChance = _perkParameterReader.Float("rewards_chance", 0f);
            var debuffRewardsChance = _perkParameterReader.Float("debuff_rewards_chance", 1f);
            var procRounds = _perkParameterReader.ProcRounds();

            if (procRounds.Count == 0)
                _coreLog.Warning($"[Story][Battle]: Perk elemental proc_rounds empty, perkId = {mapper.Id} type = {PerkType}");

            if (damageRatio <= 0f)
                _coreLog.Error($"[Story][Battle]: Perk elemental invalid damage_ratio, id = {mapper.Id}, type = {PerkType}");

            return CreateElemental(mapper, projectileCount, damageRatio, hitRewards, rewardsChance, debuffRewardsChance, procRounds);
        }

        protected abstract IPerk CreateElemental(
            IPerkMapper mapper,
            int projectileCount,
            float damageRatio,
            IReadOnlyList<BattleReward> hitRewards,
            float rewardsChance,
            float debuffRewardsChance,
            IReadOnlyList<int> procRounds);
    }
}
