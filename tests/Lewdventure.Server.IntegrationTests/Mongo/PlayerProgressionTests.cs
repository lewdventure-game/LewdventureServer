using Microsoft.Extensions.DependencyInjection;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Services;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class PlayerProgressionTests
    {
        private const string UserId = "usr_progression";
        private const int SummonId = 1;

        private readonly FixturePaths _paths = new();

        private MongoTestEnvironment _environment = null!;
        private IConfigDistributor _configDistributor = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _environment = new MongoTestEnvironment();

            await _environment.StartAsync(new PlayerServicesRegistrar().Register);
            await _environment.Services.GetRequiredService<MongoStartupInitializer>().InitializeAsync(CancellationToken.None);

            var snapshot = await _environment.Services.GetRequiredService<FileConfigSnapshotSource>().LoadAsync(_paths.FixturePath, CancellationToken.None);
            var buildResult = _environment.Services.GetRequiredService<GameConfigSetBuilder>().Build(snapshot, "test");
            var provider = _environment.Services.GetRequiredService<IGameConfigSetProvider>();

            provider.Swap(buildResult.ConfigSet!);

            _configDistributor = provider.Current.Distributor;

            await PrepareAsync();
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _environment.DisposeAsync();
        }

        [Test]
        [Order(1)]
        public async Task SummonLevel_SpendsResourcesAndRaisesLevel()
        {
            var service = _environment.Services.GetRequiredService<PlayerProgressionService>();

            var result = await service.UpgradeSummonLevelAsync(UserId, SummonId, "level-1", _configDistributor, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Profile!.Summons[0].Level, Is.EqualTo(2));
            Assert.That(result.Profile.Resources["summon_lvl"], Is.EqualTo(98));
        }

        [Test]
        [Order(2)]
        public async Task SummonLevel_SameRequestId_IsAppliedOnce()
        {
            var service = _environment.Services.GetRequiredService<PlayerProgressionService>();

            var result = await service.UpgradeSummonLevelAsync(UserId, SummonId, "level-1", _configDistributor, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Profile!.Summons[0].Level, Is.EqualTo(2));
            Assert.That(result.Profile.Resources["summon_lvl"], Is.EqualTo(98));
        }

        [Test]
        [Order(3)]
        public async Task SummonMastery_SpendsCopies()
        {
            var service = _environment.Services.GetRequiredService<PlayerProgressionService>();

            var result = await service.UpgradeSummonMasteryAsync(UserId, SummonId, "mastery-1", _configDistributor, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Profile!.Summons[0].MasteryLevel, Is.EqualTo(2));
            Assert.That(result.Profile.Summons[0].Copies, Is.EqualTo(4));
        }

        [Test]
        [Order(4)]
        public async Task SummonLevel_WithoutResources_IsRejected()
        {
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var service = _environment.Services.GetRequiredService<PlayerProgressionService>();
            var profile = await profileService.GetOrCreateAsync(UserId, CancellationToken.None);
            var expectedRev = profile.Rev;

            profile.Resources["summon_lvl"] = 0;
            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);

            var result = await service.UpgradeSummonLevelAsync(UserId, SummonId, "level-2", _configDistributor, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Not enough summon_lvl"));
        }

        [Test]
        [Order(5)]
        public async Task SummonProgression_ForUnownedSummon_IsRejected()
        {
            var service = _environment.Services.GetRequiredService<PlayerProgressionService>();

            var result = await service.UpgradeSummonMasteryAsync(UserId, 2, "mastery-2", _configDistributor, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("not owned").Or.Some.Contains("missing in configs"));
        }

        [Test]
        [Order(6)]
        public async Task EquipmentLevel_WithoutConfig_IsRejected()
        {
            var service = _environment.Services.GetRequiredService<PlayerProgressionService>();

            var result = await service.UpgradeEquipmentLevelAsync(UserId, "eq_unknown", "equipment-1", _configDistributor, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("not owned"));
        }

        private async Task PrepareAsync()
        {
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profile = await profileService.GetOrCreateAsync(UserId, CancellationToken.None);
            var expectedRev = profile.Rev;

            profile.Summons.Add(new PlayerSummonDocument { ConfigId = SummonId, Level = 1, Copies = 5 });
            profile.Resources["summon_lvl"] = 100;
            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);
        }
    }
}
