namespace Server.Api.Endpoints
{
    internal sealed class CheatFlagRequest
    {
        public string Key { get; set; } = string.Empty;

        public int Value { get; set; }
    }
}
