using System.Net;
using System.Text;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class BattleRateLimitTests
    {
        [Test]
        public async Task Replay_OverPermitLimit_Returns429()
        {
            using var factory = new ApiWebApplicationFactory(new Dictionary<string, string>
            {
                ["RateLimit:Enabled"] = "true",
                ["RateLimit:Battle:PermitLimit"] = "2",
                ["RateLimit:Battle:WindowSeconds"] = "60",
            });
            using var client = factory.CreateClient();

            var statusCodes = new List<HttpStatusCode>();

            for (int i = 0; i < 3; i++)
            {
                using var content = new StringContent("null", Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("/api/battle/replay", content);

                statusCodes.Add(response.StatusCode);
            }

            Assert.That(statusCodes[0], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(statusCodes[1], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(statusCodes[2], Is.EqualTo((HttpStatusCode)429));
        }

        [Test]
        public async Task Replay_MalformedJson_Returns400()
        {
            using var factory = new ApiWebApplicationFactory(new Dictionary<string, string>());
            using var client = factory.CreateClient();
            using var content = new StringContent("{\"teamA\":", Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/api/battle/replay", content);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }
    }
}
