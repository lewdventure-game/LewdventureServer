using Server.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Battles;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Infrastructure.Qa;
using Server.Runs;
using Server.Services;
using Server.Skills;
using Tests.Unit.Api;

namespace Tests.Unit.Qa
{
    [TestFixture]
    public sealed class RunCheatEditorTests
    {
        private readonly RunCheatEditor _editor = new();

        private IConfigDistributor _configDistributor = null!;
        private CheatProfileEditor _profileEditor = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var hasher = new ConfigSnapshotHasher();
            var domainNames = new ConfigDomainNames();
            var source = new FileConfigSnapshotSource(new ConfigSnapshotSerializer(hasher));
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);
            var builder = new GameConfigSetBuilder(
                new BonusWorkModeParser(new SilentCoreLog()),
                new ConfigRowsParser(new ConfigRowLocator(new ConfigRangeReader())),
                new ConfigSnapshotValidator(domainNames, new EffectParametersValidator(new EffectParameterRegistry()), new EnemyDataValidator(), new SkillComponentValidator(new SkillComponentRegistry())),
                new SilentCoreLog());
            var parser = new BattleRewardParser(new SilentCoreLog());
            var rewardApplier = new RewardApplier(parser, new BonusWorkModeParser(new SilentCoreLog()), NullLogger<RewardApplier>.Instance);

            _configDistributor = builder.Build(snapshot, "test").ConfigSet!.Distributor;
            _profileEditor = new CheatProfileEditor(new ProgressionLimits(), rewardApplier);
        }

        [Test]
        public void Stage_MovesIndexAndMarksPreviousResolved()
        {
            var run = CreateRun();

            run.StageIndex = 4;
            run.PendingChoice = new RunPendingChoiceDocument { Kind = RunPendingChoiceDocument.ForkKind };

            var error = _editor.Apply(run, new RunCheatCommand { Action = RunCheatCommand.StageAction, Stage = 2 }, _configDistributor);

            Assert.That(error, Is.Empty);
            Assert.That(run.StageIndex, Is.EqualTo(1));
            Assert.That(run.Stages[0].Resolved, Is.True);
            Assert.That(run.Stages[1].Resolved, Is.False);
            Assert.That(run.Stages[4].Resolved, Is.False);
            Assert.That(run.PendingChoice, Is.Null);
        }

        [TestCase(0)]
        [TestCase(6)]
        public void Stage_OutOfRange_Fails(int stage)
        {
            var error = _editor.Apply(CreateRun(), new RunCheatCommand { Action = RunCheatCommand.StageAction, Stage = stage }, _configDistributor);

            Assert.That(error, Does.Contain("from 1 to 5"));
        }

        [Test]
        public void Event_ReplacesEventOfUpcomingStage()
        {
            var run = CreateRun();

            var error = _editor.Apply(run, new RunCheatCommand { Action = RunCheatCommand.EventAction, Stage = 3, Id = 8 }, _configDistributor);

            Assert.That(error, Is.Empty);
            Assert.That(run.Stages[2].EventId, Is.EqualTo(8));
        }

        [Test]
        public void Event_PassedStage_Fails()
        {
            var run = CreateRun();

            run.StageIndex = 3;

            var error = _editor.Apply(run, new RunCheatCommand { Action = RunCheatCommand.EventAction, Stage = 1, Id = 2 }, _configDistributor);

            Assert.That(error, Does.Contain("already passed"));
        }

        [Test]
        public void Event_Unknown_Fails()
        {
            var error = _editor.Apply(CreateRun(), new RunCheatCommand { Action = RunCheatCommand.EventAction, Stage = 1, Id = 999 }, _configDistributor);

            Assert.That(error, Does.Contain("999"));
        }

        [Test]
        public void Health_SetsValueAndRejectsZero()
        {
            var run = CreateRun();

            Assert.That(_editor.Apply(run, new RunCheatCommand { Action = RunCheatCommand.HealthAction, Value = 250f }, _configDistributor), Is.Empty);
            Assert.That(run.CurrentHealth, Is.EqualTo(250f));
            Assert.That(_editor.Apply(run, new RunCheatCommand { Action = RunCheatCommand.HealthAction, Value = 0f }, _configDistributor), Is.Not.Empty);
        }

        [Test]
        public void Perk_AddAndRemove_ClearsUsages()
        {
            var run = CreateRun();

            _editor.Apply(run, new RunCheatCommand { Action = RunCheatCommand.AddPerkAction, Id = 7 }, _configDistributor);
            run.PerkUsages.Add(new RunPerkUsageDocument { PerkId = 7, UsedCount = 1 });

            var error = _editor.Apply(run, new RunCheatCommand { Action = RunCheatCommand.RemovePerkAction, Id = 7 }, _configDistributor);

            Assert.That(error, Is.Empty);
            Assert.That(run.Perks, Is.Empty);
            Assert.That(run.PerkUsages, Is.Empty);
        }

        [Test]
        public void Bonus_AddsWithCountAndBattles()
        {
            var run = CreateRun();

            var error = _editor.Apply(run, new RunCheatCommand { Action = RunCheatCommand.AddBonusAction, Id = 1, Count = 20, Battles = 2 }, _configDistributor);

            Assert.That(error, Is.Empty);
            Assert.That(run.Bonuses[0].Count, Is.EqualTo(20));
            Assert.That(run.Bonuses[0].RemainingBattles, Is.EqualTo(2));
        }

        [Test]
        public void Status_UnknownId_Fails()
        {
            var run = CreateRun();

            Assert.That(_editor.Apply(run, new RunCheatCommand { Action = RunCheatCommand.AddStatusAction, Id = 3 }, _configDistributor), Is.Empty);
            Assert.That(_editor.Apply(run, new RunCheatCommand { Action = RunCheatCommand.AddStatusAction, Id = 99 }, _configDistributor), Is.Not.Empty);
            Assert.That(run.Statuses, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void UnknownAction_Fails()
        {
            Assert.That(_editor.Apply(CreateRun(), new RunCheatCommand { Action = "win" }, _configDistributor), Does.Contain("win"));
        }

        [Test]
        public void CompletedLevels_SetExactlyAndValidated()
        {
            var profile = new PlayerProfileDocument { Id = "usr_story" };
            var entries = new List<PlayerLedgerEntryDocument>();

            profile.Story.CompletedLevelIds.Add(1);

            Assert.That(_profileEditor.SetCompletedLevels(profile, new[] { 2, 2 }, _configDistributor, entries, out _), Is.True);
            Assert.That(profile.Story.CompletedLevelIds, Is.EqualTo(new[] { 2 }));
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(_profileEditor.SetCompletedLevels(profile, new[] { 42 }, _configDistributor, entries, out var error), Is.False);
            Assert.That(error, Does.Contain("42"));
        }

        private RunDocument CreateRun()
        {
            var run = new RunDocument { Id = "run_test", UserId = "usr_test", StoryLevelId = 1, CurrentHealth = 100f };

            for (int i = 0; i < 5; i++)
                run.Stages.Add(new RunStageDocument { StageId = i + 1, EventId = 1 });

            return run;
        }
    }
}
