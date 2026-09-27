using Server.Equipments;
using Server.Infrastructure.Players;

namespace Tests.Unit.Players
{
    [TestFixture]
    public sealed class EquipmentProgressionRulesTests
    {
        private readonly EquipmentProgressionRules _rules = new();

        [Test]
        public void TryResolveLevelStep_FirstLevel_TakesFirstCost()
        {
            var mapper = new FakeEquipmentMapper("equip_lvl;equip_lvl;equip_break", "10;20;1");

            var resolved = _rules.TryResolveLevelStep(mapper, 1, out var costs, out var error);

            Assert.That(resolved, Is.True, error);
            Assert.That(costs, Has.Count.EqualTo(1));
            Assert.That(costs[0].Key, Is.EqualTo("equip_lvl"));
            Assert.That(costs[0].Amount, Is.EqualTo(10));
        }

        [Test]
        public void TryResolveLevelStep_ThirdLevel_TakesThirdCost()
        {
            var mapper = new FakeEquipmentMapper("equip_lvl;equip_lvl;equip_break", "10;20;1");

            var resolved = _rules.TryResolveLevelStep(mapper, 3, out var costs, out var error);

            Assert.That(resolved, Is.True, error);
            Assert.That(costs[0].Key, Is.EqualTo("equip_break"));
            Assert.That(costs[0].Amount, Is.EqualTo(1));
        }

        [Test]
        public void TryResolveLevelStep_PrefixedResource_IsAccepted()
        {
            var mapper = new FakeEquipmentMapper("resource:equip_lvl", "5");

            var resolved = _rules.TryResolveLevelStep(mapper, 1, out var costs, out var error);

            Assert.That(resolved, Is.True, error);
            Assert.That(costs[0].Key, Is.EqualTo("equip_lvl"));
        }

        [Test]
        public void TryResolveLevelStep_BeyondMaxLevel_Fails()
        {
            var mapper = new FakeEquipmentMapper("equip_lvl;equip_lvl", "10;20");

            var resolved = _rules.TryResolveLevelStep(mapper, 3, out _, out var error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("max level 2"));
        }

        [Test]
        public void TryResolveLevelStep_WithoutConfig_Fails()
        {
            var mapper = new FakeEquipmentMapper(string.Empty, string.Empty);

            var resolved = _rules.TryResolveLevelStep(mapper, 1, out _, out var error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("no level up costs"));
        }

        [Test]
        public void TryResolveLevelStep_UnsupportedType_Fails()
        {
            var mapper = new FakeEquipmentMapper("summon:1", "5");

            var resolved = _rules.TryResolveLevelStep(mapper, 1, out _, out var error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("unsupported cost type"));
        }

        [Test]
        public void TryResolveLevelStep_InvalidValue_Fails()
        {
            var mapper = new FakeEquipmentMapper("equip_lvl", "abc");

            var resolved = _rules.TryResolveLevelStep(mapper, 1, out _, out var error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("invalid cost"));
        }

        private sealed class FakeEquipmentMapper : IEquipmentMapper
        {
            public FakeEquipmentMapper(string levelUpTypes, string levelUpValues)
            {
                LevelUpTypes = levelUpTypes;
                LevelUpValues = levelUpValues;
            }

            public string Id => "210";

            public string Type => "weapon";

            public string Rarity => "common";

            public string LevelUpTypes { get; }

            public string LevelUpValues { get; }

            public string EquipmentBonusTypeOne => string.Empty;

            public string EquipmentBonusValuesOne => string.Empty;

            public string EquipmentBonusTypeTwo => string.Empty;

            public string EquipmentBonusValuesTwo => string.Empty;

            public string EquipmentBonusTypeThree => string.Empty;

            public string EquipmentBonusValuesThree => string.Empty;

            public string MergeGroup => string.Empty;

            public string MergeNumber => string.Empty;

            public string MergeRequirements => string.Empty;

            public string ArtName => string.Empty;

            public string IsMelee => string.Empty;

            public string SkillId => string.Empty;
        }
    }
}
