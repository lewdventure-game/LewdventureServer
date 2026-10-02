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

            GrantBonusIds(unitState, trainingMapper.BonusIds, $"build:training:{trainingLevel}");

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

                GrantBonusIds(unitState, artifactMapper.BonusIds, $"build:artifact:{artifactId}");

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

                GrantBonusIds(unitState, aspectMapper.BonusIds, $"build:aspect:{aspectId}");

                _coreLog.Debug($"[Story][Battle]: Aspect bonuses granted, aspectId = {aspectId}, bonusCount = {aspectMapper.BonusIds.Length}");
            }
        }

        private void GrantBonusIds(UnitState unitState, int[] bonusIds, string sourcePrefix)
        {
            if (bonusIds.Length == 0)
                return;

            for (int i = 0; i < bonusIds.Length; i++)
                GrantBuildBonus(unitState, bonusIds[i], $"{sourcePrefix}:{i}");
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

                    GrantBuildBonus(mainUnit, masteryMapper.BonusId, sourceKey);

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

                if (entry == null || entry.Id <= 0)
                    continue;

                var equipmentId = entry.Id;
                var equipmentLevel = entry.Level;

                if (_configDistributor.Equipments.TryGet(equipmentId, out var equipmentMapper) == false)
                {
                    _coreLog.Error($"[Story][Battle]: Equipment missing id = {equipmentId}");

                    throw new InvalidOperationException($"[Story][Battle]: Equipment missing id = {equipmentId}");
                }

                var bonusIds = equipmentMapper.BonusIds;

                for (int bonusIndex = 0; bonusIndex < bonusIds.Length; bonusIndex++)
                    GrantEquipmentBonus(unitState, bonusIds[bonusIndex], equipmentLevel, $"build:equip:{equipmentId}:{bonusIds[bonusIndex]}");
            }
        }

        private void GrantEquipmentBonus(UnitState unitState, int bonusId, int equipmentLevel, string sourceKey)
        {
            if (bonusId <= 0)
                return;

            if (TryReadBonus(bonusId, out var bonusMapper) == false)
                return;

            var values = bonusMapper.BonusValues;

            if (values.Length == 0)
            {
                _coreLog.Warning($"[Story][Battle]: Equipment bonus has no values, bonusId = {bonusId}, sourceKey = {sourceKey}");

                return;
            }

            var index = equipmentLevel - 1;

            if (index < 0)
                index = 0;

            if (values.Length <= index)
            {
                _coreLog.Warning($"[Story][Battle]: Equipment bonus has no value for level, bonusId = {bonusId}, level = {equipmentLevel}, values = {values.Length}");

                index = values.Length - 1;
            }

            _coreLog.Debug($"[Story][Battle] equipment grant sourceKey = {sourceKey} level = {equipmentLevel} bonusId = {bonusId} value = {values[index]}");

            GrantResolvedBonus(unitState, bonusId, bonusMapper, values[index], sourceKey);
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

        public void GrantBuildBonus(IUnitState unitState, int bonusId, string sourceKey)
        {
            if (bonusId <= 0)
                return;

            if (TryReadBonus(bonusId, out var bonusMapper) == false)
                return;

            GrantResolvedBonus(unitState, bonusId, bonusMapper, bonusMapper.BonusValue, sourceKey);
        }

        private void GrantLeveledBonus(IUnitState unitState, int bonusId, float value, string sourceKey)
        {
            if (bonusId <= 0)
                return;

            if (TryReadBonus(bonusId, out var bonusMapper) == false)
                return;

            GrantResolvedBonus(unitState, bonusId, bonusMapper, value, sourceKey);
        }

        private bool TryReadBonus(int bonusId, out IBonusMapper bonusMapper)
        {
            if (_configDistributor.Bonuses.TryGet(bonusId, out bonusMapper))
                return true;

            _coreLog.Error($"[Story][Battle]: Bonus missing, id = {bonusId}");

            throw new InvalidOperationException($"[Story][Battle]: Bonus missing, id = {bonusId}");
        }

        private void GrantResolvedBonus(IUnitState unitState, int bonusId, IBonusMapper bonusMapper, float value, string sourceKey)
        {
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

            unitState.ActiveBonuses.Add(
                new ActiveBattleBonus(
                    bonusId,
                    1,
                    bonusMapper.BonusType,
                    value,
                    bonusMapper.OperatorType,
                    workMode,
                    sourceKey));

            _coreLog.Debug($"[Story][Battle]: Build bonus queued, id = {bonusId}, type = {bonusMapper.BonusType}, value = {value}, operator = {bonusMapper.OperatorType}, workMode = {workMode.Format()}, sourceKey = {sourceKey}");
        }

    }
}
