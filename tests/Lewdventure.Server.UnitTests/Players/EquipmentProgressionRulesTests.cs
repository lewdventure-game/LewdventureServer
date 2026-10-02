using Server.Common;
using Server.Equipments;
using Server.Infrastructure.Players;

namespace Tests.Unit.Players
{
    [TestFixture]
    public sealed class EquipmentProgressionRulesTests
    {
        private const int PromoteId = 4;

        private readonly EquipmentProgressionRules _rules = new();

        [Test]
        public void TryResolveLevelStep_SecondLevel_TakesCostOfThatLevel()
        {
            var promotes = CreatePromotes();

            var resolved = _rules.TryResolveLevelStep(CreateMapper(), 1, promotes, out var costs, out var error);

            Assert.That(resolved, Is.True, error);
            Assert.That(costs, Has.Count.EqualTo(1));
            Assert.That(costs[0].Key, Is.EqualTo("equip_lvl"));
            Assert.That(costs[0].Amount, Is.EqualTo(1));
        }

        [Test]
        public void TryResolveLevelStep_LevelWithTwoResources_TakesBoth()
        {
            var promotes = CreatePromotes();

            var resolved = _rules.TryResolveLevelStep(CreateMapper(), 4, promotes, out var costs, out var error);

            Assert.That(resolved, Is.True, error);
            Assert.That(costs, Has.Count.EqualTo(2));
            Assert.That(costs[0].Key, Is.EqualTo("equip_lvl"));
            Assert.That(costs[0].Amount, Is.EqualTo(4));
            Assert.That(costs[1].Key, Is.EqualTo("equip_break"));
            Assert.That(costs[1].Amount, Is.EqualTo(1));
        }

        [Test]
        public void TryResolveLevelStep_BeyondMaxLevel_Fails()
        {
            var promotes = CreatePromotes();

            var resolved = _rules.TryResolveLevelStep(CreateMapper(), 5, promotes, out _, out var error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("max level 5"));
        }

        [Test]
        public void TryResolveLevelStep_WithoutPromoteId_Fails()
        {
            var promotes = CreatePromotes();

            var resolved = _rules.TryResolveLevelStep(CreateMapper(0), 1, promotes, out _, out var error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("no promote_id"));
        }

        [Test]
        public void TryResolveLevelStep_UnsupportedType_Fails()
        {
            var promotes = new EquipmentPromoteMapperManagerDouble();

            promotes.Add(new FakeEquipmentPromoteMapper(PromoteId, 2, new[] { "summon" }, new[] { "1" }, new[] { 5 }));

            var resolved = _rules.TryResolveLevelStep(CreateMapper(), 1, promotes, out _, out var error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("unsupported cost type"));
        }

        [Test]
        public void TryResolveLevelStep_InvalidValue_Fails()
        {
            var promotes = new EquipmentPromoteMapperManagerDouble();

            promotes.Add(new FakeEquipmentPromoteMapper(PromoteId, 2, new[] { "resource" }, new[] { "equip_lvl" }, new[] { 0 }));

            var resolved = _rules.TryResolveLevelStep(CreateMapper(), 1, promotes, out _, out var error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("invalid cost"));
        }

        [Test]
        public void TryResolveLevelRefund_SumsEverySpentLevel()
        {
            var promotes = CreatePromotes();

            var resolved = _rules.TryResolveLevelRefund(CreateMapper(), 5, 0.8f, promotes, out var refunds, out var error);

            Assert.That(resolved, Is.True, error);
            Assert.That(refunds, Has.Count.EqualTo(2));
            Assert.That(refunds[0].Key, Is.EqualTo("equip_lvl"));
            Assert.That(refunds[0].Amount, Is.EqualTo(8));
            Assert.That(refunds[1].Key, Is.EqualTo("equip_break"));
            Assert.That(refunds[1].Amount, Is.EqualTo(1));
        }

        [Test]
        public void TryResolveLevelRefund_FirstLevel_Fails()
        {
            var promotes = CreatePromotes();

            var resolved = _rules.TryResolveLevelRefund(CreateMapper(), 1, 0.8f, promotes, out _, out var error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("already at level 1"));
        }

        private EquipmentPromoteMapperManagerDouble CreatePromotes()
        {
            var promotes = new EquipmentPromoteMapperManagerDouble();

            promotes.Add(new FakeEquipmentPromoteMapper(PromoteId, 2, new[] { "resource" }, new[] { "equip_lvl" }, new[] { 1 }));
            promotes.Add(new FakeEquipmentPromoteMapper(PromoteId, 3, new[] { "resource" }, new[] { "equip_lvl" }, new[] { 2 }));
            promotes.Add(new FakeEquipmentPromoteMapper(PromoteId, 4, new[] { "resource" }, new[] { "equip_lvl" }, new[] { 3 }));
            promotes.Add(new FakeEquipmentPromoteMapper(PromoteId, 5, new[] { "resource", "resource" }, new[] { "equip_lvl", "equip_break" }, new[] { 4, 1 }));

            return promotes;
        }

        private FakeEquipmentMapper CreateMapper()
        {
            return CreateMapper(PromoteId);
        }

        private FakeEquipmentMapper CreateMapper(int promoteId)
        {
            return new FakeEquipmentMapper(promoteId);
        }

        private sealed class EquipmentPromoteMapperManagerDouble : IEquipmentPromoteMapperManager
        {
            private readonly List<IEquipmentPromoteMapper> _collection = new();

            public IReadOnlyList<IEquipmentPromoteMapper> Collection => _collection;

            public void Add(IEquipmentPromoteMapper value)
            {
                _collection.Add(value);
            }

            public void Remove(IEquipmentPromoteMapper value)
            {
                _collection.Remove(value);
            }

            public void Clear()
            {
                _collection.Clear();
            }

            public bool TryGet(int promoteId, int level, out IEquipmentPromoteMapper mapper)
            {
                for (int i = 0; i < _collection.Count; i++)
                {
                    if (_collection[i].Id != promoteId || _collection[i].Level != level)
                        continue;

                    mapper = _collection[i];

                    return true;
                }

                mapper = null!;

                return false;
            }

            public int GetMaxLevel(int promoteId)
            {
                var maxLevel = 1;

                for (int i = 0; i < _collection.Count; i++)
                {
                    if (_collection[i].Id != promoteId || _collection[i].Level <= maxLevel)
                        continue;

                    maxLevel = _collection[i].Level;
                }

                return maxLevel;
            }
        }

        private sealed class FakeEquipmentPromoteMapper : IEquipmentPromoteMapper
        {
            public FakeEquipmentPromoteMapper(int id, int level, string[] resourceTypes, string[] resourceIds, int[] resourceValues)
            {
                Id = id;
                Level = level;
                ResourceTypes = resourceTypes;
                ResourceIds = resourceIds;
                ResourceValues = resourceValues;
            }

            public int Id { get; }

            public int Level { get; }

            public string[] ResourceTypes { get; }

            public string[] ResourceIds { get; }

            public int[] ResourceValues { get; }
        }

        private sealed class FakeEquipmentMapper : IEquipmentMapper
        {
            public FakeEquipmentMapper(int promoteId)
            {
                PromoteId = promoteId;
            }

            public int Id => 210;

            public EquipmentType Type => EquipmentType.Body;

            public RarityType Rarity => RarityType.Common;

            public int PromoteId { get; }

            public int[] BonusIds => Array.Empty<int>();

            public int[] SkillIds => Array.Empty<int>();

            public int MergeGroup => 0;

            public int MergeNumber => 0;

            public string MergeRequirements => string.Empty;

            public string ArtName => string.Empty;
        }
    }
}
