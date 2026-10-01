using Server.Bonuses;
using Server.Entities;
using Server.Equipments;
using Server.GameConfigs;
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
                new ConfigSnapshotValidator(new ConfigDomainNames(), new EffectParametersValidator(new EffectParameterRegistry()), new EnemyDataValidator()),
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
        public void EquipmentLevelRefund_UsesDropProportion()
        {
            var equipment = CreateEquipment("5", "weapon", "rare", levelUpTypes: "resource:equip_lvl;resource:equip_lvl", levelUpValues: "10;20");

            Assert.That(_equipmentRules.TryResolveLevelRefund(equipment, 3, 0.8f, out var refunds, out var error), Is.True, error);
            Assert.That(refunds, Has.Count.EqualTo(1));
            Assert.That(refunds[0].Key, Is.EqualTo("equip_lvl"));
            Assert.That(refunds[0].Amount, Is.EqualTo(24));
        }

        [Test]
        public void MergeRequirements_ParseIdAndRarity()
        {
            var source = CreateEquipment("10", "weapon", "epic", mergeGroup: "sword", mergeNumber: "0", mergeRequirements: "equipment_id:5;equipment_rarity:rare");

            Assert.That(_mergeRules.TryResolveRequirements(source, out var requirements, out var error), Is.True, error);
            Assert.That(requirements, Has.Count.EqualTo(2));
            Assert.That(requirements[0].Kind, Is.EqualTo(EquipmentMergeRequirementKind.ConfigId));
            Assert.That(requirements[0].Value, Is.EqualTo("5"));
            Assert.That(requirements[1].Kind, Is.EqualTo(EquipmentMergeRequirementKind.Rarity));
            Assert.That(requirements[1].Value, Is.EqualTo("rare"));
        }

        [Test]
        public void MergeRequirements_Empty_IsRejected()
        {
            var source = CreateEquipment("10", "weapon", "epic", mergeGroup: "sword", mergeNumber: "0");

            Assert.That(_mergeRules.TryResolveRequirements(source, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("merge_requirements is empty"));
        }

        [Test]
        public void MergePayment_MatchesIdAndRarityOfSameType()
        {
            var source = CreateEquipment("10", "weapon", "epic", mergeGroup: "sword", mergeNumber: "0", mergeRequirements: "equipment_id:5;equipment_rarity:rare");

            Assert.That(_mergeRules.TryResolveRequirements(source, out var requirements, out _), Is.True);

            var candidates = new List<EquipmentMergeCandidate>
            {
                CreateCandidate("eq_a", CreateEquipment("7", "weapon", "rare")),
                CreateCandidate("eq_b", CreateEquipment("5", "armor", "common")),
            };

            Assert.That(_mergeRules.TryMatchPayment(source, requirements, candidates, out var error), Is.True, error);
            Assert.That(candidates[0].IsUsed, Is.True);
            Assert.That(candidates[1].IsUsed, Is.True);
        }

        [Test]
        public void MergePayment_RarityOfOtherType_IsRejected()
        {
            var source = CreateEquipment("10", "weapon", "epic", mergeGroup: "sword", mergeNumber: "0", mergeRequirements: "equipment_rarity:rare");

            Assert.That(_mergeRules.TryResolveRequirements(source, out var requirements, out _), Is.True);

            var candidates = new List<EquipmentMergeCandidate>
            {
                CreateCandidate("eq_a", CreateEquipment("7", "armor", "rare")),
            };

            Assert.That(_mergeRules.TryMatchPayment(source, requirements, candidates, out var error), Is.False);
            Assert.That(error, Does.Contain("rarity rare"));
        }

        [Test]
        public void MergePayment_WrongCount_IsRejected()
        {
            var source = CreateEquipment("10", "weapon", "epic", mergeGroup: "sword", mergeNumber: "0", mergeRequirements: "equipment_id:5;equipment_id:5");

            Assert.That(_mergeRules.TryResolveRequirements(source, out var requirements, out _), Is.True);

            var candidates = new List<EquipmentMergeCandidate>
            {
                CreateCandidate("eq_a", CreateEquipment("5", "weapon", "rare")),
            };

            Assert.That(_mergeRules.TryMatchPayment(source, requirements, candidates, out var error), Is.False);
            Assert.That(error, Does.Contain("needs 2 items"));
        }

        private EquipmentMergeCandidate CreateCandidate(string instanceId, IEquipmentMapper mapper)
        {
            var instance = new Server.Infrastructure.Mongo.Players.PlayerEquipmentDocument
            {
                InstanceId = instanceId,
                ConfigId = int.Parse(mapper.Id),
                Level = 1,
            };

            return new EquipmentMergeCandidate(instance, mapper);
        }

        private IEquipmentMapper CreateEquipment(
            string id,
            string type,
            string rarity,
            string levelUpTypes = "",
            string levelUpValues = "",
            string mergeGroup = "",
            string mergeNumber = "",
            string mergeRequirements = "")
        {
            return new EquipmentMapper
            {
                Id = id,
                Type = type,
                Rarity = rarity,
                LevelUpTypes = levelUpTypes,
                LevelUpValues = levelUpValues,
                MergeGroup = mergeGroup,
                MergeNumber = mergeNumber,
                MergeRequirements = mergeRequirements,
            };
        }
    }
}
