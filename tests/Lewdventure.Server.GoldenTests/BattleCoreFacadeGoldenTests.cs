using Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Server.Battles;
using Server.GameConfigs;
using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    public sealed class BattleCoreFacadeGoldenTests
    {
        private const ulong Seed = 42;

        private GoldenTestHost _host = null!;
        private readonly List<CoreConfigDomain> _domains = new();
        private IBattleCore _battleCore = null!;
        private JsonSerializerSettings _serializerSettings = null!;
        private string _fixtureVersion = string.Empty;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _host = new GoldenTestHost();
            _serializerSettings = new BattleJsonSettingsFactory().Create();

            var fileSource = _host.Services.GetRequiredService<FileConfigSnapshotSource>();
            var snapshot = await fileSource.LoadAsync(_host.Paths.FixturePath, CancellationToken.None);
            var domains = new List<CoreConfigDomain>(snapshot.Domains.Count);

            for (int i = 0; i < snapshot.Domains.Count; i++)
                domains.Add(new CoreConfigDomain(snapshot.Domains[i].Domain, snapshot.Domains[i].RowsJson));

            _domains.AddRange(domains);

            _fixtureVersion = snapshot.Version;

            var result = new BattleCoreFactory().CreateFromDomains(domains, new SilentCoreLog());

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Succeeded, Is.True);

            _battleCore = result.BattleCore!;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _host.Dispose();
        }

        [Test]
        public void ConfigVersion_MatchesServerSnapshotVersion()
        {
            Assert.That(_battleCore.ConfigVersion, Is.EqualTo(_fixtureVersion));
        }

        [Test]
        public void Configs_ExposeReadModelForVisuals()
        {
            Assert.That(_battleCore.Configs.Characters.Collection.Count, Is.GreaterThan(0));
            Assert.That(_battleCore.Configs.Enemies.Collection.Count, Is.GreaterThan(0));
            Assert.That(_battleCore.Configs.Skills.Collection.Count, Is.GreaterThan(0));
        }

        [Test]
        public void CreateFromBundle_ReadsClientBundleAndKeepsVersion()
        {
            var entries = new List<object>(_domains.Count);

            for (int i = 0; i < _domains.Count; i++)
                entries.Add(new { Name = _domains[i].Domain, Content = _domains[i].RowsJson });

            var bundleJson = JsonConvert.SerializeObject(new { Configs = entries });
            var result = new BattleCoreFactory().CreateFromBundle(bundleJson, new SilentCoreLog());

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ConfigVersion, Is.EqualTo(_fixtureVersion));
        }

        [Test]
        public void CreateFromBundle_WithBrokenJson_ReportsError()
        {
            var result = new BattleCoreFactory().CreateFromBundle("{", new SilentCoreLog());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Config bundle parse failed"));
        }

        [TestCaseSource(typeof(GoldenCaseSource), nameof(GoldenCaseSource.SuccessfulCases))]
        public async Task FacadeReplay_MatchesServerReplay(string caseName)
        {
            var goldenCase = _host.Catalog.Load(caseName);
            var expected = await _host.ReplayAsync(goldenCase, Seed);

            if (expected.StatusCode != 200)
                Assert.Ignore("[Golden] negative case");

            var body = _host.RequestBuilder.BuildReplayBody(goldenCase.RequestText, Seed);
            var request = JsonConvert.DeserializeObject<BattleReplayData>(body, _serializerSettings)!;
            var response = _battleCore.Replay(request);
            var actual = JsonConvert.SerializeObject(response, _serializerSettings);

            Assert.That(actual, Is.EqualTo(expected.Body));
        }
    }
}
