namespace Server.Api.Endpoints
{
    internal sealed class PlayerLoadoutRequest
    {
        public string RequestId { get; set; } = string.Empty;

        public int CharacterId { get; set; }

        public Dictionary<string, string> Equipment { get; set; } = new();

        public List<int> Summons { get; set; } = new();
    }
}
