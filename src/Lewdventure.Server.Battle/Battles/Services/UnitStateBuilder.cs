using System.Globalization;
using Server.Common;
using Server.Configs;
using Server.Entities;
using Server.Equipments;
using Server.Services;
using Server.Statuses;

namespace Server.Battles
{
    internal sealed class UnitStateBuilder : IUnitStateBuilder
    {
        private readonly ICoreLog _coreLog;
        private readonly IBattleBonusService _battleBonusService;
        private readonly ICharacteristicCalculator _characteristicCalculator;
        private readonly IConfigDistributor _configDistributor;
        private readonly IUnitBonusGranter _unitBonusGranter;
        private readonly IUnitBucketsFactory _unitBucketsFactory;
        private readonly ISkillLevelResolver _skillLevelResolver;
        private readonly IUnitLoadoutBinder _unitLoadoutBinder;

        public UnitStateBuilder(
            ICoreLog coreLog,
            IBattleBonusService battleBonusService,
            ICharacteristicCalculator characteristicCalculator,
            IConfigDistributor configDistributor,
            IUnitBonusGranter unitBonusGranter,
            IUnitBucketsFactory unitBucketsFactory,
            ISkillLevelResolver skillLevelResolver,
            IUnitLoadoutBinder unitLoadoutBinder)
        {
            _coreLog = coreLog;
            _battleBonusService = battleBonusService;
            _characteristicCalculator = characteristicCalculator;
            _configDistributor = configDistributor;
            _unitBonusGranter = unitBonusGranter;
            _unitBucketsFactory = unitBucketsFactory;
            _skillLevelResolver = skillLevelResolver;
            _unitLoadoutBinder = unitLoadoutBinder;
        }

        public IUnitState Build(IUnitSnapshot unitSnapshot, BattleSide battleSide, bool isSummon, int storyLevelId, int stageId)
        {
            CharacteristicBuckets baseBuckets;
            UnitFlags flags;
            var attackCooldownTurns = 0;

            if (isSummon)
            {
                baseBuckets = _unitBucketsFactory.BuildSummonBuckets(unitSnapshot);
                flags = UnitFlags.Summon | _unitBucketsFactory.ResolveSummonMeleeFlags(unitSnapshot);
                attackCooldownTurns = _unitBucketsFactory.ResolveSummonAttackCooldown(unitSnapshot);
            }
            else if (battleSide == BattleSide.Defending)
            {
                if (_configDistributor.Enemies.TryGet(unitSnapshot.Id, out var enemyMapper))
                {
                    baseBuckets = _unitBucketsFactory.BuildEnemyBuckets(enemyMapper, storyLevelId, stageId);
                    flags = enemyMapper.IsMelee ? UnitFlags.Melee : UnitFlags.Range;

                    _coreLog.Debug($"[Story][Battle]: Enemy melee flags, id = {unitSnapshot.Id}, isMelee = {enemyMapper.IsMelee}, flags = {flags}");
                }
                else
                {
                    _coreLog.Error($"[Story][Battle]: Enemy config missing, id = {unitSnapshot.Id}");

                    throw new InvalidOperationException($"[Story][Battle]: Enemy config missing, id = {unitSnapshot.Id}");
                }
            }
            else if (_configDistributor.Characters.TryGet(unitSnapshot.Id, out var characterMapper))
            {
                baseBuckets = _unitBucketsFactory.BuildConstantsBuckets();
                flags = characterMapper.IsMelee ? UnitFlags.Melee : UnitFlags.Range;

                _coreLog.Debug($"[Story][Battle]: Character melee flags, id = {unitSnapshot.Id}, isMelee = {characterMapper.IsMelee}, flags = {flags}");
            }
            else
            {
                _coreLog.Error($"[Story][Battle]: Character config missing, id = {unitSnapshot.Id}, side = {battleSide}");

                throw new InvalidOperationException($"[Story][Battle]: Character config missing, id = {unitSnapshot.Id}, side = {battleSide}");
            }

            var perks = _unitLoadoutBinder.BuildPerks(unitSnapshot);
            var skills = _unitLoadoutBinder.BuildSkills(unitSnapshot, isSummon, battleSide);

            if (isSummon == false && battleSide == BattleSide.Defending)
                ApplyEnemyEnergyMax(baseBuckets, skills, unitSnapshot.Id);

            ApplyQaHealthMultiplier(baseBuckets, unitSnapshot);

            var characteristics = new CharacteristicState();
            _characteristicCalculator.ApplyToState(baseBuckets, characteristics, Array.Empty<ReplaceOverride>(), false, true);

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

            _unitLoadoutBinder.RegisterSnapshotEquippedEntities(unitState, unitSnapshot, isSummon, battleSide);
            ApplySkillLevel(unitState, unitSnapshot, isSummon, battleSide);

            var hasPassiveBonuses = GrantPassiveSkillBonuses(unitState);
            var isAttackingMain = isSummon == false && battleSide == BattleSide.Attacking;

            if (hasPassiveBonuses && isAttackingMain == false)
                _battleBonusService.Rebuild(unitState, 0, new List<BattleCommand>(), false);

            if (isAttackingMain)
            {
                _unitBonusGranter.GrantTrainingBonuses(unitState, unitSnapshot.TrainingLevel);
                _unitBonusGranter.GrantEquipmentBonuses(unitState, unitSnapshot);
                _unitBonusGranter.GrantArtifactBonuses(unitState, unitSnapshot);
                _unitBonusGranter.GrantAspectBonuses(unitState, unitSnapshot);
                _unitBonusGranter.GrantSnapshotRunBonuses(unitState, unitSnapshot);
                _battleBonusService.Rebuild(unitState, 0, new List<BattleCommand>(), false);
            }

            _unitLoadoutBinder.SeedActiveStatuses(unitState, unitSnapshot, isSummon);
            ApplySnapshotHealth(unitState, unitSnapshot);
            ApplyQa(unitState, unitSnapshot);

            _coreLog.Debug($"[Story][Battle]: Built, id = {unitState.Id}, level = {unitState.Level}, slot = {unitState.SlotIndex}, hp = {characteristics.Health}/{characteristics.MaxHealth}, damage = {characteristics.Damage}, vampyrism = {characteristics.Vampyrism}, healingBoost = {characteristics.HealingBoost}, statuses = {unitState.ActiveStatuses.Count}, perks = {perks.Count}, skills = {skills.Count}, activeBonuses = {unitState.ActiveBonuses.Count}");

            return unitState;
        }

