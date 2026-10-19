using Microsoft.Extensions.Logging.Abstractions;
using Server.Balance;
using Server.Battles;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.Players;
using Server.Logging;
using Server.Runs;
using Server.Skills;
using Tests.Unit.Api;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class BalanceConfigDraftsTests
    {
        private GameConfigSet _configSet = null!;
        private BalanceConfigDrafts _drafts = null!;
        private BalanceService _service = null!;
        private BalanceSweepRunner _sweepRunner = null!;

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
            var statistics = new BalanceStatistics();
            var snapshotFactory = new BalanceSnapshotFactory(new RunSnapshotBuilder());
            var rewardApplier = new RewardApplier(new BattleRewardParser(new SilentCoreLog()), new BonusWorkModeParser(new SilentCoreLog()), NullLogger<RewardApplier>.Instance);
            var simulator = new BalanceLevelSimulator(snapshotFactory, statistics, rewardApplier, new RunEventKeys(), runRandomFactory, new RunRecords(), new RunSnapshotBuilder(), new RunStageRoller(runRandomFactory));

            _configSet = builder.Build(snapshot, "test").ConfigSet!;
            _drafts = new BalanceConfigDrafts(builder);
            _service = new BalanceService(snapshotFactory, statistics, new BattleScriptAnalyzer());
            _sweepRunner = new BalanceSweepRunner(_drafts, simulator, _service);
        }

        [Test]
        public void NoEdits_ReturnsTheSameDistributor()
        {
            var draft = _drafts.Apply(_configSet, new List<BalanceConfigEdit>());

            Assert.That(draft.Distributor, Is.SameAs(_configSet.Distributor));
            Assert.That(draft.Version, Is.EqualTo(_configSet.Version));
        }

        [Test]
        public void EnemyHealthEdit_ChangesOnlyTheDraft()
        {
            var draft = _drafts.Apply(_configSet, Edits(new BalanceConfigEdit { Sheet = "enemies", Row = "id=10101", Column = "health", Value = "900" }));

            Assert.That(draft.Errors, Is.Empty);
            Assert.That(draft.Version, Does.EndWith("+draft"));
            Assert.That(draft.Distributor!.Enemies.TryGet(10101, out var edited), Is.True);
            Assert.That(edited.Health, Is.EqualTo(900f));
            Assert.That(_configSet.Distributor.Enemies.TryGet(10101, out var original), Is.True);
            Assert.That(original.Health, Is.EqualTo(500f), "черновик не должен менять опубликованную версию");
        }

        [Test]
        public void ConstantEdit_ChangesCharacterBase()
        {
            var draft = _drafts.Apply(_configSet, Edits(new BalanceConfigEdit { Sheet = "Constants", Row = "constant_name=max_health_base", Column = "constant_value", Value = "1200" }));
            var setup = new BalanceSetup { CharacterId = 1, PromoteLevel = 1 };
            var target = new BalanceTarget { StoryLevelId = 1, StageId = 1, EventId = 2 };

            Assert.That(draft.Errors, Is.Empty);
            Assert.That(_service.TryBuildCharacteristics(setup, target, draft.Distributor!, out var layers, out var error), Is.True, error);
            Assert.That(layers[0].Characteristics.MaxHealth, Is.EqualTo(1200f).Within(0.001f));
        }

        [Test]
        public void WeakerEnemy_RaisesWinRate()
        {
            var setup = new BalanceSetup { CharacterId = 1, PromoteLevel = 1 };
            var target = new BalanceTarget { StoryLevelId = 1, StageId = 1, EventId = 2 };
            var draft = _drafts.Apply(_configSet, Edits(new BalanceConfigEdit { Sheet = "Enemies", Row = "id=10101", Column = "damage", Value = "1" }));

            Assert.That(_service.TryRunSeries(setup, target, 1, 30, _configSet.Distributor, CancellationToken.None, out var before, out var error), Is.True, error);
            Assert.That(_service.TryRunSeries(setup, target, 1, 30, draft.Distributor!, CancellationToken.None, out var after, out _), Is.True);
            Assert.That(after.WinRate, Is.GreaterThan(before.WinRate));
        }

        [TestCase("Nope", "id=1", "health", "sheet Nope")]
        [TestCase("Enemies", "id=999", "health", "no row matches")]
        [TestCase("Enemies", "id=10101", "nope", "column nope")]
        [TestCase("Enemies", "#99", "health", "out of range")]
        [TestCase("Enemies", "10101", "health", "column=value")]
        [TestCase("Enemies", "id=10101", "is_off", "does not fit")]
        public void BadEdit_IsReported(string sheet, string row, string column, string expected)
        {
            var draft = _drafts.Apply(_configSet, Edits(new BalanceConfigEdit { Sheet = sheet, Row = row, Column = column, Value = "abc" }));

            Assert.That(draft.Distributor, Is.Null);
            Assert.That(string.Join("; ", draft.Errors), Does.Contain(expected));
        }

        [Test]
        public void InvalidValue_FailsConfigValidation()
        {
            var draft = _drafts.Apply(_configSet, Edits(new BalanceConfigEdit { Sheet = "Enemies", Row = "id=10101", Column = "health", Value = "много" }));

            Assert.That(draft.Distributor, Is.Null);
            Assert.That(draft.Errors, Is.Not.Empty);
        }

        [Test]
        public void SeriesSweep_GivesOnePointPerValue()
        {
            var axis = new BalanceSweepAxis { Sheet = "Enemies", Row = "id=10101", Column = "damage", From = 10, To = 130, Step = 60 };

            Assert.That(_sweepRunner.TryCreateValues(axis, 25, out var values, out var error), Is.True, error);
            Assert.That(values, Is.EqualTo(new List<double> { 10, 70, 130 }));

            var result = _sweepRunner.RunSeries(_configSet, new List<BalanceConfigEdit>(), axis, values, new BalanceSetup { CharacterId = 1 }, new BalanceTarget { StoryLevelId = 1, StageId = 1, EventId = 2 }, 1, 20, CancellationToken.None);

            Assert.That(result.Points, Has.Count.EqualTo(3));
            Assert.That(result.Points[0].Error, Is.Empty);
            Assert.That(result.Points[0].WinRate, Is.GreaterThanOrEqualTo(result.Points[2].WinRate));
        }

        [Test]
        public void SweepLimits_AreChecked()
        {
            Assert.That(_sweepRunner.TryCreateValues(new BalanceSweepAxis { Sheet = "Enemies", Row = "id=10101", Column = "damage", From = 0, To = 100, Step = 1 }, 25, out _, out var tooMany), Is.False);
            Assert.That(tooMany, Does.Contain("limit"));
            Assert.That(_sweepRunner.TryCreateValues(new BalanceSweepAxis { Sheet = "Enemies", Row = "id=10101", Column = "damage", From = 10, To = 0, Step = 1 }, 25, out _, out _), Is.False);
            Assert.That(_sweepRunner.TryCreateValues(new BalanceSweepAxis { Sheet = "Enemies", Row = "id=10101", Column = "damage", From = 0, To = 1, Step = 0 }, 25, out _, out _), Is.False);
        }

        private List<BalanceConfigEdit> Edits(BalanceConfigEdit edit)
        {
            return new List<BalanceConfigEdit> { edit };
        }
    }
}
