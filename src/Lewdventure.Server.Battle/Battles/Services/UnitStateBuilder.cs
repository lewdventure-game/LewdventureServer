using System;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Server.Bonuses;
using Server.Entities;
using Server.Perks;
using Server.Services;
using Server.Statuses;

namespace Server.Battles
{
    internal sealed class UnitStateBuilder : IUnitStateBuilder
    {
        private readonly ILogger<UnitStateBuilder> _logger;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly ICharacteristicCalculator _characteristicCalculator;
        private readonly IConfigDistributor _configDistributor;
        private readonly IPerkFactory _perkFactory;
        private readonly ISkillFactory _skillFactory;
        private readonly IStatusParametersParser _statusParametersParser;

        public UnitStateBuilder(
            ILogger<UnitStateBuilder> logger,
            IBattleBonusService battleBonusService,
            IBonusWorkModeParser bonusWorkModeParser,
            ICharacteristicCalculator characteristicCalculator,
            IConfigDistributor configDistributor,
            IPerkFactory perkFactory,
            ISkillFactory skillFactory,
            IStatusParametersParser statusParametersParser)
        {
            _logger = logger;
            _battleBonusService = battleBonusService;
            _bonusWorkModeParser = bonusWorkModeParser;
            _characteristicCalculator = characteristicCalculator;
            _configDistributor = configDistributor;
            _perkFactory = perkFactory;
            _skillFactory = skillFactory;
            _statusParametersParser = statusParametersParser;
        }

        public IUnitState Build(IUnitSnapshot unitSnapshot, BattleSide battleSide, bool isSummon, int storyLevelId, int stageId)
        {
            CharacteristicBuckets baseBuckets;
            UnitFlags flags;
            var attackCooldownTurns = 0;

            if (isSummon)
            {
                baseBuckets = BuildSummonBuckets(unitSnapshot);
                flags = UnitFlags.Summon | ResolveSummonMeleeFlags(unitSnapshot);
                attackCooldownTurns = ResolveSummonAttackCooldown(unitSnapshot);
            }
            else if (battleSide == BattleSide.Defending)
            {
                if (_configDistributor.Enemies.TryGet(unitSnapshot.Id, out var enemyMapper))
                {
                    baseBuckets = BuildEnemyBuckets(enemyMapper, storyLevelId, stageId);
                    flags = enemyMapper.IsMelee ? UnitFlags.Melee : UnitFlags.Range;

                    _logger.LogDebug($"[Story][Battle]: Enemy melee flags, id = {unitSnapshot.Id}, isMelee = {enemyMapper.IsMelee}, flags = {flags}");
                }
                else
                {
                    _logger.LogError($"[Story][Battle]: Enemy config missing, id = {unitSnapshot.Id}");

                    throw new InvalidOperationException($"[Story][Battle]: Enemy config missing, id = {unitSnapshot.Id}");
                }
            }
            else if (_configDistributor.Characters.TryGet(unitSnapshot.Id, out var characterMapper))
            {
                baseBuckets = BuildConstantsBuckets();
                flags = characterMapper.IsMelee ? UnitFlags.Melee : UnitFlags.Range;

                _logger.LogDebug($"[Story][Battle]: Character melee flags, id = {unitSnapshot.Id}, isMelee = {characterMapper.IsMelee}, flags = {flags}");
            }
            else
            {
                _logger.LogError($"[Story][Battle]: Character config missing, id = {unitSnapshot.Id}, side = {battleSide}");

                throw new InvalidOperationException($"[Story][Battle]: Character config missing, id = {unitSnapshot.Id}, side = {battleSide}");
            }

            var characteristics = new CharacteristicState();
            _characteristicCalculator.ApplyToState(baseBuckets, characteristics, Array.Empty<ReplaceOverride>(), false, true);

            var perks = BuildPerks(unitSnapshot);
            var skills = BuildSkills(unitSnapshot, isSummon);
            var unitState = new UnitState(
                unitSnapshot.Id,
                unitSnapshot.Level,
                characteristics,
                baseBuckets,
                skills,
                perks,
                flags,
                attackCooldownTurns,
                unitSnapshot.SlotIndex,
                battleSide);

            RegisterSnapshotEquippedEntities(unitState, unitSnapshot, isSummon, battleSide);

            if (isSummon == false && battleSide == BattleSide.Attacking && _configDistributor.Characters.TryGet(unitSnapshot.Id, out var attackingCharacter))
            {
                GrantBuildBonus(unitState, attackingCharacter.StartBonusId, attackingCharacter.StartBonusValue, "build:start");
                GrantCharacterUpgradeBonuses(unitState, attackingCharacter, unitSnapshot.Level);
                GrantTrainingBonuses(unitState, unitSnapshot.TrainingLevel);
                GrantEquipmentBonuses(unitState, unitSnapshot);
                GrantArtifactBonuses(unitState, unitSnapshot);
                GrantAspectBonuses(unitState, unitSnapshot);
                GrantSnapshotRunBonuses(unitState, unitSnapshot);
                _battleBonusService.Rebuild(unitState, 0, new List<BattleCommand>(), false);
            }

            if (isSummon && _configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapperForBreakout))
                ApplyBreakoutHook(unitSnapshot, summonMapperForBreakout);

