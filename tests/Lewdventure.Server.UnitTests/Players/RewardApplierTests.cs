using Server.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Battles;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Services;
using Tests.Unit.Api;

namespace Tests.Unit.Players
{
    [TestFixture]
    public sealed class RewardApplierTests
    {
        private readonly BattleRewardParser _parser = new(new SilentCoreLog());

        private IConfigDistributor _configDistributor = null!;
        private RewardApplier _applier = null!;

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
                new ConfigSnapshotValidator(domainNames, new EffectParametersValidator(new EffectParameterRegistry()), new EnemyDataValidator()),
                new SilentCoreLog());
            var result = builder.Build(snapshot, "test");

            _configDistributor = result.ConfigSet!.Distributor;
            _applier = new RewardApplier(_parser, new BonusWorkModeParser(new SilentCoreLog()), NullLogger<RewardApplier>.Instance);
        }

        [Test]
        public void Apply_PermanentBonus_GoesToAccount()
        {
            var profile = CreateProfile();

            _applier.Apply(profile, _parser.Parse("bonus:1:2"), _configDistributor, DateTime.UtcNow);
            _applier.Apply(profile, _parser.Parse("bonus:1:1"), _configDistributor, DateTime.UtcNow);

            Assert.That(profile.Bonuses, Has.Count.EqualTo(1));
            Assert.That(profile.Bonuses[0].BonusId, Is.EqualTo(1));
            Assert.That(profile.Bonuses[0].Count, Is.EqualTo(3));
        }

        [Test]
        public void Apply_RunScopedBonus_IsNotGrantedToAccount()
        {
            var profile = CreateProfile();

            _applier.Apply(profile, _parser.Parse("bonus:4:1"), _configDistributor, DateTime.UtcNow);

            Assert.That(profile.Bonuses, Is.Empty);
        }

        [Test]
        public void Apply_Resources_AreSummed()
        {
            var profile = CreateProfile();

            _applier.Apply(profile, _parser.Parse("resource:soft_money:100,resource:soft_money:50"), _configDistributor, DateTime.UtcNow);

            Assert.That(profile.Resources["soft_money"], Is.EqualTo(150));
        }

        [Test]
        public void Apply_FirstCharacterCopy_UnlocksWithoutCopies()
        {
            var profile = CreateProfile();

            _applier.Apply(profile, _parser.Parse("character:1:1"), _configDistributor, DateTime.UtcNow);

            Assert.That(profile.Characters, Has.Count.EqualTo(1));
            Assert.That(profile.Characters[0].Copies, Is.EqualTo(0));
            Assert.That(profile.Characters[0].UpgradesApplied, Is.EqualTo(0));
        }

        [Test]
        public void Apply_EnoughCopies_AppliesUpgradeAndOverflow()
        {
            var profile = CreateProfile();

            _applier.Apply(profile, _parser.Parse("character:1:12"), _configDistributor, DateTime.UtcNow);

            var character = profile.Characters[0];

            Assert.That(character.UpgradesApplied, Is.EqualTo(1));
            Assert.That(character.Copies, Is.EqualTo(0));
            Assert.That(profile.Resources["hard_money"], Is.EqualTo(500));
        }

        [Test]
        public void Apply_UnknownCharacter_IsSkipped()
        {
            var profile = CreateProfile();

            var entries = _applier.Apply(profile, _parser.Parse("character:999:1"), _configDistributor, DateTime.UtcNow);

            Assert.That(entries, Is.Empty);
            Assert.That(profile.Characters, Is.Empty);
        }

        [Test]
        public void Apply_AccountFlag_IsCounted()
        {
            var profile = CreateProfile();

            _applier.Apply(profile, _parser.Parse("account:start_perk_choice_single:1"), _configDistributor, DateTime.UtcNow);
            _applier.Apply(profile, _parser.Parse("account:start_perk_choice_single:2"), _configDistributor, DateTime.UtcNow);

            Assert.That(profile.Flags["start_perk_choice_single"], Is.EqualTo(3));
        }

        [Test]
        public void Apply_Summon_AddsCopiesAfterUnlock()
        {
            var profile = CreateProfile();

            _applier.Apply(profile, _parser.Parse("summon:1:1"), _configDistributor, DateTime.UtcNow);
            _applier.Apply(profile, _parser.Parse("summon:1:4"), _configDistributor, DateTime.UtcNow);

            Assert.That(profile.Summons, Has.Count.EqualTo(1));
            Assert.That(profile.Summons[0].Copies, Is.EqualTo(4));
            Assert.That(profile.Summons[0].Level, Is.EqualTo(1));
        }

        [Test]
        public void Apply_RunScopedReward_DoesNotTouchProfile()
        {
            var profile = CreateProfile();

            var entries = _applier.Apply(profile, _parser.Parse("status:1:1"), _configDistributor, DateTime.UtcNow);

            Assert.That(entries, Is.Empty);
            Assert.That(profile.Resources, Is.Empty);
            Assert.That(profile.Bonuses, Is.Empty);
        }

        [Test]
        public void Apply_WritesLedgerEntries()
        {
            var profile = CreateProfile();

            var entries = _applier.Apply(profile, _parser.Parse("resource:soft_money:10,character:1:1"), _configDistributor, DateTime.UtcNow);

            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].Type, Is.EqualTo("resource"));
            Assert.That(entries[0].Key, Is.EqualTo("soft_money"));
            Assert.That(entries[1].Type, Is.EqualTo("character"));
        }

        private PlayerProfileDocument CreateProfile()
        {
            return new PlayerProfileDocument { Id = "usr_unit", Rev = 1 };
        }
    }
}
