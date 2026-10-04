using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Server.GameConfigs;
using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Infrastructure.Mongo.Experiments;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class ExperimentFlowTests
    {
        private const string Actor = "it";
        private const string StoryLevelsDomain = "Story_levels";
        private const string BaseMultiplier = "\"enemy_stats_multiplier\":\"1\"";
        private const string VariantMultiplier = "\"enemy_stats_multiplier\":\"3\"";

        private readonly FixturePaths _paths = new();

        private MongoTestEnvironment _environment = null!;
        private GameConfigSnapshot _baseSnapshot = null!;
        private GameConfigSnapshot _variantSnapshot = null!;
        private GameConfigSnapshot _incompatibleSnapshot = null!;
        private string _existingUserId = string.Empty;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _environment = new MongoTestEnvironment();

            await _environment.StartAsync(new RunServicesTestRegistrar().Register);
            await _environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            _baseSnapshot = await _environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            _variantSnapshot = ReplaceStoryLevels(_baseSnapshot, CreateVariantRows);
            _incompatibleSnapshot = ReplaceStoryLevels(_baseSnapshot, RemoveLastRow);

            var publishingService = _environment.Services.GetRequiredService<ConfigPublishingService>();

            await PublishAsync(publishingService, _baseSnapshot, true);
            await PublishAsync(publishingService, _variantSnapshot, false);
            await PublishAsync(publishingService, _incompatibleSnapshot, false);

            _existingUserId = await AuthenticateAsync("device-existing", "US");
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _environment.DisposeAsync();
        }

        [Test]
        [Order(1)]
        public async Task Start_WithUnknownSnapshot_IsRejected()
        {
            var service = _environment.Services.GetRequiredService<ExperimentService>();

            await CreateAsync("missing", "sha256:missing", 10, true);

            var result = await service.StartAsync("missing", Actor, string.Empty, CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(ExperimentOperationStatus.Invalid));
            Assert.That(result.Errors, Has.Some.Contains("is not published"));
            Assert.That((await service.DeleteDraftAsync("missing", Actor, CancellationToken.None)).Succeeded, Is.True);
        }

        [Test]
        [Order(2)]
        public async Task Start_WithSnapshotRemovingIds_IsRejected()
        {
            var service = _environment.Services.GetRequiredService<ExperimentService>();

            await CreateAsync("incompatible", _incompatibleSnapshot.Version, 10, true);

            var result = await service.StartAsync("incompatible", Actor, string.Empty, CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(ExperimentOperationStatus.Invalid));
            Assert.That(result.Errors, Has.Some.Contains("removes " + StoryLevelsDomain));
            Assert.That((await service.DeleteDraftAsync("incompatible", Actor, CancellationToken.None)).Succeeded, Is.True);
        }

        [Test]
        [Order(3)]
        public async Task NewPlayersExperiment_AssignsFreezesAndRemoves()
        {
            var service = _environment.Services.GetRequiredService<ExperimentService>();
            var resolver = _environment.Services.GetRequiredService<PlayerConfigVersionResolver>();
            var users = _environment.Services.GetRequiredService<UserRepository>();

            await CreateAsync("balance", _variantSnapshot.Version, 100, true);

            var started = await service.StartAsync("balance", Actor, "test", CancellationToken.None);

            Assert.That(started.Succeeded, Is.True, string.Join("; ", started.Errors));

            await AuthenticateAsync("device-existing", "US");

            Assert.That(await users.GetExperimentAsync(_existingUserId, CancellationToken.None), Is.Null, "старый игрок не попадает в группу только для новых");
            Assert.That(await resolver.ResolveAsync(_existingUserId, CancellationToken.None), Is.Empty);

            var newUserId = await AuthenticateAsync("device-new", "DE");
            var assignment = await users.GetExperimentAsync(newUserId, CancellationToken.None);

            Assert.That(assignment, Is.Not.Null);
            Assert.That(assignment!.GroupId, Is.EqualTo("test"));
            Assert.That(assignment.Country, Is.EqualTo("DE"));
            Assert.That(await resolver.ResolveAsync(newUserId, CancellationToken.None), Is.EqualTo(_variantSnapshot.Version));

            await AuthenticateAsync("device-new", "DE");

            Assert.That((await users.GetExperimentAsync(newUserId, CancellationToken.None))!.AssignedAt, Is.EqualTo(assignment.AssignedAt).Within(TimeSpan.FromMilliseconds(1)), "перезаход не меняет группу");

            var frozen = await service.FreezeGroupAsync("balance", "test", Actor, string.Empty, CancellationToken.None);

            Assert.That(frozen.Succeeded, Is.True, string.Join("; ", frozen.Errors));

            var lateUserId = await AuthenticateAsync("device-late", "US");

            Assert.That(await users.GetExperimentAsync(lateUserId, CancellationToken.None), Is.Null, "после остановки набора новых не берём");
            Assert.That(await resolver.ResolveAsync(newUserId, CancellationToken.None), Is.EqualTo(_variantSnapshot.Version), "участники замороженной группы остаются на снапшоте");

            var removed = await service.RemoveGroupAsync("balance", "test", Actor, string.Empty, CancellationToken.None);

            Assert.That(removed.Succeeded, Is.True, string.Join("; ", removed.Errors));
            Assert.That(await resolver.ResolveAsync(newUserId, CancellationToken.None), Is.Empty, "удалённая группа уходит на мастер");

            var summary = await service.GetAsync("balance", CancellationToken.None);

            Assert.That(summary!.Participants["test"], Is.EqualTo(1));
            Assert.That((await service.FinishAsync("balance", Actor, string.Empty, CancellationToken.None)).Succeeded, Is.True);
            Assert.That((await service.ListChangesAsync("balance", 10, CancellationToken.None)).Count, Is.EqualTo(5));
        }

        [Test]
        [Order(4)]
        public async Task Rollout_ActivatesGroupSnapshotAndFinishes()
        {
            var service = _environment.Services.GetRequiredService<ExperimentService>();
            var provider = _environment.Services.GetRequiredService<IGameConfigSetProvider>();

            await CreateAsync("rollout", _variantSnapshot.Version, 50, false);

            Assert.That((await service.StartAsync("rollout", Actor, string.Empty, CancellationToken.None)).Succeeded, Is.True);

            var result = await service.RolloutAsync("rollout", "test", Actor, string.Empty, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Experiment!.Status, Is.EqualTo(ExperimentDocument.FinishedStatus));
            Assert.That(provider.Current.Version, Is.EqualTo(_variantSnapshot.Version));
            Assert.That(_environment.Services.GetRequiredService<ExperimentRegistry>().Running, Is.Empty);
        }

        private async Task CreateAsync(string experimentId, string snapshotVersion, double percent, bool newPlayersOnly)
        {
            var experiment = new ExperimentDocument { Id = experimentId, Name = experimentId };

            experiment.Groups.Add(new ExperimentGroupDocument
            {
                Id = "test",
                SnapshotVersion = snapshotVersion,
                Percent = percent,
                Filter = new ExperimentFilterDocument { NewPlayersOnly = newPlayersOnly },
            });

            var result = await _environment.Services.GetRequiredService<ExperimentService>().CreateAsync(experiment, Actor, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
        }

        private async Task<string> AuthenticateAsync(string deviceId, string country)
        {
            var session = await _environment.Services.GetRequiredService<PlayerAuthService>().AuthenticateDeviceAsync(deviceId, "1.0.0", country, CancellationToken.None);

            Assert.That(session.Succeeded, Is.True, session.Error);

            return session.UserId;
        }

        private async Task PublishAsync(ConfigPublishingService publishingService, GameConfigSnapshot snapshot, bool activate)
        {
            var result = await publishingService.PublishAsync(snapshot, Actor, "experiment", activate, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
        }

        private string CreateVariantRows(string rowsJson)
        {
            var index = rowsJson.IndexOf(BaseMultiplier, StringComparison.Ordinal);

            Assert.That(0 <= index, Is.True, "в фикстуре нет множителя врагов уровня 1");

            return rowsJson.Substring(0, index) + VariantMultiplier + rowsJson.Substring(index + BaseMultiplier.Length);
        }

        private string RemoveLastRow(string rowsJson)
        {
            var rows = JArray.Parse(rowsJson);

            Assert.That(1 < rows.Count, Is.True, "в листе уровней нужно минимум две строки");

            rows.RemoveAt(rows.Count - 1);

            return rows.ToString(Newtonsoft.Json.Formatting.None);
        }

        private GameConfigSnapshot ReplaceStoryLevels(GameConfigSnapshot snapshot, Func<string, string> transform)
        {
            var domains = new List<ConfigSnapshotDomain>(snapshot.Domains.Count);

            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                if (string.Equals(domain.Domain, StoryLevelsDomain, StringComparison.Ordinal))
                    domains.Add(new ConfigSnapshotDomain(domain.Domain, domain.SpreadsheetId, domain.Range, transform(domain.RowsJson)));
                else
                    domains.Add(domain);
            }

            var version = _environment.Services.GetRequiredService<ConfigSnapshotHasher>().ComputeVersion(domains);

            return new GameConfigSnapshot(version, snapshot.CreatedAt, snapshot.SourceKind, domains);
        }
    }
}
