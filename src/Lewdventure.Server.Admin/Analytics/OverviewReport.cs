namespace Server.Admin.Analytics
{
    public sealed class OverviewReport
    {
        public long ActivePlayers { get; set; }

        public long NewPlayers { get; set; }

        public long Sessions { get; set; }

        public long RunsStarted { get; set; }

        public long RunsCompleted { get; set; }

        public double BattleWinRate { get; set; }

        public List<ChartSeries> ActivePlayersSeries { get; set; } = new();

        public List<ChartSeries> NewPlayersSeries { get; set; } = new();

        public List<ChartSeries> RunsSeries { get; set; } = new();

        public List<ChartSeries> BattleWinRateSeries { get; set; } = new();

        public List<BreakdownRow> TopEvents { get; set; } = new();

        public List<BreakdownRow> Countries { get; set; } = new();

        public List<string> Errors { get; set; } = new();
    }
}
