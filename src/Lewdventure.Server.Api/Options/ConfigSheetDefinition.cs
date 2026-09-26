using System.ComponentModel.DataAnnotations;

namespace Server.Api.Options
{
    internal sealed class ConfigSheetDefinition
    {
        [Required]
        public string Domain { get; set; } = string.Empty;

        [Required]
        public string SpreadsheetId { get; set; } = string.Empty;
    }
}
