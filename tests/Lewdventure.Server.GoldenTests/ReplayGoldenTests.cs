using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    public sealed class ReplayGoldenTests
    {
        private GoldenTestHost _host = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _host = new GoldenTestHost();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _host.Dispose();
        }

        [TestCaseSource(typeof(GoldenCaseSource), nameof(GoldenCaseSource.ReplayCases))]
        public async Task Replay_ReturnsGoldenScript(string caseName, ulong seed)
        {
            var goldenCase = _host.Catalog.Load(caseName);
            var response = await _host.ReplayAsync(goldenCase, seed);

            _host.Verifier.Verify(goldenCase, seed, response);
        }
    }
}
