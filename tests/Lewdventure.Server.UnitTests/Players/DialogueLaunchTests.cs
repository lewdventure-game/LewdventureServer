using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.Players;
using Server.Logging;
using Server.Services;
using Server.Skills;
using Tests.Unit.Api;

namespace Tests.Unit.Players
{
    [TestFixture]
    public sealed class DialogueLaunchTests
    {
        private readonly DialogueLaunchKeys _launchKeys = new();
        private readonly DialoguePathValidator _pathValidator = new();

        private IConfigDistributor _configDistributor = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var source = new FileConfigSnapshotSource(new ConfigSnapshotSerializer(new ConfigSnapshotHasher()));
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);
            var builder = new GameConfigSetBuilder(
                new BonusWorkModeParser(new SilentCoreLog()),
                new ConfigRowsParser(new ConfigRowLocator(new ConfigRangeReader())),
                new ConfigSnapshotValidator(new ConfigDomainNames(), new DialogueDataValidator(), new EffectParametersValidator(new EffectParameterRegistry()), new EnemyDataValidator(), new ProgressionDataValidator(), new SkillComponentValidator(new SkillComponentRegistry())),
                new SilentCoreLog());

            _configDistributor = builder.Build(snapshot, "test").ConfigSet!.Distributor;
        }

        [Test]
        public void LevelKey_RoundTripsWithNormalizedTrigger()
        {
            var launchKey = _launchKeys.ForLevel(2, 3, " On_Stage_Entered:2 ");

            Assert.That(launchKey, Is.EqualTo("level:2:3:on_stage_entered:2"));
            Assert.That(_launchKeys.TryParseLevel(launchKey, out var storyLevelId, out var dialogueId, out var trigger), Is.True);
            Assert.That(storyLevelId, Is.EqualTo(2));
            Assert.That(dialogueId, Is.EqualTo(3));
            Assert.That(trigger, Is.EqualTo("on_stage_entered:2"));
            Assert.That(_launchKeys.IsReward(launchKey), Is.False);
        }

        [Test]
        public void RewardKey_IsUniquePerLaunch()
        {
            var first = _launchKeys.ForReward(1);
            var second = _launchKeys.ForReward(1);

            Assert.That(first, Does.StartWith("reward:1:"));
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(_launchKeys.IsReward(first), Is.True);
            Assert.That(_launchKeys.TryParseLevel(first, out _, out _, out _), Is.False);
        }

        [TestCase("level:2:3:on_stage_entered:2", "on_stage_entered:2")]
        [TestCase("level:1:1:on_win", "on_win")]
        [TestCase("reward:1:5f0c", "reward")]
        [TestCase("other:1", "")]
        public void Trigger_IsReadFromLaunchKey(string launchKey, string trigger)
        {
            Assert.That(_launchKeys.ResolveTrigger(launchKey), Is.EqualTo(trigger));
        }

        [TestCase("level:x:1:on_start")]
        [TestCase("level:1:1")]
        [TestCase("level:1:1:")]
        [TestCase("other:1:1:on_start")]
        public void LevelKey_RejectsBrokenKeys(string launchKey)
        {
            Assert.That(_launchKeys.TryParseLevel(launchKey, out _, out _, out _), Is.False);
        }

        [Test]
        public void Path_FollowsPicksToTheEnd()
        {
            Assert.That(_pathValidator.Validate(1, new[] { 1 }, _configDistributor), Is.Empty);
            Assert.That(_pathValidator.Validate(1, new[] { 2 }, _configDistributor), Is.Empty);
            Assert.That(_pathValidator.Validate(3, new[] { 1, 1, 4 }, _configDistributor), Is.Empty);
        }

        [Test]
        public void Path_WithoutPickAtChoice_IsError()
        {
            Assert.That(_pathValidator.Validate(1, Array.Empty<int>(), _configDistributor), Does.Contain("step 4 needs a pick"));
        }

        [Test]
        public void Path_WithUnknownChoice_IsError()
        {
            Assert.That(_pathValidator.Validate(1, new[] { 3 }, _configDistributor), Does.Contain("has no choice 3"));
        }

        [Test]
        public void Path_WithExtraPicks_IsError()
        {
            Assert.That(_pathValidator.Validate(1, new[] { 1, 1 }, _configDistributor), Does.Contain("ended before pick 2"));
        }

        [Test]
        public void Path_OfMissingDialogue_IsError()
        {
            Assert.That(_pathValidator.Validate(77, Array.Empty<int>(), _configDistributor), Does.Contain("has no step 1"));
        }
    }
}
