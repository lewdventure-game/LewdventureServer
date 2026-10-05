namespace Server.Api.Endpoints
{
    internal sealed class QaRunResponse
    {
        public string RunId { get; set; } = string.Empty;

        public int StoryLevelId { get; set; }

        public string ConfigVersion { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int StageNumber { get; set; }

        public List<QaRunStageResponse> Stages { get; set; } = new();

        public float CurrentHealth { get; set; }

        public int Experience { get; set; }

        public int ExperienceLevel { get; set; }

        public int PendingLevelUps { get; set; }

        public string PendingChoice { get; set; } = string.Empty;

        public List<int> Perks { get; set; } = new();

        public List<int> Statuses { get; set; } = new();

        public List<RunBonusResponse> Bonuses { get; set; } = new();
    }
}
