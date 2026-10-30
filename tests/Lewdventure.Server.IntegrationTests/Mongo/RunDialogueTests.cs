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
    public sealed class RunDialogueTests
    {
        private const string UserId = "usr_dialogues";
        private const int StoryLevelId = 1;
        private const int CharacterId = 1;
        private const string StartLaunchKey = "level:1:1:on_start";
        private const string StageLaunchKey = "level:1:2:on_stage_entered:2";
        private const string WinLaunchKey = "level:1:3:on_win";
        private const string LossLaunchKey = "level:1:1:on_loss";

        private readonly DialogueLaunchKeys _launchKeys = new();
        private readonly FixturePaths _paths = new();

        private MongoTestEnvironment _environment = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _environment = new MongoTestEnvironment();

            await _environment.StartAsync(new RunServicesTestRegistrar().Register);
            await _environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            var snapshot = await _environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            var buildResult = _environment.Services.GetRequiredService<GameConfigSetBuilder>().Build(AddLevelDialogues(snapshot), "test");

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
        public async Task Start_OffersStartDialogueUntilItIsCompleted()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();

            var started = await runService.StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(started.Succeeded, Is.True, string.Join("; ", started.Errors));
            Assert.That(ReadKeys(started), Is.EqualTo(new[] { StartLaunchKey }));

            var current = await runService.GetCurrentAsync(UserId, CancellationToken.None);

            Assert.That(ReadKeys(current), Is.EqualTo(new[] { StartLaunchKey }));
        }

        [Test]
        [Order(2)]
        public async Task Complete_MarksLaunchShownOnceAndStoresPicks()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();

            var completed = await CompleteAsync(StartLaunchKey, 1);

            Assert.That(completed.Succeeded, Is.True, string.Join("; ", completed.Errors));
            Assert.That(completed.Profile!.Dialogues.Completed, Has.Count.EqualTo(1));
            Assert.That(completed.Profile.Dialogues.Completed[0].Picks, Is.EqualTo(new[] { 1 }));

            var repeated = await CompleteAsync(StartLaunchKey, 2);

            Assert.That(repeated.Succeeded, Is.True, string.Join("; ", repeated.Errors));
            Assert.That(repeated.Profile!.Dialogues.Completed, Has.Count.EqualTo(1));
            Assert.That(repeated.Profile.Dialogues.Completed[0].Picks, Is.EqualTo(new[] { 1 }));

            var current = await runService.GetCurrentAsync(UserId, CancellationToken.None);

            Assert.That(ReadKeys(current), Is.Empty);
        }

        [Test]
        [Order(3)]
        public async Task Complete_RejectsUnknownLaunchesAndBrokenPaths()
        {
            var missingPick = await CompleteAsync(StageLaunchKey);
            var unknownLevelDialogue = await CompleteAsync("level:1:9:on_start");
            var unknownReward = await CompleteAsync(_launchKeys.ForReward(1));

            Assert.That(missingPick.Errors, Has.Some.Contains("needs a pick"));
            Assert.That(unknownLevelDialogue.Errors, Has.Some.Contains("has no dialogue 9"));
            Assert.That(unknownReward.Errors, Has.Some.Contains("is not pending"));
        }

        [Test]
        [Order(4)]
        public async Task RewardDialogue_IsOfferedFromProfileUntilCompleted()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profile = await repository.GetAsync(UserId, CancellationToken.None);
            var expectedRev = profile!.Rev;
            var launchKey = _launchKeys.ForReward(3);

            profile.Dialogues.Pending.Add(new PlayerDialogueLaunchDocument { LaunchKey = launchKey, DialogueId = 3, CreatedAt = DateTime.UtcNow });
            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);

            var offered = await runService.GetCurrentAsync(UserId, CancellationToken.None);

            Assert.That(ReadKeys(offered), Is.EqualTo(new[] { launchKey }));

            var completed = await CompleteAsync(launchKey, 1, 1, 4);

            Assert.That(completed.Succeeded, Is.True, string.Join("; ", completed.Errors));
            Assert.That(completed.Profile!.Dialogues.Pending, Is.Empty);

            var current = await runService.GetCurrentAsync(UserId, CancellationToken.None);

            Assert.That(ReadKeys(current), Is.Empty);
        }

        [Test]
        [Order(5)]
        public async Task Run_OffersStageAndFinishDialogues()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();
            var runRepository = _environment.Services.GetRequiredService<RunRepository>();
            var current = await runService.GetCurrentAsync(UserId, CancellationToken.None);
            var runId = current.Run!.Id;
            var offered = new List<string>();
            var guard = 0;

            while (guard < 50)
            {
                guard += 1;

                var refreshed = await runRepository.GetAsync(runId, CancellationToken.None);

                if (string.Equals(refreshed!.Status, RunDocument.ActiveStatus, StringComparison.Ordinal) == false)
                    break;

                RunOperationResult result;

                if (refreshed.PendingBattle != null)
                    result = await runService.FinishBattleAsync(UserId, runId, "dialogue-finish-" + guard, CancellationToken.None);
                else if (refreshed.PendingChoice != null)
                    result = await runService.ChooseAsync(UserId, runId, refreshed.PendingChoice.Options.GetRange(0, refreshed.PendingChoice.ChoiceCount), string.Empty, CancellationToken.None);
                else
                    result = await runService.AdvanceAsync(UserId, runId, "dialogue-advance-" + guard, CancellationToken.None);

                Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));

                offered.AddRange(ReadKeys(result));
            }

            var final = await runRepository.GetAsync(runId, CancellationToken.None);
            var finishKey = string.Equals(final!.Status, RunDocument.CompletedStatus, StringComparison.Ordinal) ? WinLaunchKey : LossLaunchKey;

            TestContext.Out.WriteLine($"run status = {final.Status}, stageIndex = {final.StageIndex}/{final.Stages.Count}, offered = {string.Join(", ", offered)}");
            Assert.That(offered, Does.Not.Contain(StartLaunchKey));
            Assert.That(offered, Does.Contain(finishKey));

            if (1 <= final.StageIndex)
                Assert.That(offered, Does.Contain(StageLaunchKey));
        }

        private async Task<PlayerUpdateResult> CompleteAsync(string launchKey, params int[] picks)
        {
            var service = _environment.Services.GetRequiredService<PlayerDialogueService>();
            var distributor = _environment.Services.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;

            return await service.CompleteAsync(UserId, new DialogueCompletion(launchKey, picks), distributor, CancellationToken.None);
        }

        private List<string> ReadKeys(RunOperationResult result)
        {
            var keys = new List<string>(result.Dialogues.Count);

            for (int i = 0; i < result.Dialogues.Count; i++)
                keys.Add(result.Dialogues[i].LaunchKey);

            return keys;
        }

        private GameConfigSnapshot AddLevelDialogues(GameConfigSnapshot snapshot)
        {
            var domains = new List<ConfigSnapshotDomain>(snapshot.Domains.Count);

            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                if (string.Equals(domain.Domain, ConfigDomainNames.StoryLevels, StringComparison.Ordinal))
                {
                    var rows = JArray.Parse(domain.RowsJson);

                    for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                    {
                        var row = (JObject)rows[rowIndex];

                        if (row["id"]!.ToString() != StoryLevelId.ToString())
                            continue;

                        row["dialogues_ids"] = "1;2;3;1";
                        row["dialogues_triggers"] = "on_start;on_stage_entered:2;on_win;on_loss";
                    }

                    domain = new ConfigSnapshotDomain(domain.Domain, domain.SpreadsheetId, domain.Range, rows.ToString(), domain.SourceRows);
                }

                domains.Add(domain);
            }

            return new GameConfigSnapshot(snapshot.Version, snapshot.CreatedAt, snapshot.SourceKind, domains);
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