            SeedActiveStatuses(unitState, unitSnapshot, isSummon);
            ApplyEquippedPerks(unitState);
            ApplySnapshotHealth(unitState, unitSnapshot);

            _logger.LogDebug($"[Story][Battle]: Built, id = {unitState.Id}, level = {unitState.Level}, slot = {unitState.SlotIndex}, hp = {characteristics.Health}/{characteristics.MaxHealth}, damage = {characteristics.Damage}, vampyrism = {characteristics.Vampyrism}, healingBoost = {characteristics.HealingBoost}, statuses = {unitState.ActiveStatuses.Count}, perks = {perks.Count}, skills = {skills.Count}, activeBonuses = {unitState.ActiveBonuses.Count}");

            return unitState;
        }

        private void ApplySnapshotHealth(IUnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var characteristics = unitState.CharacteristicState;
            var currentHealth = unitSnapshot.CurrentHealth;

            if (currentHealth <= 0f)
            {
                _logger.LogError($"[Story][Battle]: Invalid currentHealth = {currentHealth}, unitId = {unitSnapshot.Id}, slot = {unitSnapshot.SlotIndex}");

                throw new InvalidOperationException($"[Story][Battle]: Invalid currentHealth = {currentHealth}, unitId = {unitSnapshot.Id}, slot = {unitSnapshot.SlotIndex}");
            }

            if (characteristics.MaxHealth < currentHealth)
                currentHealth = characteristics.MaxHealth;

            characteristics.Health = currentHealth;

            _logger.LogDebug($"[Story][Battle]: Snapshot health applied, unitId = {unitState.Id}, health = {characteristics.Health}/{characteristics.MaxHealth}");
        }

        private void ApplyEquippedPerks(IUnitState unitState)
        {
            var perks = unitState.Perks;
            var commands = new List<BattleCommand>();

            for (int i = 0; i < perks.Count; i++)
                perks[i].OnEquipped(unitState, commands);
        }

        private void RegisterSnapshotEquippedEntities(
            UnitState unitState,
            IUnitSnapshot unitSnapshot,
            bool isSummon,
            BattleSide battleSide)
        {
            if (isSummon)
                unitState.RegisterEquippedEntity("summons", unitSnapshot.Id);
            else if (battleSide == BattleSide.Attacking)
                unitState.RegisterEquippedEntity("characters", unitSnapshot.Id);

            var equipment = unitSnapshot.Equipments;

            for (int i = 0; i < equipment.Count; i++)
            {
                var entry = equipment[i];

                if (entry == null || entry.Id <= 0)
                    continue;

                unitState.RegisterEquippedEntity("equipments", entry.Id);
            }

            _logger.LogDebug($"[Story][Battle]: Equipped entities registered, unitId = {unitState.Id}, count = {unitState.EquippedEntities.Count}");
        }

        private CharacteristicBuckets BuildEnemyBuckets(IEnemyMapper enemyMapper, int storyLevelId, int stageId)
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

        private CharacteristicBuckets BuildSummonBuckets(IUnitSnapshot unitSnapshot)
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

            var masteryMapper = ResolveMastery(summonMapper.MasteryId, masteryLevel, unitSnapshot.Id);

            _logger.LogDebug($"[Story][Battle]: Summon mastery damage, summonId = {unitSnapshot.Id}, masteryId = {summonMapper.MasteryId}, masteryLevel = {masteryLevel}, multiplier = {masteryMapper.DamageMultiplier}, baseDamage = {baseDamage}");

