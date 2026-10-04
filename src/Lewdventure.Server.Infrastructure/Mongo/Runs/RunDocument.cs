namespace Server.Infrastructure.Mongo.Runs
{
    internal sealed class RunDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;
        public const string ActiveStatus = "active";
        public const string CompletedStatus = "completed";
        public const string FailedStatus = "failed";
        public const string AbandonedStatus = "abandoned";

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public long Rev { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string Status { get; set; } = ActiveStatus;

        public int StoryLevelId { get; set; }

        public string ConfigVersion { get; set; } = string.Empty;

        public long Seed { get; set; }

        public string BattleSeedKey { get; set; } = string.Empty;

        public string LastRequestId { get; set; } = string.Empty;

        public int RollIndex { get; set; }

        public int StageIndex { get; set; }

        public List<RunStageDocument> Stages { get; set; } = new();

        public int Experience { get; set; }

        public int ExperienceLevel { get; set; } = 1;

        public float CurrentHealth { get; set; }

        public int PendingLevelUps { get; set; }

        public List<int> Perks { get; set; } = new();

        public List<int> Statuses { get; set; } = new();

        public List<RunPerkUsageDocument> PerkUsages { get; set; } = new();

        public List<RunBonusDocument> Bonuses { get; set; } = new();

        public RunPendingChoiceDocument? PendingChoice { get; set; }
    }
}
