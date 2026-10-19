using System.ComponentModel.DataAnnotations;

namespace Server.Api.Options
{
    internal sealed class BalanceOptions
    {
        public const string SectionName = "Balance";

        public bool Enabled { get; set; }

        [Range(1, 100000)]
        public int MaxSeriesBattles { get; set; } = 2000;

        [Range(1, 10000)]
        public int MaxLevelRuns { get; set; } = 300;

        [Range(1, 100)]
        public int MaxSweepPoints { get; set; } = 25;

        [Range(1, 1000000)]
        public int MaxSweepBattles { get; set; } = 20000;

        [Range(1, 100000)]
        public int MaxSweepRuns { get; set; } = 3000;
    }
}
