using System.Diagnostics.CodeAnalysis;
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
        private readonly ICharacteristicCalculator _characteristicCalculator;
        private readonly IConfigDistributor _configDistributor;
        private readonly IPerkFactory _perkFactory;
        private readonly ISkillFactory _skillFactory;
        private readonly IStatusParametersParser _statusParametersParser;

        public UnitStateBuilder(
            ILogger<UnitStateBuilder> logger,
            IBattleBonusService battleBonusService,
            ICharacteristicCalculator characteristicCalculator,
            IConfigDistributor configDistributor,
            IPerkFactory perkFactory,
            ISkillFactory skillFactory,
            IStatusParametersParser statusParametersParser)
        {
            _logger = logger;
            _battleBonusService = battleBonusService;
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

            if (isSummon)
            {
                baseBuckets = BuildSummonBuckets(unitSnapshot);
                flags = UnitFlags.Summon | ResolveSummonMeleeFlags(unitSnapshot);
            }
            else if (battleSide == BattleSide.Defending)
            {
                if (_configDistributor.Enemies.TryGet(unitSnapshot.Id, out var enemyMapper))
                {
                    baseBuckets = BuildEnemyBuckets(enemyMapper, storyLevelId, stageId);
                    flags = enemyMapper.IsMelee ? UnitFlags.Melee : UnitFlags.Range;
                }
                else
                {
                    _logger.LogError($"[Story][Battle] enemy config missing id = {unitSnapshot.Id}; falling back to constants");

                    baseBuckets = BuildConstantsBuckets();
                    flags = UnitFlags.Melee;
                }
            }
            else if (_configDistributor.Characters.TryGet(unitSnapshot.Id, out var characterMapper))
            {
                baseBuckets = BuildConstantsBuckets();
                flags = ResolveCharacterMeleeFlags(unitSnapshot);
            }
            else
            {
                _logger.LogError($"[Story][Battle] character config missing id = {unitSnapshot.Id} side = {battleSide}; falling back to constants");

                baseBuckets = BuildConstantsBuckets();
                flags = UnitFlags.Melee;
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
                _battleBonusService.Rebuild(unitState, 0, new List<BattleCommand>(), false);
            }

            if (isSummon && _configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapperForBreakout))
                ApplyBreakoutHook(unitSnapshot, summonMapperForBreakout);

            SeedActiveStatuses(unitState, unitSnapshot);
            ApplyEquippedPerks(unitState);

            _logger.LogDebug($"[Story][Battle] built id = {unitState.Id} level = {unitState.Level} slot = {unitState.SlotIndex} hp = {characteristics.Health}/{characteristics.MaxHealth} dmg = {characteristics.Damage} vampyrism = {characteristics.Vampyrism} healingBoost = {characteristics.HealingBoost} statuses = {unitState.ActiveStatuses.Count} perks = {perks.Count} skills = {skills.Count} activeBonuses = {unitState.ActiveBonuses.Count}");

            return unitState;
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

            _logger.LogDebug($"[Story][Battle] equipped entities registered unitId = {unitState.Id} count = {unitState.EquippedEntities.Count}");
        }

        private CharacteristicBuckets BuildEnemyBuckets(IEnemyMapper enemyMapper, int storyLevelId, int stageId)
        {
            var attackMultiplier = 1f;
            var healthMultiplier = 1f;
            var stageMultiplier = 1f;

            if (_configDistributor.StoryLevels.TryGet(storyLevelId, out var storyLevel))
            {
                attackMultiplier = storyLevel.EnemiesAttackMultiplier;
                healthMultiplier = storyLevel.EnemiesHealthMultiplier;

                if (attackMultiplier <= 0f)
                    attackMultiplier = 1f;

                if (healthMultiplier <= 0f)
                    healthMultiplier = 1f;
            }
            else
            {
                _logger.LogWarning($"[Story][Battle] story level missing id = {storyLevelId}; enemy multipliers default 1");
            }

            if (0 < stageId && _configDistributor.StoryStages.TryGet(stageId, out var storyStage))
            {
                stageMultiplier = storyStage.EnemyStatsMultiplier;

                if (stageMultiplier <= 0f)
                    stageMultiplier = 1f;
            }
            else if (0 < stageId)
            {
                _logger.LogWarning($"[Story][Battle] story stage missing id = {stageId}; stage multiplier default 1");
            }

            var finalAttackMultiplier = attackMultiplier * stageMultiplier;
            var finalHealthMultiplier = healthMultiplier * stageMultiplier;
            _logger.LogDebug($"[Story][Battle] enemy multipliers storyLevelId = {storyLevelId} stageId = {stageId} attack = {finalAttackMultiplier} health = {finalHealthMultiplier} levelAttack = {attackMultiplier} levelHealth = {healthMultiplier} stage = {stageMultiplier}");

            var buckets = new CharacteristicBuckets
            {
                HealthBase = enemyMapper.Health * finalHealthMultiplier,
                DamageBase = enemyMapper.Damage * finalAttackMultiplier,
                AttackMultiplierBase = 1f,
                DefenceBase = enemyMapper.Defence,
                DefenceCoefficient = GetConstant(ConstantKeys.DefenceCoefficientKey),
                EvasionBase = enemyMapper.Evasion,
                CriticalChanceBase = enemyMapper.CriticalChance,
                CriticalMultiplierBase = enemyMapper.CriticalMultiplier,
                Combo1ChanceBase = enemyMapper.Combo1Chance,
                Combo2ChanceBase = enemyMapper.Combo2Chance,
                Combo1MultiplierBase = enemyMapper.Combo1Multiplier,
                Combo2MultiplierBase = enemyMapper.Combo2Multiplier,
                CounterChanceBase = enemyMapper.CounterChance,
                CounterMultiplierBase = enemyMapper.CounterMultiplier,
                SkillMultiplierBase = enemyMapper.SpellMultiplier,
                EnergyBase = enemyMapper.Energy,
                EnergyMaxBase = enemyMapper.MaxEnergy,
                VampyrismBase = enemyMapper.Vampyrism,
                HealingBoostBase = enemyMapper.HealingBoost - 1f,
            };

            if (buckets.HealingBoostBase < -1f)
                buckets.HealingBoostBase = -1f;

            return buckets;
        }

        private CharacteristicBuckets BuildSummonBuckets(IUnitSnapshot unitSnapshot)
        {
            var buckets = BuildConstantsBuckets();

            if (_configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapper) == false)
            {
                _logger.LogError($"[Story][Battle] summon config missing id = {unitSnapshot.Id}");

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
                _logger.LogError($"[Story][Battle] summon id = {unitSnapshot.Id} has empty dmg_on_lvls");

            var masteryMultiplier = 1f;
            var masteryLevel = unitSnapshot.MasteryLevel;

            if (TryResolveMastery(summonMapper.MasteryId, masteryLevel, unitSnapshot.Id, out var masteryMapper))
            {
                masteryMultiplier = masteryMapper.DamageMultiplier;
                _logger.LogDebug($"[Story][Battle] summon mastery damage summonId = {unitSnapshot.Id} masteryId = {summonMapper.MasteryId} masteryLevel = {masteryLevel} multiplier = {masteryMultiplier} baseDamage = {baseDamage}");
            }

            return baseDamage * masteryMultiplier;
        }

        private bool TryResolveMastery(int masteryId, int masteryLevel, int summonId, [MaybeNullWhen(false)] out IMasteryMapper masteryMapper)
        {
            var masteries = _configDistributor.Masteries;

            if (masteries.TryGet(masteryId, masteryLevel, out masteryMapper))
                return true;

            if (masteryLevel != 0 && masteries.TryGet(masteryId, 0, out masteryMapper))
            {
                _logger.LogWarning($"[Story][Battle] mastery level missing masteryId = {masteryId} masteryLevel = {masteryLevel} summonId = {summonId}; fallback masteryLevel = 0");

                return true;
            }

            _logger.LogError($"[Story][Battle] mastery missing masteryId = {masteryId} masteryLevel = {masteryLevel} summonId = {summonId}; using multiplier 1.0");
            masteryMapper = null;

            return false;
        }

        private void ApplyBreakoutHook(IUnitSnapshot unitSnapshot, ISummonMapper summonMapper)
        {
            if (summonMapper.BreakoutMultipliers == null || summonMapper.BreakoutMultipliers.Length == 0)
                return;

            _logger.LogError($"[Story][Battle] breakout_multis loaded but formula unknown summonId = {unitSnapshot.Id} level = {unitSnapshot.Level} valuesCount = {summonMapper.BreakoutMultipliers.Length}; hook no-op");
        }

        private void GrantTrainingBonuses(UnitState unitState, int trainingLevel)
        {
            if (trainingLevel <= 0)
                return;

            if (_configDistributor.Trainings.Collection.Count == 0)
            {
                _logger.LogDebug($"[Story][Battle] training grant skipped; Trainings manager empty trainingLevel = {trainingLevel}");

                return;
            }

            if (_configDistributor.Trainings.TryGet(trainingLevel, out var trainingMapper) == false)
            {
                _logger.LogWarning($"[Story][Battle] unknown trainingLevel = {trainingLevel}");

                return;
            }

            GrantBonusPairs(unitState, trainingMapper.BonusIds, trainingMapper.BonusValues, $"build:training:{trainingLevel}");
            _logger.LogDebug($"[Story][Battle] training bonuses granted trainingLevel = {trainingLevel} bonusCount = {trainingMapper.BonusIds.Length}");
        }

        private void GrantArtifactBonuses(UnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var artifactIds = unitSnapshot.ArtifactIds;

            if (artifactIds.Count == 0)
                return;

            if (_configDistributor.Artifacts.Collection.Count == 0)
            {
                _logger.LogDebug($"[Story][Battle] artifact grant skipped; Artifacts manager empty requestedCount = {artifactIds.Count}");

                return;
            }

            for (int i = 0; i < artifactIds.Count; i++)
            {
                var artifactId = artifactIds[i];

                if (_configDistributor.Artifacts.TryGet(artifactId, out var artifactMapper) == false)
                {
                    _logger.LogWarning($"[Story][Battle] unknown artifact id = {artifactId}");

                    continue;
                }

                GrantBonusPairs(unitState, artifactMapper.BonusIds, artifactMapper.BonusValues, $"build:artifact:{artifactId}");
                _logger.LogDebug($"[Story][Battle] artifact bonuses granted artifactId = {artifactId} bonusCount = {artifactMapper.BonusIds.Length}");
            }
        }

        private void GrantAspectBonuses(UnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var aspectIds = unitSnapshot.AspectIds;

            if (aspectIds.Count == 0)
                return;

            if (_configDistributor.Aspects.Collection.Count == 0)
            {
                _logger.LogDebug($"[Story][Battle] aspect grant skipped; Aspects manager empty requestedCount = {aspectIds.Count}");

                return;
            }

            for (int i = 0; i < aspectIds.Count; i++)
            {
                var aspectId = aspectIds[i];

                if (_configDistributor.Aspects.TryGet(aspectId, out var aspectMapper) == false)
                {
                    _logger.LogWarning($"[Story][Battle] unknown aspect id = {aspectId}");

                    continue;
                }

                GrantBonusPairs(unitState, aspectMapper.BonusIds, aspectMapper.BonusValues, $"build:aspect:{aspectId}");
                _logger.LogDebug($"[Story][Battle] aspect bonuses granted aspectId = {aspectId} bonusCount = {aspectMapper.BonusIds.Length}");
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
            var purchasedUpgrades = characterLevel - 1;

            if (purchasedUpgrades <= 0)
                return;

            var upgradeCosts = characterMapper.UpgradeCosts;
            var upgradeBonusTypes = characterMapper.UpgradeBonusTypes;
            var upgradeBonusValues = characterMapper.UpgradeBonusValues;

            if (upgradeCosts.Length == 0)
            {
                _logger.LogDebug($"[Story][Battle] character upgrades skipped id = {characterMapper.Id}; upgrade_costs empty");

                return;
            }

            var appliedUpgrades = purchasedUpgrades;

            if (upgradeCosts.Length < appliedUpgrades)
                appliedUpgrades = upgradeCosts.Length;

            for (int upgradeIndex = 0; upgradeIndex < appliedUpgrades; upgradeIndex++)
            {
                if (upgradeBonusTypes.Length <= upgradeIndex)
                {
                    _logger.LogWarning($"[Story][Battle] character upgrade type missing characterId = {characterMapper.Id} upgrade = {upgradeIndex + 1}");

                    break;
                }

                if (upgradeBonusValues.Length <= upgradeIndex)
                {
                    _logger.LogWarning($"[Story][Battle] character upgrade value missing characterId = {characterMapper.Id} upgrade = {upgradeIndex + 1}");

                    break;
                }

                var bonusId = upgradeBonusTypes[upgradeIndex];
                var value = upgradeBonusValues[upgradeIndex];
                var sourceKey = $"build:upgrade:{characterMapper.Id}:{upgradeIndex + 1}";
                GrantBuildBonus(unitState, bonusId, value, sourceKey);
                _logger.LogDebug($"[Story][Battle] character upgrade grant characterId = {characterMapper.Id} upgrade = {upgradeIndex + 1} cost = {upgradeCosts[upgradeIndex]} bonusId = {bonusId} value = {value}");
            }

            if (upgradeCosts.Length < purchasedUpgrades)
                _logger.LogDebug($"[Story][Battle] character upgrades capped characterId = {characterMapper.Id} level = {characterLevel} purchased = {purchasedUpgrades} maxFromCosts = {upgradeCosts.Length}");
        }

        public void GrantSummonAccountBonuses(IUnitState mainUnit, IUnitSnapshot summonSnapshot)
        {
            if (_configDistributor.Summons.TryGet(summonSnapshot.Id, out var summonMapper) == false)
            {
                _logger.LogWarning($"[Story][Battle] summon account bonuses skipped; missing summon id = {summonSnapshot.Id}");

                return;
            }

            var masteryLevel = summonSnapshot.MasteryLevel;

            if (TryResolveMastery(summonMapper.MasteryId, masteryLevel, summonSnapshot.Id, out var masteryMapper)
                && 0 < masteryMapper.BonusId)
            {
                var sourceKey = $"build:summon-mastery:{summonSnapshot.Id}:{masteryLevel}";
                GrantBuildBonus(mainUnit, masteryMapper.BonusId, 0f, sourceKey);
                _logger.LogDebug($"[Story][Battle] summon mastery bonus grant mainId = {mainUnit.Id} summonId = {summonSnapshot.Id} masteryLevel = {masteryLevel} bonusId = {masteryMapper.BonusId}");
            }

            var bonusMasteryLevels = summonMapper.BonusMasteryLevels;
            var bonusTypes = summonMapper.BonusTypes;
            var accountBonusCount = bonusMasteryLevels.Length;

            if (bonusTypes.Length < accountBonusCount)
                accountBonusCount = bonusTypes.Length;

            for (int i = 0; i < accountBonusCount; i++)
            {
                var requiredMasteryLevel = bonusMasteryLevels[i];

                if (masteryLevel < requiredMasteryLevel)
                    continue;

                var bonusId = bonusTypes[i];
                var sourceKey = $"build:summon-account:{summonSnapshot.Id}:{requiredMasteryLevel}:{bonusId}";
                GrantBuildBonus(mainUnit, bonusId, 0f, sourceKey);
                _logger.LogDebug($"[Story][Battle] summon account bonus grant mainId = {mainUnit.Id} summonId = {summonSnapshot.Id} requiredMastery = {requiredMasteryLevel} bonusId = {bonusId}");
            }
        }

        private UnitFlags ResolveSummonMeleeFlags(IUnitSnapshot unitSnapshot)
        {
            if (_configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapper) == false)
            {
                _logger.LogWarning($"[Story][Battle] summon missing for melee resolve id = {unitSnapshot.Id}");

                return UnitFlags.Range;
            }

            if (bool.TryParse(summonMapper.IsMelee, out var isMelee) && isMelee)
            {
                _logger.LogDebug($"[Story][Battle] summon melee flags id = {unitSnapshot.Id} flags = Melee");

                return UnitFlags.Melee;
            }

            _logger.LogDebug($"[Story][Battle] summon melee flags id = {unitSnapshot.Id} flags = Range isMeleeRaw = {summonMapper.IsMelee}");

            return UnitFlags.Range;
        }

        private UnitFlags ResolveCharacterMeleeFlags(IUnitSnapshot unitSnapshot)
        {
            var equipment = unitSnapshot.Equipments;

            for (int i = 0; i < equipment.Count; i++)
            {
                var entry = equipment[i];

                if (entry == null)
                    continue;

                if (_configDistributor.Equipments.TryGet(entry.Id, out var equipmentMapper) == false)
                {
                    _logger.LogWarning($"[Story][Battle] equipment missing for melee resolve id = {entry.Id}");

                    continue;
                }

                if (bool.TryParse(equipmentMapper.IsMelee, out var isMelee) && isMelee)
                {
                    _logger.LogDebug($"[Story][Battle] character melee flags unitId = {unitSnapshot.Id} equipmentId = {entry.Id} flags = Melee");

                    return UnitFlags.Melee;
                }
            }

            _logger.LogDebug($"[Story][Battle] character melee flags unitId = {unitSnapshot.Id} flags = Range");

            return UnitFlags.Range;
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
            buckets.DefenceBase = GetConstant(ConstantKeys.DefenceBaseKey);
            buckets.EvasionBase = GetConstant(ConstantKeys.EvasionBaseKey);
            buckets.CriticalChanceBase = GetConstant(ConstantKeys.CriticalChanceBaseKey);
            buckets.CriticalMultiplierBase = GetConstant(ConstantKeys.CriticalMultiplierBaseKey);
            buckets.Combo1ChanceBase = GetConstant(ConstantKeys.ComboOneChanceBaseKey);
            buckets.Combo2ChanceBase = GetConstant(ConstantKeys.ComboTwoChanceBaseKey);
            SeedComboMultiplierBases(buckets);
            buckets.CounterChanceBase = GetConstant(ConstantKeys.CounterChanceBaseKey);
            buckets.CounterMultiplierBase = GetConstant(ConstantKeys.CounterMultiplierBaseKey);
            buckets.SkillMultiplierBase = GetConstant(ConstantKeys.SkillMultiplierBaseKey);
            buckets.EnergyBase = GetConstant(ConstantKeys.EnergyBaseKey);
            buckets.EnergyMaxBase = GetConstant(ConstantKeys.EnergyMaxBaseKey);
            buckets.DefenceCoefficient = GetConstant(ConstantKeys.DefenceCoefficientKey);
            buckets.HealingBoostBase = GetConstant(ConstantKeys.HealingBoostBaseKey);
            buckets.VampyrismBase = 0f;
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
                    _logger.LogWarning($"[Story][Battle] equipment missing id = {equipmentId}");

                    continue;
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
                    _logger.LogWarning($"[Story][Battle] equipment bonus id invalid raw = {bonusTypeRaw}");

                    return false;
                }

                return true;
            }

            if (TryParseBonusTypeName(trimmed, out var bonusType) == false || bonusType == BonusType.Unknown)
            {
                _logger.LogWarning($"[Story][Battle] equipment bonus type parse failed raw = {bonusTypeRaw}");

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
                _logger.LogWarning($"[Story][Battle] equipment bonus type missing type = {bonusType}, raw = {bonusTypeRaw}");

                return false;
            }

            if (1 < matchCount)
                _logger.LogWarning($"[Story][Battle] equipment bonus type ambiguous type = {bonusType}, matches = {matchCount}, usingId = {firstMatch.Id}");

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

            _logger.LogWarning($"[Story][Battle] equipment bonus value parse failed raw = {bonusValueRaw} level = {equipmentLevel} segment = {segment}");

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

        private void GrantBuildBonus(IUnitState unitState, int bonusId, float value, string sourceKey)
        {
            if (bonusId <= 0)
                return;

            if (_configDistributor.Bonuses.TryGet(bonusId, out var bonusMapper) == false)
            {
                _logger.LogWarning($"[Story][Battle] bonus missing id = {bonusId}");

                return;
            }

            if (bonusMapper.BonusType == BonusType.Healing
                || bonusMapper.BonusType == BonusType.HealingFromMax
                || bonusMapper.BonusType == BonusType.CurrentHealthLocal)
            {
                var commands = new List<BattleCommand>();
                _battleBonusService.Grant(unitState, bonusId, 1, sourceKey, commands, 0);

                return;
            }

            var workMode = BonusWorkModeParser.ParseCore(bonusMapper.WorkModeParameters);
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

            _logger.LogDebug($"[Story][Battle] build bonus queued id = {bonusId} type = {bonusMapper.BonusType} value = {grantValue} operator = {bonusMapper.OperatorType} workMode = {workMode.Kind} sourceKey = {sourceKey}");
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
                    _logger.LogWarning($"[Story][Battle] perk missing id = {perkId}");

                    continue;
                }

                if (perkMapper.PerkType == PerkType.Unknown)
                {
                    _logger.LogError($"[Config] perk unknown type id = {perkId}, raw type unresolved");

                    continue;
                }

                try
                {
                    var perk = _perkFactory.Create(perkMapper);
                    perks.Add(perk);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, $"[Story][Battle] perk create failed id = {perkId} type = {perkMapper.PerkType}");
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
                AddUniqueSkillId(skillIds, summonMapper.SkillId);

            if (isSummon == false)
                InjectEquipmentSkillIds(unitSnapshot, skillIds);

            var skills = new List<ISkill>(skillIds.Count);

            for (int i = 0; i < skillIds.Count; i++)
            {
                var skillId = skillIds[i];
                var skill = _skillFactory.Create(skillId);

                if (skill.SkillType == SkillType.Unknown)
                {
                    _logger.LogWarning($"[Story][Battle] skill skipped unknown id = {skillId}, unitId = {unitSnapshot.Id}");

                    continue;
                }

                skills.Add(skill);
                _logger.LogDebug($"[Story][Battle] skill bound unitId = {unitSnapshot.Id}, skillId = {skillId}, type = {skill.SkillType}");
            }

            return skills;
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
                    _logger.LogWarning($"[Story][Battle] equipment skill_id unknown skipped equipmentId = {entry.Id} skillId = {trimmed} unitId = {unitSnapshot.Id}");

                    continue;
                }

                var beforeCount = skillIds.Count;
                AddUniqueSkillId(skillIds, trimmed);

                if (beforeCount < skillIds.Count)
                    _logger.LogDebug($"[Story][Battle] equipment skill injected unitId = {unitSnapshot.Id} equipmentId = {entry.Id} skillId = {trimmed} type = {probe.SkillType}");
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

        private void SeedActiveStatuses(UnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var statusIds = unitSnapshot.ActiveStatusIds;
            var activeStatuses = unitState.ActiveStatuses;
            var characteristics = unitState.CharacteristicState;
            var commands = new List<BattleCommand>();

            for (int i = 0; i < statusIds.Count; i++)
            {
                var statusId = statusIds[i];

                if (_configDistributor.Statuses.TryGet(statusId, out var statusMapper) == false)
                {
                    _logger.LogWarning($"[Story][Battle] status missing id = {statusId}");

                    continue;
                }

                if (statusMapper.StatusType == StatusType.Unknown)
                {
                    _logger.LogError($"[Config] status unknown type id = {statusId}");

                    continue;
                }

                var parameters = _statusParametersParser.Parse(statusMapper.Parameters);
                var currentStacks = CountStatusStacks(activeStatuses, statusId);

                if (parameters.MaxStacks <= currentStacks)
                {
                    _logger.LogWarning($"[Story][Battle] status max stacks reached id = {statusId}, maxStacks = {parameters.MaxStacks}");

                    continue;
                }

                var appliesDamageOverTime = IsDamageOverTime(statusMapper.StatusType);
                var appliesBonuses = IsBonusStatus(statusMapper.StatusType);
                var remainingTicks = parameters.DamageLength;

                if (appliesDamageOverTime == false && remainingTicks <= 0)
                    remainingTicks = int.MaxValue;

                if (appliesDamageOverTime && remainingTicks <= 0)
                {
                    _logger.LogWarning($"[Story][Battle] status damage_length missing id = {statusId}");

                    continue;
                }

                var sourceKey = $"status:{statusId}:{currentStacks}";
                var activeStatus = new ActiveStatus(
                    statusId,
                    remainingTicks,
                    -1,
                    parameters.DamageRatio,
                    appliesDamageOverTime,
                    appliesBonuses,
                    parameters.Bonuses,
                    sourceKey);

                if (appliesBonuses)
                    _battleBonusService.GrantRewardBonuses(unitState, parameters.Bonuses, 1, sourceKey, commands, 0);

                activeStatuses.Add(activeStatus);

                _logger.LogDebug($"[Story][Battle] seeded status id = {statusId}, remainingTicks = {remainingTicks}, damageRatio = {parameters.DamageRatio}, stacks = {currentStacks + 1}, bonuses = {parameters.Bonuses.Count}, target = {statusMapper.StatusTarget}");
            }
        }

        private bool IsDamageOverTime(StatusType statusType)
        {
            return statusType == StatusType.Burning
                || statusType == StatusType.BurningStrong
                || statusType == StatusType.Poison
                || statusType == StatusType.PoisonStrong;
        }

        private bool IsBonusStatus(StatusType statusType)
        {
            return statusType == StatusType.BonusChange
                || statusType == StatusType.BurningStrong
                || statusType == StatusType.PoisonStrong;
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

        private void SeedComboMultiplierBases(CharacteristicBuckets buckets)
        {
            var hasLegacy = TryGetConstant(ConstantKeys.ComboMultiplierBaseKey, out var legacyComboMultiplier);
            var hasCombo1 = TryGetConstant(ConstantKeys.ComboOneMultiplierBaseKey, out var combo1Multiplier);
            var hasCombo2 = TryGetConstant(ConstantKeys.ComboTwoMultiplierBaseKey, out var combo2Multiplier);

            if (hasCombo1 == false && hasLegacy)
            {
                combo1Multiplier = legacyComboMultiplier;
                hasCombo1 = true;
            }

            if (hasCombo2 == false && hasLegacy)
            {
                combo2Multiplier = legacyComboMultiplier;
                hasCombo2 = true;
            }

            if (hasCombo1 == false)
            {
                _logger.LogError($"[Story][Battle] constant missing key = {ConstantKeys.ComboOneMultiplierBaseKey} and fallback {ConstantKeys.ComboMultiplierBaseKey}");
                combo1Multiplier = 0f;
            }

            if (hasCombo2 == false)
            {
                _logger.LogError($"[Story][Battle] constant missing key = {ConstantKeys.ComboTwoMultiplierBaseKey} and fallback {ConstantKeys.ComboMultiplierBaseKey}");
                combo2Multiplier = 0f;
            }

            buckets.Combo1MultiplierBase = combo1Multiplier;
            buckets.Combo2MultiplierBase = combo2Multiplier;
            _logger.LogDebug($"[Story][Battle] combo multipliers base combo1 = {buckets.Combo1MultiplierBase} combo2 = {buckets.Combo2MultiplierBase}");
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
                _logger.LogError($"[Story][Battle] constant parse failed key = {constantKey} value = {constant.ConstantValue}");
                value = 0f;

                return false;
            }

            return true;
        }

        private float GetConstant(string constantKey)
        {
            if (TryGetConstant(constantKey, out var value) == false)
            {
                _logger.LogError($"[Story][Battle] constant missing key = {constantKey}");

                return 0f;
            }

            return value;
        }
    }
}

