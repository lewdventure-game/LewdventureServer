using Server.Configs;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class ParserUtilsTests
    {
        [Test]
        public void ParseToDictionary_SheetDuration_UnwrapsBracketsAndKeepsDecimalComma()
        {
            var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ParserUtils.ParseToDictionary("damage_ratio:[1];projectile_count:[1];duration:[2,5]", output);

            Assert.That(output["duration"], Is.EqualTo("2,5"));
            Assert.That(output["damage_ratio"], Is.EqualTo("1"));
            Assert.That(output["projectile_count"], Is.EqualTo("1"));
        }

        [Test]
        public void ParseToDictionary_RewardsInsideBrackets_DoesNotSplitOnInnerSeparators()
        {
            var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ParserUtils.ParseToDictionary("hit_rewards:[status:3:2];duration:[2,5]", output);

            Assert.That(output["hit_rewards"], Is.EqualTo("status:3:2"));
            Assert.That(output["duration"], Is.EqualTo("2,5"));
        }

        [Test]
        public void GetFloat_DecimalComma_Parses()
        {
            var value = ParserUtils.GetFloat("2,5", 0f);

            Assert.That(value, Is.EqualTo(2.5f).Within(0.0001f));
        }
    }
}
