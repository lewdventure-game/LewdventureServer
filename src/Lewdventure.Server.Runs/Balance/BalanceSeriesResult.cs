namespace Server.Balance
{
    internal sealed class BalanceSeriesResult
    {
        public int Battles { get; set; }

        public int Wins { get; set; }

        public float WinRate { get; set; }

        public ulong FirstSeed { get; set; }

        public BalanceDistribution Turns { get; set; } = new();

        public BalanceDistribution TurnsOnWin { get; set; } = new();

        public BalanceDistribution HeroHealthEndOnWin { get; set; } = new();

        public BalanceDistribution DamagePerTurn { get; set; } = new();

        public BalanceDistribution DamageTaken { get; set; } = new();

        public Dictionary<string, float> DamageShareBySource { get; set; } = new(StringComparer.Ordinal);

        public float CriticalHitsPerBattle { get; set; }

        public float MissesPerBattle { get; set; }

        public List<ulong> LossSeeds { get; set; } = new();
    }
}
