using Server.Infrastructure.GoogleSheets;

namespace Tests.Unit.Infrastructure
{
    [TestFixture]
    public sealed class GoogleSheetsOptionsValidatorTests
    {
        private readonly string[] _domains =
        {
            "Constants", "Characters", "Bonuses", "Statuses", "Summons", "Summon_levels", "Mastery", "Enemies",
            "Equipments", "Story_levels", "Story_stages", "Story_events", "Exp_levels_patterns", "Perks", "Perk_groups",
        };

        [Test]
        public void Validate_AllDomains_Succeeds()
        {
            var result = new GoogleSheetsOptionsValidator().Validate(null, CreateOptions(_domains.Length));

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Validate_MissingDomain_Fails()
        {
            var result = new GoogleSheetsOptionsValidator().Validate(null, CreateOptions(_domains.Length - 1));

            Assert.That(result.Failed, Is.True);
            Assert.That(result.FailureMessage, Does.Contain("Perk_groups"));
        }

        [Test]
        public void Validate_DuplicateDomain_Fails()
        {
            var options = CreateOptions(_domains.Length);

            options.Sheets.Add(new GoogleSheetDefinition { Domain = "Constants", SpreadsheetId = "id", Range = "B:E" });

            var result = new GoogleSheetsOptionsValidator().Validate(null, options);

            Assert.That(result.Failed, Is.True);
        }

        [Test]
        public void Validate_NoCredentials_Fails()
        {
            var options = CreateOptions(_domains.Length);

            options.CredentialsPath = string.Empty;
            options.CredentialsJson = string.Empty;

            var result = new GoogleSheetsOptionsValidator().Validate(null, options);

            Assert.That(result.Failed, Is.True);
        }

        private GoogleSheetsOptions CreateOptions(int domainCount)
        {
            var options = new GoogleSheetsOptions();

            for (int i = 0; i < domainCount; i++)
                options.Sheets.Add(new GoogleSheetDefinition { Domain = _domains[i], SpreadsheetId = $"sheet-{i}", Range = "B:Z" });

            return options;
        }
    }
}
