using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Server;
using Server.Api.Security;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Infrastructure.Players;
using Server.Infrastructure.Qa;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class QaGrantEndpointTests
    {
        private const string SigningKey = "integration-test-auth-signing-key-0123456789";
        private const string Rewards = "character:1:1";

        private readonly FixturePaths _paths = new();

        [Test]
        public async Task Grant_OnlyForTestAccount_AndRepeatIsIdempotent()
        {
            await using var environment = new MongoTestEnvironment();

            await environment.StartAsync(new ConfigServicesRegistrar().Register);
            await environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            var fixture = await environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            var publish = await environment.Services.GetRequiredService<ConfigPublishingService>().PublishAsync(fixture, "it", "seed", true, CancellationToken.None);

            Assert.That(publish.Succeeded, Is.True, string.Join("; ", publish.Errors));

            using var factory = new QaApiFactory(_paths.ApiDirectory, environment.ConnectionString, environment.DatabaseName);
            using var client = factory.CreateClient();
            using var scope = factory.Services.CreateScope();

            var session = await scope.ServiceProvider.GetRequiredService<PlayerAuthService>().AuthenticateDeviceAsync("device-qa-grant", "it", "XX", CancellationToken.None);
            var token = scope.ServiceProvider.GetRequiredService<AccessTokenIssuer>().Issue(session.UserId).Value;

            Assert.That(session.Succeeded, Is.True, session.Error);

            using var regular = await PostAsync(client, token, "grant-regular");

            Assert.That(regular.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "обычный аккаунт выдал себе награду");

            var mark = await scope.ServiceProvider.GetRequiredService<QaAccountService>().MarkAsync(session.UserId, "qa-grant", "admin:it", CancellationToken.None);

            Assert.That(mark.Succeeded, Is.True, mark.Error);

            using var first = await PostAsync(client, token, "grant-1");
            var firstBody = await first.Content.ReadAsStringAsync();

            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK), firstBody);

            using var repeated = await PostAsync(client, token, "grant-1");
            var repeatedBody = await repeated.Content.ReadAsStringAsync();

            Assert.That(repeated.StatusCode, Is.EqualTo(HttpStatusCode.OK), repeatedBody);
            Assert.That(ReadCharacters(firstBody), Does.Contain("\"id\":1"));
            Assert.That(ReadCharacters(repeatedBody), Is.EqualTo(ReadCharacters(firstBody)), "повтор с тем же requestId выдал ещё раз");
            Assert.That(JObject.Parse(repeatedBody)["rev"]!.Value<int>(), Is.EqualTo(JObject.Parse(firstBody)["rev"]!.Value<int>()));
        }

        private async Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string requestId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/qa/grant");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent("{\"rewards\":\"" + Rewards + "\",\"requestId\":\"" + requestId + "\"}", Encoding.UTF8, "application/json");

            return await client.SendAsync(request);
        }

        private string ReadCharacters(string body)
        {
            return JObject.Parse(body)["characters"]!.ToString(Newtonsoft.Json.Formatting.None);
        }

        private sealed class QaApiFactory : WebApplicationFactory<Program>
        {
            private readonly string _connectionString;
            private readonly string _databaseName;

            public QaApiFactory(string apiDirectory, string connectionString, string databaseName)
            {
                _connectionString = connectionString;
                _databaseName = databaseName;

                Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_LEWDVENTURE_SERVER_API", apiDirectory);
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Mongo:Enabled", "true");
                builder.UseSetting("Mongo:ConnectionString", _connectionString);
                builder.UseSetting("Mongo:DatabaseName", _databaseName);
                builder.UseSetting("GameConfig:Source", "Mongo");
                builder.UseSetting("GameConfig:FailStartupIfUnavailable", "true");
                builder.UseSetting("Auth:Enabled", "true");
                builder.UseSetting("Auth:SigningKey", SigningKey);
                builder.UseSetting("Cheats:Enabled", "true");
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }
        }
    }
}
