using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Server.Api.Payments
{
    internal sealed class SteamPayments
    {
        private readonly PaymentOptions _options;
        private readonly PaymentStore _store;
        private readonly SteamPaymentsClient _client;
        private readonly ILogger<SteamPayments> _logger;
        private readonly TimeProvider _timeProvider;

        public SteamPayments(IOptions<PaymentOptions> options, PaymentStore store, SteamPaymentsClient client, ILogger<SteamPayments> logger, TimeProvider timeProvider)
        {
            _options = options.Value;
            _store = store;
            _client = client;
            _logger = logger;
            _timeProvider = timeProvider;
        }

        public string GetOrderId(string userId, string requestKey)
        {
            var material = Encoding.UTF8.GetBytes(_options.SteamScope + ":" + userId + ":" + requestKey);
            var digest = SHA256.HashData(material);
            var number = BinaryPrimitives.ReadUInt64BigEndian(digest);

            return (number == 0 ? 1UL : number).ToString(CultureInfo.InvariantCulture);
        }

        public Task<PaymentOrder> ReserveAsync(string userId, string subject, string requestKey, PaymentOffer offer, string currency, long amount, CancellationToken cancellationToken)
        {
            var orderId = GetOrderId(userId, requestKey);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            return _store.SaveNewAsync(new PaymentOrder
            {
                Id = _store.OrderId(_options.SteamScope, orderId),
                Scope = _options.SteamScope,
                ExternalId = orderId,
                UserId = userId,
                Subject = subject,
                RequestKey = requestKey,
                Sku = offer.Sku,
                Name = offer.Name,
                Entitlement = offer.Entitlement,
                SteamItemId = offer.SteamItemId,
                Currency = currency,
                Amount = amount,
                Test = _options.SteamSandbox,
                CreatedAt = now,
                NextCheckAt = now,
            }, cancellationToken);
        }

        public async Task<PaymentOrder> StartAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            if (order.State != PaymentOrderState.Created || await _store.BeginSteamAsync(order, cancellationToken) == false)
                return await _store.GetAsync(order.Scope, order.ExternalId, cancellationToken);

            var response = await _client.InitializeAsync(order, cancellationToken);
            var parameters = response["params"] as JObject;
            var transactionId = parameters?.Value<string>("transid") ?? string.Empty;

            if (response.Value<string>("result") == "OK" && parameters?.Value<string>("orderid") == order.ExternalId &&
                ulong.TryParse(transactionId, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && 0 < number)
                await _store.SetSteamInitializedAsync(order, transactionId, cancellationToken);
            else if (response.Value<string>("result") == "Failure" && response["error"] is JObject error &&
                error.Value<int>("errorcode") is 3 or 7 or 8 or 10 or 11 or 100 or 102 or 103 or 104 or 107)
                await _store.FailSteamOrderAsync(order, cancellationToken);

            return await _store.GetAsync(order.Scope, order.ExternalId, cancellationToken);
        }

        public async Task RefreshAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            var response = await _client.QueryAsync(order.ExternalId, cancellationToken);

            if (response.Value<string>("result") != "OK" || response["params"] is not JObject parameters)
                return;

            var transaction = new SteamTransaction().Read(parameters);

            if (transaction.Matches(order) == false)
            {
                _logger.LogWarning("[Error] Steam query did not match the local order orderId = {OrderId}", order.ExternalId);

                return;
            }

            if (transaction.Status == "Approved" && transaction.MatchesPrice(order) &&
                order.State is PaymentOrderState.Initiating or PaymentOrderState.AwaitingApproval)
            {
                var finalized = await _client.FinalizeAsync(order.ExternalId, cancellationToken);

                if (finalized.Value<string>("result") == "OK" && finalized["params"] is JObject details &&
                    details.Value<string>("orderid") == order.ExternalId && details.Value<string>("transid") == transaction.TransactionId)
                    await _store.SettleAsync(order, false, transaction.TransactionId, "steam:FinalizeTxn", cancellationToken);

                return;
            }

            await ApplyAsync(order, transaction, cancellationToken);
        }

        public async Task ApplyAsync(PaymentOrder order, SteamTransaction transaction, CancellationToken cancellationToken)
        {
            if (transaction.Matches(order) == false)
            {
                _logger.LogWarning("[Error] Steam settlement did not match the local order orderId = {OrderId}", order.ExternalId);

                return;
            }

            if (transaction.Reversed)
                await _store.SettleAsync(order, true, transaction.TransactionId, "steam:" + transaction.Status + ":" + transaction.ItemStatus, cancellationToken);
            else if (transaction.Status == "Succeeded" && transaction.ItemStatus == "Succeeded" && transaction.MatchesPrice(order))
                await _store.SettleAsync(order, false, transaction.TransactionId, "steam:Succeeded", cancellationToken);
            else if (transaction.Status == "Failed")
                await _store.FailSteamOrderAsync(order, cancellationToken);
            else if (transaction.Status is "Approved" or "Succeeded")
                _logger.LogWarning("[Error] Steam transaction requires payment review orderId = {OrderId} status = {Status}", order.ExternalId, transaction.Status);
        }
    }
}
