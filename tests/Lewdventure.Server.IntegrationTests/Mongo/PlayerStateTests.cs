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
        public async Task Refresh_ParallelSameToken_KeepsWinnerAndPrevious()
        {
            var authService = _environment.Services.GetRequiredService<PlayerAuthService>();
            var users = _environment.Services.GetRequiredService<UserRepository>();
            var tokenGenerator = _environment.Services.GetRequiredService<TokenGenerator>();

            for (int i = 0; i < 8; i++)
            {
                var session = await authService.AuthenticateDeviceAsync($"device-refresh-race-{i}", "1.0.0", "XX", CancellationToken.None);
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var first = RefreshAfter(start, authService, session.UserId, session.RefreshToken);
                var second = RefreshAfter(start, authService, session.UserId, session.RefreshToken);

                start.SetResult();

                var results = await Task.WhenAll(first, second);
                var wins = 0;

                if (results[0].Succeeded)
                    ++wins;

                if (results[1].Succeeded)
                    ++wins;

                Assert.That(wins, Is.EqualTo(1), $"iteration {i}");

                var winner = results[0].Succeeded ? results[0] : results[1];
                var user = await users.GetAsync(session.UserId, CancellationToken.None);

                Assert.That(user, Is.Not.Null);
                Assert.That(user!.Devices.Count, Is.EqualTo(1));
                Assert.That(user.Devices[0].RefreshTokenHash, Is.EqualTo(tokenGenerator.Hash(winner.RefreshToken)));
                Assert.That(user.Devices[0].PreviousRefreshTokenHash, Is.EqualTo(tokenGenerator.Hash(session.RefreshToken)));

                var chained = await authService.RefreshAsync(winner.UserId, winner.RefreshToken, "XX", CancellationToken.None);

                Assert.That(chained.Succeeded, Is.True, chained.Error);
            }
        }

        [Test]
        public async Task DeviceAuth_ParallelSameDevice_KeepsPreviousToken()
        {
            var authService = _environment.Services.GetRequiredService<PlayerAuthService>();
            var users = _environment.Services.GetRequiredService<UserRepository>();
            var tokenGenerator = _environment.Services.GetRequiredService<TokenGenerator>();

            for (int i = 0; i < 8; i++)
            {
                var deviceId = $"device-login-race-{i}";

                await authService.AuthenticateDeviceAsync(deviceId, "1.0.0", "XX", CancellationToken.None);

                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var first = DeviceAfter(start, authService, deviceId);
                var second = DeviceAfter(start, authService, deviceId);

                start.SetResult();

                var results = await Task.WhenAll(first, second);

                Assert.That(results[0].Succeeded, Is.True, results[0].Error);
                Assert.That(results[1].Succeeded, Is.True, results[1].Error);
                Assert.That(results[0].UserId, Is.EqualTo(results[1].UserId));

                var user = await users.GetAsync(results[0].UserId, CancellationToken.None);

                Assert.That(user, Is.Not.Null);
                Assert.That(user!.Devices.Count, Is.EqualTo(1));

                var currentHash = user.Devices[0].RefreshTokenHash;
                var previousHash = user.Devices[0].PreviousRefreshTokenHash;
                var leftHash = tokenGenerator.Hash(results[0].RefreshToken);
                var rightHash = tokenGenerator.Hash(results[1].RefreshToken);
                var leftIsCurrent = string.Equals(leftHash, currentHash, StringComparison.Ordinal);
                var rightIsCurrent = string.Equals(rightHash, currentHash, StringComparison.Ordinal);

                Assert.That(leftIsCurrent == rightIsCurrent, Is.False, $"iteration {i}");
                Assert.That(previousHash, Is.EqualTo(leftIsCurrent ? rightHash : leftHash));
            }
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
        [Order(5)]
        public async Task Loadout_EmptySummonSlot_IsStoredAndNegativeIdIsRejected()
        {
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var profile = await profileService.GetOrCreateAsync("usr_summon_slots", CancellationToken.None);

            profile.Characters.Add(new PlayerCharacterDocument { ConfigId = 1, Copies = 1 });
            profile.Summons.Add(new PlayerSummonDocument { ConfigId = 1, Copies = 1, Level = 1 });

            var expectedRev = profile.Rev;

            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);

            var withEmptySlot = new LoadoutUpdate(1, new Dictionary<string, string>(), new[] { 0, 1, 0 });
            var stored = await profileService.UpdateLoadoutAsync("usr_summon_slots", withEmptySlot, "slots-1", _configDistributor, CancellationToken.None);

            Assert.That(stored.Succeeded, Is.True, string.Join("; ", stored.Errors));
            Assert.That(stored.Profile!.Loadout.Summons, Is.EqualTo(new[] { 0, 1, 0 }));

            var withNegativeId = new LoadoutUpdate(1, new Dictionary<string, string>(), new[] { -1, 1 });
            var rejected = await profileService.UpdateLoadoutAsync("usr_summon_slots", withNegativeId, "slots-2", _configDistributor, CancellationToken.None);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Errors, Has.Some.Contains("Summon slot 0 has invalid id -1"));
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
        [Order(6)]
        public async Task Grant_ReservationInProgress_IsNotApplied()
        {
            var rewardService = _environment.Services.GetRequiredService<PlayerRewardService>();
            var idempotency = _environment.Services.GetRequiredService<IdempotencyRepository>();

            await ReserveAsync(idempotency, "usr_reward_busy", "grant-busy", DateTime.UtcNow);

            var result = await rewardService.GrantAsync("usr_reward_busy", "resource:soft_money:100", "test", "grant-busy", _configDistributor, CancellationToken.None);

            Assert.That(result.Conflict, Is.True, "незавершённый резерв не должен давать повторную выдачу");
        }

        [Test]
        [Order(6)]
        public async Task Grant_StaleReservation_IsTakenOverAndAppliedOnce()
        {
            var rewardService = _environment.Services.GetRequiredService<PlayerRewardService>();
            var idempotency = _environment.Services.GetRequiredService<IdempotencyRepository>();

            await ReserveAsync(idempotency, "usr_reward_stale", "grant-stale", DateTime.UtcNow.AddMinutes(-5));

            var first = await rewardService.GrantAsync("usr_reward_stale", "resource:soft_money:100", "test", "grant-stale", _configDistributor, CancellationToken.None);
            var repeated = await rewardService.GrantAsync("usr_reward_stale", "resource:soft_money:100", "test", "grant-stale", _configDistributor, CancellationToken.None);

            Assert.That(first.Succeeded, Is.True, string.Join("; ", first.Errors));
            Assert.That(first.Profile!.Resources["soft_money"], Is.EqualTo(100));
            Assert.That(repeated.Profile!.Resources["soft_money"], Is.EqualTo(100));
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

        private async Task<AuthSessionResult> RefreshAfter(TaskCompletionSource start, PlayerAuthService authService, string userId, string refreshToken)
        {
            await start.Task;

            return await authService.RefreshAsync(userId, refreshToken, "XX", CancellationToken.None);
        }

        private async Task<AuthSessionResult> DeviceAfter(TaskCompletionSource start, PlayerAuthService authService, string deviceId)
        {
            await start.Task;

            return await authService.AuthenticateDeviceAsync(deviceId, "1.0.0", "XX", CancellationToken.None);
        }

        private async Task ReserveAsync(IdempotencyRepository idempotency, string userId, string requestId, DateTime updatedAt)
        {
            Assert.That(await idempotency.TryReserveAsync(new IdempotencyDocument
            {
                Id = idempotency.CreateKey(userId, requestId),
                UserId = userId,
                RequestId = requestId,
                Action = "test",
                CreatedAt = updatedAt,
                UpdatedAt = updatedAt,
            }, CancellationToken.None), Is.True);
        }
    }
}
