namespace Server.Runs
{
    internal sealed class RunBattleFinish
    {
        public int StageIndex { get; set; }

        public float HealthBefore { get; set; }

        public int StageId { get; set; }

        public int EventId { get; set; }

        public string Outcome { get; set; } = string.Empty;

        public int Steps { get; set; }
    }
}
