using Newtonsoft.Json;
using Server.GameConfigs;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class EnemyDataValidatorTests
    {
        private const string FullCharacteristics =
            "armor:[10];evasion:[0];vampyrism:[0];healing_boost:[0];crit_chance:[0];crit_multiplier:[0];"
            + "combo_1_chance:[0];combo_2_chance:[0];combo_multiplier:[0];combo_2_multiplier:[0];counter_chance:[0];counter_multiplier:[0];"
            + "spell_multiplier:[0];energy:[0]";

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
            var warnings = Collect("armor:[10];evasion:[0]");

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

        [Test]
        public void Collect_WithoutCombo2Multiplier_IsListedMissing()
        {
            var warnings = Collect(FullCharacteristics.Replace("combo_2_multiplier:[0];", string.Empty));

            Assert.That(warnings, Has.Some.Contains("combo_2_multiplier"));
        }

        [Test]
        public void Collect_Combo2ChanceWithoutCombo2Multiplier_Warns()
        {
            var warnings = Collect(FullCharacteristics.Replace("combo_2_chance:[0]", "combo_2_chance:[0.5]").Replace("combo_multiplier:[0]", "combo_multiplier:[1.5]"));

            Assert.That(warnings, Has.Some.Contains("combo_2_chance"));
        }

        [Test]
        public void Collect_LegacyDefenceKey_WarnsObsolete()
        {
            var warnings = Collect(FullCharacteristics.Replace("armor:[10]", "defence:[10]"));

            Assert.That(warnings, Has.Some.Contains("ключ defence"));
            Assert.That(warnings, Has.Some.Contains("armor"));
        }

        [Test]
        public void Collect_WithoutEnergyMax_HasNoWarnings()
        {
            var warnings = Collect(FullCharacteristics.Replace("energy:[0]", "energy:[10]"));

            Assert.That(warnings, Is.Empty);
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
