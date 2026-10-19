using Server.Balance;

namespace Server.Api.Endpoints
{
    internal sealed class BalanceCompareRequest
    {
        public string Kind { get; set; } = string.Empty;

        public BalanceVariantRequest A { get; set; } = new();

        public BalanceVariantRequest B { get; set; } = new();

        public BalanceTarget Target { get; set; } = new();

        public int StoryLevelId { get; set; } = 1;

        public long Seed { get; set; } = 1;

        public int Battles { get; set; } = 200;

        public int Runs { get; set; } = 100;

        public string PerkPolicy { get; set; } = string.Empty;

        public List<int> PerkPriority { get; set; } = new();

        public string ForkPolicy { get; set; } = string.Empty;

        public string Scenario { get; set; } = string.Empty;
    }
}
