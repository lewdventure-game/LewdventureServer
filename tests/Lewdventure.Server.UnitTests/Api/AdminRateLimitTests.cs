using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Server.Api.Options;
using Server.Api.Security;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class AdminRateLimitTests
    {
        private const string Path = "/admin/limited";

        [Test]
        public async Task AdminLimit_IsCountedPerActorNotPerAddress()
        {
            await using var application = await CreateApplicationAsync(2);
            using var client = application.GetTestClient();

            var first = new List<HttpStatusCode>();

            for (int i = 0; i < 3; i++)
                first.Add(await SendAsync(client, "first"));

            var second = await SendAsync(client, "second");

            Assert.That(first[0], Is.EqualTo(HttpStatusCode.OK));
            Assert.That(first[1], Is.EqualTo(HttpStatusCode.OK));
            Assert.That(first[2], Is.EqualTo((HttpStatusCode)429), "лимит админа не сработал");
            Assert.That(second, Is.EqualTo(HttpStatusCode.OK), "второй админ с того же адреса получил лимит первого");
        }

        [Test]
        public void AdminLimit_DefaultFitsSeveralPlayerPages()
        {
            Assert.That(new RateLimitOptions().Admin.PermitLimit, Is.GreaterThanOrEqualTo(300));
        }

        private async Task<WebApplication> CreateApplicationAsync(int permitLimit)
        {
            var builder = WebApplication.CreateBuilder();
            var options = new RateLimitOptions { Admin = new FixedWindowLimitOptions { PermitLimit = permitLimit, WindowSeconds = 60 } };

            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<AdminActorReader>();
            builder.Services.AddRateLimiter(rateLimiterOptions => new RateLimiterConfigurator(options).Configure(rateLimiterOptions));

            var application = builder.Build();

            application.UseRateLimiter();
            application.MapGet(Path, () => Results.Ok()).RequireRateLimiting(SecurityNames.AdminRateLimitPolicy);

            await application.StartAsync();

            return application;
        }

        private async Task<HttpStatusCode> SendAsync(HttpClient client, string actor)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Path);

            request.Headers.Add(AdminActorReader.HeaderName, actor);

            using var response = await client.SendAsync(request);

            return response.StatusCode;
        }
    }
}
