using Server.Battles;
using Server.Logging;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class CharacteristicFormulaTests
    {
        private readonly CharacteristicCalculator _calculator = new(new SilentCoreLog());

        [Test]
        public void Formula3_SumsStartAndLocalWithoutExtraOne()
        {
            var buckets = CreateBuckets();

            buckets.CriticalChanceBase = 0.05f;
            buckets.CriticalChanceLocal = 0.1f;
            buckets.CriticalChancePerk = 0.5f;

            var state = Apply(buckets);

            Assert.That(state.CriticalChance, Is.EqualTo((0.05f + 0.1f) * 1.5f).Within(0.0001f));
        }

        [Test]
        public void Formula3_EnergyCannotGoBelowZero()
        {
            var buckets = CreateBuckets();

            buckets.EnergyBase = 30f;
            buckets.EnergyLocal = -50f;

            var state = Apply(buckets);

            Assert.That(state.EnergyGain, Is.EqualTo(0f));
        }

        [Test]
        public void Formula2_ArmorCanGoBelowZero()
        {
            var buckets = CreateBuckets();

            buckets.ArmorBase = 7f;
            buckets.ArmorLocal = -20f;

            var state = Apply(buckets);

            Assert.That(state.Armor, Is.EqualTo(-13f));
            Assert.That(state.Defence, Is.LessThan(0f));
        }

        [Test]
        public void Formula2_ArmorRoundsDown()
        {
            var buckets = CreateBuckets();

            buckets.ArmorBase = 7f;
            buckets.ArmorPerk = -0.2f;

            var positive = Apply(buckets);

            buckets.ArmorBase = -7f;

            var negative = Apply(buckets);

            Assert.That(positive.Armor, Is.EqualTo(5f));
            Assert.That(negative.Armor, Is.EqualTo(-6f));
        }

        [Test]
        public void ComboMultipliers_AreSeparatePerComboAttack()
        {
            var buckets = CreateBuckets();

            buckets.ComboMultiplierBase = 1.5f;
            buckets.Combo2MultiplierBase = 2f;
            buckets.Combo2MultiplierPerk = 0.5f;

            var state = Apply(buckets);

            Assert.That(state.ComboMultiplier, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(state.Combo2Multiplier, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void EntityMultiplier_AppliesToFinalValueAfterLocalBonus()
        {
            var buckets = CreateBuckets();

            buckets.DamageBase = 50f;
            buckets.DamageLocal = -10f;
            buckets.DamageEntityMultiplier = 1.5f;
            buckets.HealthBase = 401f;
            buckets.HealthEntityMultiplier = 1.5f;

            var state = Apply(buckets);

            Assert.That(state.Damage, Is.EqualTo(60f));
            Assert.That(state.MaxHealth, Is.EqualTo(602f));
        }

        [Test]
        public void Formula1_CannotGoBelowOne()
        {
            var buckets = CreateBuckets();

            buckets.DamageBase = 57f;
            buckets.DamagePerk = -2f;

            var state = Apply(buckets);

            Assert.That(state.Damage, Is.EqualTo(1f));
        }

        [Test]
        public void Formula7_KeepsFractionalHealingBoost()
        {
            var buckets = CreateBuckets();

            buckets.HealingBoostPerk = 0.3f;

            var state = Apply(buckets);

            Assert.That(state.HealingBoost, Is.EqualTo(1.3f).Within(0.0001f));
        }

        private CharacteristicBuckets CreateBuckets()
        {
            return new CharacteristicBuckets
            {
                HealthBase = 600f,
                DamageBase = 57f,
                AttackMultiplierBase = 1f,
                DefenceCoefficient = 0.003f,
            };
        }

        private ICharacteristicState Apply(CharacteristicBuckets buckets)
        {
            var state = new CharacteristicState();

            _calculator.ApplyToState(buckets, state, Array.Empty<ReplaceOverride>(), false, true);

            return state;
        }
    }
}
