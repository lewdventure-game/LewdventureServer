using System.Net;
using System.Text;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class GameConfigNotLoadedTests
    {
        [Test]
        public async Task Replay_WhenConfigsNotLoaded_Returns503()
        {
            using var factory = new ApiWebApplicationFactory(new Dictionary<string, string>
            {
                ["GameConfig:FilePath"] = Path.Combine(Path.GetTempPath(), "lewdventure-missing-snapshot.json"),
                ["GameConfig:FailStartupIfUnavailable"] = "false",
            });
            using var client = factory.CreateClient();
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/api/battle/replay", content);

            var body = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(body, Does.Contain("Game configs are not loaded."));
        }
    }
}
