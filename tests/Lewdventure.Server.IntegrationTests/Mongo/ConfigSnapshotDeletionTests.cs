using Microsoft.Extensions.DependencyInjection;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Infrastructure.Mongo.Experiments;
using Server.Infrastructure.Mongo.Runs;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class ConfigSnapshotDeletionTests
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

            var result = await _environment.Services.GetRequiredService<ConfigPublishingService>().PublishAsync(_fixture, "it", "base", true, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _environment.DisposeAsync();
        }

        [Test]
        public async Task DeleteAsync_ActiveSnapshot_ReturnsConflict()
        {
            var service = _environment.Services.GetRequiredService<ConfigPublishingService>();

            var result = await service.DeleteAsync(_fixture.Version, "it", CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(ConfigSnapshotDeletionStatus.Conflict));
            Assert.That(await service.GetSnapshotAsync(_fixture.Version, CancellationToken.None), Is.Not.Null);
        }

        [Test]
        public async Task DeleteAsync_UnknownSnapshot_ReturnsNotFound()
        {
            var service = _environment.Services.GetRequiredService<ConfigPublishingService>();

            var result = await service.DeleteAsync("sha256:unknown", "it", CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(ConfigSnapshotDeletionStatus.NotFound));
        }

        [Test]
        public async Task DeleteAsync_SnapshotUsedByActiveRun_ReturnsConflict()
        {
            const string version = "sha256:active-run";
            const string userId = "usr_config-delete";

            await StoreSnapshotAsync(version);

            var runs = _environment.Services.GetRequiredService<RunRepository>();

            await runs.InsertAsync(new RunDocument { Id = "run_config-delete", UserId = userId, ConfigVersion = version }, CancellationToken.None);

            var result = await _environment.Services.GetRequiredService<ConfigPublishingService>().DeleteAsync(version, "it", CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(ConfigSnapshotDeletionStatus.Conflict));
            Assert.That(result.Errors, Has.Some.Contains("active run"));
            Assert.That(await _environment.Services.GetRequiredService<ConfigPublishingService>().GetSnapshotAsync(version, CancellationToken.None), Is.Not.Null);
        }

        [Test]
        public async Task DeleteAsync_SnapshotUsedByDraftExperiment_ReturnsConflict()
        {
            const string version = "sha256:draft-experiment";

            await StoreSnapshotAsync(version);

            var experiment = new ExperimentDocument { Id = "config-delete", Name = "config-delete" };

            experiment.Groups.Add(new ExperimentGroupDocument { Id = "test", SnapshotVersion = version });

            var experiments = _environment.Services.GetRequiredService<ExperimentRepository>();

            await experiments.InsertIfMissingAsync(experiment, CancellationToken.None);

            var result = await _environment.Services.GetRequiredService<ConfigPublishingService>().DeleteAsync(version, "it", CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(ConfigSnapshotDeletionStatus.Conflict));
            Assert.That(result.Errors, Has.Some.Contains(experiment.Id));
            Assert.That(await _environment.Services.GetRequiredService<ConfigPublishingService>().GetSnapshotAsync(version, CancellationToken.None), Is.Not.Null);
        }

        [Test]
        public async Task DeleteAsync_UnusedSnapshot_DeletesSnapshot()
        {
            const string version = "sha256:unused";

            await StoreSnapshotAsync(version);

            var service = _environment.Services.GetRequiredService<ConfigPublishingService>();

            var result = await service.DeleteAsync(version, "it", CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(ConfigSnapshotDeletionStatus.Ok), string.Join("; ", result.Errors));
            Assert.That(await service.GetSnapshotAsync(version, CancellationToken.None), Is.Null);
        }

        private async Task StoreSnapshotAsync(string version)
        {
            var snapshot = new GameConfigSnapshot(version, _fixture.CreatedAt, _fixture.SourceKind, _fixture.Domains);
            var stored = await _environment.Services.GetRequiredService<ConfigSnapshotRepository>().InsertIfMissingAsync(snapshot, "it", CancellationToken.None);

            Assert.That(stored, Is.True);
        }
    }
}
