namespace Server.Admin.Backend.Models
{
    public sealed class BalanceEventModel
    {
        public int Id { get; set; }

        public string Type { get; set; } = string.Empty;

        public List<int> EnemyIds { get; set; } = new();
    }
}
