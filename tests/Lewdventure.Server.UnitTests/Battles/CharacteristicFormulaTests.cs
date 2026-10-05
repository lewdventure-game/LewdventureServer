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
        public void Formula1_CannotGoBelowOne()
        {
            var buckets = CreateBuckets();

            buckets.DamageBase = 57f;
            buckets.DamagePerk = -2f;

            var state = Apply(buckets);

            Assert.That(state.Damage, Is.EqualTo(1f));
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
