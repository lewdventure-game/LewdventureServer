using Server.Logging;
using Microsoft.Extensions.DependencyInjection;
using Server.Battles;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Runs;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class RunFlowTests
    {
        private const string UserId = "usr_runner";
        private const string UserWithoutLoadoutId = "usr_without_loadout";
        private const int StoryLevelId = 1;
        private const int CharacterId = 1;
        private const int SummonId = 1;

        private readonly FixturePaths _paths = new();

        private MongoTestEnvironment _environment = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _environment = new MongoTestEnvironment();

            await _environment.StartAsync(new RunServicesTestRegistrar().Register);
            await _environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            var snapshot = await _environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            var buildResult = _environment.Services.GetRequiredService<GameConfigSetBuilder>().Build(snapshot, "test");

            Assert.That(buildResult.Succeeded, Is.True, string.Join("; ", buildResult.Errors));

            _environment.Services.GetRequiredService<IGameConfigSetProvider>().Swap(buildResult.ConfigSet!);

            await PrepareProfileAsync();
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _environment.DisposeAsync();
        }

        [Test]
        [Order(1)]
        public async Task Start_LaysOutStagesAndPinsConfigVersion()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();
            var provider = _environment.Services.GetRequiredService<IGameConfigSetProvider>();

            var result = await runService.StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Run!.Stages, Is.Not.Empty);
            Assert.That(result.Run.ConfigVersion, Is.EqualTo(provider.Current.Version));
            Assert.That(result.Run.Status, Is.EqualTo(RunDocument.ActiveStatus));

            var profile = await _environment.Services.GetRequiredService<PlayerProfileService>().GetOrCreateAsync(UserId, CancellationToken.None);

            Assert.That(profile.Story.CurrentRunId, Is.EqualTo(result.Run.Id));
        }

        [Test]
        [Order(2)]
        public async Task Start_WhenRunIsActive_IsRejected()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();

            var result = await runService.StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("still active"));
        }

        [Test]
        [Order(3)]
        public async Task Advance_PlaysWholeRunAndCompletesIt()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();
            var current = await runService.GetCurrentAsync(UserId, CancellationToken.None);
            var run = current.Run!;
            var battles = 0;
            var guard = 0;

            while (guard < 50)
            {
                guard += 1;

                var refreshed = await _environment.Services.GetRequiredService<RunRepository>().GetAsync(run.Id, CancellationToken.None);

                Assert.That(refreshed, Is.Not.Null);

                if (string.Equals(refreshed!.Status, RunDocument.ActiveStatus, StringComparison.Ordinal) == false)
                    break;

                if (refreshed.PendingChoice != null)
                {
                    var picks = refreshed.PendingChoice.Options.GetRange(0, refreshed.PendingChoice.ChoiceCount);
                    var choice = await runService.ChooseAsync(UserId, run.Id, picks, string.Empty, CancellationToken.None);

                    Assert.That(choice.Succeeded, Is.True, string.Join("; ", choice.Errors));

                    continue;
                }

                var step = await runService.AdvanceAsync(UserId, run.Id, "run-request", CancellationToken.None);

                Assert.That(step.Succeeded, Is.True, string.Join("; ", step.Errors));

                if (step.Step?.BattleScript != null)
                {
                    battles += 1;

                    AssertSeedOnlyDeliveryReproducesScript(step.Step);
                }
            }

            var final = await _environment.Services.GetRequiredService<RunRepository>().GetAsync(run.Id, CancellationToken.None);
            var profile = await _environment.Services.GetRequiredService<PlayerProfileService>().GetOrCreateAsync(UserId, CancellationToken.None);

            TestContext.Out.WriteLine($"run status = {final!.Status}, battles = {battles}, stageIndex = {final.StageIndex}/{final.Stages.Count}, level = {final.ExperienceLevel}, perks = {final.Perks.Count}");
            Assert.That(final.Status, Is.EqualTo(RunDocument.CompletedStatus).Or.EqualTo(RunDocument.FailedStatus));
            Assert.That(0 < battles, Is.True, "run had no battles");
            Assert.That(profile.Story.CurrentRunId, Is.Empty);

            if (string.Equals(final.Status, RunDocument.CompletedStatus, StringComparison.Ordinal))
                Assert.That(profile.Story.CompletedLevelIds, Does.Contain(StoryLevelId));
        }

        private void AssertSeedOnlyDeliveryReproducesScript(RunStepOutcome step)
        {
            Assert.That(step.BattleInput, Is.Not.Null, "в шаге боя должен быть вход для переигровки по сиду");
            Assert.That(step.BattleDigest, Does.StartWith("sha256:"));
            Assert.That(step.BattleStepCount, Is.EqualTo(step.BattleScript!.Steps.Count));

            var distributor = _environment.Services.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
            var clientCore = new BattleComposition(distributor, new SilentCoreLog());
            var clientScript = clientCore.BattleSimulatorService.Replay(step.BattleInput!);

            Assert.That(clientCore.BattleScriptDigest.Compute(clientScript), Is.EqualTo(step.BattleDigest), "клиент по входу и сиду обязан получить тот же бой");
            Assert.That(clientScript.Steps.Count, Is.EqualTo(step.BattleScript!.Steps.Count));
            Assert.That(clientScript.OutcomeType, Is.EqualTo(step.BattleScript!.OutcomeType));
        }

        [Test]
        [Order(4)]
        public async Task Advance_WithoutActiveRun_IsRejected()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();

            var result = await runService.AdvanceAsync(UserId, string.Empty, string.Empty, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("No active run."));
        }

        [Test]
        [Order(5)]
        public async Task Start_LockedLevel_IsRejected()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();

            var result = await runService.StartAsync(UserId, 3, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
        }

        [Test]
        [Order(6)]
        public async Task Characteristics_AreComputedFromProfile()
        {
            var service = _environment.Services.GetRequiredService<PlayerCharacteristicsService>();

            var result = await service.GetAsync(UserId, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(0f < result.Characteristics!.MaxHealth, Is.True);
            Assert.That(0f < result.Characteristics.Damage, Is.True);
        }

        [Test]
        [Order(7)]
        public async Task Characteristics_WithoutLoadout_AreRejected()
        {
            var service = _environment.Services.GetRequiredService<PlayerCharacteristicsService>();

            await PrepareProfileWithoutLoadoutAsync();

            var result = await service.GetAsync(UserWithoutLoadoutId, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("no character"));
        }

        private async Task PrepareProfileWithoutLoadoutAsync()
        {
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profile = await profileService.GetOrCreateAsync(UserWithoutLoadoutId, CancellationToken.None);
            var expectedRev = profile.Rev;

            profile.Summons.Add(new PlayerSummonDocument { ConfigId = SummonId, Copies = 0 });
            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);
        }

        private async Task PrepareProfileAsync()
        {
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profile = await profileService.GetOrCreateAsync(UserId, CancellationToken.None);
            var expectedRev = profile.Rev;

            profile.Characters.Add(new PlayerCharacterDocument { ConfigId = CharacterId, Copies = 0 });
            profile.Loadout.CharacterId = CharacterId;
            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);
        }
    }
}
