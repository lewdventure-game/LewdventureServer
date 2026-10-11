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
        private const int SeededStatusId = 1;
        private const int ResurrectionPerkId = 7;
        private const int AccountBonusId = 1;
        private const int RewardPerkId = 1;
        private const int RewardPerkBonusId = 1;
        private const int RewardPerkExperience = 20;
        private const int FirePerkId = 2;

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
                    AssertOfferedPerksRespectMultiplePicks(refreshed);

                    var picks = refreshed.PendingChoice.Options.GetRange(0, refreshed.PendingChoice.ChoiceCount);
                    var choice = await runService.ChooseAsync(UserId, run.Id, picks, string.Empty, CancellationToken.None);

                    Assert.That(choice.Succeeded, Is.True, string.Join("; ", choice.Errors));

                    continue;
                }

                var step = await runService.AdvanceAsync(UserId, run.Id, "run-request-" + guard, CancellationToken.None);

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

        private void AssertOfferedPerksRespectMultiplePicks(RunDocument run)
        {
            var distributor = _environment.Services.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
            var options = run.PendingChoice!.Options;

            for (int i = 0; i < options.Count; i++)
            {
                var perkId = options[i];

                if (run.Perks.Contains(perkId) == false)
                    continue;

                Assert.That(distributor.Perks.TryGet(perkId, out var perkMapper), Is.True);
                Assert.That(perkMapper.IsMultiplePicks, Is.True, $"перк {perkId} уже активен и не помечен multiple_picks");
            }
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

        [Test]
        [Order(8)]
        public async Task Statuses_AreDeliveredToBattleAndConsumed()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();
            var repository = _environment.Services.GetRequiredService<RunRepository>();
            var started = await runService.StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(started.Succeeded, Is.True, string.Join("; ", started.Errors));

            var run = started.Run!;

            run.Statuses.Add(SeededStatusId);
            run.Perks.Add(ResurrectionPerkId);
            run.Rev += 1;

            await GrantAccountBonusAsync();

            Assert.That(await repository.ReplaceAsync(run, run.Rev - 1, CancellationToken.None), Is.True);

            var guard = 0;
            RunStepOutcome? fight = null;

            while (guard < 50)
            {
                guard += 1;

                var refreshed = await repository.GetAsync(run.Id, CancellationToken.None);

                Assert.That(refreshed, Is.Not.Null);

                if (string.Equals(refreshed!.Status, RunDocument.ActiveStatus, StringComparison.Ordinal) == false)
                    break;

                if (refreshed.PendingChoice != null)
                {
                    var picks = refreshed.PendingChoice.Options.GetRange(0, refreshed.PendingChoice.ChoiceCount);

                    await runService.ChooseAsync(UserId, run.Id, picks, string.Empty, CancellationToken.None);

                    continue;
                }

                var step = await runService.AdvanceAsync(UserId, run.Id, $"status-request-{guard}", CancellationToken.None);

                Assert.That(step.Succeeded, Is.True, string.Join("; ", step.Errors));

                if (step.Step?.BattleScript == null)
                    continue;

                fight = step.Step;

                break;
            }

            Assert.That(fight, Is.Not.Null, "в забеге не дошли до боя");
            Assert.That(fight!.BattleInput!.TeamA.MainUnits[0].ActiveStatusIds, Does.Contain(SeededStatusId));
            AssertAccountBonusIsDelivered(fight.BattleInput!);

            var afterFight = await repository.GetAsync(run.Id, CancellationToken.None);

            Assert.That(afterFight!.Statuses, Is.Empty, "статусы забега обязаны расходоваться в бою");

            await runService.AbandonAsync(UserId, run.Id, CancellationToken.None);
        }

        [Test]
        [Order(9)]
        public async Task ChooseRewardPerk_GrantsItsRewardsOnce()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();
            var repository = _environment.Services.GetRequiredService<RunRepository>();
            var run = await StartWithPerkChoiceAsync(new List<int> { RewardPerkId }, 1);
            var experienceBefore = run.Experience;

            var chosen = await runService.ChooseAsync(UserId, run.Id, new List<int> { RewardPerkId }, "reward-perk-choice", CancellationToken.None);

            Assert.That(chosen.Succeeded, Is.True, string.Join("; ", chosen.Errors));

            var afterChoice = await repository.GetAsync(run.Id, CancellationToken.None);

            Assert.That(afterChoice!.Perks, Does.Contain(RewardPerkId));
            Assert.That(CountRunBonus(afterChoice, RewardPerkBonusId), Is.EqualTo(1), "бонус перка reward обязан лечь в забег");
            Assert.That(afterChoice.Experience + ExperienceSpentOnLevels(afterChoice), Is.EqualTo(experienceBefore + RewardPerkExperience), "опыт перка reward не выдан");

            await runService.AbandonAsync(UserId, run.Id, CancellationToken.None);
        }

        [Test]
        [Order(10)]
        public async Task ChoosePerks_SamePerkTwice_IsRejected()
        {
            var runService = _environment.Services.GetRequiredService<RunService>();
            var run = await StartWithPerkChoiceAsync(new List<int> { RewardPerkId, FirePerkId }, 2);

            var chosen = await runService.ChooseAsync(UserId, run.Id, new List<int> { RewardPerkId, RewardPerkId }, "double-pick", CancellationToken.None);

            Assert.That(chosen.Succeeded, Is.False);
            Assert.That(chosen.Errors, Has.Some.Contains("picked twice"));

            await runService.AbandonAsync(UserId, run.Id, CancellationToken.None);
        }

        private async Task<RunDocument> StartWithPerkChoiceAsync(List<int> options, int choiceCount)
        {
            var runService = _environment.Services.GetRequiredService<RunService>();
            var repository = _environment.Services.GetRequiredService<RunRepository>();
            var started = await runService.StartAsync(UserId, StoryLevelId, CancellationToken.None);

            Assert.That(started.Succeeded, Is.True, string.Join("; ", started.Errors));

            var run = started.Run!;

            run.PendingChoice = new RunPendingChoiceDocument
            {
                Kind = RunPendingChoiceDocument.PerkKind,
                Options = options,
                ChoiceCount = choiceCount,
            };
            run.Rev += 1;

            Assert.That(await repository.ReplaceAsync(run, run.Rev - 1, CancellationToken.None), Is.True);

            return run;
        }

        private int CountRunBonus(RunDocument run, int bonusId)
        {
            var count = 0;

            for (int i = 0; i < run.Bonuses.Count; i++)
            {
                if (run.Bonuses[i].BonusId == bonusId)
                    count += run.Bonuses[i].Count;
            }

            return count;
        }

        private int ExperienceSpentOnLevels(RunDocument run)
        {
            var distributor = _environment.Services.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
            var spent = 0;

            Assert.That(distributor.StoryLevels.TryGet(run.StoryLevelId, out var level), Is.True);

            for (int experienceLevel = 1; experienceLevel < run.ExperienceLevel; experienceLevel++)
            {
                Assert.That(distributor.ExperienceLevelPatterns.TryGet(level.ExperienceLevelId, experienceLevel, out var pattern), Is.True);

                spent += pattern.ExperienceForNextLevel;
            }

            return spent;
        }

        private async Task GrantAccountBonusAsync()
        {
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profile = await profileService.GetOrCreateAsync(UserId, CancellationToken.None);
            var expectedRev = profile.Rev;

            profile.Bonuses.Add(new PlayerBonusDocument { BonusId = AccountBonusId, Count = 1, GrantedAt = DateTime.UtcNow });
            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);
        }

        private void AssertAccountBonusIsDelivered(IBattleReplayData battleInput)
        {
            var bonuses = battleInput.TeamA.MainUnits[0].ActiveBonuses;
            var found = false;

            for (int i = 0; i < bonuses.Count; i++)
            {
                if (bonuses[i].Id != AccountBonusId)
                    continue;

                found = true;

                Assert.That(bonuses[i].RemainingBattles, Is.EqualTo(0), "бонус аккаунта не сжигается по боям");
            }

            Assert.That(found, Is.True, "бонус аккаунта обязан приезжать в снапшот боя");
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
