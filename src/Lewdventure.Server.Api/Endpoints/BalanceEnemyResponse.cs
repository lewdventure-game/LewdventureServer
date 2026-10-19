namespace Server.Api.Endpoints
{
    internal sealed class BalanceEnemyResponse
    {
        public int Id { get; set; }

        public string SkinName { get; set; } = string.Empty;

        public bool IsMelee { get; set; }

        public float Health { get; set; }

        public float Damage { get; set; }

        public List<int> SkillIds { get; set; } = new();
    }
}
