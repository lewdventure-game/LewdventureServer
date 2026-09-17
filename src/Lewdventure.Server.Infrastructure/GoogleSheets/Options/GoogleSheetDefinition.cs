using System.ComponentModel.DataAnnotations;

namespace Server.Infrastructure.GoogleSheets
{
    internal sealed class GoogleSheetDefinition
    {
        [Required]
        public string Domain { get; set; } = string.Empty;

        [Required]
        public string SpreadsheetId { get; set; } = string.Empty;

        [Required]
        public string Range { get; set; } = string.Empty;
    }
}
