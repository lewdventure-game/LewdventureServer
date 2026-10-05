using System.Globalization;
using Server.Common;
using Server.Configs;
using Server.Entities;
using Server.Equipments;
using Server.Services;
using Server.Statuses;

namespace Server.Battles
{
    internal sealed class UnitBucketsFactory : IUnitBucketsFactory
    {

        private const float DefaultMasteryMultiplier = 1f;

        private readonly IBattleConstantsReader _battleConstantsReader;
        private readonly ICoreLog _coreLog;
        private readonly IConfigDistributor _configDistributor;

        public UnitBucketsFactory(
            IBattleConstantsReader battleConstantsReader,
            ICoreLog coreLog,
            IConfigDistributor configDistributor)
        {
            _battleConstantsReader = battleConstantsReader;
            _coreLog = coreLog;
            _configDistributor = configDistributor;
        }

        public CharacteristicBuckets BuildEnemyBuckets(IEnemyMapper enemyMapper, int storyLevelId, int stageId)
        {
            var levelMultiplier = ReadStoryLevelMultiplier(storyLevelId);
            var stageMultiplier = 1f;

            if (0 < stageId)
            {
                if (_configDistributor.StoryStages.TryGet(stageId, out var storyStage) == false)
                {
                    _coreLog.Error($"[Story][Battle]: Story stage missing id = {stageId}");

                    throw new InvalidOperationException($"[Story][Battle]: Story stage missing id = {stageId}");
                }

                stageMultiplier = storyStage.EnemyStatsMultiplier;

                if (stageMultiplier <= 0f)
                {
                    _coreLog.Error($"[Story][Battle]: Invalid enemy_stats_multiplier = {stageMultiplier}, stageId = {stageId}");

                    throw new InvalidOperationException($"[Story][Battle]: Invalid enemy_stats_multiplier = {stageMultiplier}, stageId = {stageId}");
                }
            }

            stageMultiplier *= levelMultiplier;

            _coreLog.Debug($"[Story][Battle]: Enemy multipliers, storyLevelId = {storyLevelId}, stageId = {stageId}, level = {levelMultiplier}, total = {stageMultiplier}");

            var buckets = new CharacteristicBuckets
            {
                HealthBase = enemyMapper.Health * stageMultiplier,
                DamageBase = enemyMapper.Damage * stageMultiplier,
                AttackMultiplierBase = 1f,
                ArmorBase = enemyMapper.Defence,
                DefenceCoefficient = _battleConstantsReader.Get(ConstantKeys.DefenceCoefficientKey),
                EvasionBase = enemyMapper.Evasion,
                CriticalChanceBase = enemyMapper.CriticalChance,
                CriticalMultiplierBase = enemyMapper.CriticalMultiplier,
                Combo1ChanceBase = enemyMapper.Combo1Chance,
                Combo2ChanceBase = enemyMapper.Combo2Chance,
                ComboMultiplierBase = ResolveEnemyComboMultiplier(enemyMapper),
                CounterChanceBase = enemyMapper.CounterChance,
                CounterMultiplierBase = enemyMapper.CounterMultiplier,
                SkillMultiplierBase = enemyMapper.SpellMultiplier,
                EnergyBase = enemyMapper.Energy,
                EnergyMaxBase = enemyMapper.MaxEnergy,
                VampyrismBase = enemyMapper.Vampyrism,
                HealingBoostBase = enemyMapper.HealingBoost,
            };

            _coreLog.Debug($"[Story][Battle]: Enemy buckets seeded, id = {enemyMapper.Id}, armor = {buckets.ArmorBase}, comboMnStart = {buckets.ComboMultiplierBase}");

            return buckets;
        }

        public CharacteristicBuckets BuildSummonBuckets(IUnitSnapshot unitSnapshot)
        {
            var buckets = BuildConstantsBuckets();

            if (_configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapper) == false)
            {
                _coreLog.Error($"[Story][Battle]: Summon config missing id = {unitSnapshot.Id}");

                return buckets;
            }

            buckets.DamageBase = ResolveSummonDamage(unitSnapshot, summonMapper);

