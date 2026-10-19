using Microsoft.Extensions.Logging.Abstractions;
using Server.Balance;
using Server.Battles;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.Players;
using Server.Logging;
using Server.Runs;
using Server.Services;
using Server.Skills;
using Tests.Unit.Api;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class BalanceLevelSimulatorTests
    {
        private const int CharacterId = 1;
        private const int StoryLevelId = 1;

        private IConfigDistributor _configDistributor = null!;
        private BalanceLevelSimulator _simulator = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var hasher = new ConfigSnapshotHasher();
            var source = new FileConfigSnapshotSource(new ConfigSnapshotSerializer(hasher));
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);
            var builder = new GameConfigSetBuilder(
                new BonusWorkModeParser(new SilentCoreLog()),
                new ConfigRowsParser(new ConfigRowLocator(new ConfigRangeReader())),
                new ConfigSnapshotValidator(new ConfigDomainNames(), new EffectParametersValidator(new EffectParameterRegistry()), new EnemyDataValidator(), new ProgressionDataValidator(), new SkillComponentValidator(new SkillComponentRegistry())),
                new SilentCoreLog());
            var runRandomFactory = new RunRandomFactory();
            var rewardApplier = new RewardApplier(new BattleRewardParser(new SilentCoreLog()), new BonusWorkModeParser(new SilentCoreLog()), NullLogger<RewardApplier>.Instance);

            _configDistributor = builder.Build(snapshot, "test").ConfigSet!.Distributor;
            _simulator = new BalanceLevelSimulator(
                new BalanceSnapshotFactory(new RunSnapshotBuilder()),
                new BalanceStatistics(),
                rewardApplier,
                new RunEventKeys(),
                runRandomFactory,
                new RunRecords(),
                new RunSnapshotBuilder(),
                new RunStageRoller(runRandomFactory));
        }

        [Test]
        public async Task Level_IsDeterministicForTheSameSeeds()
        {
            var first = await RunAsync(CreateSetup(), new BalanceLevelPolicy(), 20);
            var second = await RunAsync(CreateSetup(), new BalanceLevelPolicy(), 20);

            Assert.That(first.Runs, Is.EqualTo(20));
            Assert.That(first.Completed, Is.EqualTo(second.Completed));
            Assert.That(first.Battles, Is.EqualTo(second.Battles));
            Assert.That(first.StagesCleared.Average, Is.EqualTo(second.StagesCleared.Average).Within(0.0001f));
            Assert.That(first.Stages, Is.Not.Empty);
            Assert.That(first.Battles, Is.GreaterThan(0));
        }

        [Test]
        public async Task StageStats_AddUpToRuns()
        {
            var result = await RunAsync(CreateSetup(), new BalanceLevelPolicy(), 30);
            var deaths = 0;

            for (int i = 0; i < result.Stages.Count; i++)
            {
                var stats = result.Stages[i];

                deaths += stats.Deaths;
                Assert.That(stats.Reached, Is.EqualTo(stats.Cleared + stats.Deaths));
            }

            Assert.That(result.Completed + deaths, Is.EqualTo(result.Runs), "каждый прогон либо проходит уровень, либо умирает на одном этапе");
            Assert.That(result.Stages[0].Reached, Is.EqualTo(result.Runs));
            Assert.That(result.BattlesWon + deaths, Is.EqualTo(result.Battles));
        }

        [Test]
        public async Task StrongerSetup_ClearsMoreStages()
        {
            var weakResult = await RunAsync(CreateSetup(), new BalanceLevelPolicy(), 20);
            var strongResult = await RunAsync(CreateStrongSetup(), new BalanceLevelPolicy(), 20);

            Assert.That(strongResult.StagesCleared.Average, Is.GreaterThanOrEqualTo(weakResult.StagesCleared.Average));
            Assert.That(strongResult.CompletionRate, Is.EqualTo(1f), "усиленный бонусами герой обязан проходить уровень");
        }

        [Test]
        public async Task PriorityPolicy_PicksListedPerkWheneverOffered()
        {
            var baseline = await RunAsync(CreateStrongSetup(), new BalanceLevelPolicy { PerkPolicy = BalancePerkPolicy.Random }, 30);

            Assert.That(baseline.Perks, Is.Not.Empty, "в фикстуре должны предлагаться перки");

            var perkId = baseline.Perks[baseline.Perks.Count - 1].PerkId;
            var policy = new BalanceLevelPolicy { PerkPolicy = BalancePerkPolicy.Priority, PerkPriority = new List<int> { perkId } };
            var result = await RunAsync(CreateStrongSetup(), policy, 30);
            var stats = Find(result, perkId);

            Assert.That(stats, Is.Not.Null);
            Assert.That(stats!.Picked, Is.EqualTo(stats.Offered), "перк из списка приоритетов берётся каждый раз, когда предложен");
        }

        [Test]
        public async Task UnknownLevel_IsReportedAsError()
        {
            var levelRun = await _simulator.RunAsync(CreateSetup(), 999, 1, 5, new BalanceLevelPolicy(), "test", _configDistributor, CancellationToken.None);

            Assert.That(levelRun.Result, Is.Null);
            Assert.That(levelRun.Error, Does.Contain("999"));
        }

        private async Task<BalanceLevelResult> RunAsync(BalanceSetup setup, BalanceLevelPolicy policy, int runs)
        {
            var levelRun = await _simulator.RunAsync(setup, StoryLevelId, 1, runs, policy, "test", _configDistributor, CancellationToken.None);

            Assert.That(levelRun.Result, Is.Not.Null, levelRun.Error);

            return levelRun.Result!;
        }

        private BalancePerkStats? Find(BalanceLevelResult result, int perkId)
        {
            for (int i = 0; i < result.Perks.Count; i++)
            {
                if (result.Perks[i].PerkId == perkId)
                    return result.Perks[i];
            }

            return null;
        }

        private BalanceSetup CreateStrongSetup()
        {
            var setup = CreateSetup();

            setup.AccountBonuses.Add(new BalanceBonusSetup { Id = 2, Count = 50 });
            setup.AccountBonuses.Add(new BalanceBonusSetup { Id = 1, Count = 50 });

            return setup;
        }

        private BalanceSetup CreateSetup()
        {
            return new BalanceSetup { CharacterId = CharacterId, PromoteLevel = 1 };
        }
    }
}
