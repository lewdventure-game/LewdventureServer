using Server.Services;

namespace Server.Balance
{
    internal sealed class BalanceConfigDraft
    {
        public BalanceConfigDraft(string version, IConfigDistributor? distributor, List<string> errors)
        {
            Version = version;
            Distributor = distributor;
            Errors = errors;
        }

        public string Version { get; }

        public IConfigDistributor? Distributor { get; }

        public List<string> Errors { get; }
    }
}
