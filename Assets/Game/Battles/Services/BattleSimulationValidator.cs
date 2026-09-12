using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleSimulationValidator : IBattleSimulationValidator
    {
        private readonly ILogger<BattleSimulationValidator> _logger;
        private readonly IConfigDistributor _configDistributor;
        private readonly ISkillFactory _skillFactory;

        public BattleSimulationValidator(
            ILogger<BattleSimulationValidator> logger,
            IConfigDistributor configDistributor,
            ISkillFactory skillFactory)
        {
            _logger = logger;
            _configDistributor = configDistributor;
            _skillFactory = skillFactory;
        }

        public bool TryValidate(IBattleSimulationData data, out string errorMessage)
        {
            if (data == null)
            {
                errorMessage = "Request body is required.";

                _logger.LogWarning($"[Story][Battle]: Validation failed: {errorMessage}");
                _logger.LogInformation($"[Config]: Snapshot summary unavailable; request body is null");

                return false;
            }

            if (data.TeamA == null)
            {
                errorMessage = "teamA is required.";

                LogValidationFailure(data, errorMessage);

                return false;
            }

            if (data.TeamB == null)
            {
                errorMessage = "teamB is required.";

                LogValidationFailure(data, errorMessage);

                return false;
            }

            if (ValidateTeam(data.TeamA, BattleSide.Attacking, out errorMessage) == false)
            {
                LogValidationFailure(data, errorMessage);

                return false;
            }

            if (ValidateTeam(data.TeamB, BattleSide.Defending, out errorMessage) == false)
            {
                LogValidationFailure(data, errorMessage);

                return false;
            }

            if (_configDistributor.StoryLevels.TryGet(data.StoryLevelId, out _) == false)
            {
                errorMessage = $"Unknown storyLevelId = {data.StoryLevelId}.";

                LogValidationFailure(data, errorMessage);

                return false;
            }

            if (data.StageId != 0 && _configDistributor.StoryStages.TryGet(data.StageId, out _) == false)
            {
                errorMessage = $"Unknown stageId = {data.StageId}.";

                LogValidationFailure(data, errorMessage);

                return false;
            }

            errorMessage = string.Empty;

            LogSnapshotSummary(data);

            return true;
        }

        private void LogSnapshotSummary(IBattleSimulationData data)
        {
            LogTeamSnapshot("A", data.TeamA, data.StoryLevelId, data.StageId);
            LogTeamSnapshot("B", data.TeamB, data.StoryLevelId, data.StageId);
        }

        private void LogTeamSnapshot(string teamName, ITeamSnapshot team, int storyLevelId, int stageId)
        {
            if (team == null)
            {
                _logger.LogDebug($"[Story][Battle]: Snapshot team = {teamName} missing, storyLevelId = {storyLevelId}, stageId = {stageId}");

                return;
            }

            LogUnitsSnapshot(teamName, "main", team.MainUnits, storyLevelId, stageId);
            LogUnitsSnapshot(teamName, "summon", team.Summons, storyLevelId, stageId);
        }

        private void LogUnitsSnapshot(
            string teamName,
            string groupName,
            List<IUnitSnapshot> units,
            int storyLevelId,
            int stageId)
        {
            if (units == null)
                return;

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                if (unit == null)
                    continue;

                var perkCount = unit.ActivePerkIds == null ? 0 : unit.ActivePerkIds.Count;
                var skillCount = unit.ActiveSkillIds == null ? 0 : unit.ActiveSkillIds.Count;
                var equipmentCount = unit.Equipments == null ? 0 : unit.Equipments.Count;
                var bonusCount = unit.ActiveBonuses == null ? 0 : unit.ActiveBonuses.Count;

                _logger.LogDebug($"[Story][Battle]: Snapshot team = {teamName}, group = {groupName}, id = {unit.Id}, slot = {unit.SlotIndex}, level = {unit.Level}, mastery = {unit.MasteryLevel}, training = {unit.TrainingLevel}, perkCount = {perkCount}, skillCount = {skillCount}, equipmentCount = {equipmentCount}, bonusCount = {bonusCount}, storyLevelId = {storyLevelId}, stageId = {stageId}");
            }
        }

        private bool ValidateTeam(ITeamSnapshot team, BattleSide battleSide, out string errorMessage)
        {
            if (team.MainUnits == null || team.MainUnits.Count == 0)
            {
                errorMessage = $"team {(battleSide == BattleSide.Attacking ? "A" : "B")} mainUnits must not be empty.";

                return false;
            }

            if (team.Summons == null)
            {
                errorMessage = $"team {(battleSide == BattleSide.Attacking ? "A" : "B")} summons must not be null.";

                return false;
            }

            for (int i = 0; i < team.MainUnits.Count; i++)
            {
                var unit = team.MainUnits[i];

                if (ValidateUnit(unit, battleSide, false, out errorMessage) == false)
                    return false;
            }

            for (int i = 0; i < team.Summons.Count; i++)
            {
                var unit = team.Summons[i];

                if (ValidateUnit(unit, battleSide, true, out errorMessage) == false)
                    return false;
            }

            errorMessage = string.Empty;

            return true;
        }

        private bool ValidateUnit(IUnitSnapshot unit, BattleSide battleSide, bool isSummon, out string errorMessage)
        {
            if (unit == null)
            {
                errorMessage = "Unit snapshot must not be null.";

                return false;
            }

            if (isSummon)
            {
                if (_configDistributor.Summons.TryGet(unit.Id, out _) == false)
                {
                    errorMessage = $"Unknown summon id = {unit.Id}.";

                    return false;
                }
            }
            else if (battleSide == BattleSide.Attacking)
            {
                if (_configDistributor.Characters.TryGet(unit.Id, out _) == false)
                {
                    errorMessage = $"Unknown character id = {unit.Id}.";

                    return false;
                }
            }
            else if (_configDistributor.Enemies.TryGet(unit.Id, out _) == false)
            {
                errorMessage = $"Unknown enemy id = {unit.Id}.";

                return false;
            }

            if (unit.Equipments == null)
            {
                errorMessage = $"equipment must not be null for unit id = {unit.Id}.";

                return false;
            }

            if (unit.EquipmentIds == null)
            {
                errorMessage = $"equipmentIds must not be null for unit id = {unit.Id}.";

                return false;
            }

            for (int i = 0; i < unit.Equipments.Count; i++)
            {
                var equipmentEntry = unit.Equipments[i];

                if (equipmentEntry == null)
                {
                    errorMessage = $"equipment entry is null for unit id = {unit.Id}.";

                    return false;
                }

                if (equipmentEntry.Level <= 0)
                {
                    errorMessage = $"equipment level must be >= 1 for equipment id = {equipmentEntry.Id}, unit id = {unit.Id}.";

                    return false;
                }

                if (_configDistributor.Equipments.TryGet(equipmentEntry.Id, out _) == false)
                {
                    errorMessage = $"Unknown equipment id = {equipmentEntry.Id} for unit id = {unit.Id}.";

                    return false;
                }
            }

            if (unit.ArtifactIds == null)
            {
                errorMessage = $"artifactIds must not be null for unit id = {unit.Id}.";

                return false;
            }

            if (unit.AspectIds == null)
            {
                errorMessage = $"aspectIds must not be null for unit id = {unit.Id}.";

                return false;
            }

            if (unit.ActivePerkIds == null)
            {
                errorMessage = $"activePerkIds must not be null for unit id = {unit.Id}.";

                return false;
            }

            for (int i = 0; i < unit.ActivePerkIds.Count; i++)
            {
                var perkId = unit.ActivePerkIds[i];

                if (_configDistributor.Perks.TryGet(perkId, out _) == false)
                {
                    errorMessage = $"Unknown perk id = {perkId} for unit id = {unit.Id}.";

                    return false;
                }
            }

            if (unit.ActiveStatusIds == null)
            {
                errorMessage = $"activeStatusIds must not be null for unit id = {unit.Id}.";

                return false;
            }

            for (int i = 0; i < unit.ActiveStatusIds.Count; i++)
            {
                var statusId = unit.ActiveStatusIds[i];

                if (_configDistributor.Statuses.TryGet(statusId, out _) == false)
                {
                    errorMessage = $"Unknown status id = {statusId} for unit id = {unit.Id}.";

                    return false;
                }
            }

            if (unit.ActiveSkillIds == null)
            {
                errorMessage = $"activeSkillIds must not be null for unit id = {unit.Id}.";

                return false;
            }

            for (int i = 0; i < unit.ActiveSkillIds.Count; i++)
            {
                var skillId = unit.ActiveSkillIds[i];

                if (string.IsNullOrWhiteSpace(skillId))
                {
                    errorMessage = $"activeSkillIds contains empty skill id for unit id = {unit.Id}.";

                    return false;
                }

                if (_skillFactory.IsKnownSkillId(skillId) == false)
                {
                    errorMessage = $"Unknown skill id = {skillId} for unit id = {unit.Id}.";

                    return false;
                }
            }

            if (isSummon
                && _configDistributor.Summons.TryGet(unit.Id, out var summonMapper)
                && string.IsNullOrWhiteSpace(summonMapper.SkillId) == false
                && _skillFactory.IsKnownSkillId(summonMapper.SkillId) == false)
            {
                errorMessage = $"Unknown summon skill id = {summonMapper.SkillId} for unit id = {unit.Id}.";

                return false;
            }

            if (ValidateActiveBonuses(unit, out errorMessage) == false)
                return false;

            _logger.LogDebug($"[Story][Battle]: Unit snapshot resolved id = {unit.Id}, masteryLevel = {unit.MasteryLevel}, trainingLevel = {unit.TrainingLevel}, equipmentCount = {unit.Equipments.Count}, artifactCount = {unit.ArtifactIds.Count}, aspectCount = {unit.AspectIds.Count}, bonusCount = {unit.ActiveBonuses.Count}");

            errorMessage = string.Empty;

            return true;
        }

        private bool ValidateActiveBonuses(IUnitSnapshot unit, out string errorMessage)
        {
            if (unit.ActiveBonuses == null)
            {
                errorMessage = $"activeBonuses must not be null for unit id = {unit.Id}.";

                return false;
            }

            for (int i = 0; i < unit.ActiveBonuses.Count; i++)
            {
                var grant = unit.ActiveBonuses[i];

                if (grant == null)
                {
                    errorMessage = $"activeBonuses entry is null for unit id = {unit.Id}.";

                    return false;
                }

                if (grant.Id <= 0 || grant.Count <= 0)
                {
                    errorMessage = $"activeBonuses id and count must be > 0 for unit id = {unit.Id}.";

                    return false;
                }

                if (_configDistributor.Bonuses.TryGet(grant.Id, out _) == false)
                {
                    _logger.LogWarning($"[Story][Battle] unknown run bonus id = {grant.Id} for unit id = {unit.Id}");

                    continue;
                }
            }

            errorMessage = string.Empty;

            return true;
        }

        private void LogValidationFailure(IBattleSimulationData data, string errorMessage)
        {
            _logger.LogWarning($"[Story][Battle]: Validation failed: {errorMessage}");

            var teamAEquipmentCount = CountTeamEquipment(data.TeamA);
            var teamBEquipmentCount = CountTeamEquipment(data.TeamB);
            var teamAArtifactCount = CountTeamArtifacts(data.TeamA);
            var teamBArtifactCount = CountTeamArtifacts(data.TeamB);
            var teamAAspectCount = CountTeamAspects(data.TeamA);
            var teamBAspectCount = CountTeamAspects(data.TeamB);

            _logger.LogInformation($"[Config] snapshot summary stageId = {data.StageId}, storyLevelId = {data.StoryLevelId}, teamAEquipment = {teamAEquipmentCount}, teamBEquipment = {teamBEquipmentCount}, teamAArtifacts = {teamAArtifactCount}, teamBArtifacts = {teamBArtifactCount}, teamAAspects = {teamAAspectCount}, teamBAspects = {teamBAspectCount}");
        }

        private static int CountTeamEquipment(ITeamSnapshot team)
        {
            if (team == null)
                return 0;

            var mainUnitEquipments = CountUnitsEquipments(team.MainUnits);
            var summonEquipments = CountUnitsEquipments(team.Summons);

            return mainUnitEquipments + summonEquipments;
        }

        private static int CountUnitsEquipments(List<IUnitSnapshot> units)
        {
            if (units == null)
                return 0;

            var count = 0;

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                if (unit == null || unit.Equipments == null)
                    continue;

                count += unit.Equipments.Count;
            }

            return count;
        }

        private static int CountTeamArtifacts(ITeamSnapshot team)
        {
            if (team == null)
                return 0;

            var mainUnitArtifacts = CountUnitsArtifacts(team.MainUnits);
            var summonArtifacts = CountUnitsArtifacts(team.Summons);

            return mainUnitArtifacts + summonArtifacts;
        }

        private static int CountUnitsArtifacts(List<IUnitSnapshot> units)
        {
            if (units == null)
                return 0;

            var count = 0;

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                if (unit == null || unit.ArtifactIds == null)
                    continue;

                count += unit.ArtifactIds.Count;
            }

            return count;
        }

        private static int CountTeamAspects(ITeamSnapshot team)
        {
            if (team == null)
                return 0;

            var mainUnitAspects = CountUnitsAspects(team.MainUnits);
            var summonAspects = CountUnitsAspects(team.Summons);

            return mainUnitAspects + summonAspects;
        }

        private static int CountUnitsAspects(List<IUnitSnapshot> units)
        {
            if (units == null)
                return 0;

            var count = 0;

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                if (unit == null || unit.AspectIds == null)
                    continue;

                count += unit.AspectIds.Count;
            }

            return count;
        }
    }
}
