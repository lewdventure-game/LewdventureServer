using Server.Bonuses;
using Server.Entities;
using Server.Common;
using Server.Equipments;
using Server.GameConfigs;
using Server.Skills;
using Server.Infrastructure.Players;
using Server.Logging;
using Server.Services;
using Tests.Unit.Api;

namespace Tests.Unit.Players
{
    [TestFixture]
    public sealed class ProgressionRulesTests
    {
        private readonly EquipmentMergeRules _mergeRules = new();
        private readonly EquipmentProgressionRules _equipmentRules = new();
        private readonly SummonProgressionRules _summonRules = new();

        private IConfigDistributor _configDistributor = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var hasher = new ConfigSnapshotHasher();
            var source = new FileConfigSnapshotSource(new ConfigSnapshotSerializer(hasher));
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);
            var builder = new GameConfigSetBuilder(
                new BonusWorkModeParser(new SilentCoreLog()),
                new ConfigRowsParser(new ConfigRowLocator(new ConfigRangeReader())),
                new ConfigSnapshotValidator(new ConfigDomainNames(), new EffectParametersValidator(new EffectParameterRegistry()), new EnemyDataValidator(), new SkillComponentValidator(new SkillComponentRegistry())),
                new SilentCoreLog());
            var result = builder.Build(snapshot, "test");

