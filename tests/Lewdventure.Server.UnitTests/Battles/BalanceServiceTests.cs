using Server.Balance;
using Server.Battles;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Logging;
using Server.Runs;
using Server.Services;
using Server.Skills;
using Tests.Unit.Api;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class BalanceServiceTests
    {
        private const int CharacterId = 1;
        private const int FightEventId = 2;
        private const int EnemyId = 10101;

        private IConfigDistributor _configDistributor = null!;
        private BalanceService _service = null!;

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

            _configDistributor = builder.Build(snapshot, "test").ConfigSet!.Distributor;
            _service = new BalanceService(new BalanceSnapshotFactory(new RunSnapshotBuilder()), new BalanceStatistics(), new BattleScriptAnalyzer());
        }

        [Test]
        public void Characteristics_AreLayeredBySource()
        {
            var setup = CreateSetup();

            setup.AccountBonuses.Add(new BalanceBonusSetup { Id = 1, Count = 2 });

            Assert.That(_service.TryBuildCharacteristics(setup, CreateTarget(), _configDistributor, out var layers, out var error), Is.True, error);
            Assert.That(layers, Has.Count.EqualTo(5));
            Assert.That(layers[1].Characteristics.MaxHealth, Is.GreaterThan(layers[0].Characteristics.MaxHealth), "бонус аккаунта к МАКС.ХП не попал во второй слой");
            Assert.That(layers[4].Characteristics.MaxHealth, Is.EqualTo(layers[1].Characteristics.MaxHealth).Within(0.001f));
        }

        [Test]
        public void Battle_MatchesTheSameReplayInTheCore()
        {
            var setup = CreateSetup();
            var target = CreateTarget();

            Assert.That(_service.TryRunBattle(setup, target, 42, true, _configDistributor, out var first, out var error), Is.True, error);
            Assert.That(_service.TryRunBattle(setup, target, 42, false, _configDistributor, out var second, out _), Is.True);

            Assert.That(first.Outcome, Is.EqualTo(second.Outcome));
            Assert.That(first.Turns, Is.EqualTo(second.Turns));
            Assert.That(first.DamageDealt, Is.EqualTo(second.DamageDealt).Within(0.001f));
            Assert.That(first.DamageDealt, Is.GreaterThan(0f));
            Assert.That(first.Log, Is.Not.Empty);
            Assert.That(second.Log, Is.Empty);

            var sourceTotal = 0f;

            foreach (var pair in first.DamageBySource)
                sourceTotal += pair.Value;

            Assert.That(sourceTotal, Is.EqualTo(first.DamageDealt).Within(0.01f), "урон по источникам не сходится с общим уроном");
        }

        [Test]
        public void Series_IsDeterministicAndCountsWins()
        {
            var setup = CreateSetup();
            var target = CreateTarget();

            Assert.That(_service.TryRunSeries(setup, target, 1, 30, _configDistributor, CancellationToken.None, out var first, out var error), Is.True, error);
            Assert.That(_service.TryRunSeries(setup, target, 1, 30, _configDistributor, CancellationToken.None, out var second, out _), Is.True);

            Assert.That(first.Battles, Is.EqualTo(30));
            Assert.That(first.Wins, Is.EqualTo(second.Wins));
            Assert.That(first.WinRate, Is.EqualTo((float)first.Wins / 30).Within(0.0001f));
            Assert.That(first.Turns.Count, Is.EqualTo(30));
            Assert.That(first.Turns.Min <= first.Turns.Median && first.Turns.Median <= first.Turns.Max, Is.True);
        }

        [Test]
        public void StrongerSetup_WinsMoreOften()
        {
            var target = CreateTarget();
            var weak = CreateSetup();
            var strong = CreateSetup();

            strong.AccountBonuses.Add(new BalanceBonusSetup { Id = 2, Count = 50 });
            strong.AccountBonuses.Add(new BalanceBonusSetup { Id = 1, Count = 50 });

            Assert.That(_service.TryRunSeries(weak, target, 1, 40, _configDistributor, CancellationToken.None, out var weakSeries, out var error), Is.True, error);
            Assert.That(_service.TryRunSeries(strong, target, 1, 40, _configDistributor, CancellationToken.None, out var strongSeries, out _), Is.True);
            Assert.That(strongSeries.WinRate, Is.GreaterThanOrEqualTo(weakSeries.WinRate));
            Assert.That(strongSeries.WinRate, Is.EqualTo(1f), "усиленный бонусами герой обязан выигрывать");
        }

        [Test]
        public void NegativeBonus_LowersDamageInItsLayer()
        {
            var setup = CreateSetup();

            setup.AccountBonuses.Add(new BalanceBonusSetup { Id = 7, Count = 10 });

            Assert.That(_service.TryBuildCharacteristics(setup, CreateTarget(), _configDistributor, out var layers, out var error), Is.True, error);
            Assert.That(layers[1].Characteristics.Damage, Is.LessThan(layers[0].Characteristics.Damage), "бонус damage_perk -0.05 обязан снижать урон");
        }

        [Test]
        public void UnknownEvent_IsReportedAsError()
        {
            var target = new BalanceTarget { StoryLevelId = 1, EventId = 999 };

            Assert.That(_service.TryRunBattle(CreateSetup(), target, 1, false, _configDistributor, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("999"));
        }

        [Test]
        public void ExplicitEnemies_OverrideEvent()
        {
            var target = new BalanceTarget { StoryLevelId = 1, EnemyIds = new List<int> { EnemyId, EnemyId } };

            Assert.That(_service.TryRunBattle(CreateSetup(), target, 7, true, _configDistributor, out var result, out var error), Is.True, error);
            Assert.That(result.Steps, Is.GreaterThan(0));
        }

        private BalanceSetup CreateSetup()
        {
            return new BalanceSetup { CharacterId = CharacterId, PromoteLevel = 1 };
        }

        private BalanceTarget CreateTarget()
        {
            return new BalanceTarget { StoryLevelId = 1, StageId = 1, EventId = FightEventId };
        }
    }
}
