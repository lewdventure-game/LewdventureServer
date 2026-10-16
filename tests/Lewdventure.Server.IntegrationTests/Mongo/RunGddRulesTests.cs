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
    public sealed class RunGddRulesTests
    {
        private const string UserId = "usr_gdd_rules";
        private const int StoryLevelId = 1;
        private const int CharacterId = 1;
        private const int SummonId = 1;
        private const int RewardPerkId = 1;
        private const int RewardPerkGroupId = 1;
        private const int PerkRewardEventId = 901;
        private const int ExperienceEventId = 902;
        private const int FightEventId = 903;
        private const int LastPatternLevel = 14;
        private const int ExperiencePerLevel = 20;

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
        public async Task PerkRewardsFromEvent_GrantPerkAndOpenGroupChoice()
        {
            var run = await InsertRunAsync("run_perk_rewards", PerkRewardEventId, 1);
            var step = await RunService.AdvanceAsync(UserId, run.Id, "perk-rewards", CancellationToken.None);

            Assert.That(step.Succeeded, Is.True, string.Join("; ", step.Errors));
            Assert.That(step.Run!.Perks, Does.Contain(RewardPerkId), "перк из награды события не лёг в забег");
            Assert.That(step.Run.PendingChoice, Is.Not.Null, "группа перков из награды события не открыла выбор");
            Assert.That(step.Run.PendingChoice!.Kind, Is.EqualTo(RunPendingChoiceDocument.PerkKind));

            await RunService.AbandonAsync(UserId, run.Id, CancellationToken.None);
        }

        [Test]
        public async Task ExperienceBeyondLastPatternRow_RepeatsLastRow()
        {
            var run = await InsertRunAsync("run_last_row", ExperienceEventId, LastPatternLevel);
            var step = await RunService.AdvanceAsync(UserId, run.Id, "last-row", CancellationToken.None);

            Assert.That(step.Succeeded, Is.True, string.Join("; ", step.Errors));
            Assert.That(step.Run!.ExperienceLevel, Is.EqualTo(LastPatternLevel + 2), "за последней строкой уровни не растут");
            Assert.That(step.Run.Experience, Is.EqualTo(5));
            Assert.That(step.Run.PendingChoice, Is.Not.Null, "повышение за последней строкой не дало выбора перков");

            await RunService.AbandonAsync(UserId, run.Id, CancellationToken.None);
        }

        [Test]
        public async Task FightStep_KeepsResultUntilFinish_AndAbandonDiscardsIt()
        {
            var run = await InsertRunAsync("run_pending_abandon", FightEventId, 1);
            var step = await RunService.AdvanceAsync(UserId, run.Id, "pending-advance", CancellationToken.None);

            Assert.That(step.Succeeded, Is.True, string.Join("; ", step.Errors));
            Assert.That(step.Step!.BattleScript, Is.Not.Null);
            Assert.That(step.Run!.PendingBattle, Is.Not.Null, "бой не ждёт подтверждения");
            Assert.That(step.Run.StageIndex, Is.EqualTo(0), "этап засчитан до конца просмотра боя");
            Assert.That(step.Run.Experience, Is.EqualTo(0), "опыт за бой выдан до конца просмотра боя");

            var blocked = await RunService.AdvanceAsync(UserId, run.Id, "pending-advance-again", CancellationToken.None);

            Assert.That(blocked.Succeeded, Is.False);

            var abandoned = await RunService.AbandonAsync(UserId, run.Id, CancellationToken.None);

            Assert.That(abandoned.Succeeded, Is.True, string.Join("; ", abandoned.Errors));
            Assert.That(abandoned.Run!.Status, Is.EqualTo(RunDocument.AbandonedStatus));
            Assert.That(abandoned.Run.PendingBattle, Is.Null);
            Assert.That(abandoned.Run.StageIndex, Is.EqualTo(0), "сдача посреди боя засчитала этап");
            Assert.That(abandoned.Run.Experience, Is.EqualTo(0), "сдача посреди боя выдала опыт");
        }

        [Test]
        public async Task FinishBattle_AppliesStoredResult()
        {
            var run = await InsertRunAsync("run_pending_finish", FightEventId, 1);
            var step = await RunService.AdvanceAsync(UserId, run.Id, "finish-advance", CancellationToken.None);

            Assert.That(step.Succeeded, Is.True, string.Join("; ", step.Errors));

            var isWin = step.Run!.PendingBattle!.IsWin;
            var finished = await RunService.FinishBattleAsync(UserId, run.Id, "finish-battle", CancellationToken.None);

            Assert.That(finished.Succeeded, Is.True, string.Join("; ", finished.Errors));
            Assert.That(finished.Run!.PendingBattle, Is.Null);
            Assert.That(finished.Step!.EventType, Is.EqualTo("fight"));

            var repeated = await RunService.FinishBattleAsync(UserId, run.Id, "finish-battle", CancellationToken.None);

            Assert.That(repeated.Replayed, Is.True);

            if (isWin)
            {
                Assert.That(finished.Run.StageIndex, Is.EqualTo(1));
                Assert.That(finished.Step.ExperienceGained, Is.GreaterThan(0));
                Assert.That(finished.Run.Status, Is.EqualTo(RunDocument.CompletedStatus).Or.EqualTo(RunDocument.ActiveStatus));
            }
            else
            {
                Assert.That(finished.Run.Status, Is.EqualTo(RunDocument.FailedStatus));
                Assert.That(finished.Step.RunFailed, Is.True);
            }

            if (string.Equals(finished.Run.Status, RunDocument.ActiveStatus, StringComparison.Ordinal))
                await RunService.AbandonAsync(UserId, run.Id, CancellationToken.None);
        }

        [Test]
        public async Task ActiveRun_LocksSummonsAndEquipment()
        {
            var started = await RunService.StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(started.Succeeded, Is.True, string.Join("; ", started.Errors));

            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var distributor = _environment.Services.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
            var same = new LoadoutUpdate(CharacterId, new Dictionary<string, string>(), new[] { SummonId });
            var sameResult = await profileService.UpdateLoadoutAsync(UserId, same, string.Empty, distributor, CancellationToken.None);

            Assert.That(sameResult.Succeeded, Is.True, string.Join("; ", sameResult.Errors));

            var changed = new LoadoutUpdate(CharacterId, new Dictionary<string, string>(), Array.Empty<int>());
            var changedResult = await profileService.UpdateLoadoutAsync(UserId, changed, string.Empty, distributor, CancellationToken.None);

            Assert.That(changedResult.Succeeded, Is.False);
            Assert.That(changedResult.Errors, Has.Some.Contains("Summons cannot be changed during run"));

            await RunService.AbandonAsync(UserId, started.Run!.Id, CancellationToken.None);
        }

        [Test]
        public async Task ActiveRun_LocksProgression()
        {
            var started = await RunService.StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(started.Succeeded, Is.True, string.Join("; ", started.Errors));

            var progressionService = _environment.Services.GetRequiredService<PlayerProgressionService>();
            var distributor = _environment.Services.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
            var level = await progressionService.UpgradeSummonLevelAsync(UserId, SummonId, "locked-summon-level", distributor, CancellationToken.None);
            var promote = await progressionService.PromoteCharacterAsync(UserId, CharacterId, "locked-promote", distributor, CancellationToken.None);

            Assert.That(level.Succeeded, Is.False);
            Assert.That(level.Errors, Has.Some.Contains("Progression is locked during run"));
            Assert.That(promote.Succeeded, Is.False);
            Assert.That(promote.Errors, Has.Some.Contains("Progression is locked during run"));

            await RunService.AbandonAsync(UserId, started.Run!.Id, CancellationToken.None);
        }

        private RunService RunService => _environment.Services.GetRequiredService<RunService>();

        private async Task<RunDocument> InsertRunAsync(string runId, int eventId, int experienceLevel)
        {
            var now = DateTime.UtcNow;
            var run = new RunDocument
            {
                Id = runId,
                UserId = UserId,
                StoryLevelId = StoryLevelId,
                ConfigVersion = _environment.Services.GetRequiredService<IGameConfigSetProvider>().Current.Version,
                Seed = 42,
                BattleSeedKey = string.Empty,
                Stages = new List<RunStageDocument>
                {
                    new() { StageId = 1, EventId = eventId },
                    new() { StageId = 2, EventId = ExperienceEventId },
                },
                ExperienceLevel = experienceLevel,
                CurrentHealth = 600,
                CreatedAt = now,
                UpdatedAt = now,
                Rev = 1,
            };

            await _environment.Services.GetRequiredService<RunRepository>().InsertAsync(run, CancellationToken.None);

            return run;
        }

        private GameConfigSnapshot CreateVariant(GameConfigSnapshot snapshot)
        {
            var domains = new List<ConfigSnapshotDomain>(snapshot.Domains.Count);

            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                if (string.Equals(domain.Domain, "Story_events", StringComparison.Ordinal))
                {
                    domains.Add(PatchRows(domain, AddEvents));

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

        private void AddEvents(JArray rows)
        {
            rows.Add(CreateEvent(rows, PerkRewardEventId, "default_event", $"loc_key:[narrative.event.1.text];\nrewards:[perk:{RewardPerkId}:1,perk_group:{RewardPerkGroupId}:1]", "0"));
            rows.Add(CreateEvent(rows, ExperienceEventId, "default_event", "loc_key:[narrative.event.1.text]", "45"));
            rows.Add(CreateEvent(rows, FightEventId, "fight", "loc_key_start:[narrative.event.2.start];\nloc_key_end:[narrative.event.2.end];\nenemies:[10101]", "20"));
        }

        private JObject CreateEvent(JArray rows, int eventId, string eventType, string parameters, string levelExperience)
        {
            var row = (JObject)rows[0].DeepClone();

            row["id"] = eventId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            row["event_type"] = eventType;
            row["event_parameters"] = parameters;
            row["level_exp"] = levelExperience;

            return row;
        }

        private async Task PrepareProfileAsync()
        {
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profile = await profileService.GetOrCreateAsync(UserId, CancellationToken.None);
            var expectedRev = profile.Rev;

            profile.Characters.Add(new PlayerCharacterDocument { ConfigId = CharacterId, Copies = 0 });
            profile.Summons.Add(new PlayerSummonDocument { ConfigId = SummonId, Level = 1, MasteryLevel = 1 });
            profile.Loadout.CharacterId = CharacterId;
            profile.Loadout.Summons = new List<int> { SummonId };
            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);
        }
    }
}
