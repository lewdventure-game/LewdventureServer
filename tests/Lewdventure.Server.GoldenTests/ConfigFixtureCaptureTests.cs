using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Bonuses;
using Server.Services;
using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Capture")]
    [Explicit("Читает живые Google Sheets и перезаписывает фикстуру конфигов")]
    public sealed class ConfigFixtureCaptureTests
    {
        private const string CredentialsVariable = "LEWD_GOOGLE_CREDENTIALS_PATH";

        [Test]
        public async Task Capture_FromGoogleSheets_WritesFixtureMatchingLiveConfigService()
        {
            var paths = new GoldenPaths();
            var loader = new ConfigSnapshotLoader();
            var credentialsPath = ResolveCredentialsPath(paths);

            if (File.Exists(credentialsPath) == false)
                Assert.Inconclusive($"[Golden] credentials not found path = {credentialsPath}");

            var capturer = new GoogleSheetsSnapshotCapturer(new ConfigSheetCatalog(), loader, new SheetRowsBuilder());
            var snapshot = await capturer.CaptureAsync(credentialsPath);

            loader.Save(paths.FixturePath, snapshot);

            using var provider = CreateLiveProvider();

            var liveDistributor = provider.GetRequiredService<IConfigDistributor>();
            var liveService = provider.GetRequiredService<IGameConfigService>();
            var (success, message) = await liveService.UpdateAllConfigsAsync(true);

            Assert.That(success, Is.True, message);

            var fixtureDistributor = new ConfigDistributor();
            var filler = new ConfigDistributorFiller(provider.GetRequiredService<IBonusWorkModeParser>());

            filler.Fill(fixtureDistributor, loader.Load(paths.FixturePath));

            var dump = new ConfigDistributorDump();
            var liveDump = dump.Create(liveDistributor);
            var fixtureDump = dump.Create(fixtureDistributor);

            foreach (var pair in liveDump)
                Assert.That(fixtureDump[pair.Key], Is.EqualTo(pair.Value), $"[Golden] manager mismatch = {pair.Key}");
        }

        private string ResolveCredentialsPath(GoldenPaths paths)
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(CredentialsVariable);

            if (string.IsNullOrEmpty(fromEnvironment) == false)
                return fromEnvironment;

            return Path.Combine(paths.RepositoryDirectory, "google-credentials.json");
        }

        private ServiceProvider CreateLiveProvider()
        {
            var services = new ServiceCollection();

            services.AddLogging(ConfigureLogging);
            services.AddSingleton<IConfigDistributor, ConfigDistributor>();
            services.AddSingleton<IBonusWorkModeParser, BonusWorkModeParser>();
            services.AddSingleton<IGameConfigService, GameConfigService>();

            return services.BuildServiceProvider();
        }

        private void ConfigureLogging(ILoggingBuilder loggingBuilder)
        {
            loggingBuilder.ClearProviders();
        }
    }
}
