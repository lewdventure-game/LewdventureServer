using System.Text;
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
    public sealed class BattleRandomTraceGoldenTests
    {
        private const string TraceFileName = "seed-42.rolls.txt";
        private const ulong Seed = 42;

        private readonly RecordingRandomFactory _recordingRandomFactory = new();
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

            _battleComposition = new BattleComposition(distributor, new SilentCoreLog(), _recordingRandomFactory);
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

            _recordingRandomFactory.Reset();
            _battleComposition.BattleSimulatorService.Replay(request);

            var trace = string.Join("\n", _recordingRandomFactory.Rolls) + "\n";
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
    }
}
