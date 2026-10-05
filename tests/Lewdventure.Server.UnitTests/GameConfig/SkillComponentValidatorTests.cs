using Server.GameConfigs;
using Server.Skills;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class SkillComponentValidatorTests
    {
        private const string Triggers = "energy_needed:[energy:{30}]";
        private const string Actions = "damage:[damage:{1.1:1.2},target:{1:1},is_spell_amp:{0:0},timing:{0.5:0.5}]";

        private readonly SkillComponentRegistry _registry = new();

        [Test]
        public void CanonicalRow_HasNoComplaints()
        {
            Validate(Triggers, Actions, out var errors, out var warnings);

            Assert.That(errors, Is.Empty);
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void PassiveBonusRow_HasNoComplaints()
        {
            Validate("as_bonus_logic:[]", "set_bonus:[bonus_id:{14:15},target:{0:0},timing:{0:0}]", out var errors, out var warnings);

            Assert.That(errors, Is.Empty);
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void PassiveBonusRow_WithDamage_IsWarned()
        {
            Validate("as_bonus_logic:[]", Actions, out var errors, out var warnings);

            Assert.That(errors, Is.Empty);
            Assert.That(warnings, Has.Some.Contains("as_bonus_logic"));
        }

        [Test]
        public void PassiveBonusRow_WithoutCommas_IsReported()
        {
            Validate("as_bonus_logic:[]", "set_bonus:[\nbonus_id:{14:14:15}\ntarget:{0:0:0}\ntiming:{0:0:0}]", out var errors, out var warnings);

            Assert.That(errors.Count + warnings.Count, Is.GreaterThan(0));
        }

        [Test]
        public void LegacyRow_IsNotTouched()
        {
            var domain = new ConfigSnapshotDomain("Skills", "sheet", "A1:E2", "[{\"id\":\"1\",\"type\":\"fireball\",\"parameters\":\"damage_ratio:[1]\"}]");
            var errors = new List<string>();
            var warnings = new List<string>();

            new SkillComponentValidator(_registry).Validate(domain, errors, warnings);

            Assert.That(errors, Is.Empty);
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void UnknownTrigger_IsError()
        {
            Validate("on_approach:[radius:{3}]", Actions, out var errors, out _);

            Assert.That(errors, Has.Some.Contains("неизвестное условие активации on_approach"));
        }

        [Test]
        public void UnknownAction_IsError()
        {
            Validate(Triggers, "dash_forward:[distance:{3},timing:{0.1}]", out var errors, out _);

            Assert.That(errors, Has.Some.Contains("неизвестное целевое действие dash_forward"));
        }

        [Test]
        public void CommaInsideArguments_IsWarning()
        {
            Validate(Triggers, "damage:[damage:{1.1:1.2},target:{1:1},is_spell_amp:{0:0},timing:{0.5,0.5}]", out var errors, out var warnings);

            Assert.That(errors, Is.Empty);
            Assert.That(warnings, Has.Some.Contains("разделитель третьего порядка - двоеточие"));
        }

        [Test]
        public void UnknownParameter_IsWarning()
        {
            Validate(Triggers, "damage:[damage:{1},target:{1},timing:{0.1},radius:{3}]", out var errors, out var warnings);

            Assert.That(errors, Is.Empty);
            Assert.That(warnings, Has.Some.Contains("параметр radius у damage сервером не используется"));
        }

        [Test]
        public void ArgumentsWithoutBraces_IsWarning()
        {
            Validate(Triggers, "damage:[damage:1.1,target:{1},timing:{0.1}]", out _, out var warnings);

            Assert.That(warnings, Has.Some.Contains("не обёрнуты в фигурные скобки"));
        }

        [Test]
        public void DashBetweenArguments_IsError()
        {
            Validate(Triggers, "damage:[damage:{1.1-1.2-1.3},target:{1:1:1},timing:{0.1:0.1:0.1}]", out var errors, out _);

            Assert.That(errors, Has.Some.Contains("не число"));
        }

        [Test]
        public void TextArgument_IsError()
        {
            Validate("energy_needed:[energy:{много}]", Actions, out var errors, out _);

            Assert.That(errors, Has.Some.Contains("не число"));
        }

        [Test]
        public void TriggersWithoutActions_IsError()
        {
            Validate(Triggers, string.Empty, out var errors, out _);

            Assert.That(errors, Has.Some.Contains("пустые actions"));
        }

        [Test]
        public void ActionsWithoutTriggers_IsError()
        {
            Validate(string.Empty, Actions, out var errors, out _);

            Assert.That(errors, Has.Some.Contains("пустые triggers"));
        }

        private void Validate(string triggers, string actions, out List<string> errors, out List<string> warnings)
        {
            var rows = "[{\"id\":\"1\",\"triggers\":\"" + Escape(triggers) + "\",\"actions\":\"" + Escape(actions) + "\"}]";
            var domain = new ConfigSnapshotDomain("Skills", "sheet", "A1:C2", rows);

            errors = new List<string>();
            warnings = new List<string>();

            new SkillComponentValidator(_registry).Validate(domain, errors, warnings);
        }

        private string Escape(string value)
        {
            return value.Replace("\"", "\\\"");
        }
    }
}
