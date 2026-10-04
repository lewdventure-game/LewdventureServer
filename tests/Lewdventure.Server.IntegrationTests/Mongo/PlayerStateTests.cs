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
    public sealed class PlayerStateTests
    {
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
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _environment.DisposeAsync();
        }

        [Test]
        [Order(1)]
        public async Task DeviceAuth_SameDevice_KeepsSameAccount()
        {
            var authService = _environment.Services.GetRequiredService<PlayerAuthService>();

            var first = await authService.AuthenticateDeviceAsync("device-a", "1.0.0", "XX", CancellationToken.None);
            var second = await authService.AuthenticateDeviceAsync("device-a", "1.0.0", "XX", CancellationToken.None);

            Assert.That(first.Succeeded, Is.True, first.Error);
            Assert.That(second.Succeeded, Is.True, second.Error);
            Assert.That(second.UserId, Is.EqualTo(first.UserId));
            Assert.That(second.RefreshToken, Is.Not.EqualTo(first.RefreshToken));
        }

        [Test]
        [Order(2)]
        public async Task Refresh_RotatesTokenAndRejectsOldOne()
        {
            var authService = _environment.Services.GetRequiredService<PlayerAuthService>();
            var session = await authService.AuthenticateDeviceAsync("device-b", "1.0.0", "XX", CancellationToken.None);

            var refreshed = await authService.RefreshAsync(session.UserId, session.RefreshToken, "XX", CancellationToken.None);
            var reused = await authService.RefreshAsync(session.UserId, session.RefreshToken, "XX", CancellationToken.None);
            var chained = await authService.RefreshAsync(session.UserId, refreshed.RefreshToken, "XX", CancellationToken.None);

            Assert.That(refreshed.Succeeded, Is.True, refreshed.Error);
            Assert.That(reused.Succeeded, Is.False);
            Assert.That(chained.Succeeded, Is.True, chained.Error);
        }

        [Test]
        [Order(3)]
        public async Task Profile_IsCreatedOnFirstRead()
        {
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();

            var profile = await profileService.GetOrCreateAsync("usr_profile", CancellationToken.None);
            var again = await profileService.GetOrCreateAsync("usr_profile", CancellationToken.None);

            Assert.That(profile.Rev, Is.EqualTo(1));
            Assert.That(again.Rev, Is.EqualTo(1));
            Assert.That(again.Resources, Is.Empty);
            Assert.That(again.Loadout.Summons, Is.Empty);
        }

        [Test]
        [Order(2)]
        public async Task StartContent_IsGrantedOnceAndFillsLoadout()
        {
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();

            var profile = await profileService.GetOrCreateAsync("usr_starter", _configDistributor, CancellationToken.None);
            var again = await profileService.GetOrCreateAsync("usr_starter", _configDistributor, CancellationToken.None);

            if (_configDistributor.Constants.TryGet("start_content", out _) == false)
                Assert.Ignore("в фикстуре конфигов нет константы start_content");

            Assert.That(profile.Characters, Is.Not.Empty, "новый профиль должен получить стартового персонажа");
            Assert.That(profile.Loadout.CharacterId, Is.EqualTo(profile.Characters[0].ConfigId));
            Assert.That(again.Characters.Count, Is.EqualTo(profile.Characters.Count), "стартовый набор выдаётся один раз");
        }

        [Test]
        [Order(3)]
        public async Task ResetProgress_ClearsProfileAndKeepsAccount()
        {
            var authService = _environment.Services.GetRequiredService<PlayerAuthService>();
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var dataService = _environment.Services.GetRequiredService<PlayerDataService>();
            var userRepository = _environment.Services.GetRequiredService<UserRepository>();
            var session = await authService.AuthenticateDeviceAsync("device-reset", "test", "XX", CancellationToken.None);
            var userId = session.UserId;

            var profile = await profileService.GetOrCreateAsync(userId, CancellationToken.None);

            profile.Resources["soft_money"] = 500;

            await _environment.Services.GetRequiredService<PlayerProfileRepository>().ReplaceAsync(profile, profile.Rev, CancellationToken.None);

            var reset = await dataService.ResetProgressAsync(userId, "test", CancellationToken.None);
            var fresh = await profileService.GetOrCreateAsync(userId, CancellationToken.None);
            var user = await userRepository.GetAsync(userId, CancellationToken.None);

            Assert.That(reset.Profiles, Is.EqualTo(1));
            Assert.That(reset.Users, Is.EqualTo(0));
            Assert.That(fresh.Resources, Is.Empty);
            Assert.That(user, Is.Not.Null, "аккаунт должен остаться после сброса прогресса");
        }

        [Test]
        [Order(3)]
        public async Task DeleteAccount_RemovesAccountToo()
        {
            var authService = _environment.Services.GetRequiredService<PlayerAuthService>();
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var dataService = _environment.Services.GetRequiredService<PlayerDataService>();
            var userRepository = _environment.Services.GetRequiredService<UserRepository>();
            var session = await authService.AuthenticateDeviceAsync("device-delete", "test", "XX", CancellationToken.None);
            var userId = session.UserId;

            await profileService.GetOrCreateAsync(userId, CancellationToken.None);

            var deletion = await dataService.DeleteAsync(userId, "test", CancellationToken.None);
            var user = await userRepository.GetAsync(userId, CancellationToken.None);

            Assert.That(deletion.Profiles, Is.EqualTo(1));
            Assert.That(deletion.Users, Is.EqualTo(1));
            Assert.That(user, Is.Null);
        }

        [Test]
        [Order(4)]
        public async Task Loadout_WithoutOwnedContent_IsRejected()
        {
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var update = new LoadoutUpdate(1, new Dictionary<string, string>(), new[] { 1 });

            var result = await profileService.UpdateLoadoutAsync("usr_loadout", update, string.Empty, _configDistributor, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Character 1 is not unlocked."));
            Assert.That(result.Errors, Has.Some.Contains("Summon 1 is not owned."));
        }

        [Test]
        [Order(5)]
        public async Task Loadout_WithOwnedContent_IsStoredOncePerRequestId()
        {
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var profile = await profileService.GetOrCreateAsync("usr_owner", CancellationToken.None);

            profile.Characters.Add(new PlayerCharacterDocument { ConfigId = 1, Copies = 1 });
            profile.Summons.Add(new PlayerSummonDocument { ConfigId = 1, Copies = 1, Level = 1 });

            var expectedRev = profile.Rev;

            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);

            var update = new LoadoutUpdate(1, new Dictionary<string, string>(), new[] { 1 });
            var first = await profileService.UpdateLoadoutAsync("usr_owner", update, "request-1", _configDistributor, CancellationToken.None);
            var repeated = await profileService.UpdateLoadoutAsync("usr_owner", update, "request-1", _configDistributor, CancellationToken.None);

            Assert.That(first.Succeeded, Is.True, string.Join("; ", first.Errors));
            Assert.That(first.Profile!.Loadout.CharacterId, Is.EqualTo(1));
            Assert.That(first.Profile.Loadout.Summons, Is.EqualTo(new[] { 1 }));
            Assert.That(repeated.Succeeded, Is.True);
            Assert.That(repeated.Profile!.Rev, Is.EqualTo(first.Profile.Rev));
        }

        [Test]
        [Order(6)]
        public async Task Grant_AppliesRewardsOnceAndWritesLedger()
        {
            var rewardService = _environment.Services.GetRequiredService<PlayerRewardService>();
            var ledgerRepository = _environment.Services.GetRequiredService<PlayerLedgerRepository>();

            var first = await rewardService.GrantAsync("usr_reward", "resource:soft_money:100,character:1:1", "test", "grant-1", _configDistributor, CancellationToken.None);
            var repeated = await rewardService.GrantAsync("usr_reward", "resource:soft_money:100,character:1:1", "test", "grant-1", _configDistributor, CancellationToken.None);
            var ledger = await ledgerRepository.ListAsync("usr_reward", 10, CancellationToken.None);

            Assert.That(first.Succeeded, Is.True, string.Join("; ", first.Errors));
            Assert.That(first.Profile!.Resources["soft_money"], Is.EqualTo(100));
            Assert.That(first.Profile.Characters, Has.Count.EqualTo(1));
            Assert.That(repeated.Succeeded, Is.True);
            Assert.That(repeated.Profile!.Resources["soft_money"], Is.EqualTo(100));
            Assert.That(repeated.Profile.Rev, Is.EqualTo(first.Profile.Rev));
            Assert.That(ledger, Has.Count.EqualTo(1));
            Assert.That(ledger[0].Entries, Has.Count.EqualTo(5));
        }

        [Test]
        [Order(7)]
        public async Task Grant_WithoutRequestId_AppliesEveryTime()
        {
            var rewardService = _environment.Services.GetRequiredService<PlayerRewardService>();

            await rewardService.GrantAsync("usr_reward_repeat", "resource:hard_money:5", "test", string.Empty, _configDistributor, CancellationToken.None);
            var second = await rewardService.GrantAsync("usr_reward_repeat", "resource:hard_money:5", "test", string.Empty, _configDistributor, CancellationToken.None);

            Assert.That(second.Profile!.Resources["hard_money"], Is.EqualTo(10));
        }

        [Test]
        [Order(8)]
        public async Task Export_ReturnsProfileAndLedger()
        {
            var dataService = _environment.Services.GetRequiredService<PlayerDataService>();

            var export = await dataService.ExportAsync("usr_reward", CancellationToken.None);

            Assert.That(export.Profile, Is.Not.Null);
            Assert.That(export.Ledger, Is.Not.Empty);
        }

        [Test]
        [Order(9)]
        public async Task Delete_RemovesEverythingForUser()
        {
            var dataService = _environment.Services.GetRequiredService<PlayerDataService>();

            var deletion = await dataService.DeleteAsync("usr_reward", "test", CancellationToken.None);
            var export = await dataService.ExportAsync("usr_reward", CancellationToken.None);

            Assert.That(deletion.HasData, Is.True);
            Assert.That(deletion.Profiles, Is.EqualTo(1));
            Assert.That(export.Profile, Is.Null);
            Assert.That(export.Ledger, Is.Empty);
        }

        [Test]
        [Order(10)]
        public async Task Profile_StaleRevision_IsRejected()
        {
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var profile = await profileService.GetOrCreateAsync("usr_conflict", CancellationToken.None);

            var staleRev = profile.Rev - 1;

            Assert.That(await repository.ReplaceAsync(profile, staleRev, CancellationToken.None), Is.False);
        }
    }
}
