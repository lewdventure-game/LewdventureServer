using Server.Battles;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class FireballSkillTests
    {
        [Test]
        public void Constructor_SheetDuration_ParsesTwoAndAHalfSeconds()
        {
            var mapper = new SkillMapper(
                1,
                "fireball",
                SkillType.Fireball,
                "damage_ratio:[1];projectile_count:[1];duration:[2,5]");

            var skill = new FireballSkill(mapper);

            Assert.That(skill.CastDurationSeconds, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(skill.DamageRatio, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(skill.ProjectileCount, Is.EqualTo(1));
        }
    }
}
