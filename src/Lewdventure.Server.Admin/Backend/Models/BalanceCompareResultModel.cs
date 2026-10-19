namespace Server.Admin.Backend.Models
{
    public sealed class BalanceCompareResultModel
    {
        public string Kind { get; set; } = string.Empty;

        public string VersionA { get; set; } = string.Empty;

        public string VersionB { get; set; } = string.Empty;

        public BalanceSeriesResultModel? SeriesA { get; set; }

        public BalanceSeriesResultModel? SeriesB { get; set; }

        public BalanceLevelResultModel? LevelA { get; set; }

        public BalanceLevelResultModel? LevelB { get; set; }

        public string RunId { get; set; } = string.Empty;
    }
}
