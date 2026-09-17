using System.ComponentModel.DataAnnotations;

namespace Server.Infrastructure.GoogleSheets
{
    internal sealed class GoogleSheetsOptions
    {
        public const string SectionName = "GoogleSheets";

        public string CredentialsPath { get; set; } = "google-credentials.json";

        public string CredentialsJson { get; set; } = string.Empty;

        [Required]
        public string ApplicationName { get; set; } = "GameConfigReader";

        [Range(0, 10000)]
        public int DelayBetweenSheetsMs { get; set; } = 150;

        [Range(1, 10)]
        public int MaxRetries { get; set; } = 3;

        [MinLength(1)]
        public List<GoogleSheetDefinition> Sheets { get; set; } = new();
    }
}
