using System.Globalization;
using Server.Common;
using Server.Perks;
using Server.Configs;
using Server.Entities;
using Server.Equipments;
using Server.Services;
using Server.Statuses;

namespace Server.Battles
{
    internal sealed class UnitLoadoutBinder : IUnitLoadoutBinder
    {
        private readonly ICoreLog _coreLog;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IConfigDistributor _configDistributor;
        private readonly IPerkFactory _perkFactory;
        private readonly ISkillFactory _skillFactory;
        private readonly IStatusParametersParser _statusParametersParser;

        public UnitLoadoutBinder(
            ICoreLog coreLog,
            IBattleBonusService battleBonusService,
            IConfigDistributor configDistributor,
            IPerkFactory perkFactory,
            ISkillFactory skillFactory,
            IStatusParametersParser statusParametersParser)
        {
            _coreLog = coreLog;
            _battleBonusService = battleBonusService;
            _configDistributor = configDistributor;
            _perkFactory = perkFactory;
            _skillFactory = skillFactory;
            _statusParametersParser = statusParametersParser;
        }

        public void ApplyEquippedPerks(IUnitState unitState)
        {
            var perks = unitState.Perks;
            var commands = new List<BattleCommand>();

            for (int i = 0; i < perks.Count; i++)
                perks[i].OnEquipped(unitState, commands);
        }

        public void RegisterSnapshotEquippedEntities(
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

                if (_configDistributor.Equipments.TryGet(entry.Id, out var equipmentMapper) == false)
                    continue;

                var equipmentSkillIds = equipmentMapper.SkillIds;

                for (int skillIndex = 0; skillIndex < equipmentSkillIds.Length; skillIndex++)
                    unitState.RegisterEquipmentSkill(equipmentSkillIds[skillIndex].ToString(CultureInfo.InvariantCulture));
            }

            _coreLog.Debug($"[Story][Battle]: Equipped entities registered, unitId = {unitState.Id}, count = {unitState.EquippedEntities.Count}");
        }

        public List<IPerk> BuildPerks(IUnitSnapshot unitSnapshot)
        {
            var perkIds = unitSnapshot.ActivePerkIds;
            var perks = new List<IPerk>(perkIds.Count);

            for (int i = 0; i < perkIds.Count; i++)
            {
                var perkId = perkIds[i];

                if (_configDistributor.Perks.TryGet(perkId, out var perkMapper) == false)
                {
                    _coreLog.Warning($"[Story][Battle]: Perk missing, id = {perkId}");

                    continue;
                }

                if (perkMapper.PerkType == PerkType.Unknown)
                {
                    _coreLog.Error($"[Config]: Perk unknown type, id = {perkId}, raw type unresolved");

                    continue;
                }

                try
                {
                    var perk = _perkFactory.Create(perkMapper);
                    perk.RestoreUsage(ReadPerkUsage(unitSnapshot, perkId));
                    perks.Add(perk);
                }
                catch (Exception exception)
                {
                    _coreLog.Warning(exception, $"[Story][Battle]: Perk create failed, id = {perkId}, type = {perkMapper.PerkType}");
                }
            }

            return perks;
        }

        private int ReadPerkUsage(IUnitSnapshot unitSnapshot, int perkId)
        {
            var usages = unitSnapshot.PerkUsages;

            for (int i = 0; i < usages.Count; i++)
            {
                var usage = usages[i];

                if (usage == null || usage.PerkId != perkId)
                    continue;

                return usage.UsedCount;
            }

            return 0;
        }

        public IReadOnlyList<ISkill> BuildSkills(IUnitSnapshot unitSnapshot, bool isSummon)
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
                    _coreLog.Warning($"[Story][Battle]: Skill skipped unknown id = {skillId}, unitId = {unitSnapshot.Id}");

                    continue;
                }

                skills.Add(skill);

                _coreLog.Debug($"[Story][Battle]: Skill bound, unitId = {unitSnapshot.Id}, skillId = {skillId}, type = {skill.SkillType}");
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
                    _coreLog.Debug($"[Story][Battle]: Character skill injected, unitId = {unitSnapshot.Id}, skillId = {skillId}");
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
                    _coreLog.Debug($"[Story][Battle]: Enemy skill injected, unitId = {unitSnapshot.Id}, skillId = {skillId}");
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

                var equipmentSkillIds = equipmentMapper.SkillIds;

                for (int skillIndex = 0; skillIndex < equipmentSkillIds.Length; skillIndex++)
                {
                    var skillId = equipmentSkillIds[skillIndex].ToString(CultureInfo.InvariantCulture);
                    var beforeCount = skillIds.Count;

                    AddUniqueSkillId(skillIds, skillId);

                    if (beforeCount < skillIds.Count)
                        _coreLog.Debug($"[Story][Battle]: Equipment skill injected, unitId = {unitSnapshot.Id}, equipmentId = {entry.Id}, skillId = {skillId}");
                }
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

        public void SeedActiveStatuses(UnitState unitState, IUnitSnapshot unitSnapshot, bool isSummon)
        {
            var statusIds = unitSnapshot.ActiveStatusIds;

            if (statusIds.Count == 0)
                return;

            if (isSummon)
            {
                _coreLog.Error($"[Story][Battle]: Status seed on summon forbidden, unitId = {unitSnapshot.Id}, count = {statusIds.Count}");

                return;
            }

            var activeStatuses = unitState.ActiveStatuses;
            var commands = new List<BattleCommand>();

            for (int i = 0; i < statusIds.Count; i++)
            {
                var statusId = statusIds[i];

                if (_configDistributor.Statuses.TryGet(statusId, out var statusMapper) == false)
                {
                    _coreLog.Error($"[Story][Battle]: Status missing, id = {statusId}");

                    continue;
                }

                if (statusMapper.StatusType == StatusType.Unknown)
                {
                    _coreLog.Error($"[Config]: Status unknown type, id = {statusId}");

                    continue;
                }

                if (statusMapper.StatusType == StatusType.BonusChange)
                {
                    _coreLog.Error($"[Story][Battle]: bonus_change cannot hang in snapshot, id = {statusId}, unitId = {unitState.Id}");

                    continue;
                }

                if (_statusParametersParser.TryParse(statusMapper.Parameters, statusMapper.StatusType, out var parameters) == false)
                    continue;

                var currentStacks = CountStatusStacks(activeStatuses, statusId);

                if (parameters.MaxStacks <= currentStacks)
                {
                    _coreLog.Warning($"[Story][Battle]: Status max stacks reached, id = {statusId}, maxStacks = {parameters.MaxStacks}");

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
                    parameters.FlatValue,
                    true,
                    appliesBonuses,
                    parameters.Bonuses,
                    sourceKey);

                if (appliesBonuses)
                    _battleBonusService.GrantRewardBonuses(unitState, parameters.Bonuses, 1, sourceKey, commands, 0);

                activeStatuses.Add(activeStatus);

                _coreLog.Debug($"[Story][Battle]: Seeded status, id = {statusId}, remainingTicks = {parameters.DamageLength}, damageRatio = {parameters.DamageRatio}, flatValue = {parameters.FlatValue}, stacks = {currentStacks + 1}, bonuses = {parameters.Bonuses.Count}, applyingMainId = -1");
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
    }
}
