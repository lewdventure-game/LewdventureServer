using Server.Balance;

namespace Server.Api.Endpoints
{
    internal sealed class BalanceCompareResponse : IBalanceRunResult
    {
        public string Kind { get; set; } = string.Empty;

        public string VersionA { get; set; } = string.Empty;

        public string VersionB { get; set; } = string.Empty;

        public BalanceSeriesResult? SeriesA { get; set; }

        public BalanceSeriesResult? SeriesB { get; set; }

        public BalanceLevelResult? LevelA { get; set; }

        public BalanceLevelResult? LevelB { get; set; }

        public string RunId { get; set; } = string.Empty;
    }
}
