using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Server.Api.Payments
{
    internal sealed class SteamTransaction
    {
        public string OrderId { get; init; } = string.Empty;
        public string TransactionId { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string Currency { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string ItemStatus { get; init; } = string.Empty;
        public uint ItemId { get; init; }
        public int Quantity { get; init; }
        public long NetAmount { get; init; }
        public long Tax { get; init; }
        public DateTime Time { get; init; }

        public bool Matches(PaymentOrder order)
        {
            return OrderId == order.ExternalId && TransactionId.Length != 0 &&
                (order.TransactionId.Length == 0 || TransactionId == order.TransactionId) &&
                Subject == order.Subject && Currency == order.Currency && ItemId == order.SteamItemId && Quantity == 1;
        }

        public bool MatchesPrice(PaymentOrder order)
        {
            return 0 <= NetAmount && 0 <= Tax && Tax <= order.Amount && NetAmount == order.Amount - Tax;
        }

        public bool Reversed => IsReversal(Status) || (Status == "PartialRefund" && IsReversal(ItemStatus));

        private bool IsReversal(string status)
        {
            return status is "Refunded" or "Chargedback" or "RefundedSuspectedFraud" or "RefundedFriendlyFraud";
        }

        public SteamTransaction Read(JObject transaction)
        {
            var items = transaction["items"] as JArray;
            var item = items?.Count == 1 ? items[0] as JObject : null;
            var time = transaction.Value<string>("time");
            DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var updatedAt);

            return new SteamTransaction
            {
                OrderId = transaction.Value<string>("orderid") ?? string.Empty,
                TransactionId = transaction.Value<string>("transid") ?? string.Empty,
                Subject = transaction.Value<string>("steamid") ?? string.Empty,
                Currency = transaction.Value<string>("currency") ?? string.Empty,
                Status = transaction.Value<string>("status") ?? string.Empty,
                ItemStatus = item?.Value<string>("itemstatus") ?? string.Empty,
                ItemId = item?.Value<uint>("itemid") ?? 0,
                Quantity = item?.Value<int>("qty") ?? 0,
                NetAmount = item?.Value<long?>("amount") ?? -1,
                Tax = item?.Value<long?>("vat") ?? -1,
                Time = updatedAt,
            };
        }
    }
}