            _configDistributor = result.ConfigSet!.Distributor;
        }

        [Test]
        public void Summons_WithEmptySkillColumns_LoadWithoutSkills()
        {
            Assert.That(_configDistributor.Summons.TryGet(1, out var summon), Is.True);
            Assert.That(summon!.SkillIds, Is.Empty);
            Assert.That(summon.MasteryForSkills, Is.Empty);
            Assert.That(summon.SkillUpgradeIds, Is.Empty);
        }

        [Test]
        public void SummonLevelStep_UsesRowOfTargetLevel()
        {
            Assert.That(_configDistributor.Summons.TryGet(1, out var summon), Is.True);
            Assert.That(_summonRules.TryResolveLevelStep(summon!, 1, _configDistributor, out var costs, out var error), Is.True, error);
            Assert.That(costs, Has.Count.EqualTo(1));
            Assert.That(costs[0].Key, Is.EqualTo("summon_lvl"));
            Assert.That(costs[0].Amount, Is.EqualTo(2));
        }

        [Test]
        public void SummonLevelStep_BreakLevel_NeedsBothResources()
        {
            Assert.That(_configDistributor.Summons.TryGet(1, out var summon), Is.True);
            Assert.That(_summonRules.TryResolveLevelStep(summon!, 25, _configDistributor, out var costs, out var error), Is.True, error);
            Assert.That(costs, Has.Count.EqualTo(2));
            Assert.That(costs[0].Key, Is.EqualTo("summon_lvl"));
            Assert.That(costs[0].Amount, Is.EqualTo(50));
            Assert.That(costs[1].Key, Is.EqualTo("summon_break"));
            Assert.That(costs[1].Amount, Is.EqualTo(20));
        }

        [Test]
        public void SummonLevelStep_AfterLastRow_IsRejected()
        {
            Assert.That(_configDistributor.Summons.TryGet(1, out var summon), Is.True);
            Assert.That(_summonRules.TryResolveLevelStep(summon!, 100, _configDistributor, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("no level 101"));
        }

        [Test]
        public void SummonLevelRefund_SumsSpentExpAndAppliesCoefficient()
        {
            Assert.That(_configDistributor.Summons.TryGet(1, out var summon), Is.True);
            Assert.That(_summonRules.TryResolveLevelRefund(summon!, 5, 0.85f, _configDistributor, out var refunds, out var error), Is.True, error);
            Assert.That(refunds, Has.Count.EqualTo(1));
            Assert.That(refunds[0].Key, Is.EqualTo("summon_lvl"));
            Assert.That(refunds[0].Amount, Is.EqualTo(17));
        }

        [Test]
        public void SummonLevelRefund_ReturnsEveryResourceByCoefficient()
        {
            Assert.That(_configDistributor.Summons.TryGet(1, out var summon), Is.True);
            Assert.That(_summonRules.TryResolveLevelRefund(summon!, 26, 0.85f, _configDistributor, out var refunds, out var error), Is.True, error);
            Assert.That(refunds, Has.Count.EqualTo(2));
            Assert.That(refunds[0].Key, Is.EqualTo("summon_lvl"));
            Assert.That(refunds[0].Amount, Is.EqualTo(553));
            Assert.That(refunds[1].Key, Is.EqualTo("summon_break"));
            Assert.That(refunds[1].Amount, Is.EqualTo(17));
        }

        [Test]
        public void SummonMasteryStep_FromFirstLevel_CostsCopiesOfSecond()
        {
            Assert.That(_configDistributor.Summons.TryGet(1, out var summon), Is.True);
            Assert.That(_summonRules.TryResolveMasteryStep(summon!, 1, _configDistributor, out var copies, out var error), Is.True, error);
            Assert.That(copies, Is.EqualTo(1));
        }

        [Test]
        public void SummonSkillStep_FirstUpgrade_CostsSkillPoints()
        {
            var summon = CreateSkilledSummon();

            Assert.That(_summonRules.TryResolveSkillIndex(summon, 7, out var skillIndex, out var indexError), Is.True, indexError);
            Assert.That(_summonRules.TryResolveSkillStep(summon, skillIndex, 1, 1, 1, _configDistributor, out var costs, out var error), Is.True, error);
            Assert.That(costs, Has.Count.EqualTo(1));
            Assert.That(costs[0].Key, Is.EqualTo("summon_skill_points"));
            Assert.That(costs[0].Amount, Is.EqualTo(10));
        }

        [Test]
        public void SummonSkillStep_BelowLevelToUnlock_IsRejected()
        {
            var summon = CreateSkilledSummon();

            Assert.That(_summonRules.TryResolveSkillStep(summon, 0, 4, 24, 1, _configDistributor, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("needs summon level 25"));
            Assert.That(_summonRules.TryResolveSkillStep(summon, 0, 4, 25, 1, _configDistributor, out _, out var unlockedError), Is.True, unlockedError);
        }

        [Test]
        public void SummonSkillStep_LockedByMastery_IsRejected()
        {
            var summon = CreateSkilledSummon();

            Assert.That(_summonRules.TryResolveSkillStep(summon, 1, 1, 50, 4, _configDistributor, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("needs mastery 5"));
        }

        [Test]
        public void SummonSkillStep_AfterLastRow_IsRejected()
        {
            var summon = CreateSkilledSummon();

            Assert.That(_summonRules.TryResolveSkillStep(summon, 0, 10, 100, 20, _configDistributor, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("no level 11"));
        }

        [Test]
        public void SummonLevelRefund_FirstLevel_IsRejected()
        {
            Assert.That(_configDistributor.Summons.TryGet(1, out var summon), Is.True);
            Assert.That(_summonRules.TryResolveLevelRefund(summon!, 1, 0.85f, _configDistributor, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("already at level 1"));
        }

        private SummonMapper CreateSkilledSummon()
        {
            return new SummonMapper
            {
                Id = 99,
                Rarity = RarityType.Rare,
                DamageOnLevels = new[] { 10f },
                SkillIds = new[] { "7", "8" },
                MasteryForSkills = new[] { 1, 5 },
                SkillUpgradeIds = new[] { 1, 1 },
                MasteryId = 1,
                LevelPatternId = 1,
            };
        }

        [Test]
        public void MergeRequirements_ParseIdAndRarity()
        {
            var source = CreateEquipment(10, EquipmentType.Body, RarityType.Epic, mergeGroup: 1, mergeNumber: 0, mergeRequirements: "equipment_id:5:1;equipment_rarity:rare:1");

            Assert.That(_mergeRules.TryResolveRequirements(source, out var requirements, out var error), Is.True, error);
            Assert.That(requirements, Has.Count.EqualTo(2));
            Assert.That(requirements[0].Kind, Is.EqualTo(EquipmentMergeRequirementKind.ConfigId));
            Assert.That(requirements[0].ConfigId, Is.EqualTo(5));
            Assert.That(requirements[0].Count, Is.EqualTo(1));
            Assert.That(requirements[1].Kind, Is.EqualTo(EquipmentMergeRequirementKind.Rarity));
            Assert.That(requirements[1].Rarity, Is.EqualTo(RarityType.Rare));
            Assert.That(requirements[1].Count, Is.EqualTo(1));
        }

        [Test]
        public void MergeRequirements_Empty_IsRejected()
        {
            var source = CreateEquipment(10, EquipmentType.Body, RarityType.Epic, mergeGroup: 1, mergeNumber: 0);

            Assert.That(_mergeRules.TryResolveRequirements(source, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("merge_requirements is empty"));
        }

        [Test]
        public void MergePayment_MatchesIdAndRarityOfSameType()
        {
            var source = CreateEquipment(10, EquipmentType.Body, RarityType.Epic, mergeGroup: 1, mergeNumber: 0, mergeRequirements: "equipment_id:5:1;equipment_rarity:rare:1");

            Assert.That(_mergeRules.TryResolveRequirements(source, out var requirements, out _), Is.True);

            var candidates = new List<EquipmentMergeCandidate>
            {
                CreateCandidate("eq_a", CreateEquipment(7, EquipmentType.Body, RarityType.Rare)),
                CreateCandidate("eq_b", CreateEquipment(5, EquipmentType.Pants, RarityType.Common)),
            };

            Assert.That(_mergeRules.TryMatchPayment(source, requirements, candidates, out var error), Is.True, error);
            Assert.That(candidates[0].IsUsed, Is.True);
            Assert.That(candidates[1].IsUsed, Is.True);
        }

        [Test]
        public void MergePayment_RarityOfOtherType_IsRejected()
        {
            var source = CreateEquipment(10, EquipmentType.Body, RarityType.Epic, mergeGroup: 1, mergeNumber: 0, mergeRequirements: "equipment_rarity:rare");

            Assert.That(_mergeRules.TryResolveRequirements(source, out var requirements, out _), Is.True);

            var candidates = new List<EquipmentMergeCandidate>
            {
                CreateCandidate("eq_a", CreateEquipment(7, EquipmentType.Pants, RarityType.Rare)),
            };

            Assert.That(_mergeRules.TryMatchPayment(source, requirements, candidates, out var error), Is.False);
            Assert.That(error, Does.Contain("rarity Rare"));
        }

        [Test]
        public void MergePayment_WrongCount_IsRejected()
        {
            var source = CreateEquipment(10, EquipmentType.Body, RarityType.Epic, mergeGroup: 1, mergeNumber: 0, mergeRequirements: "equipment_id:5:2");

            Assert.That(_mergeRules.TryResolveRequirements(source, out var requirements, out _), Is.True);

            var candidates = new List<EquipmentMergeCandidate>
            {
                CreateCandidate("eq_a", CreateEquipment(5, EquipmentType.Body, RarityType.Rare)),
            };

            Assert.That(_mergeRules.TryMatchPayment(source, requirements, candidates, out var error), Is.False);
            Assert.That(error, Does.Contain("needs 2 items"));
        }

        private EquipmentMergeCandidate CreateCandidate(string instanceId, IEquipmentMapper mapper)
        {
            var instance = new Server.Infrastructure.Mongo.Players.PlayerEquipmentDocument
            {
                InstanceId = instanceId,
                ConfigId = mapper.Id,
                Level = 1,
            };

            return new EquipmentMergeCandidate(instance, mapper);
        }

        private IEquipmentMapper CreateEquipment(
            int id,
            EquipmentType type,
            RarityType rarity,
            int mergeGroup = 0,
            int mergeNumber = 0,
            string mergeRequirements = "")
        {
            return new EquipmentMapper
            {
                Id = id,
                Type = type,
                Rarity = rarity,
                MergeGroup = mergeGroup,
                MergeNumber = mergeNumber,
                MergeRequirements = mergeRequirements,
            };
        }
    }
}
