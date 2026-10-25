namespace Server.Battles
{
    public sealed class BattleFormula
    {
        public string Kind { get; set; } = string.Empty;

        public BattlePhaseType Phase { get; set; }

        public int ActorId { get; set; }

        public int ActorSlot { get; set; }

        public int TargetId { get; set; }

        public int TargetSlot { get; set; }

        public int SourceId { get; set; }

        public float BaseDamage { get; set; }

        public float DamageMultiplier { get; set; } = 1f;

        public float QaMultiplier { get; set; } = 1f;

        public bool IsCritical { get; set; }

        public float CriticalMultiplier { get; set; } = 1f;

        public float Defence { get; set; }

        public float Result { get; set; }

        public List<BattleFormulaTerm> Terms { get; set; } = new();
    }
}
