using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.GameConfigs;
using Server.Infrastructure.GoogleSheets;
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
        public async Task Capture_FromGoogleSheets_WritesFixture()
        {
            var paths = new GoldenPaths();
            var credentialsPath = ResolveCredentialsPath(paths);

            if (File.Exists(credentialsPath) == false)
                Assert.Inconclusive($"[Golden] credentials not found path = {credentialsPath}");

            using var provider = CreateProvider(paths, credentialsPath);

            var importer = provider.GetRequiredService<GoogleSheetsConfigImporter>();
            var fileSource = provider.GetRequiredService<FileConfigSnapshotSource>();
            var snapshot = await importer.ImportAsync(CancellationToken.None);

            await fileSource.SaveAsync(paths.FixturePath, snapshot, CancellationToken.None);

            var reloaded = await fileSource.LoadAsync(paths.FixturePath, CancellationToken.None);

            Assert.That(reloaded.Version, Is.EqualTo(snapshot.Version));
        }

        private string ResolveCredentialsPath(GoldenPaths paths)
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(CredentialsVariable);

            if (string.IsNullOrEmpty(fromEnvironment) == false)
                return fromEnvironment;

            return Path.Combine(paths.RepositoryDirectory, "google-credentials.json");
        }

        private ServiceProvider CreateProvider(GoldenPaths paths, string credentialsPath)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(paths.ServerProjectDirectory)
                .AddJsonFile("appsettings.json", false)
                .Build();
            var services = new ServiceCollection();

            services.AddLogging(ConfigureLogging);
            services.AddOptions<GoogleSheetsOptions>()
                .Bind(configuration.GetSection(GoogleSheetsOptions.SectionName))
                .PostConfigure(options => options.CredentialsPath = credentialsPath);
            services.AddSingleton<ConfigDomainNames>();
            services.AddSingleton<ConfigSnapshotHasher>();
            services.AddSingleton<ConfigSnapshotSerializer>();
            services.AddSingleton<FileConfigSnapshotSource>();
            services.AddSingleton<GoogleCredentialProvider>();
            services.AddSingleton<GoogleSheetsConfigImporter>();

            return services.BuildServiceProvider();
        }

        private void ConfigureLogging(ILoggingBuilder loggingBuilder)
        {
            loggingBuilder.ClearProviders();
        }
    }
}