            return buckets;
        }

        private float ResolveSummonDamage(IUnitSnapshot unitSnapshot, ISummonMapper summonMapper)
        {
            var damageOnLevels = summonMapper.DamageOnLevels;
            var levelIndex = unitSnapshot.Level - 1;
            var baseDamage = 0f;

            if (0 <= levelIndex && levelIndex < damageOnLevels.Length)
                baseDamage = damageOnLevels[levelIndex];
            else if (0 < damageOnLevels.Length)
                baseDamage = damageOnLevels[damageOnLevels.Length - 1];
            else
                _coreLog.Error($"[Story][Battle]: Summon id = {unitSnapshot.Id} has empty dmg_on_lvls");

            var masteryLevel = unitSnapshot.MasteryLevel;

            if (masteryLevel <= 0)
            {
                _coreLog.Debug($"[Story][Battle]: Summon mastery unused, summonId = {unitSnapshot.Id}, masteryLevel = {masteryLevel}, baseDamage = {baseDamage}");

                return baseDamage;
            }

            var multiplier = ResolveMasteryMultiplier(summonMapper.MasteryId, masteryLevel, unitSnapshot.Id);

            _coreLog.Debug($"[Story][Battle]: Summon mastery damage, summonId = {unitSnapshot.Id}, masteryId = {summonMapper.MasteryId}, masteryLevel = {masteryLevel}, multiplier = {multiplier}, baseDamage = {baseDamage}");

            return baseDamage * multiplier;
        }

        private float ResolveMasteryMultiplier(int masteryId, int masteryLevel, int summonId)
        {
            if (TryResolveMastery(masteryId, masteryLevel, summonId, out var masteryMapper))
                return masteryMapper.DamageMultiplier;

            return DefaultMasteryMultiplier;
        }

        private bool TryResolveMastery(int masteryId, int masteryLevel, int summonId, out ISummonMasteryMapper masteryMapper)
        {
            if (_configDistributor.SummonMasteries.TryGet(masteryId, masteryLevel, out masteryMapper))
                return true;

            _coreLog.Warning($"[Story][Battle]: Mastery missing, masteryId = {masteryId}, masteryLevel = {masteryLevel}, summonId = {summonId}; using multiplier {DefaultMasteryMultiplier}");

            return false;
        }

        public UnitFlags ResolveSummonMeleeFlags(IUnitSnapshot unitSnapshot)
        {
            if (_configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapper) == false)
            {
                _coreLog.Error($"[Story][Battle]: Summon melee flags missing config, id = {unitSnapshot.Id}, flags = Range");

                return UnitFlags.Range;
            }

            if (summonMapper.IsMelee)
            {
                _coreLog.Debug($"[Story][Battle]: Summon melee flags, id = {unitSnapshot.Id}, isMelee = {summonMapper.IsMelee}, flags = Melee");

                return UnitFlags.Melee;
            }

            _coreLog.Debug($"[Story][Battle]: Summon melee flags, id = {unitSnapshot.Id}, isMelee = {summonMapper.IsMelee}, flags = Range");

