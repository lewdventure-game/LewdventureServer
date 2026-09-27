using Newtonsoft.Json;
using Server.GameConfigs;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class EnemyDataValidatorTests
    {
        private const string FullCharacteristics =
            "defence:[10];evasion:[0];vampyrism:[0];healing_boost:[0];crit_chance:[0];crit_multiplier:[0];"
            + "combo_1_chance:[0];combo_2_chance:[0];combo_multiplier:[0];counter_chance:[0];counter_multiplier:[0];"
            + "spell_multiplier:[0];energy:[0];energy_max:[0]";

        private readonly EnemyDataValidator _validator = new();

        [Test]
        public void Collect_FullRow_HasNoWarnings()
        {
            var warnings = Collect(FullCharacteristics);

            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void Collect_EmptyCharacteristics_Warns()
        {
            var warnings = Collect(string.Empty);

            Assert.That(warnings, Has.Some.Contains("колонка other_characteristics пустая"));
        }

        [Test]
        public void Collect_MissingKeys_AreListed()
        {
            var warnings = Collect("defence:[10];evasion:[0]");

            Assert.That(warnings, Has.Some.Contains("нет ключей"));
            Assert.That(warnings, Has.Some.Contains("crit_multiplier"));
        }

        [Test]
        public void Collect_CritChanceWithoutMultiplier_Warns()
        {
            var warnings = Collect(FullCharacteristics.Replace("crit_chance:[0]", "crit_chance:[0.2]"));

            Assert.That(warnings, Has.Some.Contains("crit_chance = 0.2"));
            Assert.That(warnings, Has.Some.Contains("не нанесёт урона"));
        }

        [Test]
        public void Collect_CounterChanceWithMultiplier_HasNoMechanicWarning()
        {
            var raw = FullCharacteristics
                .Replace("counter_chance:[0]", "counter_chance:[0.3]")
                .Replace("counter_multiplier:[0]", "counter_multiplier:[0.75]");

            var warnings = Collect(raw);

            Assert.That(warnings, Has.None.Contains("counter_chance"));
        }

        private List<string> Collect(string characteristics)
        {
            var rows = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = "10101",
                    ["health"] = "500",
                    ["damage"] = "130",
                    ["other_characteristics"] = characteristics,
                },
            };
            var domain = new ConfigSnapshotDomain("Enemies", "sheet", "A1:Z2", JsonConvert.SerializeObject(rows));
            var warnings = new List<string>();

            _validator.Collect(domain, warnings);

            return warnings;
        }
    }
}
