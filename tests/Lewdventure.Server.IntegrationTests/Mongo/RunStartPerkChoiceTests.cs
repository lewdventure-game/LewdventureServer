using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
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
    public sealed class RunStartPerkChoiceTests
    {
        private const string UserId = "usr_start_perk_choice";
        private const int StoryLevelId = 1;
        private const int CharacterId = 1;
        private const string StartPerkGroups = "1,2";

        private readonly FixturePaths _paths = new();

        private MongoTestEnvironment _environment = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _environment = new MongoTestEnvironment();

            await _environment.StartAsync(new RunServicesTestRegistrar().Register);
            await _environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            var snapshot = await _environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            var buildResult = _environment.Services.GetRequiredService<GameConfigSetBuilder>().Build(CreateVariant(snapshot), "test");

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
        public async Task Start_OffersChoicePerStartGroupInOrder()
        {
            var started = await RunService.StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(started.Succeeded, Is.True, string.Join("; ", started.Errors));

            var run = started.Run!;

            Assert.That(run.PendingChoice, Is.Not.Null, "на старте забега обязан открыться выбор перка из первой группы");
            Assert.That(run.PendingChoice!.Kind, Is.EqualTo(RunPendingChoiceDocument.PerkKind));
            Assert.That(run.PendingChoice.ChoiceCount, Is.EqualTo(1));
            Assert.That(run.PendingStartPerkGroups, Is.EqualTo(new List<int> { 2 }));

            var blocked = await RunService.AdvanceAsync(UserId, run.Id, "start-advance-blocked", CancellationToken.None);

            Assert.That(blocked.Succeeded, Is.False, "пока стартовый выбор не сделан, advance обязан отказывать");

            var first = await RunService.ChooseAsync(UserId, run.Id, new List<int> { run.PendingChoice.Options[0] }, "start-choice-1", CancellationToken.None);

            Assert.That(first.Succeeded, Is.True, string.Join("; ", first.Errors));
            Assert.That(first.Run!.PendingChoice, Is.Not.Null, "вторая стартовая группа обязана дать второй выбор");
            Assert.That(first.Run.PendingChoice!.ChoiceCount, Is.EqualTo(2));
            Assert.That(first.Run.PendingStartPerkGroups, Is.Empty);

            var options = first.Run.PendingChoice.Options;
            var second = await RunService.ChooseAsync(UserId, run.Id, new List<int> { options[0], options[1] }, "start-choice-2", CancellationToken.None);

            Assert.That(second.Succeeded, Is.True, string.Join("; ", second.Errors));
            Assert.That(second.Run!.PendingChoice, Is.Null);
            Assert.That(second.Run.Perks.Count, Is.EqualTo(3));

            var advanced = await RunService.AdvanceAsync(UserId, run.Id, "start-advance", CancellationToken.None);

            Assert.That(advanced.Succeeded, Is.True, string.Join("; ", advanced.Errors));

            await RunService.AbandonAsync(UserId, run.Id, CancellationToken.None);
        }

        private RunService RunService => _environment.Services.GetRequiredService<RunService>();

        private GameConfigSnapshot CreateVariant(GameConfigSnapshot snapshot)
        {
            var domains = new List<ConfigSnapshotDomain>(snapshot.Domains.Count);

            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                if (string.Equals(domain.Domain, "Constants", StringComparison.Ordinal))
                {
                    domains.Add(PatchRows(domain, AddStartPerkGroups));

                    continue;
                }

                domains.Add(domain);
            }

            var version = _environment.Services.GetRequiredService<ConfigSnapshotHasher>().ComputeVersion(domains);

            return new GameConfigSnapshot(version, snapshot.CreatedAt, snapshot.SourceKind, domains);
        }

        private ConfigSnapshotDomain PatchRows(ConfigSnapshotDomain domain, Action<JArray> patch)
        {
            var rows = JArray.Parse(domain.RowsJson);

            patch(rows);

            return new ConfigSnapshotDomain(domain.Domain, domain.SpreadsheetId, domain.Range, rows.ToString(), domain.SourceRows);
        }

        private void AddStartPerkGroups(JArray rows)
        {
            var row = (JObject)rows[0].DeepClone();

            row["constant_name"] = "start_perk_groups";
            row["constant_value"] = StartPerkGroups;
            row["constant_type"] = "Array";

            rows.Add(row);
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
