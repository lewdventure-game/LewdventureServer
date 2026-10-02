using Server.Logging;
using Server.Battles;
using Server.GameConfigs;
using Server.Services;
using Server.Skills;
using Tests.Unit.Api;

namespace Tests.Unit.Battles
{
    [TestFixture]
    public sealed class EffectRegistryConsistencyTests
    {
        private readonly EffectParameterRegistry _effectParameterRegistry = new();
        private readonly SkillComponentRegistry _skillComponentRegistry = new();

        private BattleComposition _battleComposition = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var coreLog = new SilentCoreLog();
            var hasher = new ConfigSnapshotHasher();
            var source = new FileConfigSnapshotSource(new ConfigSnapshotSerializer(hasher));
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);
            var configSet = new GameConfigComposition(coreLog).GameConfigSetBuilder.Build(snapshot, "test").ConfigSet!;

            _battleComposition = new BattleComposition(configSet.Distributor, coreLog);
        }

        [Test]
        public void PerkCreators_MatchValidatorDescriptors()
        {
            Assert.That(Sorted(_battleComposition.PerkFactory.KnownTypeKeys), Is.EqualTo(Sorted(_effectParameterRegistry.PerkTypeKeys)));
        }

        [Test]
        public void SkillCreators_MatchValidatorDescriptors()
        {
            Assert.That(Sorted(_battleComposition.SkillFactory.KnownTypeKeys), Is.EqualTo(Sorted(_effectParameterRegistry.SkillTypeKeys)));
        }

        [Test]
        public void SkillTriggerEvaluators_MatchValidatorRegistry()
        {
            Assert.That(Sorted(_battleComposition.SkillTriggerRegistry.TypeKeys), Is.EqualTo(Sorted(Names(_skillComponentRegistry.Triggers))));
        }

        [Test]
        public void SkillActionExecutors_MatchValidatorRegistry()
        {
            Assert.That(Sorted(_battleComposition.SkillActionRegistry.TypeKeys), Is.EqualTo(Sorted(Names(_skillComponentRegistry.Actions))));
        }

        private List<string> Names(IReadOnlyList<SkillComponentDefinition> definitions)
        {
            var names = new List<string>(definitions.Count);

            for (int i = 0; i < definitions.Count; i++)
                names.Add(definitions[i].Name);

            return names;
        }

        private List<string> Sorted(IReadOnlyCollection<string> keys)
        {
            var sorted = new List<string>(keys.Count);

            foreach (var key in keys)
                sorted.Add(key.ToLowerInvariant());

            sorted.Sort(StringComparer.Ordinal);

            return sorted;
        }
    }
}
