using System.Globalization;
using Newtonsoft.Json.Linq;
using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    public sealed class SimulateReplayRoundTripTests
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

        [TestCaseSource(typeof(GoldenCaseSource), nameof(GoldenCaseSource.SuccessfulCases))]
        public async Task Simulate_ThenReplayWithReturnedSeed_ReturnsSameScript(string caseName)
        {
            var goldenCase = _host.Catalog.Load(caseName);
            var simulateResponse = await _host.Client.PostAsync(GoldenHttpClient.SimulatePath, goldenCase.RequestText);

            Assert.That(simulateResponse.StatusCode, Is.EqualTo(200), simulateResponse.Body);

            var seedToken = JObject.Parse(simulateResponse.Body)["seed"];

            Assert.That(seedToken, Is.Not.Null);

            var seed = ulong.Parse(seedToken!.ToString(), CultureInfo.InvariantCulture);
            var replayResponse = await _host.ReplayAsync(goldenCase, seed);

            Assert.That(replayResponse.StatusCode, Is.EqualTo(200), replayResponse.Body);
            Assert.That(replayResponse.Body, Is.EqualTo(simulateResponse.Body));
        }
    }
}
