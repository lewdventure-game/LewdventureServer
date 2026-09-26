using Microsoft.Extensions.Options;
using Server.GameConfigs;

namespace Server.Api.Options
{
    internal sealed class ConfigSheetsOptionsValidator : IValidateOptions<ConfigSheetsOptions>
    {
        private readonly ConfigDomainNames _configDomainNames;

        public ConfigSheetsOptionsValidator(ConfigDomainNames configDomainNames)
        {
            _configDomainNames = configDomainNames;
        }

        public ValidateOptionsResult Validate(string? name, ConfigSheetsOptions options)
        {
            var failures = new List<string>();
            var domains = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < options.Sheets.Count; i++)
            {
                var sheet = options.Sheets[i];

                if (string.IsNullOrWhiteSpace(sheet.Domain) || string.IsNullOrWhiteSpace(sheet.SpreadsheetId))
                    failures.Add($"ConfigSheets:Sheets:{i} must define Domain and SpreadsheetId.");

                if (domains.Add(sheet.Domain) == false)
                    failures.Add($"ConfigSheets:Sheets domain {sheet.Domain} is duplicated.");
            }

            var required = _configDomainNames.Ordered;

            for (int i = 0; i < required.Count; i++)
            {
                if (domains.Contains(required[i]) == false)
                    failures.Add($"ConfigSheets:Sheets domain {required[i]} is missing.");
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
