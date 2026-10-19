using Server.Balance;

namespace Server.Api.Endpoints
{
    internal sealed class BalanceVariantRequest
    {
        public string Version { get; set; } = string.Empty;

        public List<BalanceConfigEdit> Edits { get; set; } = new();

        public BalanceSetup Setup { get; set; } = new();
    }
}
