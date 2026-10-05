using Microsoft.Extensions.DependencyInjection;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Infrastructure.Qa;
using Server.Services;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class QaToolsTests
    {
        private const string UserId = "usr_qa_tools";
        private const string OtherUserId = "usr_qa_other";
        private const string DeviceId = "device-qa-tools";
        private const string Actor = "admin:tester";

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
        public async Task Mark_SetsAliasAndClearsExperiment()
        {
            var service = _environment.Services.GetRequiredService<QaAccountService>();

            var result = await service.MarkAsync(UserId, " QA-Tools ", Actor, CancellationToken.None);
            var user = await _environment.Services.GetRequiredService<UserRepository>().GetAsync(UserId, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(user!.Qa!.Alias, Is.EqualTo("qa-tools"));
            Assert.That(user.Qa.MarkedBy, Is.EqualTo(Actor));
            Assert.That(user.Experiment, Is.Null);
        }

        [Test]
        [Order(2)]
        public async Task Mark_AliasOfAnotherUser_Fails()
        {
            var service = _environment.Services.GetRequiredService<QaAccountService>();

            var result = await service.MarkAsync(OtherUserId, "qa-tools", Actor, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain(UserId));
        }

        [Test]
        [Order(3)]
        public async Task Find_ByAliasDeviceAndUserId_ReturnsSameUser()
        {
            var service = _environment.Services.GetRequiredService<QaAccountService>();

            var byAlias = await service.FindAsync("QA-TOOLS", CancellationToken.None);
            var byDevice = await service.FindAsync(DeviceId, CancellationToken.None);
            var byUserId = await service.FindAsync(UserId, CancellationToken.None);
            var list = await service.ListAsync(CancellationToken.None);

            Assert.That(byAlias, Has.Count.EqualTo(1));
            Assert.That(byAlias[0].MatchedBy, Is.EqualTo(QaPlayerMatch.AliasMatch));
            Assert.That(byDevice[0].UserId, Is.EqualTo(UserId));
            Assert.That(byDevice[0].MatchedBy, Is.EqualTo(QaPlayerMatch.DeviceMatch));
            Assert.That(byUserId[0].UserId, Is.EqualTo(UserId));
            Assert.That(list, Has.Count.EqualTo(1));
        }

        [Test]
        [Order(4)]
        public async Task Cheats_SetResourceAndMaxOut_WriteProfileAndLedger()
        {
            var cheats = _environment.Services.GetRequiredService<CheatService>();

            var resource = await cheats.SetResourceAsync(UserId, "soft_money", 12345, Actor, CancellationToken.None);
            var preset = await cheats.GrantPresetAsync(UserId, CheatPresetBuilder.SummonsPreset, 1, Actor, _configDistributor, CancellationToken.None);
            var max = await cheats.MaxOutAsync(UserId, Actor, _configDistributor, CancellationToken.None);
            var ledger = await _environment.Services.GetRequiredService<PlayerLedgerRepository>().ListAsync(UserId, 50, CancellationToken.None);

            Assert.That(resource.Succeeded, Is.True, string.Join("; ", resource.Errors));
            Assert.That(resource.Profile!.Resources["soft_money"], Is.EqualTo(12345));
            Assert.That(preset.Succeeded, Is.True, string.Join("; ", preset.Errors));
            Assert.That(preset.Profile!.Summons, Has.Count.EqualTo(_configDistributor.Summons.Collection.Count));
            Assert.That(max.Succeeded, Is.True, string.Join("; ", max.Errors));
            Assert.That(max.Profile!.Characters[0].PromoteLevel, Is.EqualTo(20));
            Assert.That(ContainsAction(ledger, "cheat"), Is.True);
        }

        [Test]
        [Order(5)]
        public async Task Cheats_PlayerWithoutProfile_Fail()
        {
            var cheats = _environment.Services.GetRequiredService<CheatService>();

            var result = await cheats.SetResourceAsync(OtherUserId, "soft_money", 1, Actor, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors[0], Does.Contain("no profile"));
        }

        [Test]
        [Order(6)]
        public async Task Unmark_RemovesAlias()
        {
            var service = _environment.Services.GetRequiredService<QaAccountService>();

            var updated = await service.UnmarkAsync(UserId, Actor, CancellationToken.None);
            var byAlias = await service.FindAsync("qa-tools", CancellationToken.None);

            Assert.That(updated, Is.True);
            Assert.That(byAlias, Is.Empty);
        }

        [Test]
        [Order(7)]
        public async Task Template_SaveAndApply_CreatesTargetProfile()
        {
            var templates = _environment.Services.GetRequiredService<QaTemplateService>();

            var saveError = await templates.SaveAsync(UserId, "QA-Mid", "Середина", "тест", "test", Actor, CancellationToken.None);
            var applied = await templates.ApplyTemplateAsync("qa-mid", OtherUserId, Actor, CancellationToken.None);
            var list = await templates.ListAsync(CancellationToken.None);

            Assert.That(saveError, Is.Empty);
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0].Profile.Story.CurrentRunId, Is.Empty);
            Assert.That(applied.Succeeded, Is.True, string.Join("; ", applied.Errors));
            Assert.That(applied.Profile!.Id, Is.EqualTo(OtherUserId));
            Assert.That(applied.Profile.Resources["soft_money"], Is.EqualTo(12345));
            Assert.That(applied.Profile.Rev, Is.EqualTo(1));
        }

        [Test]
        [Order(8)]
        public async Task Copy_FromPlayer_ReplacesProfileAndRaisesRev()
        {
            var cheats = _environment.Services.GetRequiredService<CheatService>();
            var templates = _environment.Services.GetRequiredService<QaTemplateService>();

            await cheats.SetResourceAsync(OtherUserId, "soft_money", 1, Actor, CancellationToken.None);

            var copied = await templates.CopyProfileAsync(UserId, OtherUserId, Actor, CancellationToken.None);
            var same = await templates.CopyProfileAsync(UserId, UserId, Actor, CancellationToken.None);

            Assert.That(copied.Succeeded, Is.True, string.Join("; ", copied.Errors));
            Assert.That(copied.Profile!.Resources["soft_money"], Is.EqualTo(12345));
            Assert.That(copied.Profile.Rev, Is.EqualTo(3));
            Assert.That(same.Succeeded, Is.False);
        }

        [Test]
        [Order(9)]
        public async Task Template_Delete_RemovesIt()
        {
            var templates = _environment.Services.GetRequiredService<QaTemplateService>();

            Assert.That(await templates.DeleteAsync("qa-mid", Actor, CancellationToken.None), Is.True);
            Assert.That(await templates.ListAsync(CancellationToken.None), Is.Empty);
        }

        private bool ContainsAction(List<PlayerLedgerDocument> ledger, string action)
        {
            for (int i = 0; i < ledger.Count; i++)
            {
                if (ledger[i].Action == action)
                    return true;
            }

            return false;
        }

        private async Task PrepareAsync()
        {
            var users = _environment.Services.GetRequiredService<UserRepository>();
            var tokenGenerator = _environment.Services.GetRequiredService<TokenGenerator>();
            var now = DateTime.UtcNow;

            await users.InsertAsync(new UserDocument
            {
                Id = UserId,
                CreatedAt = now,
                UpdatedAt = now,
                Devices = new List<UserDeviceDocument> { new() { DeviceIdHash = tokenGenerator.Hash(DeviceId) } },
                Experiment = new UserExperimentDocument { ExperimentId = "exp", GroupId = "test", AssignedAt = now },
            }, CancellationToken.None);

            await users.InsertAsync(new UserDocument { Id = OtherUserId, CreatedAt = now, UpdatedAt = now }, CancellationToken.None);

            var profileService = _environment.Services.GetRequiredService<PlayerProfileService>();
            var repository = _environment.Services.GetRequiredService<PlayerProfileRepository>();
            var profile = await profileService.GetOrCreateAsync(UserId, CancellationToken.None);
            var expectedRev = profile.Rev;

            profile.Characters.Clear();
            profile.Characters.Add(new PlayerCharacterDocument { ConfigId = 1, PromoteLevel = 1, UnlockedAt = now });
            profile.Rev = expectedRev + 1;

            Assert.That(await repository.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);
        }
    }
}
