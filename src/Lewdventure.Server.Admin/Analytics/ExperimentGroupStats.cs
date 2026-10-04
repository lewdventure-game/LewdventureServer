namespace Server.Admin.Analytics
{
    public sealed class ExperimentGroupStats
    {
        public string Group { get; set; } = string.Empty;

        public long Users { get; set; }

        public double EventsPerUser { get; set; }

        public double RetentionD1 { get; set; }

        public double RetentionD3 { get; set; }

        public double RetentionD7 { get; set; }

        public long RunsStarted { get; set; }

        public long RunsCompleted { get; set; }

        public double RunCompletionRate { get; set; }

        public long Battles { get; set; }

        public double BattleWinRate { get; set; }

        public double AverageStageReached { get; set; }
    }
}
