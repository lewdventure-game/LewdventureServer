using Server.GameConfigs;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class UploadedSheetsSnapshotBuilderTests
    {
        private readonly ConfigDomainNames _configDomainNames = new();

        private UploadedSheetsSnapshotBuilder _builder = null!;

        [SetUp]
        public void SetUp()
        {
            _builder = new UploadedSheetsSnapshotBuilder(_configDomainNames, new ConfigSnapshotHasher(), new SheetRowsConverter(new ConfigRangeReader()));
        }

        [Test]
        public void TryBuild_AllDomains_ProducesStableVersion()
        {
            var sheets = CreateSheets();

            Assert.That(_builder.TryBuild(sheets, out var snapshot, out var errors), Is.True, string.Join("; ", errors));
            Assert.That(snapshot.Domains, Has.Count.EqualTo(_configDomainNames.Ordered.Count));
            Assert.That(snapshot.SourceKind, Is.EqualTo(UploadedSheetsSnapshotBuilder.SourceKind));
            Assert.That(snapshot.Version, Does.StartWith("sha256:"));

            _builder.TryBuild(sheets, out var repeated, out _);

            Assert.That(repeated.Version, Is.EqualTo(snapshot.Version));
        }

        [Test]
        public void TryBuild_MissingDomain_ReportsError()
        {
            var sheets = CreateSheets();

            sheets.RemoveAt(0);

            Assert.That(_builder.TryBuild(sheets, out _, out var errors), Is.False);
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain(_configDomainNames.Ordered[0]));
        }

        [Test]
        public void TryBuild_SheetWithoutRange_ReportsError()
        {
            var sheets = CreateSheets();

            sheets[1] = new UploadedSheet(sheets[1].Domain, sheets[1].SpreadsheetId, string.Empty, sheets[1].Values);

            Assert.That(_builder.TryBuild(sheets, out _, out var errors), Is.False);
            Assert.That(errors[0], Does.Contain("range"));
        }

        private List<UploadedSheet> CreateSheets()
        {
            var domains = _configDomainNames.Ordered;
            var sheets = new List<UploadedSheet>(domains.Count);

            for (int i = 0; i < domains.Count; i++)
            {
                var values = new List<IReadOnlyList<object?>>
                {
                    new List<object?> { "is_off", "id" },
                    new List<object?> { "FALSE", (i + 1).ToString() },
                };

                sheets.Add(new UploadedSheet(domains[i], "sheet-" + i, "B:C", values));
            }

            return sheets;
        }
    }
}
