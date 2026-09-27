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

        private readonly ILogger<UnitBucketsFactory> _logger;
        private readonly IConfigDistributor _configDistributor;

        public UnitBucketsFactory(ILogger<UnitBucketsFactory> logger, IConfigDistributor configDistributor)
        {
            _logger = logger;
            _configDistributor = configDistributor;
        }

        public CharacteristicBuckets BuildEnemyBuckets(IEnemyMapper enemyMapper, int storyLevelId, int stageId)
        {
            var stageMultiplier = 1f;

            if (0 < stageId)
            {
                if (_configDistributor.StoryStages.TryGet(stageId, out var storyStage) == false)
                {
                    _logger.LogError($"[Story][Battle]: Story stage missing id = {stageId}");

                    throw new InvalidOperationException($"[Story][Battle]: Story stage missing id = {stageId}");
                }

                stageMultiplier = storyStage.EnemyStatsMultiplier;

                if (stageMultiplier <= 0f)
                {
                    _logger.LogError($"[Story][Battle]: Invalid enemy_stats_multiplier = {stageMultiplier}, stageId = {stageId}");

                    throw new InvalidOperationException($"[Story][Battle]: Invalid enemy_stats_multiplier = {stageMultiplier}, stageId = {stageId}");
                }
            }

            _logger.LogDebug($"[Story][Battle]: Enemy multipliers, storyLevelId = {storyLevelId}, stageId = {stageId}, stage = {stageMultiplier}");

            var buckets = new CharacteristicBuckets
            {
                HealthBase = enemyMapper.Health * stageMultiplier,
                DamageBase = enemyMapper.Damage * stageMultiplier,
                AttackMultiplierBase = ToFormula3Start(1f),
                ArmorBase = enemyMapper.Defence,
                DefenceCoefficient = GetConstant(ConstantKeys.DefenceCoefficientKey),
                EvasionBase = ToFormula3Start(enemyMapper.Evasion),
                CriticalChanceBase = ToFormula3Start(enemyMapper.CriticalChance),
                CriticalMultiplierBase = ToFormula3Start(enemyMapper.CriticalMultiplier),
                Combo1ChanceBase = ToFormula3Start(enemyMapper.Combo1Chance),
                Combo2ChanceBase = ToFormula3Start(enemyMapper.Combo2Chance),
                ComboMultiplierBase = ToFormula3Start(ResolveEnemyComboMultiplier(enemyMapper)),
                CounterChanceBase = ToFormula3Start(enemyMapper.CounterChance),
                CounterMultiplierBase = ToFormula3Start(enemyMapper.CounterMultiplier),
                SkillMultiplierBase = ToFormula3Start(enemyMapper.SpellMultiplier),
                EnergyBase = enemyMapper.Energy,
                EnergyMaxBase = enemyMapper.MaxEnergy,
                VampyrismBase = enemyMapper.Vampyrism,
                HealingBoostBase = enemyMapper.HealingBoost,
            };

            _logger.LogDebug($"[Story][Battle]: Enemy buckets seeded, id = {enemyMapper.Id}, armor = {buckets.ArmorBase}, comboMnStart = {buckets.ComboMultiplierBase}");

            return buckets;
        }

        public CharacteristicBuckets BuildSummonBuckets(IUnitSnapshot unitSnapshot)
        {
            var buckets = BuildConstantsBuckets();

            if (_configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapper) == false)
            {
                _logger.LogError($"[Story][Battle]: Summon config missing id = {unitSnapshot.Id}");

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
                _logger.LogError($"[Story][Battle]: Summon id = {unitSnapshot.Id} has empty dmg_on_lvls");

            var masteryLevel = unitSnapshot.MasteryLevel;

            if (masteryLevel <= 0)
            {
                _logger.LogDebug($"[Story][Battle]: Summon mastery unused, summonId = {unitSnapshot.Id}, masteryLevel = {masteryLevel}, baseDamage = {baseDamage}");

                return baseDamage;
            }

            var multiplier = ResolveMasteryMultiplier(summonMapper.MasteryId, masteryLevel, unitSnapshot.Id);

            _logger.LogDebug($"[Story][Battle]: Summon mastery damage, summonId = {unitSnapshot.Id}, masteryId = {summonMapper.MasteryId}, masteryLevel = {masteryLevel}, multiplier = {multiplier}, baseDamage = {baseDamage}");

            return baseDamage * multiplier;
        }

        private float ResolveMasteryMultiplier(int masteryId, int masteryLevel, int summonId)
        {
            if (TryResolveMastery(masteryId, masteryLevel, summonId, out var masteryMapper))
                return masteryMapper.DamageMultiplier;

            return DefaultMasteryMultiplier;
        }

        public bool TryResolveMastery(int masteryId, int masteryLevel, int summonId, out IMasteryMapper masteryMapper)
        {
            if (_configDistributor.Masteries.TryGet(masteryId, masteryLevel, out masteryMapper))
                return true;

            _logger.LogWarning($"[Story][Battle]: Mastery missing, masteryId = {masteryId}, masteryLevel = {masteryLevel}, summonId = {summonId}; using multiplier {DefaultMasteryMultiplier}");

            return false;
        }

        public void ApplyBreakoutHook(IUnitSnapshot unitSnapshot, ISummonMapper summonMapper)
        {
            if (summonMapper.BreakoutMultipliers == null || summonMapper.BreakoutMultipliers.Length == 0)
                return;

            _logger.LogError($"[Story][Battle]: breakout_multis loaded but formula unknown, summonId = {unitSnapshot.Id}, level = {unitSnapshot.Level}, valuesCount = {summonMapper.BreakoutMultipliers.Length}; hook no-op");
        }

        public UnitFlags ResolveSummonMeleeFlags(IUnitSnapshot unitSnapshot)
        {
            if (_configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapper) == false)
            {
                _logger.LogError($"[Story][Battle]: Summon melee flags missing config, id = {unitSnapshot.Id}, flags = Range");

                return UnitFlags.Range;
            }

            if (summonMapper.IsMelee)
            {
                _logger.LogDebug($"[Story][Battle]: Summon melee flags, id = {unitSnapshot.Id}, isMelee = {summonMapper.IsMelee}, flags = Melee");

                return UnitFlags.Melee;
            }

            _logger.LogDebug($"[Story][Battle]: Summon melee flags, id = {unitSnapshot.Id}, isMelee = {summonMapper.IsMelee}, flags = Range");

            return UnitFlags.Range;
        }

        public int ResolveSummonAttackCooldown(IUnitSnapshot unitSnapshot)
        {
            if (_configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapper) == false)
            {
                _logger.LogError($"[Story][Battle]: Summon attack cooldown missing config, id = {unitSnapshot.Id}");

                return 0;
            }

            var attackCooldownTurns = summonMapper.AttackCooldown;

            if (attackCooldownTurns < 0)
            {
                _logger.LogError($"[Story][Battle]: Summon attack_cooldown negative, id = {unitSnapshot.Id}, attackCooldown = {attackCooldownTurns}");

                throw new InvalidOperationException($"[Story][Battle]: Summon attack_cooldown negative, id = {unitSnapshot.Id}, attackCooldown = {attackCooldownTurns}");
            }

            _logger.LogDebug($"[Story][Battle]: Summon attack cooldown, id = {unitSnapshot.Id}, attackCooldown = {attackCooldownTurns}");

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
            buckets.HealthBase = GetConstant(ConstantKeys.HealthBaseKey);
            buckets.DamageBase = GetConstant(ConstantKeys.DamageBaseKey);
            buckets.AttackMultiplierBase = ToFormula3Start(GetConstant(ConstantKeys.AttackMultiplierBaseKey));
            buckets.ArmorBase = GetConstant(ConstantKeys.DefenceBaseKey);
            buckets.EvasionBase = ToFormula3Start(GetConstant(ConstantKeys.EvasionBaseKey));
            buckets.CriticalChanceBase = ToFormula3Start(GetConstant(ConstantKeys.CriticalChanceBaseKey));
            buckets.CriticalMultiplierBase = ToFormula3Start(GetConstant(ConstantKeys.CriticalMultiplierBaseKey));
            buckets.Combo1ChanceBase = ToFormula3Start(GetConstant(ConstantKeys.ComboOneChanceBaseKey));
            buckets.Combo2ChanceBase = ToFormula3Start(GetConstant(ConstantKeys.ComboTwoChanceBaseKey));
            buckets.ComboMultiplierBase = ToFormula3Start(GetConstant(ConstantKeys.ComboMultiplierBaseKey));
            buckets.CounterChanceBase = ToFormula3Start(GetConstant(ConstantKeys.CounterChanceBaseKey));
            buckets.CounterMultiplierBase = ToFormula3Start(GetConstant(ConstantKeys.CounterMultiplierBaseKey));
            buckets.SkillMultiplierBase = ToFormula3Start(GetConstant(ConstantKeys.SkillMultiplierBaseKey));
            buckets.EnergyBase = GetConstant(ConstantKeys.EnergyBaseKey);
            buckets.EnergyMaxBase = GetConstant(ConstantKeys.EnergyMaxBaseKey);
            buckets.DefenceCoefficient = GetConstant(ConstantKeys.DefenceCoefficientKey);
            buckets.HealingBoostBase = GetConstant(ConstantKeys.HealingBoostBaseKey);
            buckets.VampyrismBase = GetConstant(ConstantKeys.VampyrismBaseKey);

            _logger.LogDebug($"[Story][Battle]: Constants buckets seeded, maxHealthBase = {buckets.HealthBase}, damageBase = {buckets.DamageBase}, armorBase = {buckets.ArmorBase}, vampyrismBase = {buckets.VampyrismBase}, comboMnBase = {buckets.ComboMultiplierBase}");
        }

        private float ResolveEnemyComboMultiplier(IEnemyMapper enemyMapper)
        {
            var combo1 = enemyMapper.Combo1Multiplier;
            var combo2 = enemyMapper.Combo2Multiplier;

            if (combo1 != combo2)
            {
                _logger.LogError($"[Story][Battle]: Enemy combo multipliers differ, enemyId = {enemyMapper.Id}, combo1 = {combo1}, combo2 = {combo2}");

                throw new InvalidOperationException($"[Story][Battle]: Enemy combo multipliers differ, enemyId = {enemyMapper.Id}");
            }

            return combo1;
        }

        private float ToFormula3Start(float finalValue)
        {
            return finalValue - 1f;
        }

        private bool TryGetConstant(string constantKey, out float value)
        {
            value = 0f;

            if (_configDistributor.Constants.TryGet(constantKey, out var constant) == false)
                return false;

            if (float.TryParse(
                    constant.ConstantValue,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value) == false)
            {
                _logger.LogError($"[Story][Battle]: Constant parse failed, key = {constantKey} value = {constant.ConstantValue}");

                throw new InvalidOperationException($"[Story][Battle]: Constant parse failed, key = {constantKey}");
            }

            return true;
        }

        private float GetConstant(string constantKey)
        {
            if (TryGetConstant(constantKey, out var value) == false)
            {
                _logger.LogError($"[Story][Battle]: Constant missing key = {constantKey}");

                throw new InvalidOperationException($"[Story][Battle]: Constant missing key = {constantKey}");
            }

            return value;
        }
    }
}
