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
        public void SummonLevelRefund_SumsSpentExpAndAppliesCoefficient()
        {
            Assert.That(_configDistributor.Summons.TryGet(1, out var summon), Is.True);
            Assert.That(_summonRules.TryResolveLevelRefund(summon!, 5, 0.85f, _configDistributor, out var refunds, out var error), Is.True, error);
            Assert.That(refunds, Has.Count.EqualTo(1));
            Assert.That(refunds[0].Key, Is.EqualTo("summon_exp"));
            Assert.That(refunds[0].Amount, Is.EqualTo(44));
        }

        [Test]
        public void SummonLevelRefund_FirstLevel_IsRejected()
        {
            Assert.That(_configDistributor.Summons.TryGet(1, out var summon), Is.True);
            Assert.That(_summonRules.TryResolveLevelRefund(summon!, 1, 0.85f, _configDistributor, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("already at level 1"));
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
