using Microsoft.Extensions.Options;

namespace Server.Infrastructure.GoogleSheets
{
    internal sealed class GoogleSheetsOptionsValidator : IValidateOptions<GoogleSheetsOptions>
    {
        private readonly string[] _requiredDomains =
        {
            GoogleSheetDomains.Constants,
            GoogleSheetDomains.Characters,
            GoogleSheetDomains.Bonuses,
            GoogleSheetDomains.Statuses,
            GoogleSheetDomains.Summons,
            GoogleSheetDomains.SummonLevels,
            GoogleSheetDomains.Mastery,
            GoogleSheetDomains.Enemies,
            GoogleSheetDomains.Equipments,
            GoogleSheetDomains.StoryLevels,
            GoogleSheetDomains.StoryStages,
            GoogleSheetDomains.StoryEvents,
            GoogleSheetDomains.ExpLevelsPatterns,
            GoogleSheetDomains.Perks,
            GoogleSheetDomains.PerkGroups,
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
