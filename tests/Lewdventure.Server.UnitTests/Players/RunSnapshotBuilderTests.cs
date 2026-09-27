using Server.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Runs;
using Server.Services;
using Tests.Unit.Api;

namespace Tests.Unit.Players
{
    [TestFixture]
    public sealed class RunSnapshotBuilderTests
    {
        private const int CharacterId = 1;
        private const int SummonId = 1;
        private const int EnemyId = 10101;

        private IConfigDistributor _configDistributor = null!;
        private RunSnapshotBuilder _builder = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var hasher = new ConfigSnapshotHasher();
            var source = new FileConfigSnapshotSource(new ConfigSnapshotSerializer(hasher));
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);
            var builder = new GameConfigSetBuilder(
                new BonusWorkModeParser(new SilentCoreLog()),
                new ConfigRowsParser(new ConfigRowLocator(new ConfigRangeReader())),
                new ConfigSnapshotValidator(new ConfigDomainNames(), new EffectParametersValidator(new EffectParameterRegistry()), new EnemyDataValidator()),
                new SilentCoreLog());

            _configDistributor = builder.Build(snapshot, "test").ConfigSet!.Distributor;
            _builder = new RunSnapshotBuilder();
        }

        [Test]
        public void TryBuild_LeavesActiveSkillIdsEmpty()
        {
            var profile = CreateProfile();
            var run = CreateRun();

            var built = _builder.TryBuild(profile, run, run.Stages[0], new[] { EnemyId }, _configDistributor, out var data, out var error);

            Assert.That(built, Is.True, error);

            var mainUnits = data.TeamA.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
                Assert.That(mainUnits[i].ActiveSkillIds, Is.Empty, "скиллы приходят только из конфигов, клиент их не выбирает");

            var summons = data.TeamA.Summons;

            for (int i = 0; i < summons.Count; i++)
                Assert.That(summons[i].ActiveSkillIds, Is.Empty, "скиллы саммона приходят только из конфигов");
        }

        [Test]
        public void TryBuild_MapsCharacterUpgradesPerksAndBonuses()
        {
            var profile = CreateProfile();
            var run = CreateRun();

            profile.Characters[0].UpgradesApplied = 2;
            run.Perks.Add(1);
            run.Bonuses.Add(new RunBonusDocument { BonusId = 3, Count = 2, RemainingBattles = 4 });
            run.CurrentHealth = 42f;

            var built = _builder.TryBuild(profile, run, run.Stages[0], new[] { EnemyId }, _configDistributor, out var data, out var error);

            Assert.That(built, Is.True, error);

            var unit = data.TeamA.MainUnits[0];

            Assert.That(unit.Id, Is.EqualTo(CharacterId));
            Assert.That(unit.Level, Is.EqualTo(3));
            Assert.That(unit.CurrentHealth, Is.EqualTo(42f));
            Assert.That(unit.ActivePerkIds, Is.EqualTo(new[] { 1 }));
            Assert.That(unit.ActiveBonuses[0].Id, Is.EqualTo(3));
            Assert.That(unit.ActiveBonuses[0].RemainingBattles, Is.EqualTo(4));
            Assert.That(data.TeamB.MainUnits[0].Id, Is.EqualTo(EnemyId));
            Assert.That(data.StoryLevelId, Is.EqualTo(run.StoryLevelId));
            Assert.That(data.StageId, Is.EqualTo(run.Stages[0].StageId));
        }

        [Test]
        public void TryBuild_AddsOnlyOwnedSummonsFromLoadout()
        {
            var profile = CreateProfile();
            var run = CreateRun();

            profile.Summons.Add(new PlayerSummonDocument { ConfigId = SummonId, Level = 4, MasteryLevel = 2 });
            profile.Loadout.Summons.Add(SummonId);
            profile.Loadout.Summons.Add(999);

            var built = _builder.TryBuild(profile, run, run.Stages[0], new[] { EnemyId }, _configDistributor, out var data, out var error);

            Assert.That(built, Is.True, error);
            Assert.That(data.TeamA.Summons, Has.Count.EqualTo(1));
            Assert.That(data.TeamA.Summons[0].Id, Is.EqualTo(SummonId));
            Assert.That(data.TeamA.Summons[0].Level, Is.EqualTo(4));
            Assert.That(data.TeamA.Summons[0].MasteryLevel, Is.EqualTo(2));
        }

        [Test]
        public void TryBuild_WithoutCharacter_Fails()
        {
            var profile = CreateProfile();
            var run = CreateRun();

            profile.Loadout.CharacterId = 0;

            var built = _builder.TryBuild(profile, run, run.Stages[0], new[] { EnemyId }, _configDistributor, out _, out var error);

            Assert.That(built, Is.False);
            Assert.That(error, Does.Contain("no character"));
        }

        [Test]
        public void TryBuild_WithUnknownEnemy_Fails()
        {
            var profile = CreateProfile();
            var run = CreateRun();

            var built = _builder.TryBuild(profile, run, run.Stages[0], new[] { 999999 }, _configDistributor, out _, out var error);

            Assert.That(built, Is.False);
            Assert.That(error, Does.Contain("Enemy 999999"));
        }

        [Test]
        public void TryBuild_WithoutEnemies_Fails()
        {
            var profile = CreateProfile();
            var run = CreateRun();

            var built = _builder.TryBuild(profile, run, run.Stages[0], Array.Empty<int>(), _configDistributor, out _, out var error);

            Assert.That(built, Is.False);
            Assert.That(error, Does.Contain("no enemies"));
        }

        private PlayerProfileDocument CreateProfile()
        {
            var profile = new PlayerProfileDocument { Id = "usr_unit", Rev = 1 };

            profile.Characters.Add(new PlayerCharacterDocument { ConfigId = CharacterId });
            profile.Loadout.CharacterId = CharacterId;

            return profile;
        }

        private RunDocument CreateRun()
        {
            var run = new RunDocument { Id = "run_unit", UserId = "usr_unit", StoryLevelId = 1, Rev = 1 };

            run.Stages.Add(new RunStageDocument { StageId = 1, EventId = 2 });

            return run;
        }
    }
}
