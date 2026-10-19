using Server.Balance;
using Server.GameConfigs;

namespace Server.Api.Endpoints
{
    internal sealed class BalanceDraftResolution
    {
        public BalanceDraftResolution(GameConfigSet? configSet, BalanceConfigDraft? draft, IResult? failure)
        {
            ConfigSet = configSet;
            Draft = draft;
            Failure = failure;
        }

        public GameConfigSet? ConfigSet { get; }

        public BalanceConfigDraft? Draft { get; }

        public IResult? Failure { get; }
    }
}
