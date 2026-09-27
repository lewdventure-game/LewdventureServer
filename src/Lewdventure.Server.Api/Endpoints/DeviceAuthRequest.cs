namespace Server.Api.Endpoints
{
    internal sealed class DeviceAuthRequest
    {
        public string DeviceId { get; set; } = string.Empty;

        public string ClientVersion { get; set; } = string.Empty;
    }
}
