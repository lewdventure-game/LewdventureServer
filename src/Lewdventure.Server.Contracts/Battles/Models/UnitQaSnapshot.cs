namespace Server.Battles
{
    public sealed class UnitQaSnapshot
    {
        public float StrikeDamageMultiplier { get; set; } = 1f;

        public float HealthMultiplier { get; set; } = 1f;

        public float StartHealth { get; set; }

        public bool KillAtStart { get; set; }

        public int ActionRewardProgress { get; set; }
    }
}
