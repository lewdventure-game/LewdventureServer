namespace Server.Infrastructure.Mongo.Runs
{
    internal sealed class RunQaDocument
    {
        public float HeroDamageMultiplier { get; set; } = 1f;

        public float SummonDamageMultiplier { get; set; } = 1f;

        public float EnemyDamageMultiplier { get; set; } = 1f;

        public float EnemyHealthMultiplier { get; set; } = 1f;

        public float EnemyStartHealth { get; set; }

        public bool KillEnemies { get; set; }

        public bool KillHero { get; set; }

        public int ActionRewardProgress { get; set; }
    }
}