        public void ApplyTeamSummons(IUnitState mainUnit, IReadOnlyList<int> summonIds, IUnitSnapshot mainSnapshot)
        {
            if (summonIds.Count == 0)
                return;

            for (int i = 0; i < summonIds.Count; i++)
                mainUnit.RegisterEquippedEntity("summons", summonIds[i]);

            _battleBonusService.Rebuild(mainUnit, 0, new List<BattleCommand>(), false);
            ApplySnapshotHealth(mainUnit, mainSnapshot);

            _coreLog.Debug($"[Story][Battle]: Team summons applied on main unitId = {mainUnit.Id}, summonCount = {summonIds.Count}, health = {mainUnit.CharacteristicState.Health}/{mainUnit.CharacteristicState.MaxHealth}");
        }

        private void ApplyEnemyEnergyMax(CharacteristicBuckets buckets, IReadOnlyList<ISkill> skills, int enemyId)
        {
            if (0f < buckets.EnergyMaxBase)
                return;

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];

                if (skill.RequiresEnergy == false)
                    continue;

                var requiredEnergy = skill.ResolveRequiredEnergy(0);

                if (requiredEnergy <= 0f)
                    continue;

                buckets.EnergyMaxBase = requiredEnergy;

                _coreLog.Debug($"[Story][Battle]: Enemy energy max from skill, id = {enemyId}, skillId = {skill.SkillKey}, energyMax = {requiredEnergy}");

