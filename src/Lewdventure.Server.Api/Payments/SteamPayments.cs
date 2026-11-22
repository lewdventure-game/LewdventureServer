using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Server.Services;

namespace Server.Api.Payments
{
    internal sealed class SteamPayments
    {
        private readonly PaymentOptions _options;
        private readonly PaymentStore _store;
        private readonly SteamPaymentsClient _client;
        private readonly ILogger<SteamPayments> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly IConfigDistributor _configDistributor;

        public SteamPayments(
            IOptions<PaymentOptions> options,
            PaymentStore store,
            SteamPaymentsClient client,
            ILogger<SteamPayments> logger,
            TimeProvider timeProvider,
            IConfigDistributor configDistributor)
        {
            _options = options.Value;
            _store = store;
            _client = client;
            _logger = logger;
            _timeProvider = timeProvider;
            _configDistributor = configDistributor;
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
                Rewards = offer.Rewards,
                SteamItemId = offer.SteamItemId,
                Currency = currency,
                Amount = amount,
                Test = _options.SteamSandbox,
                CreatedAt = now,
                UpdatedAt = now,
                NextCheckAt = now,
            }, cancellationToken);
        }

        public async Task<PaymentOrder> StartAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            if (order.State != PaymentOrderState.Created)
                return await _store.GetAsync(order.Scope, order.ExternalId, cancellationToken);

            if (await _store.BeginSteamAsync(order, cancellationToken) == false)
                return await _store.GetAsync(order.Scope, order.ExternalId, cancellationToken);

            await InitializeAsync(order, cancellationToken);

            return await _store.GetAsync(order.Scope, order.ExternalId, cancellationToken);
        }

        public async Task RefreshAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            var response = await _client.QueryAsync(order.ExternalId, cancellationToken);

            if (response.Value<string>("result") != "OK" || response["params"] is not JObject parameters)
            {
                var error = ReadError(response);

                if (response.Value<string>("result") == "Failure" && order.State == PaymentOrderState.Initiating &&
                    order.TransactionId.Length == 0 && order.InitializationAttempts < 10)
                {
                    await _store.RecordSteamInitializationErrorAsync(order, error, true, cancellationToken);
                    await InitializeAsync(order, cancellationToken);

                    return;
                }

                await _store.RecordSteamQueryErrorAsync(order, error, cancellationToken);

                return;
            }

            var transaction = new SteamTransaction().Read(parameters);

            if (transaction.Matches(order) == false)
            {
                _logger.LogWarning("[Error] Steam query did not match the local order orderId = {OrderId}", order.ExternalId);
                await _store.MarkReviewAsync(order, "provider_mismatch", "steam:QueryTxn", cancellationToken);

                return;
            }

            if (transaction.Status == "Approved" && transaction.MatchesPrice(order) &&
                order.State is PaymentOrderState.Initiating or PaymentOrderState.AwaitingApproval)
            {
                var finalized = await _client.FinalizeAsync(order.ExternalId, cancellationToken);

                if (finalized.Value<string>("result") == "OK" && finalized["params"] is JObject details)
                {
                    if (details.Value<string>("orderid") != order.ExternalId || details.Value<string>("transid") != transaction.TransactionId)
                    {
                        await _store.MarkReviewAsync(order, "finalize_mismatch", "steam:FinalizeTxn", cancellationToken);

                        return;
                    }

                    await _store.SettleAsync(order, false, transaction.TransactionId, "steam:FinalizeTxn", _configDistributor, cancellationToken);
                }

                return;
            }

            await ApplyAsync(order, transaction, cancellationToken);
        }

        public async Task ApplyAsync(PaymentOrder order, SteamTransaction transaction, CancellationToken cancellationToken)
        {
            if (transaction.Matches(order) == false)
            {
                _logger.LogWarning("[Error] Steam settlement did not match the local order orderId = {OrderId}", order.ExternalId);
                await _store.MarkReviewAsync(order, "provider_mismatch", "steam:" + transaction.Status, cancellationToken);

                return;
            }

            if (transaction.Reversed)
                await _store.SettleAsync(order, true, transaction.TransactionId, "steam:" + transaction.Status + ":" + transaction.ItemStatus, _configDistributor, cancellationToken);
            else if (transaction.Status == "Succeeded" && transaction.ItemStatus == "Succeeded" && transaction.MatchesPrice(order))
                await _store.SettleAsync(order, false, transaction.TransactionId, "steam:Succeeded", _configDistributor, cancellationToken);
            else if (transaction.Status == "Failed")
                await _store.FailSteamOrderAsync(order, "steam:Failed", cancellationToken);
            else if (transaction.Status is "Approved" or "Succeeded")
                await _store.MarkReviewAsync(order, "price_or_item_mismatch", "steam:" + transaction.Status + ":" + transaction.ItemStatus, cancellationToken);
        }

        private async Task InitializeAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            var response = await _client.InitializeAsync(order, cancellationToken);
            var parameters = response["params"] as JObject;
            var transactionId = parameters?.Value<string>("transid") ?? string.Empty;

            if (response.Value<string>("result") == "OK" && parameters?.Value<string>("orderid") == order.ExternalId &&
                ulong.TryParse(transactionId, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && 0 < number)
            {
                await _store.SetSteamInitializedAsync(order, transactionId, cancellationToken);

                return;
            }

            var error = ReadError(response);
            var errorCode = response["error"] is JObject details ? details.Value<int?>("errorcode") : null;

            if (response.Value<string>("result") == "Failure" && errorCode is 3 or 7 or 8 or 10 or 11 or 100 or 102 or 103 or 104 or 107)
                await _store.FailSteamOrderAsync(order, error, cancellationToken);
            else
                await _store.RecordSteamInitializationErrorAsync(order, error, false, cancellationToken);
        }

        private string ReadError(JObject response)
        {
            if (response["error"] is not JObject error)
                return response.Value<string>("result") ?? "Empty Steam response";

            var code = error.Value<int?>("errorcode");
            var description = error.Value<string>("errordesc") ?? string.Empty;

            return "steam:" + (code?.ToString(CultureInfo.InvariantCulture) ?? "unknown") + ":" + description;
        }
    }
}