            return baseDamage * masteryMapper.DamageMultiplier;
        }

        private IMasteryMapper ResolveMastery(int masteryId, int masteryLevel, int summonId)
        {
            var masteries = _configDistributor.Masteries;

            if (masteries.TryGet(masteryId, masteryLevel, out var masteryMapper))
                return masteryMapper;

            _logger.LogError($"[Story][Battle]: Mastery missing, masteryId = {masteryId}, masteryLevel = {masteryLevel}, summonId = {summonId}");

            throw new InvalidOperationException($"[Story][Battle]: Mastery missing, masteryId = {masteryId}, masteryLevel = {masteryLevel}, summonId = {summonId}");
        }

        private void ApplyBreakoutHook(IUnitSnapshot unitSnapshot, ISummonMapper summonMapper)
        {
            if (summonMapper.BreakoutMultipliers == null || summonMapper.BreakoutMultipliers.Length == 0)
                return;

            _logger.LogError($"[Story][Battle]: breakout_multis loaded but formula unknown, summonId = {unitSnapshot.Id}, level = {unitSnapshot.Level}, valuesCount = {summonMapper.BreakoutMultipliers.Length}; hook no-op");
        }

        private void GrantTrainingBonuses(UnitState unitState, int trainingLevel)
        {
            if (trainingLevel <= 0)
            {
                _logger.LogDebug($"[Story][Battle]: Training grant skipped, trainingLevel = {trainingLevel}");

                return;
            }

            if (_configDistributor.Trainings.TryGet(trainingLevel, out var trainingMapper) == false)
            {
                _logger.LogError($"[Story][Battle]: Training missing, trainingLevel = {trainingLevel}, trainingsCount = {_configDistributor.Trainings.Collection.Count}");

                throw new InvalidOperationException($"[Story][Battle]: Training missing, trainingLevel = {trainingLevel}");
            }

            GrantBonusPairs(unitState, trainingMapper.BonusIds, trainingMapper.BonusValues, $"build:training:{trainingLevel}");

            _logger.LogDebug($"[Story][Battle]: Training bonuses granted, trainingLevel = {trainingLevel}, bonusCount = {trainingMapper.BonusIds.Length}");
        }

        private void GrantArtifactBonuses(UnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var artifactIds = unitSnapshot.ArtifactIds;

            if (artifactIds.Count == 0)
                return;

            if (_configDistributor.Artifacts.Collection.Count == 0)
            {
                _logger.LogDebug($"[Story][Battle]: Artifact grant skipped; artifacts manager empty, requestedCount = {artifactIds.Count}");

                return;
            }

            for (int i = 0; i < artifactIds.Count; i++)
            {
                var artifactId = artifactIds[i];

                if (_configDistributor.Artifacts.TryGet(artifactId, out var artifactMapper) == false)
                {
                    _logger.LogWarning($"[Story][Battle]: Unknown artifact, id = {artifactId}");

                    continue;
                }

                GrantBonusPairs(unitState, artifactMapper.BonusIds, artifactMapper.BonusValues, $"build:artifact:{artifactId}");

                _logger.LogDebug($"[Story][Battle]: Artifact bonuses granted, artifactId = {artifactId}, bonusCount = {artifactMapper.BonusIds.Length}");
            }
        }

        private void GrantAspectBonuses(UnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var aspectIds = unitSnapshot.AspectIds;

            if (aspectIds.Count == 0)
                return;

            if (_configDistributor.Aspects.Collection.Count == 0)
            {
                _logger.LogDebug($"[Story][Battle]: Aspect grant skipped; aspects manager empty, requestedCount = {aspectIds.Count}");

                return;
            }

            for (int i = 0; i < aspectIds.Count; i++)
            {
                var aspectId = aspectIds[i];

                if (_configDistributor.Aspects.TryGet(aspectId, out var aspectMapper) == false)
                {
                    _logger.LogWarning($"[Story][Battle]: Unknown aspect id = {aspectId}");

                    continue;
                }

                GrantBonusPairs(unitState, aspectMapper.BonusIds, aspectMapper.BonusValues, $"build:aspect:{aspectId}");

                _logger.LogDebug($"[Story][Battle]: Aspect bonuses granted, aspectId = {aspectId}, bonusCount = {aspectMapper.BonusIds.Length}");
            }
        }

        private void GrantBonusPairs(UnitState unitState, int[] bonusIds, float[] bonusValues, string sourcePrefix)
        {
            if (bonusIds == null || bonusIds.Length == 0)
                return;

            var pairCount = bonusIds.Length;

            if (bonusValues != null && bonusValues.Length < pairCount)
                pairCount = bonusValues.Length;

            for (int i = 0; i < pairCount; i++)
            {
                var value = 0f;

                if (bonusValues != null && i < bonusValues.Length)
                    value = bonusValues[i];

                GrantBuildBonus(unitState, bonusIds[i], value, $"{sourcePrefix}:{i}");
            }
        }

        private void GrantCharacterUpgradeBonuses(UnitState unitState, ICharacterMapper characterMapper, int characterLevel)
        {
            if (characterLevel <= 0)
            {
                _logger.LogError($"[Story][Battle]: Invalid character level = {characterLevel}, characterId = {characterMapper.Id}");

                throw new InvalidOperationException($"[Story][Battle]: Invalid character level = {characterLevel}, characterId = {characterMapper.Id}");
            }

            var purchasedUpgrades = characterLevel - 1;

            if (purchasedUpgrades <= 0)
                return;

            var upgradeCosts = characterMapper.UpgradeCosts;
            var upgradeBonusTypes = characterMapper.UpgradeBonusTypes;
            var upgradeBonusValues = characterMapper.UpgradeBonusValues;

            if (upgradeCosts.Length == 0)
            {
                _logger.LogDebug($"[Story][Battle]: Character upgrades skipped, id = {characterMapper.Id}; upgrade_costs empty");

                return;
            }

            var appliedUpgrades = purchasedUpgrades;

            if (upgradeCosts.Length < appliedUpgrades)
                appliedUpgrades = upgradeCosts.Length;

            for (int upgradeIndex = 0; upgradeIndex < appliedUpgrades; upgradeIndex++)
            {
                if (upgradeBonusTypes.Length <= upgradeIndex)
                {
                    _logger.LogWarning($"[Story][Battle]: Character upgrade type missing, characterId = {characterMapper.Id}, upgrade = {upgradeIndex + 1}");

                    break;
                }

                if (upgradeBonusValues.Length <= upgradeIndex)
                {
                    _logger.LogWarning($"[Story][Battle]: Character upgrade value missing, characterId = {characterMapper.Id}, upgrade = {upgradeIndex + 1}");

                    break;
                }

                var bonusId = upgradeBonusTypes[upgradeIndex];
                var value = upgradeBonusValues[upgradeIndex];
                var sourceKey = $"build:upgrade:{characterMapper.Id}:{upgradeIndex + 1}";

                GrantBuildBonus(unitState, bonusId, value, sourceKey);

                _logger.LogDebug($"[Story][Battle]: Character upgrade grant, characterId = {characterMapper.Id}, upgrade = {upgradeIndex + 1}, cost = {upgradeCosts[upgradeIndex]}, bonusId = {bonusId}, value = {value}");
            }

            if (upgradeCosts.Length < purchasedUpgrades)
                _logger.LogDebug($"[Story][Battle]: Character upgrades capped, characterId = {characterMapper.Id}, level = {characterLevel}, purchased = {purchasedUpgrades}, maxFromCosts = {upgradeCosts.Length}");
        }

        public void GrantSummonAccountBonuses(IUnitState mainUnit, IUnitSnapshot summonSnapshot)
        {
            if (_configDistributor.Summons.TryGet(summonSnapshot.Id, out var summonMapper) == false)
            {
                _logger.LogError($"[Story][Battle]: Summon account bonuses skipped; missing summon id = {summonSnapshot.Id}");

                throw new InvalidOperationException($"[Story][Battle]: Summon missing id = {summonSnapshot.Id}");
            }

            var masteryLevel = summonSnapshot.MasteryLevel;

            if (0 < masteryLevel)
            {
                var masteryMapper = ResolveMastery(summonMapper.MasteryId, masteryLevel, summonSnapshot.Id);

                if (0 < masteryMapper.BonusId)
                {
                    var sourceKey = $"build:summon-mastery:{summonSnapshot.Id}:{masteryLevel}";

                    GrantBuildBonus(mainUnit, masteryMapper.BonusId, 0f, sourceKey);

                    _logger.LogDebug($"[Story][Battle]: Summon mastery bonus grant, mainId = {mainUnit.Id}, summonId = {summonSnapshot.Id}, masteryLevel = {masteryLevel}, bonusId = {masteryMapper.BonusId}");
                }
            }
        }

        private UnitFlags ResolveSummonMeleeFlags(IUnitSnapshot unitSnapshot)
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

        private int ResolveSummonAttackCooldown(IUnitSnapshot unitSnapshot)
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

        private CharacteristicBuckets BuildConstantsBuckets()
        {
            var buckets = new CharacteristicBuckets();
            SeedBucketsFromConstants(buckets);

            return buckets;
        }

        private void SeedBucketsFromConstants(CharacteristicBuckets buckets)
        {
            buckets.HealthBase = GetConstant(ConstantKeys.HealthBaseKey);
            buckets.DamageBase = GetConstant(ConstantKeys.DamageBaseKey);
            buckets.AttackMultiplierBase = GetConstant(ConstantKeys.AttackMultiplierBaseKey);
            buckets.ArmorBase = GetConstant(ConstantKeys.DefenceBaseKey);
            buckets.EvasionBase = GetConstant(ConstantKeys.EvasionBaseKey);
            buckets.CriticalChanceBase = GetConstant(ConstantKeys.CriticalChanceBaseKey);
            buckets.CriticalMultiplierBase = GetConstant(ConstantKeys.CriticalMultiplierBaseKey);
            buckets.Combo1ChanceBase = GetConstant(ConstantKeys.ComboOneChanceBaseKey);
            buckets.Combo2ChanceBase = GetConstant(ConstantKeys.ComboTwoChanceBaseKey);
            buckets.ComboMultiplierBase = GetConstant(ConstantKeys.ComboMultiplierBaseKey);
            buckets.CounterChanceBase = GetConstant(ConstantKeys.CounterChanceBaseKey);
            buckets.CounterMultiplierBase = GetConstant(ConstantKeys.CounterMultiplierBaseKey);
            buckets.SkillMultiplierBase = GetConstant(ConstantKeys.SkillMultiplierBaseKey);
            buckets.EnergyBase = GetConstant(ConstantKeys.EnergyBaseKey);
            buckets.EnergyMaxBase = GetConstant(ConstantKeys.EnergyMaxBaseKey);
            buckets.DefenceCoefficient = GetConstant(ConstantKeys.DefenceCoefficientKey);
            buckets.HealingBoostBase = GetConstant(ConstantKeys.HealingBoostBaseKey);
            buckets.VampyrismBase = GetConstant(ConstantKeys.VampyrismBaseKey);

            _logger.LogDebug($"[Story][Battle]: Constants buckets seeded, maxHealthBase = {buckets.HealthBase}, damageBase = {buckets.DamageBase}, armorBase = {buckets.ArmorBase}, vampyrismBase = {buckets.VampyrismBase}, comboMnBase = {buckets.ComboMultiplierBase}");
        }

        private void GrantEquipmentBonuses(UnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var equipment = unitSnapshot.Equipments;

            for (int i = 0; i < equipment.Count; i++)
            {
                var entry = equipment[i];

                if (entry == null)
                    continue;

                var equipmentId = entry.Id;
                var equipmentLevel = entry.Level;

                if (_configDistributor.Equipments.TryGet(equipmentId, out var equipmentMapper) == false)
                {
                    _logger.LogError($"[Story][Battle]: Equipment missing id = {equipmentId}");

                    throw new InvalidOperationException($"[Story][Battle]: Equipment missing id = {equipmentId}");
                }

                GrantEquipmentBonusSlot(unitState, equipmentMapper.EquipmentBonusTypeOne, equipmentMapper.EquipmentBonusValuesOne, equipmentLevel, $"build:equip:{equipmentId}:1");
                GrantEquipmentBonusSlot(unitState, equipmentMapper.EquipmentBonusTypeTwo, equipmentMapper.EquipmentBonusValuesTwo, equipmentLevel, $"build:equip:{equipmentId}:2");
                GrantEquipmentBonusSlot(unitState, equipmentMapper.EquipmentBonusTypeThree, equipmentMapper.EquipmentBonusValuesThree, equipmentLevel, $"build:equip:{equipmentId}:3");
            }
        }

        private void GrantEquipmentBonusSlot(
            UnitState unitState,
            string bonusTypeRaw,
            string bonusValueRaw,
            int equipmentLevel,
            string sourceKey)
        {
            if (TryResolveEquipmentBonusId(bonusTypeRaw, out var bonusId) == false)
                return;

            var value = ParseEquipmentBonusValue(bonusValueRaw, equipmentLevel);

            _logger.LogDebug($"[Story][Battle] equipment grant slot sourceKey = {sourceKey} level = {equipmentLevel} bonusId = {bonusId} value = {value}");

            GrantBuildBonus(unitState, bonusId, value, sourceKey);
        }

        private bool TryResolveEquipmentBonusId(string bonusTypeRaw, out int bonusId)
        {
            bonusId = 0;

            if (string.IsNullOrWhiteSpace(bonusTypeRaw))
                return false;

            var trimmed = bonusTypeRaw.Trim();

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out bonusId))
            {
                if (bonusId <= 0)
                {
                    _logger.LogWarning($"[Story][Battle]: Equipment bonus id invalid raw = {bonusTypeRaw}");

                    return false;
                }

                return true;
            }

            if (TryParseBonusTypeName(trimmed, out var bonusType) == false || bonusType == BonusType.Unknown)
            {
                _logger.LogWarning($"[Story][Battle]: Equipment bonus type parse failed raw = {bonusTypeRaw}");

                return false;
            }

            IBonusMapper firstMatch = default!;

            var hasFirstMatch = false;
            var matchCount = 0;

            foreach (var bonusMapper in _configDistributor.Bonuses.Values)
            {
                if (bonusMapper.BonusType != bonusType)
                    continue;

                matchCount += 1;

                if (hasFirstMatch == false)
                {
                    firstMatch = bonusMapper;
                    hasFirstMatch = true;
                }
            }

            if (hasFirstMatch == false)
            {
                _logger.LogWarning($"[Story][Battle]: Equipment bonus type missing, type = {bonusType}, raw = {bonusTypeRaw}");

                return false;
            }

            if (1 < matchCount)
                _logger.LogWarning($"[Story][Battle]: Equipment bonus type ambiguous, type = {bonusType}, matches = {matchCount}, usingId = {firstMatch.Id}");

            bonusId = firstMatch.Id;

            return true;
        }

        private static bool TryParseBonusTypeName(string value, out BonusType bonusType)
        {
            bonusType = BonusType.Unknown;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (var field in typeof(BonusType).GetFields())
            {
                var attributes = field.GetCustomAttributes(typeof(System.Runtime.Serialization.EnumMemberAttribute), false);

                if (attributes.Length == 0)
                    continue;

                var attribute = (System.Runtime.Serialization.EnumMemberAttribute)attributes[0];

                if (string.Equals(attribute.Value, value, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                var fieldValue = field.GetValue(null);

                if (fieldValue is BonusType parsedBonusType)
                {
                    bonusType = parsedBonusType;

                    return true;
                }
            }

            if (Enum.TryParse(value, true, out bonusType) && bonusType != BonusType.Unknown)
                return true;

            var parts = value.Split('_');
            var pascalBuilder = new System.Text.StringBuilder(value.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (part.Length == 0)
                    continue;

                pascalBuilder.Append(char.ToUpperInvariant(part[0]));

                if (1 < part.Length)
                    pascalBuilder.Append(part.Substring(1).ToLowerInvariant());
            }

            if (Enum.TryParse(pascalBuilder.ToString(), true, out bonusType) && bonusType != BonusType.Unknown)
                return true;

            bonusType = BonusType.Unknown;

            return false;
        }

        private float ParseEquipmentBonusValue(string bonusValueRaw, int equipmentLevel)
        {
            if (string.IsNullOrWhiteSpace(bonusValueRaw))
                return 0f;

            var levelIndex = equipmentLevel - 1;

            if (levelIndex < 0)
                levelIndex = 0;

            var segments = SplitEquipmentBonusValueSegments(bonusValueRaw);

            if (segments.Length == 0)
                return 0f;

            if (segments.Length <= levelIndex)
                levelIndex = segments.Length - 1;

            var segment = segments[levelIndex].Trim();

            if (float.TryParse(segment, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;

            _logger.LogWarning($"[Story][Battle]: Equipment bonus value parse failed, raw = {bonusValueRaw}, level = {equipmentLevel}, segment = {segment}");

            return 0f;
        }

        private static string[] SplitEquipmentBonusValueSegments(string bonusValueRaw)
        {
            var commaSegments = bonusValueRaw.Split(',');
            var semicolonSegments = bonusValueRaw.Split(';');

            if (commaSegments.Length == 1 && semicolonSegments.Length == 1)
                return commaSegments;

            if (semicolonSegments.Length < commaSegments.Length)
                return commaSegments;

            if (commaSegments.Length < semicolonSegments.Length)
                return semicolonSegments;

            if (1 < commaSegments.Length)
                return commaSegments;

            return semicolonSegments;
        }

        private void GrantSnapshotRunBonuses(IUnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var grants = unitSnapshot.ActiveBonuses;

            if (grants == null || grants.Count == 0)
                return;

            var commands = new List<BattleCommand>();

            for (int i = 0; i < grants.Count; i++)
            {
                var grant = grants[i];

                if (grant == null)
                    continue;

                if (grant.Id <= 0 || grant.Count == 0)
                {
                    _logger.LogError($"[Story][Battle]: Run bonus skip invalid, id = {grant.Id}, count = {grant.Count}");

                    continue;
                }

                var sourceKey = $"run:{grant.Id}:{i}";

                _battleBonusService.Grant(unitState, grant.Id, grant.Count, sourceKey, commands, 0);

                if (grant.RemainingBattles <= 0)
                    continue;

                OverrideRemainingBattles(unitState, sourceKey, grant.RemainingBattles, grant.Id);
            }
        }

        private void OverrideRemainingBattles(IUnitState unitState, string sourceKey, int remainingBattles, int bonusId)
        {
            var activeBonuses = unitState.ActiveBonuses;

            for (int i = 0; i < activeBonuses.Count; i++)
            {
                var activeBonus = activeBonuses[i];

                if (string.Equals(activeBonus.SourceKey, sourceKey, StringComparison.Ordinal) == false)
                    continue;

                if (activeBonus.WorkMode.Contains(BonusWorkModeKind.NextBattles) == false)
                {
                    _logger.LogDebug($"[Story][Battle]: Run bonus remaining override skipped, unitId = {unitState.Id}, bonusId = {bonusId}, workMode = {activeBonus.WorkMode.Format()}, sourceKey = {sourceKey}");

                    return;
                }

                activeBonus.RemainingBattles = remainingBattles;

                _logger.LogDebug($"[Story][Battle]: Run bonus remaining override, unitId = {unitState.Id}, bonusId = {activeBonus.BonusId}, remainingBattles = {remainingBattles}, sourceKey = {sourceKey}");

                return;
            }
        }

        private void GrantBuildBonus(IUnitState unitState, int bonusId, float value, string sourceKey)
        {
            if (bonusId <= 0)
                return;

            if (_configDistributor.Bonuses.TryGet(bonusId, out var bonusMapper) == false)
            {
                _logger.LogError($"[Story][Battle]: Bonus missing, id = {bonusId}");

                throw new InvalidOperationException($"[Story][Battle]: Bonus missing, id = {bonusId}");
            }

            if (bonusMapper.BonusType == BonusType.Healing
                || bonusMapper.BonusType == BonusType.HealingFromMax
                || bonusMapper.BonusType == BonusType.CurrentHealthLocal)
            {
                var commands = new List<BattleCommand>();
                _battleBonusService.Grant(unitState, bonusId, 1, sourceKey, commands, 0);

                return;
            }

            if (_bonusWorkModeParser.TryParse(bonusMapper.WorkModeParameters, out var workMode) == false)
            {
                _logger.LogError($"[Story][Battle]: work_mode parse failed, bonusId = {bonusId}");

                return;
            }

            var grantValue = value;

            if (MathF.Abs(grantValue) <= 0.0001f)
                grantValue = bonusMapper.BonusValue;

            unitState.ActiveBonuses.Add(
                new ActiveBattleBonus(
                    bonusId,
                    1,
                    bonusMapper.BonusType,
                    grantValue,
                    bonusMapper.OperatorType,
                    workMode,
                    sourceKey));

            _logger.LogDebug($"[Story][Battle]: Build bonus queued, id = {bonusId}, type = {bonusMapper.BonusType}, value = {grantValue}, operator = {bonusMapper.OperatorType}, workMode = {workMode.Format()}, sourceKey = {sourceKey}");
        }

        private List<IPerk> BuildPerks(IUnitSnapshot unitSnapshot)
        {
            var perkIds = unitSnapshot.ActivePerkIds;
            var perks = new List<IPerk>(perkIds.Count);

            for (int i = 0; i < perkIds.Count; i++)
            {
                var perkId = perkIds[i];

                if (_configDistributor.Perks.TryGet(perkId, out var perkMapper) == false)
                {
                    _logger.LogWarning($"[Story][Battle]: Perk missing, id = {perkId}");

                    continue;
                }

                if (perkMapper.PerkType == PerkType.Unknown)
                {
                    _logger.LogError($"[Config]: Perk unknown type, id = {perkId}, raw type unresolved");

                    continue;
                }

                try
                {
                    var perk = _perkFactory.Create(perkMapper);
                    perks.Add(perk);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, $"[Story][Battle]: Perk create failed, id = {perkId}, type = {perkMapper.PerkType}");
                }
            }

            return perks;
        }

        private IReadOnlyList<ISkill> BuildSkills(IUnitSnapshot unitSnapshot, bool isSummon)
        {
            var skillIds = new List<string>();
            var activeSkillIds = unitSnapshot.ActiveSkillIds;

            for (int i = 0; i < activeSkillIds.Count; i++)
                AddUniqueSkillId(skillIds, activeSkillIds[i]);

            if (isSummon && _configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapper))
            {
                var mapperSkillIds = summonMapper.SkillIds;

                for (int i = 0; i < mapperSkillIds.Length; i++)
                    AddUniqueSkillId(skillIds, mapperSkillIds[i]);
            }

            if (isSummon == false)
            {
                InjectCharacterSkillIds(unitSnapshot, skillIds);
                InjectEnemySkillIds(unitSnapshot, skillIds);
                InjectEquipmentSkillIds(unitSnapshot, skillIds);
            }

            var skills = new List<ISkill>(skillIds.Count);

            for (int i = 0; i < skillIds.Count; i++)
            {
                var skillId = skillIds[i];
                var skill = _skillFactory.Create(skillId);

                if (skill.SkillType == SkillType.Unknown)
                {
                    _logger.LogWarning($"[Story][Battle]: Skill skipped unknown id = {skillId}, unitId = {unitSnapshot.Id}");

                    continue;
                }

                skills.Add(skill);

                _logger.LogDebug($"[Story][Battle]: Skill bound, unitId = {unitSnapshot.Id}, skillId = {skillId}, type = {skill.SkillType}");
            }

            return skills;
        }

        private void InjectCharacterSkillIds(IUnitSnapshot unitSnapshot, List<string> skillIds)
        {
            if (_configDistributor.Characters.TryGet(unitSnapshot.Id, out var characterMapper) == false)
                return;

            var characterSkillIds = characterMapper.SkillIds;

            for (int i = 0; i < characterSkillIds.Length; i++)
            {
                var skillId = characterSkillIds[i].ToString();
                var beforeCount = skillIds.Count;

                AddUniqueSkillId(skillIds, skillId);

                if (beforeCount < skillIds.Count)
                    _logger.LogDebug($"[Story][Battle]: Character skill injected, unitId = {unitSnapshot.Id}, skillId = {skillId}");
            }
        }

        private void InjectEnemySkillIds(IUnitSnapshot unitSnapshot, List<string> skillIds)
        {
            if (_configDistributor.Enemies.TryGet(unitSnapshot.Id, out var enemyMapper) == false)
                return;

            var enemySkillIds = enemyMapper.SkillIds;

            for (int i = 0; i < enemySkillIds.Length; i++)
            {
                var skillId = enemySkillIds[i].ToString();
                var beforeCount = skillIds.Count;

                AddUniqueSkillId(skillIds, skillId);

                if (beforeCount < skillIds.Count)
                    _logger.LogDebug($"[Story][Battle]: Enemy skill injected, unitId = {unitSnapshot.Id}, skillId = {skillId}");
            }
        }

        private void InjectEquipmentSkillIds(IUnitSnapshot unitSnapshot, List<string> skillIds)
        {
            var equipment = unitSnapshot.Equipments;

            for (int i = 0; i < equipment.Count; i++)
            {
                var entry = equipment[i];

                if (entry == null || entry.Id <= 0)
                    continue;

                if (_configDistributor.Equipments.TryGet(entry.Id, out var equipmentMapper) == false)
                    continue;

                var skillId = equipmentMapper.SkillId;

                if (string.IsNullOrWhiteSpace(skillId))
                    continue;

                var trimmed = skillId.Trim();
                var probe = _skillFactory.Create(trimmed);

                if (probe.SkillType == SkillType.Unknown)
                {
                    _logger.LogWarning($"[Story][Battle]: Equipment skill_id unknown skipped, equipmentId = {entry.Id}, skillId = {trimmed}, unitId = {unitSnapshot.Id}");

                    continue;
                }

                var beforeCount = skillIds.Count;

                AddUniqueSkillId(skillIds, trimmed);

                if (beforeCount < skillIds.Count)
                    _logger.LogDebug($"[Story][Battle]: Equipment skill injected, unitId = {unitSnapshot.Id}, equipmentId = {entry.Id}, skillId = {trimmed}, type = {probe.SkillType}");
            }
        }

        private void AddUniqueSkillId(List<string> skillIds, string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return;

            var trimmed = skillId.Trim();

            for (int i = 0; i < skillIds.Count; i++)
            {
                if (string.Equals(skillIds[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            skillIds.Add(trimmed);
        }

        private void SeedActiveStatuses(UnitState unitState, IUnitSnapshot unitSnapshot, bool isSummon)
        {
            var statusIds = unitSnapshot.ActiveStatusIds;

            if (statusIds.Count == 0)
                return;

            if (isSummon)
            {
                _logger.LogError($"[Story][Battle]: Status seed on summon forbidden, unitId = {unitSnapshot.Id}, count = {statusIds.Count}");

                return;
            }

            var activeStatuses = unitState.ActiveStatuses;
            var commands = new List<BattleCommand>();

            for (int i = 0; i < statusIds.Count; i++)
            {
                var statusId = statusIds[i];

                if (_configDistributor.Statuses.TryGet(statusId, out var statusMapper) == false)
                {
                    _logger.LogError($"[Story][Battle]: Status missing, id = {statusId}");

                    continue;
                }

                if (statusMapper.StatusType == StatusType.Unknown)
                {
                    _logger.LogError($"[Config]: Status unknown type, id = {statusId}");

                    continue;
                }

                if (statusMapper.StatusType == StatusType.BonusChange)
                {
                    _logger.LogError($"[Story][Battle]: bonus_change cannot hang in snapshot, id = {statusId}, unitId = {unitState.Id}");

                    continue;
                }

                if (_statusParametersParser.TryParse(statusMapper.Parameters, statusMapper.StatusType, out var parameters) == false)
                    continue;

                var currentStacks = CountStatusStacks(activeStatuses, statusId);

                if (parameters.MaxStacks <= currentStacks)
                {
                    _logger.LogWarning($"[Story][Battle]: Status max stacks reached, id = {statusId}, maxStacks = {parameters.MaxStacks}");

                    continue;
                }

                var appliesBonuses = statusMapper.StatusType == StatusType.BurningStrong
                    || statusMapper.StatusType == StatusType.PoisonStrong;
                var sourceKey = $"status:{statusId}:{currentStacks}";
                var activeStatus = new ActiveStatus(
                    statusId,
                    parameters.DamageLength,
                    -1,
                    parameters.DamageRatio,
                    true,
                    appliesBonuses,
                    parameters.Bonuses,
                    sourceKey);

                if (appliesBonuses)
                    _battleBonusService.GrantRewardBonuses(unitState, parameters.Bonuses, 1, sourceKey, commands, 0);

                activeStatuses.Add(activeStatus);

                _logger.LogDebug($"[Story][Battle]: Seeded status, id = {statusId}, remainingTicks = {parameters.DamageLength}, damageRatio = {parameters.DamageRatio}, stacks = {currentStacks + 1}, bonuses = {parameters.Bonuses.Count}, applyingMainId = -1");
            }
        }

        private int CountStatusStacks(List<ActiveStatus> activeStatuses, int statusId)
        {
            var stacks = 0;

            for (int i = 0; i < activeStatuses.Count; i++)
            {
                if (activeStatuses[i].StatusId == statusId)
                    stacks += 1;
            }

            return stacks;
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

