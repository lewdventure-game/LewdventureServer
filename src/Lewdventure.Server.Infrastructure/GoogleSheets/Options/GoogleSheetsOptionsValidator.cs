using Microsoft.Extensions.Options;
using Server.GameConfigs;

namespace Server.Infrastructure.GoogleSheets
{
    internal sealed class GoogleSheetsOptionsValidator : IValidateOptions<GoogleSheetsOptions>
    {
        private readonly string[] _requiredDomains =
        {
            ConfigDomainNames.Constants,
            ConfigDomainNames.Characters,
            ConfigDomainNames.Bonuses,
            ConfigDomainNames.Statuses,
            ConfigDomainNames.Summons,
            ConfigDomainNames.SummonLevels,
            ConfigDomainNames.Mastery,
            ConfigDomainNames.Enemies,
            ConfigDomainNames.Equipments,
            ConfigDomainNames.StoryLevels,
            ConfigDomainNames.StoryStages,
            ConfigDomainNames.StoryEvents,
            ConfigDomainNames.ExpLevelsPatterns,
            ConfigDomainNames.Perks,
            ConfigDomainNames.PerkGroups,
        };

        public ValidateOptionsResult Validate(string? name, GoogleSheetsOptions options)
        {
            var failures = new List<string>();
            var domains = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < options.Sheets.Count; i++)
            {
                var sheet = options.Sheets[i];

                if (string.IsNullOrWhiteSpace(sheet.Domain) || string.IsNullOrWhiteSpace(sheet.SpreadsheetId) || string.IsNullOrWhiteSpace(sheet.Range))
                    failures.Add($"GoogleSheets:Sheets:{i} must define Domain, SpreadsheetId and Range.");

                if (domains.Add(sheet.Domain) == false)
                    failures.Add($"GoogleSheets:Sheets domain {sheet.Domain} is duplicated.");
            }

            for (int i = 0; i < _requiredDomains.Length; i++)
            {
                if (domains.Contains(_requiredDomains[i]) == false)
                    failures.Add($"GoogleSheets:Sheets domain {_requiredDomains[i]} is missing.");
            }

            if (string.IsNullOrWhiteSpace(options.CredentialsPath) && string.IsNullOrWhiteSpace(options.CredentialsJson))
                failures.Add("GoogleSheets requires CredentialsPath or CredentialsJson.");

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
