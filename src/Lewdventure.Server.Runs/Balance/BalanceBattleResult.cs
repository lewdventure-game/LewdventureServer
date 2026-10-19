namespace Server.Balance
{
    internal sealed class BalanceBattleResult
    {
        public ulong Seed { get; set; }

        public string Outcome { get; set; } = string.Empty;

        public bool IsWin { get; set; }

        public int Turns { get; set; }

        public int Steps { get; set; }

        public float HeroHealthStart { get; set; }

        public float HeroHealthEnd { get; set; }

        public float DamageDealt { get; set; }

        public float DamageTaken { get; set; }

        public float Healing { get; set; }

        public int CriticalHits { get; set; }

        public int Misses { get; set; }

        public Dictionary<string, float> DamageBySource { get; set; } = new(StringComparer.Ordinal);

        public List<BalanceLogEntry> Log { get; set; } = new();
    }
}
