using Server.Battles;

namespace Server.Api.Payments
{
    internal sealed class PaymentResponseFactory
    {
        private readonly PaymentRewardPolicy _rewards;

        public PaymentResponseFactory(PaymentRewardPolicy rewards)
        {
            _rewards = rewards;
        }

        public object CreateCatalog(PaymentOffer offer, string currency, long amount)
        {
            return new
            {
                offer.Sku,
                offer.Name,
                offer.Entitlement,
                currency,
                amount,
                rewards = CreateRewards(offer.Rewards),
            };
        }

        public object CreateOrder(PaymentOrder order)
        {
            return new
            {
                orderId = order.ExternalId,
                order.Sku,
                order.Amount,
                order.Currency,
                order.Entitlement,
                rewards = CreateRewards(order.Rewards),
                state = order.State.ToString(),
                reviewRequired = order.ReviewRequired,
                order.CreatedAt,
                grantedAt = order.GrantedAt == DateTime.MinValue ? (DateTime?)null : order.GrantedAt,
                reversedAt = order.ReversedAt == DateTime.MinValue ? (DateTime?)null : order.ReversedAt,
            };
        }

        private List<object> CreateRewards(string rawRewards)
        {
            var parsed = _rewards.Parse(rawRewards);
            var result = new List<object>(parsed.Count);

            for (var i = 0; i < parsed.Count; i++)
            {
                var reward = parsed[i];
                result.Add(new
                {
                    type = reward.Type == BattleRewardType.Resource ? "resource" : "account",
                    key = reward.RewardKey,
                    amount = reward.Count,
                });
            }

            return result;
        }
    }
}
