namespace Server.Api.Endpoints
{
    internal sealed class QaRunCheatsResponse
    {
        public float HeroDamageMultiplier { get; set; }

        public float SummonDamageMultiplier { get; set; }

        public float EnemyDamageMultiplier { get; set; }

        public float EnemyHealthMultiplier { get; set; }

        public float EnemyStartHealth { get; set; }

        public bool KillEnemies { get; set; }

        public bool KillHero { get; set; }

        public int ActionRewardProgress { get; set; }
    }
}
