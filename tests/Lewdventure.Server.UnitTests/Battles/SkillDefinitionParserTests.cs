using Server.Battles;
using Server.Logging;
using Server.Skills;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class SkillDefinitionParserTests
    {
        private const string EnergyTriggers = "energy_needed:[\nenergy:{30}]";

        private const string EnergyActions = "damage:[\ndamage:{1.1:1.2:1.3:1.4:1.5:1.6:1.7:1.8},\ntarget:{3:3:3:3:3:3:3:3},\nis_spell_amp:{0,0,0,0,0,1,1,1},\ntiming:{0.5,0.5,0.5,0.5,0.5,0.5,0.5,0.5}];\n\nset_bonus:[\nbonus_id:{1,1,1,1,1,1,1,1},\ntarget:{0,0,0,0,0,0,0,0},\ntiming:{0.2,0.2,0.2,0.2,0.2,0.2,0.2,0.2}]";

        private readonly SkillDefinitionParser _parser = new(new SilentCoreLog());
        private readonly SkillArgumentReader _reader = new(new SilentCoreLog());

        [Test]
        public void TryParse_GameDesignerRow_ReadsTriggerAndActions()
        {
            Assert.That(_parser.TryParse(CreateMapper(EnergyTriggers, EnergyActions), out var definition), Is.True);
            Assert.That(definition.Triggers, Has.Count.EqualTo(1));
            Assert.That(definition.Triggers[0].TriggerType, Is.EqualTo(SkillTriggerType.EnergyNeeded));
            Assert.That(definition.Actions, Has.Count.EqualTo(2));
            Assert.That(definition.Actions[0].ActionType, Is.EqualTo(SkillActionType.Damage));
            Assert.That(definition.Actions[1].ActionType, Is.EqualTo(SkillActionType.SetBonus));
        }

        [Test]
        public void TryParse_EnergyTrigger_ReadsEnergyAmount()
        {
            Assert.That(_parser.TryParse(CreateMapper(EnergyTriggers, EnergyActions), out var definition), Is.True);
            Assert.That(definition.TryGetTrigger(SkillTriggerType.EnergyNeeded, out var trigger), Is.True);
            Assert.That(_reader.TryReadInt(trigger.Component, "energy", 0, out var energy), Is.True);
            Assert.That(energy, Is.EqualTo(30));
        }

        [Test]
        public void TryParse_DamageAction_ReadsArgumentsPerSkillLevel()
        {
            Assert.That(_parser.TryParse(CreateMapper(EnergyTriggers, EnergyActions), out var definition), Is.True);

            var damage = definition.Actions[0].Component;

            Assert.That(_reader.TryReadFloat(damage, "damage", 0, out var first), Is.True);
            Assert.That(first, Is.EqualTo(1.1f).Within(0.0001f));

            Assert.That(_reader.TryReadFloat(damage, "damage", 7, out var last), Is.True);
            Assert.That(last, Is.EqualTo(1.8f).Within(0.0001f));

            Assert.That(_reader.TryReadInt(damage, "target", 3, out var target), Is.True);
            Assert.That(target, Is.EqualTo(3));

            Assert.That(_reader.ReadFlag(damage, "is_spell_amp", 4), Is.False);
            Assert.That(_reader.ReadFlag(damage, "is_spell_amp", 5), Is.True);

            Assert.That(_reader.TryReadFloat(damage, "timing", 0, out var timing), Is.True);
            Assert.That(timing, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void ArgumentReader_LevelOverArgumentCount_UsesLastArgument()
        {
            Assert.That(_parser.TryParse(CreateMapper(EnergyTriggers, EnergyActions), out var definition), Is.True);

            var damage = definition.Actions[0].Component;

            Assert.That(_reader.TryReadFloat(damage, "damage", 42, out var value), Is.True);
            Assert.That(value, Is.EqualTo(1.8f).Within(0.0001f));
        }

        [Test]
        public void TryParse_SetBonusAction_ReadsBonusAndAllyTarget()
        {
            Assert.That(_parser.TryParse(CreateMapper(EnergyTriggers, EnergyActions), out var definition), Is.True);

            var setBonus = definition.Actions[1].Component;

            Assert.That(_reader.TryReadInt(setBonus, "bonus_id", 0, out var bonusId), Is.True);
            Assert.That(bonusId, Is.EqualTo(1));

            Assert.That(_reader.TryReadInt(setBonus, "target", 0, out var target), Is.True);
            Assert.That(target, Is.EqualTo(0));
        }

        [Test]
        public void TryParse_LegacyRowWithoutTriggers_IsNotReadAsNewFormat()
        {
            Assert.That(_parser.TryParse(CreateMapper(string.Empty, string.Empty), out _), Is.False);
        }

        [Test]
        public void TryParse_UnknownTrigger_IsRejected()
        {
            Assert.That(_parser.TryParse(CreateMapper("on_full_moon:[value:{1}]", EnergyActions), out _), Is.False);
        }

        [Test]
        public void TryParse_MultipleTriggers_AreAllRead()
        {
            var triggers = "ally_health_lower:[\nhealth:{0.5,0.6},\nis_instant_activation:{1,1}];\non_cooldown:[\ncooldown:{3,3},\nis_start_availiable:{1,1}]";

            Assert.That(_parser.TryParse(CreateMapper(triggers, EnergyActions), out var definition), Is.True);
            Assert.That(definition.Triggers, Has.Count.EqualTo(2));
            Assert.That(definition.HasTrigger(SkillTriggerType.AllyHealthLower), Is.True);
            Assert.That(definition.HasTrigger(SkillTriggerType.OnCooldown), Is.True);
            Assert.That(definition.TryGetTrigger(SkillTriggerType.AllyHealthLower, out var allyTrigger), Is.True);
            Assert.That(_reader.ReadFlag(allyTrigger.Component, "is_instant_activation", 0), Is.True);
            Assert.That(_reader.TryReadFloat(allyTrigger.Component, "health", 1, out var health), Is.True);
            Assert.That(health, Is.EqualTo(0.6f).Within(0.0001f));
        }

        private Server.Skills.ISkillMapper CreateMapper(string triggers, string actions)
        {
            return new Server.Skills.SkillMapper
            {
                Id = 1,
                Triggers = triggers,
                Actions = actions,
            };
        }
    }
}
