using Newtonsoft.Json;
using Server.GameConfigs;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class EffectParametersValidatorTests
    {
        private readonly EffectParametersValidator _validator = new(new EffectParameterRegistry());

        [Test]
        public void CollectPerks_MissingRequiredKey_Warns()
        {
            var warnings = CollectPerks("fire_attack", "projectile_count:[3]; damage_ratio:[0.15]");

            Assert.That(warnings, Has.Some.Contains("нет обязательных ключей proc_rounds"));
        }

        [Test]
        public void CollectPerks_FullParameters_HasNoWarnings()
        {
            var warnings = CollectPerks("fire_attack", "projectile_count:[3]; damage_ratio:[0.15]; proc_rounds:[1,2,3]");

            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void CollectPerks_UnknownKey_Warns()
        {
            var warnings = CollectPerks("resurrection", "health_ratio:[0.5]; resurrections_count:[1]; healht_ratio:[0.5]");

            Assert.That(warnings, Has.Some.Contains("healht_ratio"));
            Assert.That(warnings, Has.Some.Contains("проверьте опечатку"));
        }

        [Test]
        public void CollectPerks_UnsupportedType_Warns()
        {
            var warnings = CollectPerks("ice_attack", "damage_ratio:[0.1]");

            Assert.That(warnings, Has.Some.Contains("сервером не поддерживается"));
        }

        [Test]
        public void CollectStatuses_MissingKeys_Warn()
        {
            var rows = new[]
            {
                new Dictionary<string, object> { ["id"] = "1", ["status_type"] = "burning", ["parameters"] = "damage_ratio:[0.1]" },
            };
            var warnings = new List<string>();

            _validator.CollectStatuses(CreateDomain("Statuses", rows), warnings);

            Assert.That(warnings, Has.Some.Contains("damage_length"));
            Assert.That(warnings, Has.Some.Contains("max_stacks"));
        }

        [Test]
        public void CollectSkills_MissingDamageRatio_Warns()
        {
            var warnings = CollectSkills("fireball", "projectile_count:[1]; duration:[2,5]");

            Assert.That(warnings, Has.Some.Contains("нет обязательных ключей damage_ratio"));
        }

        [Test]
        public void CollectSkills_UnknownType_Warns()
        {
            var warnings = CollectSkills("summon_4_skill_1", "damage_ratio:[1]");

            Assert.That(warnings, Has.Some.Contains("сервером не поддерживается"));
        }

        [Test]
        public void CollectSkills_UnknownKey_Warns()
        {
            var warnings = CollectSkills("summon_3_skill_1", "damage_ratio:[1]; life_stael_ratio:[0.5]");

            Assert.That(warnings, Has.Some.Contains("life_stael_ratio"));
        }

        [Test]
        public void CollectSkills_FullParameters_HasNoWarnings()
        {
            var warnings = CollectSkills("summon_2_skill_1", "damage_ratio:[0.35]; heal_from_max_ratio:[0.12]; ally_rewards:[bonus:2:1]; duration:[2,5]");

            Assert.That(warnings, Is.Empty);
        }

        private List<string> CollectSkills(string skillType, string parameters)
        {
            var rows = new[]
            {
                new Dictionary<string, object> { ["id"] = "1", ["type"] = skillType, ["parameters"] = parameters },
            };
            var warnings = new List<string>();

            _validator.CollectSkills(CreateDomain("Skills", rows), warnings);

            return warnings;
        }

        private List<string> CollectPerks(string perkType, string parameters)
        {
            var rows = new[]
            {
                new Dictionary<string, object> { ["id"] = "2", ["perk_type"] = perkType, ["perk_parameters"] = parameters },
            };
            var warnings = new List<string>();

            _validator.CollectPerks(CreateDomain("Perks", rows), warnings);

            return warnings;
        }

        private ConfigSnapshotDomain CreateDomain(string domain, IReadOnlyList<Dictionary<string, object>> rows)
        {
            return new ConfigSnapshotDomain(domain, "sheet", "A1:Z2", JsonConvert.SerializeObject(rows));
        }
    }
}
