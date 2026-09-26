using System.ComponentModel.DataAnnotations;

namespace Server.Api.Options
{
    internal sealed class ConfigSheetsOptions
    {
        public const string SectionName = "ConfigSheets";

        [MinLength(1)]
        public List<ConfigSheetDefinition> Sheets { get; set; } = new();
    }
}
