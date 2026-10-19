namespace Server.Api.Endpoints
{
    internal sealed class BalanceEventResponse
    {
        public int Id { get; set; }

        public string Type { get; set; } = string.Empty;

        public List<int> EnemyIds { get; set; } = new();
    }
}
