namespace Server.Api.Endpoints
{
    internal sealed class ConfigUploadRequest
    {
        public string Reason { get; set; } = string.Empty;

        public List<ConfigUploadSheet> Sheets { get; set; } = new();
    }
}
