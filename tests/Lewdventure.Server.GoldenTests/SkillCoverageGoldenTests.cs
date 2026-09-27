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
