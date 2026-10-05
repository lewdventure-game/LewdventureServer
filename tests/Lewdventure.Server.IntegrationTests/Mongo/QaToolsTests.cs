using Microsoft.Extensions.DependencyInjection;
using Server.GameConfigs;
using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Experiments;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Qa;
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

        [Test]
        [Order(10)]
        public async Task Diagnostics_TracesAndErrors_AreFilteredByUserAndCorrelation()
        {
            var repository = _environment.Services.GetRequiredService<QaDiagnosticsRepository>();
            var now = DateTime.UtcNow;

            await repository.InsertTracesAsync(new List<RequestTraceDocument>
            {
                new() { Id = "t1", CreatedAt = now, UserId = UserId, CorrelationId = "c1", Path = "/api/run/advance", StatusCode = 400 },
                new() { Id = "t2", CreatedAt = now.AddSeconds(1), UserId = UserId, CorrelationId = "c2", Path = "/api/player/profile", StatusCode = 200 },
                new() { Id = "t3", CreatedAt = now, UserId = OtherUserId, CorrelationId = "c3", Path = "/api/player/profile", StatusCode = 500 },
            }, CancellationToken.None);
            await repository.InsertErrorsAsync(new List<ServerErrorDocument>
            {
                new() { Id = "e1", CreatedAt = now, UserId = UserId, CorrelationId = "c1", Level = "Warning", Message = "bad" },
            }, CancellationToken.None);

            var all = await repository.ListTracesAsync(UserId, string.Empty, false, 10, CancellationToken.None);
            var errorsOnly = await repository.ListTracesAsync(UserId, string.Empty, true, 10, CancellationToken.None);
            var byCorrelation = await repository.ListTracesAsync(string.Empty, "c1", false, 10, CancellationToken.None);
            var errors = await repository.ListErrorsAsync(string.Empty, "c1", 10, CancellationToken.None);

            Assert.That(all, Has.Count.EqualTo(2));
            Assert.That(all[0].Id, Is.EqualTo("t2"));
            Assert.That(errorsOnly, Has.Count.EqualTo(1));
            Assert.That(byCorrelation[0].Id, Is.EqualTo("t1"));
            Assert.That(errors[0].Message, Is.EqualTo("bad"));
        }

        [Test]
        [Order(11)]
        public async Task Import_Profile_ReplacesTargetProfile()
        {
            var templates = _environment.Services.GetRequiredService<QaTemplateService>();
            var source = await _environment.Services.GetRequiredService<PlayerProfileRepository>().GetAsync(UserId, CancellationToken.None);

            source!.Resources["soft_money"] = 4242;

            var imported = await templates.ImportProfileAsync(source, OtherUserId, Actor, CancellationToken.None);

            Assert.That(imported.Succeeded, Is.True, string.Join("; ", imported.Errors));
            Assert.That(imported.Profile!.Id, Is.EqualTo(OtherUserId));
            Assert.That(imported.Profile.Resources["soft_money"], Is.EqualTo(4242));
        }

        [Test]
        [Order(12)]
        public async Task ForceExperiment_QaAccountGetsGroupConfigAndIsNotCounted()
        {
            var service = _environment.Services.GetRequiredService<QaAccountService>();
            var registry = _environment.Services.GetRequiredService<ExperimentRegistry>();
            var users = _environment.Services.GetRequiredService<UserRepository>();

            registry.Replace(new List<ExperimentDocument>
            {
                new()
                {
                    Id = "exp-qa",
                    Status = ExperimentDocument.RunningStatus,
                    Groups = new List<ExperimentGroupDocument>
                    {
                        new() { Id = "test", SnapshotVersion = "sha256:qa-group", Status = ExperimentGroupDocument.FrozenStatus },
                        new() { Id = "gone", SnapshotVersion = "sha256:gone", Status = ExperimentGroupDocument.RemovedStatus },
                    },
                },
            });

            var notQa = await service.ForceExperimentAsync(OtherUserId, "exp-qa", "test", Actor, CancellationToken.None);

            await service.MarkAsync(UserId, "qa-tools", Actor, CancellationToken.None);

            var removedGroup = await service.ForceExperimentAsync(UserId, "exp-qa", "gone", Actor, CancellationToken.None);
            var forced = await service.ForceExperimentAsync(UserId, "exp-qa", "test", Actor, CancellationToken.None);
            var assignment = await users.GetExperimentAsync(UserId, CancellationToken.None);
            var version = await _environment.Services.GetRequiredService<PlayerConfigVersionResolver>().ResolveAsync(UserId, CancellationToken.None);
            var participants = await users.CountInGroupAsync("exp-qa", "test", CancellationToken.None);

            Assert.That(notQa.Succeeded, Is.False);
            Assert.That(removedGroup.Succeeded, Is.False);
            Assert.That(forced.Succeeded, Is.True, forced.Error);
            Assert.That(assignment!.Forced, Is.True);
            Assert.That(assignment.ForcedBy, Is.EqualTo(Actor));
            Assert.That(version, Is.EqualTo("sha256:qa-group"));
            Assert.That(participants, Is.EqualTo(0));
        }

        [Test]
        [Order(13)]
        public async Task ForceExperiment_MasterAndUnmark_ClearAssignment()
        {
            var service = _environment.Services.GetRequiredService<QaAccountService>();
            var users = _environment.Services.GetRequiredService<UserRepository>();

            var master = await service.ForceExperimentAsync(UserId, string.Empty, string.Empty, Actor, CancellationToken.None);
            var afterMaster = await users.GetExperimentAsync(UserId, CancellationToken.None);

            await service.ForceExperimentAsync(UserId, "exp-qa", "test", Actor, CancellationToken.None);
            await service.UnmarkAsync(UserId, Actor, CancellationToken.None);

            var afterUnmark = await users.GetExperimentAsync(UserId, CancellationToken.None);

            _environment.Services.GetRequiredService<ExperimentRegistry>().Replace(new List<ExperimentDocument>());

            Assert.That(master.Succeeded, Is.True, master.Error);
            Assert.That(afterMaster, Is.Null);
            Assert.That(afterUnmark, Is.Null);
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
