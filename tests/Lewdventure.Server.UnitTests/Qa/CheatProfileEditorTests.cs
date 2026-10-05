using Server.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Battles;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Infrastructure.Qa;
using Server.Services;
using Server.Skills;
using Tests.Unit.Api;

namespace Tests.Unit.Qa
{
    [TestFixture]
    public sealed class CheatProfileEditorTests
    {
        private const int CharacterId = 1;
        private const int SummonId = 1;

        private readonly BattleRewardParser _parser = new(new SilentCoreLog());
        private readonly ProgressionLimits _limits = new();

        private IConfigDistributor _configDistributor = null!;
        private RewardApplier _rewardApplier = null!;
        private CheatProfileEditor _editor = null!;
        private ResourceKeyCollector _resourceKeyCollector = null!;
        private CheatPresetBuilder _presetBuilder = null!;

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
                new ConfigSnapshotValidator(domainNames, new EffectParametersValidator(new EffectParameterRegistry()), new EnemyDataValidator(), new SkillComponentValidator(new SkillComponentRegistry())),
                new SilentCoreLog());
            var result = builder.Build(snapshot, "test");

            _configDistributor = result.ConfigSet!.Distributor;
            _rewardApplier = new RewardApplier(_parser, new BonusWorkModeParser(new SilentCoreLog()), NullLogger<RewardApplier>.Instance);
            _editor = new CheatProfileEditor(_limits, _rewardApplier);
            _resourceKeyCollector = new ResourceKeyCollector(_parser);
            _presetBuilder = new CheatPresetBuilder(_resourceKeyCollector);
        }

        [Test]
        public void SetResource_SetsExactValueAndLogsDelta()
        {
            var profile = CreateProfile();
            var entries = new List<PlayerLedgerEntryDocument>();

            profile.Resources["soft_money"] = 700;

            var succeeded = _editor.SetResource(profile, "soft_money", 0, entries, out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(profile.Resources["soft_money"], Is.EqualTo(0));
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Amount, Is.EqualTo(-700));
        }

        [TestCase("Soft_Money")]
        [TestCase("soft money")]
        [TestCase("")]
        public void SetResource_InvalidKey_Fails(string key)
        {
            var succeeded = _editor.SetResource(CreateProfile(), key, 10, new List<PlayerLedgerEntryDocument>(), out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void SetResource_Negative_Fails()
        {
            var succeeded = _editor.SetResource(CreateProfile(), "soft_money", -1, new List<PlayerLedgerEntryDocument>(), out _);

            Assert.That(succeeded, Is.False);
        }

        [Test]
        public void SetFlag_Zero_RemovesFlag()
        {
            var profile = CreateProfile();
            var entries = new List<PlayerLedgerEntryDocument>();

            profile.Flags["start_perk_choice_single"] = 2;

            _editor.SetFlag(profile, "start_perk_choice_single", 0, entries, out _);

            Assert.That(profile.Flags.ContainsKey("start_perk_choice_single"), Is.False);
            Assert.That(entries[0].Amount, Is.EqualTo(-2));
        }

        [Test]
        public void SetCharacterPromote_NewCharacterToMax_GrantsAllPromoteRewards()
        {
            var profile = CreateProfile();
            var entries = new List<PlayerLedgerEntryDocument>();

            Assert.That(_configDistributor.Characters.TryGet(CharacterId, out var mapper), Is.True);

            var maxLevel = _limits.GetMaxPromoteLevel(mapper!, _configDistributor);
            var succeeded = _editor.SetCharacterPromote(profile, CharacterId, maxLevel, _configDistributor, DateTime.UtcNow, entries, out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(maxLevel, Is.EqualTo(20));
            Assert.That(profile.Characters[0].PromoteLevel, Is.EqualTo(20));
            Assert.That(profile.Characters[0].UnlockedSceneIds, Does.Contain(101));
            Assert.That(profile.Resources["harem_points"], Is.EqualTo(2150));
            Assert.That(profile.Bonuses[0].Count, Is.EqualTo(13));
        }

        [Test]
        public void SetCharacterPromote_Lowering_KeepsRewards()
        {
            var profile = CreateProfile();
            var entries = new List<PlayerLedgerEntryDocument>();

            _editor.SetCharacterPromote(profile, CharacterId, 5, _configDistributor, DateTime.UtcNow, entries, out _);

            var haremPoints = profile.Resources["harem_points"];

            _editor.SetCharacterPromote(profile, CharacterId, 2, _configDistributor, DateTime.UtcNow, entries, out _);

            Assert.That(profile.Characters[0].PromoteLevel, Is.EqualTo(2));
            Assert.That(profile.Resources["harem_points"], Is.EqualTo(haremPoints));
        }

        [TestCase(0)]
        [TestCase(21)]
        public void SetCharacterPromote_OutOfRange_Fails(int level)
        {
            var succeeded = _editor.SetCharacterPromote(CreateProfile(), CharacterId, level, _configDistributor, DateTime.UtcNow, new List<PlayerLedgerEntryDocument>(), out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(error, Does.Contain("from 1 to 20"));
        }

        [Test]
        public void SetCharacterPromote_UnknownCharacter_Fails()
        {
            var succeeded = _editor.SetCharacterPromote(CreateProfile(), 999, 1, _configDistributor, DateTime.UtcNow, new List<PlayerLedgerEntryDocument>(), out _);

            Assert.That(succeeded, Is.False);
        }

        [Test]
        public void SetSummon_NewSummonWithMastery_GrantsMasteryRewardsOfEveryLevel()
        {
            var profile = CreateProfile();
            var entries = new List<PlayerLedgerEntryDocument>();

            var expected = CreateProfile();

            Assert.That(_configDistributor.Summons.TryGet(SummonId, out var mapper), Is.True);

            _rewardApplier.Apply(expected, _parser.Parse($"summon:{SummonId}:1"), _configDistributor, DateTime.UtcNow);

            for (int level = 2; level <= 8; level++)
            {
                expected.Summons[0].MasteryLevel = level;
                _rewardApplier.ApplySummonMasteryRewards(expected, expected.Summons[0], mapper!, _configDistributor, DateTime.UtcNow);
            }

            var succeeded = _editor.SetSummon(profile, SummonId, null, 8, null, _configDistributor, DateTime.UtcNow, entries, out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(profile.Summons[0].MasteryLevel, Is.EqualTo(8));
            Assert.That(profile.Summons[0].UnlockedSceneIds, Is.EqualTo(expected.Summons[0].UnlockedSceneIds));
            Assert.That(profile.Summons[0].UnlockedSceneIds, Does.Contain(101).And.Contain(102));
            Assert.That(profile.Resources["harem_points"], Is.EqualTo(expected.Resources["harem_points"]));
            Assert.That(profile.Bonuses[0].BonusId, Is.EqualTo(1));
        }

        [Test]
        public void SetSummon_LevelAboveMax_Fails()
        {
            Assert.That(_configDistributor.Summons.TryGet(SummonId, out var mapper), Is.True);

            var maxLevel = _limits.GetMaxSummonLevel(mapper!, _configDistributor);
            var succeeded = _editor.SetSummon(CreateProfile(), SummonId, maxLevel + 1, null, null, _configDistributor, DateTime.UtcNow, new List<PlayerLedgerEntryDocument>(), out _);

            Assert.That(1 < maxLevel, Is.True);
            Assert.That(succeeded, Is.False);
        }

        [Test]
        public void SetSummon_SkillLevelAboveMax_IsClampedPerSkill()
        {
            var profile = CreateProfile();

            Assert.That(_configDistributor.Summons.TryGet(SummonId, out var mapper), Is.True);

            _editor.SetSummon(profile, SummonId, null, null, int.MaxValue, _configDistributor, DateTime.UtcNow, new List<PlayerLedgerEntryDocument>(), out _);

            var summon = profile.Summons[0];

            Assert.That(summon.SkillLevels, Has.Count.EqualTo(mapper!.SkillIds.Length));

            for (int i = 0; i < summon.SkillLevels.Count; i++)
                Assert.That(summon.SkillLevels[i], Is.EqualTo(_limits.GetMaxSkillLevel(mapper, i, _configDistributor)));
        }

        [Test]
        public void SetEquipmentLevel_NotOwned_Fails()
        {
            var succeeded = _editor.SetEquipmentLevel(CreateProfile(), "eq_missing", 2, _configDistributor, new List<PlayerLedgerEntryDocument>(), out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(error, Does.Contain("eq_missing"));
        }

        [Test]
        public void MaxOut_RaisesEverythingOwnedToMax()
        {
            var profile = CreateProfile();
            var entries = new List<PlayerLedgerEntryDocument>();
            var equipment = _configDistributor.Equipments.Collection[0];

            _rewardApplier.Apply(profile, _parser.Parse($"character:{CharacterId}:1,summon:{SummonId}:1,equipment:{equipment.Id}:1"), _configDistributor, DateTime.UtcNow);
            _editor.MaxOut(profile, _configDistributor, DateTime.UtcNow, entries);

            Assert.That(_configDistributor.Summons.TryGet(SummonId, out var summonMapper), Is.True);
            Assert.That(profile.Characters[0].PromoteLevel, Is.EqualTo(20));
            Assert.That(profile.Summons[0].Level, Is.EqualTo(_limits.GetMaxSummonLevel(summonMapper!, _configDistributor)));
            Assert.That(profile.Summons[0].MasteryLevel, Is.EqualTo(_limits.GetMaxMasteryLevel(summonMapper!, _configDistributor)));
            Assert.That(profile.Equipment[0].Level, Is.EqualTo(_limits.GetMaxEquipmentLevel(equipment, _configDistributor)));
            Assert.That(entries, Is.Not.Empty);
        }

        [Test]
        public void ResourceKeys_ComeFromConfigsWithoutRunExperience()
        {
            var keys = _resourceKeyCollector.Collect(_configDistributor);

            Assert.That(keys, Does.Contain("summon_lvl"));
            Assert.That(keys, Does.Contain("harem_points"));
            Assert.That(keys, Does.Contain("hard_money"));
            Assert.That(keys, Does.Not.Contain("exp_lvl"));
            Assert.That(keys, Is.Ordered.Using((IComparer<string>)StringComparer.Ordinal));
        }

        [Test]
        public void Preset_Everything_SkipsOwnedUnitsAndAddsResources()
        {
            var profile = CreateProfile();
            var rewards = new List<BattleReward>();

            _rewardApplier.Apply(profile, _parser.Parse($"summon:{SummonId}:1"), _configDistributor, DateTime.UtcNow);

            var succeeded = _presetBuilder.TryBuild(CheatPresetBuilder.EverythingPreset, 500, profile, _configDistributor, rewards, out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(Count(rewards, BattleRewardType.Character), Is.EqualTo(_configDistributor.Characters.Collection.Count));
            Assert.That(Count(rewards, BattleRewardType.Summon), Is.EqualTo(_configDistributor.Summons.Collection.Count - 1));
            Assert.That(Count(rewards, BattleRewardType.Equipment), Is.EqualTo(_configDistributor.Equipments.Collection.Count));
            Assert.That(Count(rewards, BattleRewardType.Resource), Is.EqualTo(_resourceKeyCollector.Collect(_configDistributor).Count));
        }

        [Test]
        public void Preset_Unknown_Fails()
        {
            var succeeded = _presetBuilder.TryBuild("gold", 1, CreateProfile(), _configDistributor, new List<BattleReward>(), out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(error, Does.Contain("gold"));
        }

        private int Count(List<BattleReward> rewards, BattleRewardType type)
        {
            var count = 0;

            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].Type == type)
                    count++;
            }

            return count;
        }

        private PlayerProfileDocument CreateProfile()
        {
            return new PlayerProfileDocument { Id = "usr_cheat" };
        }
    }
}
