using Server.Battles;

namespace Server.Api.Endpoints
{
    internal sealed class RunStepResponse
    {
        public string EventType { get; set; } = string.Empty;

        public int EventId { get; set; }

        public int StageId { get; set; }

        public string LocKey { get; set; } = string.Empty;

        public string LocKeyStart { get; set; } = string.Empty;

        public string LocKeyEnd { get; set; } = string.Empty;

        public int ExperienceGained { get; set; }

        public List<int> LevelUps { get; set; } = new();

        public List<RunRewardResponse> AppliedRewards { get; set; } = new();

        public long ProfileRev { get; set; }

        public bool RunCompleted { get; set; }

        public bool RunFailed { get; set; }

        public IBattleScriptResponse? Battle { get; set; }

        public IBattleReplayData? BattleInput { get; set; }

        public string BattleDigest { get; set; } = string.Empty;

        public int BattleStepCount { get; set; }
    }
}
