using System.Net;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class ConfigPublisherDisabledTests
    {
        [Test]
        public async Task Sheets_WhenPublisherDisabled_IsNotMapped()
        {
            using var factory = new ApiWebApplicationFactory(new Dictionary<string, string>
            {
                ["ConfigPublisher:Enabled"] = "false",
            });
            using var client = factory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/config/sheets");

            request.Headers.Add("X-Config-Secret", "1");

            using var response = await client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
    }
}
