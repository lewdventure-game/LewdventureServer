using System.Globalization;
using Server.Bonuses;
using Server.Common;
using Server.Configs;
using Server.Entities;
using Server.Equipments;
using Server.Services;
using Server.Statuses;

namespace Server.Battles
{
    internal sealed class UnitBonusGranter : IUnitBonusGranter
    {
        private readonly ICoreLog _coreLog;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly IConfigDistributor _configDistributor;
        private readonly IUnitBucketsFactory _unitBucketsFactory;

        public UnitBonusGranter(
            ICoreLog coreLog,
            IBattleBonusService battleBonusService,
            IBonusWorkModeParser bonusWorkModeParser,
            IConfigDistributor configDistributor,
            IUnitBucketsFactory unitBucketsFactory)
        {
            _coreLog = coreLog;
            _battleBonusService = battleBonusService;
            _bonusWorkModeParser = bonusWorkModeParser;
            _configDistributor = configDistributor;
            _unitBucketsFactory = unitBucketsFactory;
        }

        public void GrantTrainingBonuses(UnitState unitState, int trainingLevel)
        {
            if (trainingLevel <= 0)
            {
                _coreLog.Debug($"[Story][Battle]: Training grant skipped, trainingLevel = {trainingLevel}");

                return;
            }

            if (_configDistributor.Trainings.TryGet(trainingLevel, out var trainingMapper) == false)
            {
                _coreLog.Warning($"[Story][Battle]: Training missing, trainingLevel = {trainingLevel}, trainingsCount = {_configDistributor.Trainings.Collection.Count}; bonuses skipped");

                return;
            }

            GrantBonusPairs(unitState, trainingMapper.BonusIds, trainingMapper.BonusValues, $"build:training:{trainingLevel}");

            _coreLog.Debug($"[Story][Battle]: Training bonuses granted, trainingLevel = {trainingLevel}, bonusCount = {trainingMapper.BonusIds.Length}");
        }

        public void GrantArtifactBonuses(UnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var artifactIds = unitSnapshot.ArtifactIds;

            if (artifactIds.Count == 0)
                return;

            if (_configDistributor.Artifacts.Collection.Count == 0)
            {
                _coreLog.Debug($"[Story][Battle]: Artifact grant skipped; artifacts manager empty, requestedCount = {artifactIds.Count}");

                return;
            }

            for (int i = 0; i < artifactIds.Count; i++)
            {
                var artifactId = artifactIds[i];

                if (_configDistributor.Artifacts.TryGet(artifactId, out var artifactMapper) == false)
                {
                    _coreLog.Warning($"[Story][Battle]: Unknown artifact, id = {artifactId}");

                    continue;
                }

                GrantBonusPairs(unitState, artifactMapper.BonusIds, artifactMapper.BonusValues, $"build:artifact:{artifactId}");

                _coreLog.Debug($"[Story][Battle]: Artifact bonuses granted, artifactId = {artifactId}, bonusCount = {artifactMapper.BonusIds.Length}");
            }
        }

        public void GrantAspectBonuses(UnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var aspectIds = unitSnapshot.AspectIds;

            if (aspectIds.Count == 0)
                return;

            if (_configDistributor.Aspects.Collection.Count == 0)
            {
                _coreLog.Debug($"[Story][Battle]: Aspect grant skipped; aspects manager empty, requestedCount = {aspectIds.Count}");

                return;
            }

            for (int i = 0; i < aspectIds.Count; i++)
            {
                var aspectId = aspectIds[i];

                if (_configDistributor.Aspects.TryGet(aspectId, out var aspectMapper) == false)
                {
                    _coreLog.Warning($"[Story][Battle]: Unknown aspect id = {aspectId}");

                    continue;
                }

                GrantBonusPairs(unitState, aspectMapper.BonusIds, aspectMapper.BonusValues, $"build:aspect:{aspectId}");

                _coreLog.Debug($"[Story][Battle]: Aspect bonuses granted, aspectId = {aspectId}, bonusCount = {aspectMapper.BonusIds.Length}");
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

        public void GrantCharacterUpgradeBonuses(UnitState unitState, ICharacterMapper characterMapper, int characterLevel)
        {
            if (characterLevel <= 0)
            {
                _coreLog.Error($"[Story][Battle]: Invalid character level = {characterLevel}, characterId = {characterMapper.Id}");

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
                _coreLog.Debug($"[Story][Battle]: Character upgrades skipped, id = {characterMapper.Id}; upgrade_costs empty");

                return;
            }

            var appliedUpgrades = purchasedUpgrades;

            if (upgradeCosts.Length < appliedUpgrades)
                appliedUpgrades = upgradeCosts.Length;

            for (int upgradeIndex = 0; upgradeIndex < appliedUpgrades; upgradeIndex++)
            {
                if (upgradeBonusTypes.Length <= upgradeIndex)
                {
                    _coreLog.Warning($"[Story][Battle]: Character upgrade type missing, characterId = {characterMapper.Id}, upgrade = {upgradeIndex + 1}");

                    break;
                }

                if (upgradeBonusValues.Length <= upgradeIndex)
                {
                    _coreLog.Warning($"[Story][Battle]: Character upgrade value missing, characterId = {characterMapper.Id}, upgrade = {upgradeIndex + 1}");

                    break;
                }

                var bonusId = upgradeBonusTypes[upgradeIndex];
                var value = upgradeBonusValues[upgradeIndex];
                var sourceKey = $"build:upgrade:{characterMapper.Id}:{upgradeIndex + 1}";

                GrantBuildBonus(unitState, bonusId, value, sourceKey);

                _coreLog.Debug($"[Story][Battle]: Character upgrade grant, characterId = {characterMapper.Id}, upgrade = {upgradeIndex + 1}, cost = {upgradeCosts[upgradeIndex]}, bonusId = {bonusId}, value = {value}");
            }

            if (upgradeCosts.Length < purchasedUpgrades)
                _coreLog.Debug($"[Story][Battle]: Character upgrades capped, characterId = {characterMapper.Id}, level = {characterLevel}, purchased = {purchasedUpgrades}, maxFromCosts = {upgradeCosts.Length}");
        }

        public void GrantSummonAccountBonuses(IUnitState mainUnit, IUnitSnapshot summonSnapshot)
        {
            if (_configDistributor.Summons.TryGet(summonSnapshot.Id, out var summonMapper) == false)
            {
                _coreLog.Error($"[Story][Battle]: Summon account bonuses skipped; missing summon id = {summonSnapshot.Id}");

                throw new InvalidOperationException($"[Story][Battle]: Summon missing id = {summonSnapshot.Id}");
            }

            var masteryLevel = summonSnapshot.MasteryLevel;

            if (0 < masteryLevel)
            {
                if (_unitBucketsFactory.TryResolveMastery(summonMapper.MasteryId, masteryLevel, summonSnapshot.Id, out var masteryMapper) && 0 < masteryMapper.BonusId)
                {
                    var sourceKey = $"build:summon-mastery:{summonSnapshot.Id}:{masteryLevel}";

                    GrantBuildBonus(mainUnit, masteryMapper.BonusId, 0f, sourceKey);

                    _coreLog.Debug($"[Story][Battle]: Summon mastery bonus grant, mainId = {mainUnit.Id}, summonId = {summonSnapshot.Id}, masteryLevel = {masteryLevel}, bonusId = {masteryMapper.BonusId}");
                }
            }
        }

        public void GrantEquipmentBonuses(UnitState unitState, IUnitSnapshot unitSnapshot)
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
                    _coreLog.Error($"[Story][Battle]: Equipment missing id = {equipmentId}");

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

            _coreLog.Debug($"[Story][Battle] equipment grant slot sourceKey = {sourceKey} level = {equipmentLevel} bonusId = {bonusId} value = {value}");

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
                    _coreLog.Warning($"[Story][Battle]: Equipment bonus id invalid raw = {bonusTypeRaw}");

                    return false;
                }

                return true;
            }

