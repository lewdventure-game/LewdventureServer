namespace Server.Api.Endpoints
{
    internal sealed class ClientConfigBundlePayload
    {
        public string Version { get; set; } = string.Empty;

        public string ShortVersion { get; set; } = string.Empty;

        public List<ClientConfigBundleEntry> Configs { get; set; } = new();
    }
}
