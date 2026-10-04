using Server.Runs;

namespace Tests.Unit.Players
{
    [TestFixture]
    public sealed class RunRandomFactoryTests
    {
        private readonly RunRandomFactory _factory = new();

        [Test]
        public void Create_SameSeedAndIndex_GivesSameSequence()
        {
            var first = _factory.Create(12345, 3);
            var second = _factory.Create(12345, 3);

            Assert.That(second.NextULong(), Is.EqualTo(first.NextULong()));
        }

        [Test]
        public void Create_DifferentIndex_GivesDifferentSequence()
        {
            var first = _factory.Create(12345, 3).NextULong();
            var second = _factory.Create(12345, 4).NextULong();

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void PickWeighted_ZeroWeight_IsNeverPicked()
        {
            var options = new[] { 10, 20 };
            var weights = new[] { 0, 5 };

            for (int i = 0; i < 20; i++)
                Assert.That(_factory.PickWeighted(_factory.Create(7, i), options, weights), Is.EqualTo(20));
        }

        [Test]
        public void PickWeighted_SingleOption_IsPicked()
        {
            Assert.That(_factory.PickWeighted(_factory.Create(1, 0), new[] { 42 }, new[] { 1 }), Is.EqualTo(42));
        }

        [Test]
        public void PickDistinct_ReturnsRequestedCountWithoutRepeats()
        {
            var options = new[] { 1, 2, 3, 4, 5 };
            var weights = new[] { 5, 4, 3, 2, 1 };

            var picked = _factory.PickDistinct(_factory.Create(99, 0), options, weights, 3);

            Assert.That(picked, Has.Count.EqualTo(3));
            Assert.That(picked, Is.Unique);
        }

        [Test]
        public void PickDistinct_MoreThanAvailable_ReturnsAll()
        {
            var options = new[] { 1, 2 };

            var picked = _factory.PickDistinct(_factory.Create(5, 0), options, new[] { 1, 1 }, 5);

            Assert.That(picked, Has.Count.EqualTo(2));
        }

        [Test]
        public void CreateBattleSeed_IsStablePerStage()
        {
            Assert.That(_factory.CreateBattleSeed(77, 2), Is.EqualTo(_factory.CreateBattleSeed(77, 2)));
            Assert.That(_factory.CreateBattleSeed(77, 2), Is.Not.EqualTo(_factory.CreateBattleSeed(77, 3)));
        }

        [Test]
        public void CreateBattleSeed_WithRunKey_IsStableAndNotDerivableFromRunSeed()
        {
            var firstKey = _factory.CreateBattleSeedKey();
            var secondKey = _factory.CreateBattleSeedKey();

            Assert.That(_factory.CreateBattleSeed(77, 2, firstKey), Is.EqualTo(_factory.CreateBattleSeed(77, 2, firstKey)));
            Assert.That(_factory.CreateBattleSeed(77, 2, firstKey), Is.Not.EqualTo(_factory.CreateBattleSeed(77, 3, firstKey)));
            Assert.That(_factory.CreateBattleSeed(77, 2, firstKey), Is.Not.EqualTo(_factory.CreateBattleSeed(77, 2, secondKey)));
            Assert.That(_factory.CreateBattleSeed(77, 2, firstKey), Is.Not.EqualTo(_factory.CreateBattleSeed(77, 2)));
            Assert.That(_factory.CreateBattleSeed(77, 2, string.Empty), Is.EqualTo(_factory.CreateBattleSeed(77, 2)));
        }
    }
}
