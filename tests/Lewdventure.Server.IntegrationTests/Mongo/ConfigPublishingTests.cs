using Microsoft.Extensions.DependencyInjection;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class ConfigPublishingTests
    {
        private readonly FixturePaths _paths = new();

        private MongoTestEnvironment _environment = null!;
        private GameConfigSnapshot _fixture = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _environment = new MongoTestEnvironment();

            await _environment.StartAsync(new ConfigServicesRegistrar().Register);
            await _environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            _fixture = await _environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _environment.DisposeAsync();
        }

        [Test]
        [Order(1)]
        public async Task PublishWithActivation_StoresActivatesAndSwaps()
        {
            var service = _environment.Services.GetRequiredService<ConfigPublishingService>();
            var provider = _environment.Services.GetRequiredService<IGameConfigSetProvider>();

            var result = await service.PublishAsync(_fixture, "it", "first", true, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Stored, Is.True);
            Assert.That(result.Activated, Is.True);
            Assert.That(result.PreviousVersion, Is.Empty);
            Assert.That(provider.Current.Version, Is.EqualTo(_fixture.Version));
            Assert.That(await service.GetActiveVersionAsync(CancellationToken.None), Is.EqualTo(_fixture.Version));
        }

        [Test]
        [Order(2)]
        public async Task PublishSameSnapshot_IsIdempotent()
        {
            var service = _environment.Services.GetRequiredService<ConfigPublishingService>();

            var result = await service.PublishAsync(_fixture, "it", "again", true, CancellationToken.None);
            var snapshots = await service.ListAsync(10, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Stored, Is.False);
            Assert.That(result.Activated, Is.False);
            Assert.That(result.Changes.Exists(change => change.HasChanges), Is.False);
            Assert.That(snapshots, Has.Count.EqualTo(1));
        }

        [Test]
        [Order(3)]
        public async Task LoadActive_InFreshProvider_LoadsSameVersion()
        {
            var service = _environment.Services.GetRequiredService<ConfigPublishingService>();
            var builder = _environment.Services.GetRequiredService<GameConfigSetBuilder>();
            var otherProvider = new GameConfigSetProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<GameConfigSetProvider>.Instance);
            var otherService = new ConfigPublishingService(
                new Server.Infrastructure.Alerts.NullAlertPublisher(),
                _environment.Services.GetRequiredService<ConfigActivationRepository>(),
                _environment.Services.GetRequiredService<ConfigSnapshotDiff>(),
                _environment.Services.GetRequiredService<ConfigSnapshotRepository>(),
                builder,
                otherProvider,
                _environment.Services.GetRequiredService<Server.Infrastructure.GoogleSheets.GoogleSheetsConfigImporter>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigPublishingService>.Instance);

            var result = await otherService.LoadActiveAsync(string.Empty, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(otherProvider.Current.Version, Is.EqualTo(await service.GetActiveVersionAsync(CancellationToken.None)));
        }

        [Test]
        [Order(4)]
        public async Task ActivateUnknownVersion_Fails()
        {
            var service = _environment.Services.GetRequiredService<ConfigPublishingService>();

            var result = await service.ActivateAsync("sha256:unknown", "it", "bad", CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(await service.GetActiveVersionAsync(CancellationToken.None), Is.EqualTo(_fixture.Version));
        }
    }
}
