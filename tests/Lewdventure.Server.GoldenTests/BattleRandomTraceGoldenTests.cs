using System.Text;
using Server.Logging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Server.Battles;
using Server.GameConfigs;
using Server.Services;
using Server.Shared;
using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    public sealed class BattleRandomTraceGoldenTests
    {
        private const string TraceFileName = "seed-42.rolls.txt";
        private const ulong Seed = 42;

        private readonly BattleRollTrace _battleRollTrace = new();
        private readonly UTF8Encoding _encoding = new(false);

        private GoldenTestHost _host = null!;
        private BattleComposition _battleComposition = null!;
        private JsonSerializerSettings _serializerSettings = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _host = new GoldenTestHost();
            _serializerSettings = new BattleJsonSettingsFactory().Create();

            var distributor = _host.Services.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;

            _battleComposition = new BattleComposition(distributor, new SilentCoreLog(), new RecordingSeededRandomFactory(_battleRollTrace));
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _host.Dispose();
        }

        [TestCase("001-baseline-1v1-vs-tank")]
        [TestCase("029-three-summons-slot-order")]
        [TestCase("019-full-perk-kit")]
        [TestCase("062-long-battle-turn-limit")]
        public void Replay_KeepsNamedRollTrace(string caseName)
        {
            var goldenCase = _host.Catalog.Load(caseName);
            var body = _host.RequestBuilder.BuildReplayBody(goldenCase.RequestText, Seed);
            var request = JsonConvert.DeserializeObject<BattleReplayData>(body, _serializerSettings)!;

            _battleRollTrace.Clear();
            _battleComposition.BattleSimulatorService.Replay(request);

            var trace = _battleRollTrace.ToText();
            var tracePath = Path.Combine(goldenCase.Directory, TraceFileName);

            if (_host.Settings.IsUpdateMode)
            {
                File.WriteAllText(tracePath, trace, _encoding);

                Assert.Ignore($"[Golden] trace updated case = {caseName}");
            }

            if (File.Exists(tracePath) == false)
                Assert.Fail($"[Golden] missing roll trace case = {caseName}; run with LEWD_GOLDEN_UPDATE=1 in a dedicated change");

            Assert.That(trace, Is.EqualTo(File.ReadAllText(tracePath, _encoding)), $"[Golden] roll trace mismatch case = {caseName}");
        }

        [Test]
        public async Task PublicFacade_ProducesSameTrace()
        {
            var caseName = "001-baseline-1v1-vs-tank";
            var fileSource = _host.Services.GetRequiredService<FileConfigSnapshotSource>();
            var snapshot = await fileSource.LoadAsync(_host.Paths.FixturePath, CancellationToken.None);
            var domains = new List<CoreConfigDomain>(snapshot.Domains.Count);

            for (int i = 0; i < snapshot.Domains.Count; i++)
                domains.Add(new CoreConfigDomain(snapshot.Domains[i].Domain, snapshot.Domains[i].RowsJson));

            var clientTrace = new BattleRollTrace();
            var result = new SharedCoreFactory().CreateFromDomains(domains, new SilentCoreLog(), clientTrace);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));

            var goldenCase = _host.Catalog.Load(caseName);
            var body = _host.RequestBuilder.BuildReplayBody(goldenCase.RequestText, Seed);
            var request = JsonConvert.DeserializeObject<BattleReplayData>(body, _serializerSettings)!;

            result.SharedCore!.Battle.Replay(request);

            var expected = File.ReadAllText(Path.Combine(goldenCase.Directory, TraceFileName), _encoding);

            Assert.That(clientTrace.ToText(), Is.EqualTo(expected), "трасса через публичный фасад должна совпадать с эталонной");
        }
    }
}
