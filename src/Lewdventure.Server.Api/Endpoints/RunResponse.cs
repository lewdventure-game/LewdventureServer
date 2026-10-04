namespace Server.Api.Endpoints
{
    internal sealed class RunResponse
    {
        public string RunId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int StoryLevelId { get; set; }

        public string ConfigVersion { get; set; } = string.Empty;

        public int StageIndex { get; set; }

        public int StagesTotal { get; set; }

        public int Experience { get; set; }

        public int ExperienceLevel { get; set; }

        public float CurrentHealth { get; set; }

        public List<int> Perks { get; set; } = new();

        public List<RunStageResponse> Stages { get; set; } = new();

        public List<RunBonusResponse> Bonuses { get; set; } = new();

        public RunChoiceResponse? PendingChoice { get; set; }

        public RunStepResponse? Step { get; set; }

        public bool Replayed { get; set; }
    }
}