            return UnitFlags.Range;
        }

        public int ResolveSummonAttackCooldown(IUnitSnapshot unitSnapshot)
        {
            if (_configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapper) == false)
            {
                _coreLog.Error($"[Story][Battle]: Summon attack cooldown missing config, id = {unitSnapshot.Id}");

                return 0;
            }

            var attackCooldownTurns = summonMapper.AttackCooldown;

            if (attackCooldownTurns < 0)
            {
                _coreLog.Error($"[Story][Battle]: Summon attack_cooldown negative, id = {unitSnapshot.Id}, attackCooldown = {attackCooldownTurns}");

                throw new InvalidOperationException($"[Story][Battle]: Summon attack_cooldown negative, id = {unitSnapshot.Id}, attackCooldown = {attackCooldownTurns}");
            }

            _coreLog.Debug($"[Story][Battle]: Summon attack cooldown, id = {unitSnapshot.Id}, attackCooldown = {attackCooldownTurns}");

            return attackCooldownTurns;
        }

        public CharacteristicBuckets BuildConstantsBuckets()
        {
            var buckets = new CharacteristicBuckets();
            SeedBucketsFromConstants(buckets);

            return buckets;
        }

        private void SeedBucketsFromConstants(CharacteristicBuckets buckets)
        {
            buckets.HealthBase = _battleConstantsReader.Get(ConstantKeys.HealthBaseKey);
            buckets.DamageBase = _battleConstantsReader.Get(ConstantKeys.DamageBaseKey);
            buckets.AttackMultiplierBase = _battleConstantsReader.Get(ConstantKeys.AttackMultiplierBaseKey);
            buckets.ArmorBase = _battleConstantsReader.Get(ConstantKeys.DefenceBaseKey);
            buckets.EvasionBase = _battleConstantsReader.Get(ConstantKeys.EvasionBaseKey);
            buckets.CriticalChanceBase = _battleConstantsReader.Get(ConstantKeys.CriticalChanceBaseKey);
            buckets.CriticalMultiplierBase = _battleConstantsReader.Get(ConstantKeys.CriticalMultiplierBaseKey);
            buckets.Combo1ChanceBase = _battleConstantsReader.Get(ConstantKeys.ComboOneChanceBaseKey);
            buckets.Combo2ChanceBase = _battleConstantsReader.Get(ConstantKeys.ComboTwoChanceBaseKey);
            buckets.ComboMultiplierBase = _battleConstantsReader.Get(ConstantKeys.ComboMultiplierBaseKey);
            buckets.CounterChanceBase = _battleConstantsReader.Get(ConstantKeys.CounterChanceBaseKey);
            buckets.CounterMultiplierBase = _battleConstantsReader.Get(ConstantKeys.CounterMultiplierBaseKey);
            buckets.SkillMultiplierBase = _battleConstantsReader.Get(ConstantKeys.SkillMultiplierBaseKey);
            buckets.EnergyBase = _battleConstantsReader.Get(ConstantKeys.EnergyBaseKey);
            buckets.EnergyMaxBase = _battleConstantsReader.Get(ConstantKeys.EnergyMaxBaseKey);
            buckets.DefenceCoefficient = _battleConstantsReader.Get(ConstantKeys.DefenceCoefficientKey);
            buckets.HealingBoostBase = _battleConstantsReader.Get(ConstantKeys.HealingBoostBaseKey);
            buckets.VampyrismBase = _battleConstantsReader.Get(ConstantKeys.VampyrismBaseKey);

            _coreLog.Debug($"[Story][Battle]: Constants buckets seeded, maxHealthBase = {buckets.HealthBase}, damageBase = {buckets.DamageBase}, armorBase = {buckets.ArmorBase}, vampyrismBase = {buckets.VampyrismBase}, comboMnBase = {buckets.ComboMultiplierBase}");
        }

        private float ResolveEnemyComboMultiplier(IEnemyMapper enemyMapper)
        {
            var combo1 = enemyMapper.Combo1Multiplier;
            var combo2 = enemyMapper.Combo2Multiplier;

            if (combo1 != combo2)
            {
                _coreLog.Error($"[Story][Battle]: Enemy combo multipliers differ, enemyId = {enemyMapper.Id}, combo1 = {combo1}, combo2 = {combo2}");

                throw new InvalidOperationException($"[Story][Battle]: Enemy combo multipliers differ, enemyId = {enemyMapper.Id}");
            }

            return combo1;
        }

        private float ReadStoryLevelMultiplier(int storyLevelId)
        {
            if (storyLevelId <= 0)
                return 1f;

            if (_configDistributor.StoryLevels.TryGet(storyLevelId, out var storyLevel) == false)
            {
                _coreLog.Warning($"[Story][Battle]: Story level missing for enemy multiplier, storyLevelId = {storyLevelId}");

                return 1f;
            }

            if (storyLevel.EnemyStatsMultiplier <= 0f)
            {
                _coreLog.Warning($"[Story][Battle]: Story level enemy_stats_multiplier is not set, storyLevelId = {storyLevelId}");

                return 1f;
            }

            return storyLevel.EnemyStatsMultiplier;
        }

    }
}
