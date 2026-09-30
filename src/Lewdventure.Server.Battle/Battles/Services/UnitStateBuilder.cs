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
        private readonly IUnitLoadoutBinder _unitLoadoutBinder;

        public UnitStateBuilder(
            ICoreLog coreLog,
            IBattleBonusService battleBonusService,
            ICharacteristicCalculator characteristicCalculator,
            IConfigDistributor configDistributor,
            IUnitBonusGranter unitBonusGranter,
            IUnitBucketsFactory unitBucketsFactory,
            IUnitLoadoutBinder unitLoadoutBinder)
        {
            _coreLog = coreLog;
            _battleBonusService = battleBonusService;
            _characteristicCalculator = characteristicCalculator;
            _configDistributor = configDistributor;
            _unitBonusGranter = unitBonusGranter;
            _unitBucketsFactory = unitBucketsFactory;
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

            var characteristics = new CharacteristicState();
            _characteristicCalculator.ApplyToState(baseBuckets, characteristics, Array.Empty<ReplaceOverride>(), false, true);

            var perks = _unitLoadoutBinder.BuildPerks(unitSnapshot);
            var skills = _unitLoadoutBinder.BuildSkills(unitSnapshot, isSummon);
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

            if (isSummon == false && battleSide == BattleSide.Attacking)
            {
                _unitBonusGranter.GrantTrainingBonuses(unitState, unitSnapshot.TrainingLevel);
                _unitBonusGranter.GrantEquipmentBonuses(unitState, unitSnapshot);
                _unitBonusGranter.GrantArtifactBonuses(unitState, unitSnapshot);
                _unitBonusGranter.GrantAspectBonuses(unitState, unitSnapshot);
                _unitBonusGranter.GrantSnapshotRunBonuses(unitState, unitSnapshot);
                _battleBonusService.Rebuild(unitState, 0, new List<BattleCommand>(), false);
            }

            if (isSummon && _configDistributor.Summons.TryGet(unitSnapshot.Id, out var summonMapperForBreakout))
                _unitBucketsFactory.ApplyBreakoutHook(unitSnapshot, summonMapperForBreakout);

            _unitLoadoutBinder.SeedActiveStatuses(unitState, unitSnapshot, isSummon);
            _unitLoadoutBinder.ApplyEquippedPerks(unitState);
            ApplySnapshotHealth(unitState, unitSnapshot);

            _coreLog.Debug($"[Story][Battle]: Built, id = {unitState.Id}, level = {unitState.Level}, slot = {unitState.SlotIndex}, hp = {characteristics.Health}/{characteristics.MaxHealth}, damage = {characteristics.Damage}, vampyrism = {characteristics.Vampyrism}, healingBoost = {characteristics.HealingBoost}, statuses = {unitState.ActiveStatuses.Count}, perks = {perks.Count}, skills = {skills.Count}, activeBonuses = {unitState.ActiveBonuses.Count}");

            return unitState;
        }

        public void GrantSummonAccountBonuses(IUnitState mainUnit, IUnitSnapshot summonSnapshot)
        {
            _unitBonusGranter.GrantSummonAccountBonuses(mainUnit, summonSnapshot);
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
