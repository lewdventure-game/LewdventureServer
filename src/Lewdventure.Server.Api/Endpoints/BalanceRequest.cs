using Server.Balance;

namespace Server.Api.Endpoints
{
    internal sealed class BalanceRequest
    {
        public string Version { get; set; } = string.Empty;

        public List<BalanceConfigEdit> Edits { get; set; } = new();

        public BalanceSetup Setup { get; set; } = new();

        public BalanceTarget Target { get; set; } = new();

        public ulong Seed { get; set; } = 1;

        public int Battles { get; set; } = 200;

        public bool IncludeLog { get; set; } = true;

        public string Scenario { get; set; } = string.Empty;
    }
}