            if (TryParseBonusTypeName(trimmed, out var bonusType) == false || bonusType == BonusType.Unknown)
            {
                _coreLog.Warning($"[Story][Battle]: Equipment bonus type parse failed raw = {bonusTypeRaw}");

                return false;
            }

            IBonusMapper lowestIdMatch = default!;

            var hasMatch = false;
            var matchCount = 0;

            foreach (var bonusMapper in _configDistributor.Bonuses.Values)
            {
                if (bonusMapper.BonusType != bonusType)
                    continue;

                matchCount += 1;

                if (hasMatch && lowestIdMatch.Id <= bonusMapper.Id)
                    continue;

                lowestIdMatch = bonusMapper;
                hasMatch = true;
            }

            if (hasMatch == false)
            {
                _coreLog.Warning($"[Story][Battle]: Equipment bonus type missing, type = {bonusType}, raw = {bonusTypeRaw}");

                return false;
            }

            if (1 < matchCount)
                _coreLog.Warning($"[Story][Battle]: Equipment bonus type ambiguous, type = {bonusType}, matches = {matchCount}, usingId = {lowestIdMatch.Id}");

            bonusId = lowestIdMatch.Id;

            return true;
        }

        private bool TryParseBonusTypeName(string value, out BonusType bonusType)
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

            _coreLog.Warning($"[Story][Battle]: Equipment bonus value parse failed, raw = {bonusValueRaw}, level = {equipmentLevel}, segment = {segment}");

            return 0f;
        }

        private string[] SplitEquipmentBonusValueSegments(string bonusValueRaw)
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

        public void GrantSnapshotRunBonuses(IUnitState unitState, IUnitSnapshot unitSnapshot)
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
                    _coreLog.Error($"[Story][Battle]: Run bonus skip invalid, id = {grant.Id}, count = {grant.Count}");

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
                    _coreLog.Debug($"[Story][Battle]: Run bonus remaining override skipped, unitId = {unitState.Id}, bonusId = {bonusId}, workMode = {activeBonus.WorkMode.Format()}, sourceKey = {sourceKey}");

                    return;
                }

                activeBonus.RemainingBattles = remainingBattles;

                _coreLog.Debug($"[Story][Battle]: Run bonus remaining override, unitId = {unitState.Id}, bonusId = {activeBonus.BonusId}, remainingBattles = {remainingBattles}, sourceKey = {sourceKey}");

                return;
            }
        }

        public void GrantBuildBonus(IUnitState unitState, int bonusId, float value, string sourceKey)
        {
            if (bonusId <= 0)
                return;

            if (_configDistributor.Bonuses.TryGet(bonusId, out var bonusMapper) == false)
            {
                _coreLog.Error($"[Story][Battle]: Bonus missing, id = {bonusId}");

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
                _coreLog.Error($"[Story][Battle]: work_mode parse failed, bonusId = {bonusId}");

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

            _coreLog.Debug($"[Story][Battle]: Build bonus queued, id = {bonusId}, type = {bonusMapper.BonusType}, value = {grantValue}, operator = {bonusMapper.OperatorType}, workMode = {workMode.Format()}, sourceKey = {sourceKey}");
        }
    }
}
