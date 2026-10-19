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
    }
}
