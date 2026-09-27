using Server.Battles;

namespace Server.Runs
{
    internal sealed class RunStepOutcome
    {
        public string EventType { get; set; } = string.Empty;

        public int EventId { get; set; }

        public int StageId { get; set; }

        public string LocKey { get; set; } = string.Empty;

        public string LocKeyStart { get; set; } = string.Empty;

        public string LocKeyEnd { get; set; } = string.Empty;

        public int ExperienceGained { get; set; }

        public List<int> LevelUps { get; set; } = new();

        public IBattleScriptResponse? BattleScript { get; set; }

        public bool RunCompleted { get; set; }

        public bool RunFailed { get; set; }
    }
}
