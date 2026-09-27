using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Server.Api.Security;
using Server.Infrastructure.Players;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class PlayerEndpointsAuthTests
    {
        private const string SigningKey = "unit-test-auth-signing-key-0123456789";

        private ApiWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new ApiWebApplicationFactory(new Dictionary<string, string>
            {
                ["Auth:Enabled"] = "true",
                ["Auth:SigningKey"] = SigningKey,
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
        public async Task Profile_WithoutToken_Returns401()
        {
            using var response = await _client.GetAsync("/api/player/profile");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Profile_WithForeignToken_Returns401()
        {
            var issuer = new AccessTokenIssuer(
                Options.Create(new AuthOptions { Enabled = true, SigningKey = "another-signing-key-0123456789abcdef" }),
                new FakeTimeProvider(DateTimeOffset.UtcNow));

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/player/profile");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issuer.Issue("usr_foreign").Value);

            using var response = await _client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Profile_WithValidTokenAndNoStorage_Returns503()
        {
            var issuer = new AccessTokenIssuer(
                Options.Create(new AuthOptions { Enabled = true, SigningKey = SigningKey }),
                new FakeTimeProvider(DateTimeOffset.UtcNow));

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/player/profile");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issuer.Issue("usr_test").Value);

            using var response = await _client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        }

        [Test]
        public async Task DeviceAuth_WithoutStorage_Returns503()
        {
            using var response = await _client.PostAsJsonAsync("/api/auth/device", new { deviceId = "device-1", clientVersion = "1.0.0" });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        }

        [Test]
        public async Task DeviceAuth_WithoutDeviceId_Returns400()
        {
            using var response = await _client.PostAsJsonAsync("/api/auth/device", new { deviceId = string.Empty });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }
    }
}
