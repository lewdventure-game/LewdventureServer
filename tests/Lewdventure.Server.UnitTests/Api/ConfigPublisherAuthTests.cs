using System.Net;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class ConfigPublisherAuthTests
    {
        private const string Key = "unit-test-config-publisher-key-0001";

        private ApiWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new ApiWebApplicationFactory(new Dictionary<string, string>
            {
                ["ConfigPublisher:Enabled"] = "true",
                ["ConfigPublisher:ApiKey"] = Key,
            });
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task Update_WithoutKey_Returns401()
        {
            using var response = await _client.PostAsync("/api/config/update", null);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Update_WithWrongKey_Returns401()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/config/update");

            request.Headers.Add("X-Config-Key", "wrong-key");

            using var response = await _client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [TestCase("X-Config-Key")]
        [TestCase("X-Config-Secret")]
        public async Task Update_WithValidKey_Returns200(string headerName)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/config/update");

            request.Headers.Add(headerName, Key);

            using var response = await _client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task Health_OnPublicPort_Returns404()
        {
            using var response = await _client.GetAsync("/health/live");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
    }
}
