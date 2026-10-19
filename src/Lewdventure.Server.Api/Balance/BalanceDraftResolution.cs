using Server.Balance;
using Server.GameConfigs;

namespace Server.Api.Balance
{
    internal sealed class BalanceDraftResolution
    {
        public BalanceDraftResolution(GameConfigSet? configSet, BalanceConfigDraft? draft, BalanceExecution? failure)
        {
            ConfigSet = configSet;
            Draft = draft;
            Failure = failure;
        }

        public GameConfigSet? ConfigSet { get; }

        public BalanceConfigDraft? Draft { get; }

        public BalanceExecution? Failure { get; }
    }
}