                return;
            }
        }

        private void ApplySkillLevel(UnitState unitState, IUnitSnapshot unitSnapshot, bool isSummon, BattleSide battleSide)
        {
            if (isSummon)
            {
                ApplySummonSkillLevels(unitState, unitSnapshot);

                return;
            }

            if (battleSide != BattleSide.Attacking)
                return;

            if (_configDistributor.Characters.TryGet(unitSnapshot.Id, out var characterMapper) == false)
                return;

            var promoteLevel = unitSnapshot.Level;

            if (promoteLevel < 1)
                promoteLevel = 1;

            var skillLevel = _skillLevelResolver.Resolve(characterMapper.PromoteToSkillLevels, promoteLevel);

            unitState.SetSkillLevel(skillLevel);
            ApplyEquipmentSkillLevels(unitState, characterMapper.SkillIds);

            _coreLog.Debug($"[Story][Battle]: Skill level resolved, unitId = {unitState.Id}, promoteLevel = {promoteLevel}, skillLevel = {skillLevel}");
        }

        private void ApplyEquipmentSkillLevels(UnitState unitState, int[] characterSkillIds)
        {
            var skills = unitState.Skills;

            for (int i = 0; i < skills.Count; i++)
            {
                var skillId = skills[i].Id;

                if (Array.IndexOf(characterSkillIds, skillId) < 0)
                    unitState.SetSkillLevel(skillId, 0);
            }
        }

        private bool GrantPassiveSkillBonuses(UnitState unitState)
        {
            var skills = unitState.Skills;
            var bonusIds = new List<int>();
            var granted = false;

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];

                if (skill.IsPassiveBonus == false)
                    continue;

                bonusIds.Clear();
                skill.CollectPassiveBonusIds(unitState.GetSkillLevel(skill.Id), bonusIds);

                for (int bonusIndex = 0; bonusIndex < bonusIds.Count; bonusIndex++)
                {
                    _unitBonusGranter.GrantBuildBonus(unitState, bonusIds[bonusIndex], $"skill:{skill.Id}:{bonusIds[bonusIndex]}");
                    granted = true;

                    _coreLog.Debug($"[Story][Battle]: Passive skill bonus, unitId = {unitState.Id}, skillId = {skill.Id}, bonusId = {bonusIds[bonusIndex]}");
                }
            }

            return granted;
        }

        private void ApplySummonSkillLevels(UnitState unitState, IUnitSnapshot unitSnapshot)
        {
            if (_configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapper) == false)
                return;

            var skillIds = summonMapper.SkillIds;
            var snapshotLevels = unitSnapshot.SkillLevels;

            for (int i = 0; i < skillIds.Length; i++)
            {
                if (int.TryParse(skillIds[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var skillId) == false)
                    continue;

                var skillLevel = i < snapshotLevels.Count ? snapshotLevels[i] : 1;

                if (skillLevel < 1)
                    skillLevel = 1;

                unitState.SetSkillLevel(skillId, skillLevel - 1);

                _coreLog.Debug($"[Story][Battle]: Summon skill level, unitId = {unitState.Id}, skillId = {skillId}, level = {skillLevel}");
            }
        }

        private void ApplyQaHealthMultiplier(CharacteristicBuckets baseBuckets, IUnitSnapshot unitSnapshot)
        {
            var qa = unitSnapshot.Qa;

            if (qa == null || qa.HealthMultiplier == 1f)
                return;

            baseBuckets.HealthEntityMultiplier *= qa.HealthMultiplier;

            _coreLog.Debug($"[Story][Battle]: Qa health multiplier, unitId = {unitSnapshot.Id}, slot = {unitSnapshot.SlotIndex}, multiplier = {qa.HealthMultiplier}");
        }

        private void ApplyQa(IUnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var qa = unitSnapshot.Qa;

            if (qa == null)
                return;

            unitState.SetStrikeDamageMultiplier(qa.StrikeDamageMultiplier);

            var characteristics = unitState.CharacteristicState;

            if (0f < qa.StartHealth)
                characteristics.Health = characteristics.MaxHealth < qa.StartHealth ? characteristics.MaxHealth : qa.StartHealth;

            if (0 < qa.ActionRewardProgress)
            {
                var perks = unitState.Perks;

                for (int i = 0; i < perks.Count; i++)
                    perks[i].SeedActionProgress(qa.ActionRewardProgress);
            }

            _coreLog.Debug($"[Story][Battle]: Qa applied, unitId = {unitState.Id}, slot = {unitState.SlotIndex}, strikeDamageMultiplier = {qa.StrikeDamageMultiplier}, health = {characteristics.Health}/{characteristics.MaxHealth}, killAtStart = {qa.KillAtStart}, actionRewardProgress = {qa.ActionRewardProgress}");
        }

        private void ApplySnapshotHealth(IUnitState unitState, IUnitSnapshot unitSnapshot)
        {
            var characteristics = unitState.CharacteristicState;
            var currentHealth = unitSnapshot.CurrentHealth;

            if (currentHealth < 0f)
            {
                _coreLog.Error($"[Story][Battle]: Invalid currentHealth = {currentHealth}, unitId = {unitSnapshot.Id}, slot = {unitSnapshot.SlotIndex}");

                throw new InvalidOperationException($"[Story][Battle]: Invalid currentHealth = {currentHealth}, unitId = {unitSnapshot.Id}, slot = {unitSnapshot.SlotIndex}");
            }

            if (currentHealth == 0f)
                currentHealth = characteristics.MaxHealth;

            if (characteristics.MaxHealth < currentHealth)
                currentHealth = characteristics.MaxHealth;

            characteristics.Health = currentHealth;

            _coreLog.Debug($"[Story][Battle]: Snapshot health applied, unitId = {unitState.Id}, health = {characteristics.Health}/{characteristics.MaxHealth}");
        }
    }
}
