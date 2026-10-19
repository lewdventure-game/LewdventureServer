namespace Server.Admin.Backend.Models
{
    public sealed class BalanceLevelStageStatsModel
    {
        public int StageIndex { get; set; }

        public int Reached { get; set; }

        public int Cleared { get; set; }

        public int Deaths { get; set; }

        public float DeathRate { get; set; }

        public float HealthAfter { get; set; }

        public float HealthShareAfter { get; set; }

        public float ExperienceLevelAfter { get; set; }

        public List<int> StageIds { get; set; } = new();
    }
}
