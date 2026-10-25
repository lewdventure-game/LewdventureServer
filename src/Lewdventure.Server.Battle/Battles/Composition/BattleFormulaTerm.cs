namespace Server.Battles
{
    public sealed class BattleFormulaTerm
    {
        public int SourceUnitId { get; set; }

        public float BaseDamage { get; set; }

        public float DamageRatio { get; set; } = 1f;

        public bool IsCritical { get; set; }

        public float CriticalMultiplier { get; set; } = 1f;

        public float Defence { get; set; }

        public float Damage { get; set; }
    }
}
