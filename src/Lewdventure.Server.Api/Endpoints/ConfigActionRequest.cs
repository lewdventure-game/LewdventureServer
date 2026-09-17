namespace Server.Api.Endpoints
{
    internal sealed class ConfigActionRequest
    {
        public string Version { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }
}
