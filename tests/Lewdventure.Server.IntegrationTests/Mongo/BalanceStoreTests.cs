using Microsoft.Extensions.DependencyInjection;
using Server.Balance;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Balance;
using Server.Infrastructure.Mongo.Players;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class BalanceStoreTests
    {
        private const int CharacterId = 1;

        private readonly FixturePaths _paths = new();

        private MongoTestEnvironment _environment = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _environment = new MongoTestEnvironment();

            await _environment.StartAsync(Register);
            await _environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            var snapshot = await _environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            var buildResult = _environment.Services.GetRequiredService<GameConfigSetBuilder>().Build(snapshot, "test");

            Assert.That(buildResult.Succeeded, Is.True, string.Join("; ", buildResult.Errors));

            _environment.Services.GetRequiredService<IGameConfigSetProvider>().Swap(buildResult.ConfigSet!);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _environment.DisposeAsync();
        }

        [Test]
        public async Task Jobs_AreClaimedOldestFirstAndFinished()
        {
            var repository = _environment.Services.GetRequiredService<BalanceJobRepository>();
            var now = DateTime.UtcNow;

            await repository.InsertAsync(CreateJob("job_first", now.AddSeconds(-2)), CancellationToken.None);
            await repository.InsertAsync(CreateJob("job_second", now.AddSeconds(-1)), CancellationToken.None);

            Assert.That(await repository.CountPendingAsync(CancellationToken.None), Is.EqualTo(2));

            var first = await repository.ClaimNextAsync("worker", now, CancellationToken.None);
            var second = await repository.ClaimNextAsync("worker", now, CancellationToken.None);

            Assert.That(first!.Id, Is.EqualTo("job_first"));
            Assert.That(first.Status, Is.EqualTo(BalanceJobDocument.RunningStatus));
            Assert.That(second!.Id, Is.EqualTo("job_second"));
            Assert.That(await repository.ClaimNextAsync("worker", now, CancellationToken.None), Is.Null);

            await repository.UpdateProgressAsync("job_first", 5, 10, now, CancellationToken.None);
            await repository.CompleteAsync("job_first", "{\"wins\":3}", "bal_1", now, CancellationToken.None);
            await repository.FailAsync("job_second", "bad setup", 400, now, CancellationToken.None);

            var done = await repository.GetAsync("job_first", CancellationToken.None);
            var failed = await repository.GetAsync("job_second", CancellationToken.None);

            Assert.That(done!.Status, Is.EqualTo(BalanceJobDocument.DoneStatus));
            Assert.That(done.Done, Is.EqualTo(5));
            Assert.That(done.Result, Is.EqualTo("{\"wins\":3}"));
            Assert.That(done.RunId, Is.EqualTo("bal_1"));
            Assert.That(failed!.Status, Is.EqualTo(BalanceJobDocument.FailedStatus));
            Assert.That(failed.StatusCode, Is.EqualTo(400));
            Assert.That(await repository.CountPendingAsync(CancellationToken.None), Is.EqualTo(0));
        }

        [Test]
        public async Task StaleRunningJob_IsFailed()
        {
            var repository = _environment.Services.GetRequiredService<BalanceJobRepository>();
            var now = DateTime.UtcNow;

            await repository.InsertAsync(CreateJob("job_stale", now), CancellationToken.None);
            await repository.ClaimNextAsync("worker", now.AddMinutes(-10), CancellationToken.None);

            Assert.That(await repository.FailStaleAsync(now.AddMinutes(-2), "stopped", now, CancellationToken.None), Is.EqualTo(1));

            var stale = await repository.GetAsync("job_stale", CancellationToken.None);

            Assert.That(stale!.Status, Is.EqualTo(BalanceJobDocument.FailedStatus));
            Assert.That(stale.Error, Is.EqualTo("stopped"));
        }

        [Test]
        public async Task Progress_TakesPlayersStoppedOnTheLevelWithoutQa()
        {
            await InsertProfileAsync("usr_progress_a", new List<int> { 1 }, 1);
            await InsertProfileAsync("usr_progress_b", new List<int> { 1 }, 3);
            await InsertProfileAsync("usr_progress_c", new List<int> { 1 }, 2);
            await InsertProfileAsync("usr_progress_further", new List<int> { 1, 2 }, 5);
            await InsertProfileAsync("usr_progress_qa", new List<int> { 1 }, 9);
            await _environment.Services.GetRequiredService<UserRepository>().InsertAsync(
                new UserDocument { Id = "usr_progress_qa", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Qa = new UserQaDocument { Alias = "qa-progress", MarkedBy = "test", MarkedAt = DateTime.UtcNow } },
                CancellationToken.None);

            var distributor = _environment.Services.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
            var result = await _environment.Services.GetRequiredService<BalanceProgressService>().FindTypicalAsync(1, distributor, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.NextStoryLevelId, Is.EqualTo(2));
            Assert.That(result.Players, Is.EqualTo(3), "игрок дальше уровня и тестовый аккаунт не считаются");
            Assert.That(result.PromoteLevel.Median, Is.EqualTo(2f));
            Assert.That(result.UserId, Is.EqualTo("usr_progress_c"), "берётся игрок с медианной силой");
            Assert.That(result.Setup.PromoteLevel, Is.EqualTo(2));
        }

        private void Register(IServiceCollection services)
        {
            new RunServicesTestRegistrar().Register(services);
            new BalanceStoreRegistrar().Register(services);
        }

        private BalanceJobDocument CreateJob(string jobId, DateTime createdAt)
        {
            return new BalanceJobDocument { Id = jobId, CreatedAt = createdAt, UpdatedAt = createdAt, Actor = "test", Kind = "series", Request = "{}" };
        }

        private async Task InsertProfileAsync(string userId, List<int> completedLevelIds, int promoteLevel)
        {
            var profile = new PlayerProfileDocument { Id = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

            profile.Characters.Add(new PlayerCharacterDocument { ConfigId = CharacterId, PromoteLevel = promoteLevel });
            profile.Loadout.CharacterId = CharacterId;
            profile.Story.CompletedLevelIds.AddRange(completedLevelIds);

            Assert.That(await _environment.Services.GetRequiredService<PlayerProfileRepository>().InsertIfMissingAsync(profile, CancellationToken.None), Is.True);
        }
    }
}
