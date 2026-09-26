using Microsoft.Extensions.Logging.Abstractions;
using Server.Bonuses;
using Server.GameConfigs;
using Tests.Unit.Api;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class ConfigSnapshotTests
    {
        private readonly ConfigSnapshotHasher _hasher = new();
        private readonly ConfigDomainNames _domainNames = new();

        [Test]
        public async Task Fixture_RoundTrip_KeepsVersion()
        {
            var serializer = new ConfigSnapshotSerializer(_hasher);
            var source = new FileConfigSnapshotSource(serializer);
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);
            var path = Path.Combine(Path.GetTempPath(), $"lewdventure-snapshot-{Guid.NewGuid():N}.json");

            try
            {
                await source.SaveAsync(path, snapshot, CancellationToken.None);

                var reloaded = await source.LoadAsync(path, CancellationToken.None);

                Assert.That(reloaded.Version, Is.EqualTo(snapshot.Version));
                Assert.That(reloaded.Domains, Has.Count.EqualTo(16));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Hasher_SameContent_SameVersion()
        {
            var first = _hasher.ComputeVersion(CreateDomains("[{\"id\":\"1\"}]"));
            var second = _hasher.ComputeVersion(CreateDomains("[{\"id\":\"1\"}]"));

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.StartWith("sha256:"));
            Assert.That(_hasher.ToShortVersion(first), Has.Length.EqualTo(16));
        }

        [Test]
        public void Hasher_DifferentContent_DifferentVersion()
        {
            var first = _hasher.ComputeVersion(CreateDomains("[{\"id\":\"1\"}]"));
            var second = _hasher.ComputeVersion(CreateDomains("[{\"id\":\"2\"}]"));

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void Serializer_DeclaredVersionMismatch_Throws()
        {
            var serializer = new ConfigSnapshotSerializer(_hasher);
            var text = "{\"format\":\"lewdventure-config-snapshot\",\"formatVersion\":1,\"version\":\"sha256:bad\",\"domains\":[]}";

            Assert.Throws<InvalidDataException>(() => serializer.Deserialize(text));
        }

        [Test]
        public void Builder_MissingDomain_Fails()
        {
            var builder = CreateBuilder();
            var snapshot = new GameConfigSnapshot("sha256:test", DateTime.UtcNow, "test", new List<ConfigSnapshotDomain>());

            var result = builder.Build(snapshot, "test");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Has.Count.GreaterThanOrEqualTo(16));
        }

        [Test]
        public async Task Builder_Fixture_Succeeds()
        {
            var source = new FileConfigSnapshotSource(new ConfigSnapshotSerializer(_hasher));
            var snapshot = await source.LoadAsync(new ApiDirectoryLocator().FindFixture(), CancellationToken.None);

            var result = CreateBuilder().Build(snapshot, "test");

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.ConfigSet!.Version, Is.EqualTo(snapshot.Version));
            Assert.That(result.ConfigSet.Distributor.Constants.Collection, Is.Not.Empty);
        }

        [Test]
        public void Diff_DetectsAddedRemovedChanged()
        {
            var diff = new ConfigSnapshotDiff(_domainNames);
            var before = new GameConfigSnapshot("a", DateTime.UtcNow, "test", CreateDomains("[{\"id\":\"1\",\"v\":\"1\"},{\"id\":\"2\",\"v\":\"1\"}]"));
            var after = new GameConfigSnapshot("b", DateTime.UtcNow, "test", CreateDomains("[{\"id\":\"1\",\"v\":\"2\"},{\"id\":\"3\",\"v\":\"1\"}]"));

            var result = diff.Compare(before, after);
            var constants = result[0];

            Assert.That(constants.Changed, Is.EquivalentTo(new[] { "1" }));
            Assert.That(constants.Added, Is.EquivalentTo(new[] { "3" }));
            Assert.That(constants.Removed, Is.EquivalentTo(new[] { "2" }));
        }

        private List<ConfigSnapshotDomain> CreateDomains(string constantsRows)
        {
            var domains = new List<ConfigSnapshotDomain>();
            var names = _domainNames.Ordered;

            for (int i = 0; i < names.Count; i++)
                domains.Add(new ConfigSnapshotDomain(names[i], "sheet", "B:Z", i == 0 ? constantsRows : "[]"));

            return domains;
        }

        private GameConfigSetBuilder CreateBuilder()
        {
            return new GameConfigSetBuilder(
                new BonusWorkModeParser(NullLogger<BonusWorkModeParser>.Instance),
                new ConfigRowsParser(),
                new ConfigSnapshotValidator(_domainNames),
                NullLogger<GameConfigSetBuilder>.Instance);
        }
    }
}
