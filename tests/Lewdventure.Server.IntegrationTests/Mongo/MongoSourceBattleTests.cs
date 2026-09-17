using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Server;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class MongoSourceBattleTests
    {
        private const string CaseName = "001-baseline-1v1-vs-tank";
        private const ulong Seed = 42UL;

        private readonly FixturePaths _paths = new();

        [Test]
        public async Task ServerWithMongoSource_ReplayMatchesGolden()
        {
            await using var environment = new MongoTestEnvironment();

            await environment.StartAsync(new ConfigServicesRegistrar().Register);
            await environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            var fixture = await environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            var publish = await environment.Services.GetRequiredService<ConfigPublishingService>().PublishAsync(fixture, "it", "seed", true, CancellationToken.None);

            Assert.That(publish.Succeeded, Is.True, string.Join("; ", publish.Errors));

            using var factory = new MongoApiFactory(_paths.ApiDirectory, environment.ConnectionString, environment.DatabaseName);
            using var client = factory.CreateClient();

            var caseDirectory = Path.Combine(_paths.GoldenDirectory, "Cases", CaseName);
            var request = JObject.Parse(await File.ReadAllTextAsync(Path.Combine(caseDirectory, "request.json")));

            request["seed"] = new JValue(Seed);

            using var content = new StringContent(request.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/api/battle/replay", content);

            var body = await response.Content.ReadAsStringAsync();
            var expected = await File.ReadAllTextAsync(Path.Combine(caseDirectory, $"seed-{Seed}.response.json"), new UTF8Encoding(false));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
            Assert.That(body, Is.EqualTo(expected));
            Assert.That(factory.Services.GetRequiredService<IGameConfigSetProvider>().Current.Version, Is.EqualTo(fixture.Version));
        }

        private sealed class MongoApiFactory : WebApplicationFactory<Program>
        {
            private readonly string _connectionString;
            private readonly string _databaseName;

            public MongoApiFactory(string apiDirectory, string connectionString, string databaseName)
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
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }
        }
    }
}
