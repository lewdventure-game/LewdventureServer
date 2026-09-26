namespace Server.Api.Endpoints
{
    internal sealed class ConfigUploadSheet
    {
        public string Domain { get; set; } = string.Empty;

        public string SpreadsheetId { get; set; } = string.Empty;

        public string Range { get; set; } = string.Empty;

        public List<List<string>> Values { get; set; } = new();
    }
}
