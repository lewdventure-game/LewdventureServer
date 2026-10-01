using Server.Logging;
using Microsoft.Extensions.DependencyInjection;
using Server.Battles;
using Server.GameConfigs;
using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    public sealed class SkillCoverageGoldenTests
    {
        private GoldenTestHost _host = null!;
        private BattleComposition _battleComposition = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _host = new GoldenTestHost();

            var distributor = _host.Services.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;

            _battleComposition = new BattleComposition(distributor, new SilentCoreLog());
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _host.Dispose();
        }

        [Test]
        public void EverySkillRow_IsReadableByServer()
        {
            var distributor = _host.Services.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
            var parser = new SkillDefinitionParser(new SilentCoreLog());
            var unreadable = new List<string>();
            var skills = distributor.Skills.Collection;

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                var hasNewFormat = string.IsNullOrWhiteSpace(skill.Triggers) == false || string.IsNullOrWhiteSpace(skill.Actions) == false;

                if (hasNewFormat == false)
                {
                    if (_battleComposition.SkillFactory.IsKnownSkillId(skill.Id.ToString()) == false)
                        unreadable.Add($"{skill.Id}: нет ни triggers/actions, ни известного type");

                    continue;
                }

                if (parser.TryParse(skill, out var definition) == false)
                {
                    unreadable.Add($"{skill.Id}: triggers или actions не читаются");

                    continue;
                }

                if (definition.Triggers.Count == 0 || definition.Actions.Count == 0)
                    unreadable.Add($"{skill.Id}: нет условий или действий");
            }

            Assert.That(unreadable, Is.Empty, "сервер не понимает скиллы: " + string.Join("; ", unreadable));
        }

        [Test]
        public void EveryKnownSkillType_HasGoldenCase()
        {
            var cases = _host.Catalog.LoadAll();
            var missing = new List<string>();

            foreach (var typeKey in _battleComposition.SkillFactory.KnownTypeKeys)
            {
                var covered = false;

                for (int i = 0; i < cases.Count; i++)
                {
                    if (cases[i].RequestText.Contains(typeKey, StringComparison.OrdinalIgnoreCase) == false)
                        continue;

                    covered = true;

                    break;
                }

                if (covered == false)
                    missing.Add(typeKey);
            }

            Assert.That(missing, Is.Empty, "у типа скилла нет эталонного кейса: " + string.Join(", ", missing));
        }
    }
}
