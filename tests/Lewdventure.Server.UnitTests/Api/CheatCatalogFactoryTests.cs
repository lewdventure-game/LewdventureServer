using Microsoft.Extensions.Logging.Abstractions;
using Server.Api.Endpoints;
using Server.Battles;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.Players;
using Server.Infrastructure.Qa;
using Server.Logging;
using Server.Services;
using Server.Skills;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class CheatCatalogFactoryTests
    {
        private const int AccountBonusId = 1;
        private const int RunBonusId = 3;

        private IConfigDistributor _configDistributor = null!;
        private CheatCatalogFactory _factory = null!;

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
            var parser = new BattleRewardParser(new SilentCoreLog());
            var workModeParser = new BonusWorkModeParser(new SilentCoreLog());
            var rewardApplier = new RewardApplier(parser, workModeParser, NullLogger<RewardApplier>.Instance);

            _configDistributor = builder.Build(snapshot, "test").ConfigSet!.Distributor;
            _factory = new CheatCatalogFactory(workModeParser, new ProgressionLimits(), new ResourceKeyCollector(parser), rewardApplier);
        }

        [Test]
        public void Bonuses_AreSplitBetweenAccountAndRun()
        {
            var catalog = _factory.Create("test", _configDistributor);

            Assert.That(Contains(catalog.Bonuses, AccountBonusId), Is.True);
            Assert.That(Contains(catalog.RunBonuses, AccountBonusId), Is.False, "бонус с permanent в игре уходит на аккаунт, в забег его класть нельзя");
            Assert.That(Contains(catalog.RunBonuses, RunBonusId), Is.True);
            Assert.That(Contains(catalog.Bonuses, RunBonusId), Is.False);
        }

        [Test]
        public void PerkGroups_AreListed()
        {
            var catalog = _factory.Create("test", _configDistributor);

            Assert.That(catalog.PerkGroups.Count, Is.EqualTo(_configDistributor.PerkGroups.Collection.Count));
            Assert.That(0 < catalog.PerkGroups.Count, Is.True);
        }

        private bool Contains(List<CheatCatalogItem> items, int id)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id == id)
                    return true;
            }

            return false;
        }
    }
}
