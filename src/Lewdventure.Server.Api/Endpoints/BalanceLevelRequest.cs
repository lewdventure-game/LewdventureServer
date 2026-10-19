using Server.Balance;

namespace Server.Api.Endpoints
{
    internal sealed class BalanceLevelRequest
    {
        public string Version { get; set; } = string.Empty;

        public List<BalanceConfigEdit> Edits { get; set; } = new();

        public BalanceSetup Setup { get; set; } = new();

        public int StoryLevelId { get; set; } = 1;

        public long Seed { get; set; } = 1;

        public int Runs { get; set; } = 100;

        public string PerkPolicy { get; set; } = string.Empty;

        public List<int> PerkPriority { get; set; } = new();

        public string ForkPolicy { get; set; } = string.Empty;

        public string Scenario { get; set; } = string.Empty;
    }
}
