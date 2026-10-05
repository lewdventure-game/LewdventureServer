namespace Server.Admin.Backend.Models
{
    public sealed class QaRunModel
    {
        public string RunId { get; set; } = string.Empty;

        public int StoryLevelId { get; set; }

        public string ConfigVersion { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int StageNumber { get; set; }

        public List<QaRunStageModel> Stages { get; set; } = new();

        public float CurrentHealth { get; set; }

        public int Experience { get; set; }

        public int ExperienceLevel { get; set; }

        public int PendingLevelUps { get; set; }

        public string PendingChoice { get; set; } = string.Empty;

        public List<int> Perks { get; set; } = new();

        public List<int> Statuses { get; set; } = new();

        public List<QaRunBonusModel> Bonuses { get; set; } = new();
    }
}
