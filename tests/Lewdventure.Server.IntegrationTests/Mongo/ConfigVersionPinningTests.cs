using Server.Logging;
using Microsoft.Extensions.DependencyInjection;
using Server.Battles;
using Server.GameConfigs;
using Server.Infrastructure.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Runs;
using Server.Services;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class ConfigVersionPinningTests
    {
        private const string UserId = "usr_pinned";
        private const int StoryLevelId = 1;
        private const int CharacterId = 1;
        private const string StoryLevelsDomain = "Story_levels";
        private const string BaseMultiplier = "\"enemy_stats_multiplier\":\"1\"";
        private const string VariantMultiplier = "\"enemy_stats_multiplier\":\"7\"";

        private readonly FixturePaths _paths = new();

        private MongoTestEnvironment _environment = null!;
        private GameConfigSnapshot _baseSnapshot = null!;
        private GameConfigSnapshot _variantSnapshot = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _environment = new MongoTestEnvironment();

            await _environment.StartAsync(new RunServicesTestRegistrar().Register);
            await _environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            _baseSnapshot = await _environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            _variantSnapshot = CreateVariant(_baseSnapshot);

            var publishingService = _environment.Services.GetRequiredService<ConfigPublishingService>();
            var basePublish = await publishingService.PublishAsync(_baseSnapshot, "it", "base", true, CancellationToken.None);
            var variantPublish = await publishingService.PublishAsync(_variantSnapshot, "it", "variant", false, CancellationToken.None);

            Assert.That(basePublish.Succeeded, Is.True, string.Join("; ", basePublish.Errors));
            Assert.That(variantPublish.Succeeded, Is.True, string.Join("; ", variantPublish.Errors));

            await PrepareProfileAsync();
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _environment.DisposeAsync();
        }

        [Test]
        [Order(1)]
        public async Task Cache_ReturnsMasterAndBuildsPublishedVersion()
        {
            var cache = _environment.Services.GetRequiredService<GameConfigSetCache>();
            var provider = _environment.Services.GetRequiredService<IGameConfigSetProvider>();

            var master = await cache.GetAsync(_baseSnapshot.Version, CancellationToken.None);
            var variant = await cache.GetAsync(_variantSnapshot.Version, CancellationToken.None);
            var variantAgain = await cache.GetAsync(_variantSnapshot.Version, CancellationToken.None);

            Assert.That(master, Is.SameAs(provider.Current));
            Assert.That(variant, Is.Not.Null);
            Assert.That(variant!.Version, Is.EqualTo(_variantSnapshot.Version));
            Assert.That(variantAgain, Is.SameAs(variant));
        }

        [Test]
        [Order(2)]
        public async Task Cache_UnknownVersion_ReturnsNull()
        {
            var cache = _environment.Services.GetRequiredService<GameConfigSetCache>();

            var configSet = await cache.GetAsync("sha256:unknown", CancellationToken.None);

            Assert.That(configSet, Is.Null);
        }

        [Test]
        [Order(3)]
        public async Task Run_KeepsPinnedVersionAfterActivation()
        {
            var publishingService = _environment.Services.GetRequiredService<ConfigPublishingService>();
            var provider = _environment.Services.GetRequiredService<IGameConfigSetProvider>();
            var baseSet = provider.Current;

            Assert.That(baseSet.Version, Is.EqualTo(_baseSnapshot.Version));

            var runId = await StartRunAsync();
            var activation = await publishingService.ActivateAsync(_variantSnapshot.Version, "it", "mid-run", CancellationToken.None);

            Assert.That(activation.Succeeded, Is.True, string.Join("; ", activation.Errors));
            Assert.That(provider.Current.Version, Is.EqualTo(_variantSnapshot.Version));

            var step = await AdvanceUntilBattleAsync(runId);
            var cache = _environment.Services.GetRequiredService<GameConfigSetCache>();
            var pinnedSet = await cache.GetAsync(_baseSnapshot.Version, CancellationToken.None);

            Assert.That(pinnedSet, Is.SameAs(baseSet), "прежний мастер должен остаться в кэше без пересборки");
            Assert.That(ComputeReplayDigest(step, baseSet.Distributor), Is.EqualTo(step.BattleDigest), "бой забега обязан идти на закреплённой версии");
            Assert.That(ComputeReplayDigest(step, provider.Current.Distributor), Is.Not.EqualTo(step.BattleDigest), "вариант конфигов должен давать другой бой");

            var run = await _environment.Services.GetRequiredService<RunRepository>().GetAsync(runId, CancellationToken.None);

            Assert.That(run!.ConfigVersion, Is.EqualTo(_baseSnapshot.Version));
        }

        [Test]
        [Order(4)]
        public async Task NewRun_StartsOnCurrentMaster()
        {
            var runRepository = _environment.Services.GetRequiredService<RunRepository>();
            var active = await runRepository.GetActiveAsync(UserId, CancellationToken.None);

            if (active != null)
            {
                using var abandonScope = await CreatePlayerScopeAsync();

                var abandon = await abandonScope.ServiceProvider.GetRequiredService<RunService>().AbandonAsync(UserId, active.Id, CancellationToken.None);

                Assert.That(abandon.Succeeded, Is.True, string.Join("; ", abandon.Errors));
            }

            var runId = await StartRunAsync();
            var run = await runRepository.GetAsync(runId, CancellationToken.None);

            Assert.That(run!.ConfigVersion, Is.EqualTo(_variantSnapshot.Version));
        }

        private async Task<string> StartRunAsync()
        {
            using var scope = await CreatePlayerScopeAsync();

            var result = await scope.ServiceProvider.GetRequiredService<RunService>().StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));

            return result.Run!.Id;
        }

        private async Task<RunStepOutcome> AdvanceUntilBattleAsync(string runId)
        {
            for (int i = 0; i < 50; i++)
            {
                using var scope = await CreatePlayerScopeAsync();

                var runService = scope.ServiceProvider.GetRequiredService<RunService>();
                var run = await scope.ServiceProvider.GetRequiredService<RunRepository>().GetAsync(runId, CancellationToken.None);

                Assert.That(run!.Status, Is.EqualTo(RunDocument.ActiveStatus), "забег закончился раньше боя");

                if (run.PendingChoice != null)
                {
                    var picks = run.PendingChoice.Options.GetRange(0, run.PendingChoice.ChoiceCount);
                    var choice = await runService.ChooseAsync(UserId, runId, picks, string.Empty, CancellationToken.None);

                    Assert.That(choice.Succeeded, Is.True, string.Join("; ", choice.Errors));

                    continue;
                }

                var step = await runService.AdvanceAsync(UserId, runId, $"pinned-{i}", CancellationToken.None);

                Assert.That(step.Succeeded, Is.True, string.Join("; ", step.Errors));

                if (step.Step?.BattleScript != null)
                    return step.Step;
            }

            Assert.Fail("в забеге не нашлось боя");

            return null!;
        }

        private async Task<IServiceScope> CreatePlayerScopeAsync()
        {
            var scope = _environment.Services.CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<PlayerConfigVersionResolver>();
            var version = await resolver.ResolveAsync(UserId, CancellationToken.None);

            if (string.IsNullOrEmpty(version))
                return scope;

            var configSet = await scope.ServiceProvider.GetRequiredService<GameConfigSetCache>().GetAsync(version, CancellationToken.None);

            Assert.That(configSet, Is.Not.Null, $"версия {version} не загрузилась");

            scope.ServiceProvider.GetRequiredService<GameConfigSelection>().Select(configSet!);

            return scope;
        }

        private string ComputeReplayDigest(RunStepOutcome step, IConfigDistributor distributor)
        {
            var core = new BattleComposition(distributor, new SilentCoreLog());
            var script = core.BattleSimulatorService.Replay(step.BattleInput!);

            return core.BattleScriptDigest.Compute(script);
        }

        private GameConfigSnapshot CreateVariant(GameConfigSnapshot snapshot)
        {
            var domains = new List<ConfigSnapshotDomain>(snapshot.Domains.Count);
            var replaced = false;

            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                if (string.Equals(domain.Domain, StoryLevelsDomain, StringComparison.Ordinal) == false)
                {
                    domains.Add(domain);

                    continue;
                }

                var index = domain.RowsJson.IndexOf(BaseMultiplier, StringComparison.Ordinal);

                Assert.That(0 <= index, Is.True, "в фикстуре нет множителя врагов уровня 1");

                var rowsJson = domain.RowsJson.Substring(0, index) + VariantMultiplier + domain.RowsJson.Substring(index + BaseMultiplier.Length);

                domains.Add(new ConfigSnapshotDomain(domain.Domain, domain.SpreadsheetId, domain.Range, rowsJson, domain.SourceRows));
                replaced = true;
            }

            Assert.That(replaced, Is.True, "в фикстуре нет листа Story_levels");

            var version = _environment.Services.GetRequiredService<ConfigSnapshotHasher>().ComputeVersion(domains);

            return new GameConfigSnapshot(version, snapshot.CreatedAt, snapshot.SourceKind, domains);
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
