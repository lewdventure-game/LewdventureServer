using System.Net;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class ConfigPublisherDisabledTests
    {
        [Test]
        public async Task Update_WhenPublisherDisabled_IsNotMapped()
        {
            using var factory = new ApiWebApplicationFactory(new Dictionary<string, string>
            {
                ["ConfigPublisher:Enabled"] = "false",
            });
            using var client = factory.CreateClient();

            var callsBeforeRequest = factory.ConfigService.CallCount;

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/config/update");

            request.Headers.Add("X-Config-Secret", "1");

            using var response = await client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(factory.ConfigService.CallCount, Is.EqualTo(callsBeforeRequest));
        }
    }
}
