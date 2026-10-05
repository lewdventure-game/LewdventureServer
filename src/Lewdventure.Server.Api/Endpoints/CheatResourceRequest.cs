namespace Server.Api.Endpoints
{
    internal sealed class CheatResourceRequest
    {
        public string Key { get; set; } = string.Empty;

        public long Amount { get; set; }
    }
}
