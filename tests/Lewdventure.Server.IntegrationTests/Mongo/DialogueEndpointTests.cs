using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server;
using Server.Api.Security;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Infrastructure.Players;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class DialogueEndpointTests
    {
        private const string SigningKey = "integration-test-auth-signing-key-0123456789";

        private readonly FixturePaths _paths = new();

        [Test]
        public async Task Complete_NullPicks_ReturnsValidationErrorInsteadOfServerError()
        {
            await using var environment = new MongoTestEnvironment();

            await environment.StartAsync(new ConfigServicesRegistrar().Register);
            await environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            var fixture = await environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            var publish = await environment.Services.GetRequiredService<ConfigPublishingService>().PublishAsync(fixture, "it", "seed", true, CancellationToken.None);

            Assert.That(publish.Succeeded, Is.True, string.Join("; ", publish.Errors));

            using var factory = new DialogueApiFactory(_paths.ApiDirectory, environment.ConnectionString, environment.DatabaseName);
            using var client = factory.CreateClient();
            using var scope = factory.Services.CreateScope();

            var authService = scope.ServiceProvider.GetRequiredService<PlayerAuthService>();
            var session = await authService.AuthenticateDeviceAsync("device-dialogue-complete", "it", "XX", CancellationToken.None);

            Assert.That(session.Succeeded, Is.True, session.Error);

            var token = scope.ServiceProvider.GetRequiredService<AccessTokenIssuer>().Issue(session.UserId).Value;
            var distributor = scope.ServiceProvider.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
            var rewardService = scope.ServiceProvider.GetRequiredService<PlayerRewardService>();
            var granted = await rewardService.GrantAsync(session.UserId, "dialogue:1:1", "it", "dialogue-null-grant", distributor, CancellationToken.None);

            Assert.That(granted.Succeeded, Is.True, string.Join("; ", granted.Errors));
            Assert.That(granted.Profile!.Dialogues.Pending, Has.Count.EqualTo(1));

            var launchKey = granted.Profile.Dialogues.Pending[0].LaunchKey;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/dialogue/complete");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent($"{{\"requestId\":\"dialogue-null-complete\",\"launchKey\":\"{launchKey}\",\"picks\":null}}", Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), body);
            Assert.That(body, Does.Contain("needs a pick"));
        }

        private sealed class DialogueApiFactory : WebApplicationFactory<Program>
        {
            private readonly string _connectionString;
            private readonly string _databaseName;

            public DialogueApiFactory(string apiDirectory, string connectionString, string databaseName)
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
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }
        }
    }
}
