using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Server.Api.Security;
using Server.Infrastructure.Players;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class PlayerRateLimitTests
    {
        private const string SigningKey = "unit-test-auth-signing-key-0123456789";

        [Test]
        public async Task PlayerLimit_IsCountedPerPlayerNotPerAddress()
        {
            using var factory = new ApiWebApplicationFactory(new Dictionary<string, string>
            {
                ["Auth:Enabled"] = "true",
                ["Auth:SigningKey"] = SigningKey,
                ["RateLimit:Enabled"] = "true",
                ["RateLimit:Player:PermitLimit"] = "2",
                ["RateLimit:Player:WindowSeconds"] = "60",
            });
            using var client = factory.CreateClient();

            var first = new List<HttpStatusCode>();

            for (int i = 0; i < 3; i++)
                first.Add(await GetProfileAsync(client, "usr_first"));

            var second = await GetProfileAsync(client, "usr_second");

            Assert.That(first[0], Is.Not.EqualTo((HttpStatusCode)429));
            Assert.That(first[1], Is.Not.EqualTo((HttpStatusCode)429));
            Assert.That(first[2], Is.EqualTo((HttpStatusCode)429), "лимит игрока не сработал");
            Assert.That(second, Is.Not.EqualTo((HttpStatusCode)429), "второй игрок с того же адреса получил лимит первого");
        }

        [Test]
        public async Task AnalyticsLimit_DoesNotSpendPlayerLimit()
        {
            using var factory = new ApiWebApplicationFactory(new Dictionary<string, string>
            {
                ["Auth:Enabled"] = "true",
                ["Auth:SigningKey"] = SigningKey,
                ["RateLimit:Enabled"] = "true",
                ["RateLimit:Player:PermitLimit"] = "1",
                ["RateLimit:Player:WindowSeconds"] = "60",
                ["RateLimit:Analytics:PermitLimit"] = "5",
                ["RateLimit:Analytics:WindowSeconds"] = "60",
            });
            using var client = factory.CreateClient();

            for (int i = 0; i < 3; i++)
            {
                using var request = CreateRequest(HttpMethod.Post, "/api/analytics/events", "usr_events");

                request.Content = new StringContent("{\"events\":[]}", System.Text.Encoding.UTF8, "application/json");

                using var response = await client.SendAsync(request);

                Assert.That(response.StatusCode, Is.Not.EqualTo((HttpStatusCode)429), "аналитика упёрлась в лимит игровых ручек");
            }

            Assert.That(await GetProfileAsync(client, "usr_events"), Is.Not.EqualTo((HttpStatusCode)429), "аналитика потратила лимит игровых ручек");
        }

        private async Task<HttpStatusCode> GetProfileAsync(HttpClient client, string userId)
        {
            using var request = CreateRequest(HttpMethod.Get, "/api/player/profile", userId);
            using var response = await client.SendAsync(request);

            return response.StatusCode;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path, string userId)
        {
            var issuer = new AccessTokenIssuer(
                Options.Create(new AuthOptions { Enabled = true, SigningKey = SigningKey }),
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            var request = new HttpRequestMessage(method, path);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issuer.Issue(userId).Value);

            return request;
        }
    }
}
