namespace Server.Admin.Backend.Models
{
    public sealed class BalanceSeriesResultModel
    {
        public int Battles { get; set; }

        public int Wins { get; set; }

        public float WinRate { get; set; }

        public ulong FirstSeed { get; set; }

        public BalanceDistributionModel Turns { get; set; } = new();

        public BalanceDistributionModel TurnsOnWin { get; set; } = new();

        public BalanceDistributionModel HeroHealthEndOnWin { get; set; } = new();

        public BalanceDistributionModel DamagePerTurn { get; set; } = new();

        public BalanceDistributionModel DamageTaken { get; set; } = new();

        public Dictionary<string, float> DamageShareBySource { get; set; } = new();

        public float CriticalHitsPerBattle { get; set; }

        public float MissesPerBattle { get; set; }

        public List<ulong> LossSeeds { get; set; } = new();

        public string RunId { get; set; } = string.Empty;
    }
}
