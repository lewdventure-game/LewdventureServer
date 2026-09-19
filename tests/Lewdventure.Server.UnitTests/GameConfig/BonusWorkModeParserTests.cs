using Microsoft.Extensions.Logging.Abstractions;
using Server.Bonuses;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class BonusWorkModeParserTests
    {
        [Test]
        public void TryParse_Permanent_Succeeds()
        {
            var parsed = CreateParser().TryParse("permanent", out var workMode);

            Assert.That(parsed, Is.True);
            Assert.That(workMode.Parts.Count, Is.EqualTo(1));
            Assert.That(workMode.Parts[0].Kind, Is.EqualTo(BonusWorkModeKind.Permanent));
        }

        [Test]
        public void TryParse_Empty_Fails()
        {
            var parsed = CreateParser().TryParse("", out var workMode);

            Assert.That(parsed, Is.False);
            Assert.That(workMode.Parts, Is.Empty);
        }

        [Test]
        public void TryParse_IfEquippedAndFirstTurns_KeepsBoth()
        {
            var parsed = CreateParser().TryParse("if_equipped:characters:1;first_turns:3", out var workMode);

            Assert.That(parsed, Is.True);
            Assert.That(workMode.Parts.Count, Is.EqualTo(2));
            Assert.That(workMode.Contains(BonusWorkModeKind.IfEquipped), Is.True);
            Assert.That(workMode.Contains(BonusWorkModeKind.FirstTurns), Is.True);
            Assert.That(workMode.TryGetPart(BonusWorkModeKind.IfEquipped, out var equippedPart), Is.True);
            Assert.That(equippedPart.EquippedEntityType, Is.EqualTo("characters"));
            Assert.That(equippedPart.EquippedEntityId, Is.EqualTo(1));
            Assert.That(workMode.TryGetPart(BonusWorkModeKind.FirstTurns, out var firstTurnsPart), Is.True);
            Assert.That(firstTurnsPart.Count, Is.EqualTo(3));
        }

        [Test]
        public void TryParse_PermanentAndFirstTurns_KeepsBoth()
        {
            var parsed = CreateParser().TryParse("permanent;first_turns:3", out var workMode);

            Assert.That(parsed, Is.True);
            Assert.That(workMode.Parts.Count, Is.EqualTo(2));
            Assert.That(workMode.Contains(BonusWorkModeKind.Permanent), Is.True);
            Assert.That(workMode.Contains(BonusWorkModeKind.FirstTurns), Is.True);
            Assert.That(workMode.TryGetPart(BonusWorkModeKind.FirstTurns, out var firstTurnsPart), Is.True);
            Assert.That(firstTurnsPart.Count, Is.EqualTo(3));
        }

        [Test]
        public void TryParse_Unknown_Fails()
        {
            var parsed = CreateParser().TryParse("not_a_mode", out var workMode);

            Assert.That(parsed, Is.False);
            Assert.That(workMode.Parts, Is.Empty);
        }

        [Test]
        public void TryParse_IfEquipped_Succeeds()
        {
            var parsed = CreateParser().TryParse("if_equipped:characters:1", out var workMode);

            Assert.That(parsed, Is.True);
            Assert.That(workMode.Parts.Count, Is.EqualTo(1));
            Assert.That(workMode.Parts[0].Kind, Is.EqualTo(BonusWorkModeKind.IfEquipped));
            Assert.That(workMode.Parts[0].EquippedEntityType, Is.EqualTo("characters"));
            Assert.That(workMode.Parts[0].EquippedEntityId, Is.EqualTo(1));
        }

        private BonusWorkModeParser CreateParser()
        {
            return new BonusWorkModeParser(NullLogger<BonusWorkModeParser>.Instance);
        }
    }
}
