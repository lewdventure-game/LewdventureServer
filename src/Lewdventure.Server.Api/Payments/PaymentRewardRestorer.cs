using Server.Services;

namespace Server.Api.Payments
{
    internal sealed class PaymentRewardRestorer
    {
        private readonly PaymentStore _store;
        private readonly IConfigDistributor _configDistributor;

        public PaymentRewardRestorer(PaymentStore store, IConfigDistributor configDistributor)
        {
            _store = store;
            _configDistributor = configDistributor;
        }

        public Task RestoreAsync(string userId, CancellationToken cancellationToken)
        {
            return _store.RestoreGrantedRewardsAsync(userId, _configDistributor, cancellationToken);
        }
    }
}
