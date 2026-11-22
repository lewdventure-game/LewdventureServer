using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;

namespace Server.Api.Payments
{
    internal sealed class SteamPaymentReconciler
    {
        private readonly PaymentStore _store;
        private readonly SteamPayments _payments;
        private readonly SteamPaymentsClient _steam;
        private readonly PaymentOptions _options;
        private readonly ILogger<SteamPaymentReconciler> _logger;
        private readonly PaymentMetrics _metrics;

        public SteamPaymentReconciler(
            PaymentStore store,
            SteamPayments payments,
            SteamPaymentsClient steam,
            IOptions<PaymentOptions> options,
            ILogger<SteamPaymentReconciler> logger,
            PaymentMetrics metrics)
        {
            _store = store;
            _payments = payments;
            _steam = steam;
            _options = options.Value;
            _logger = logger;
            _metrics = metrics;
        }

        public async Task CheckPendingAsync(CancellationToken cancellationToken)
        {
            try
            {
                for (var i = 0; i < 15; i++)
                {
                    var order = await _store.LeaseNextSteamOrderAsync(_options.SteamScope, cancellationToken);

                    if (order == null)
                        return;

                    await ProcessOrderAsync(order, cancellationToken);
                }
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                _metrics.ProviderError(_options.SteamScope, "QueryTxn");
                _logger.LogWarning("[Error] Steam pending orders will be retried errorType = {ErrorType}", exception.GetType().Name);
            }
        }

        public async Task ReconcileReportAsync(CancellationToken cancellationToken)
        {
            try
            {
                var cursor = await _store.GetReportCursorAsync(_options.SteamScope, cancellationToken);
                await _store.RecordReportAttemptAsync(_options.SteamScope, cancellationToken);
                var response = await _steam.GetReportAsync(cursor, cancellationToken);

                if (response.Value<string>("result") != "OK" || response["params"] is not JObject parameters)
                    throw new SteamPaymentsException();

                if (parameters.Value<int?>("count") == 0)
                {
                    await _store.RecordReportSuccessAsync(_options.SteamScope, cursor, cancellationToken);

                    return;
                }

                if (parameters["orders"] is not JArray orders)
                    throw new SteamPaymentsException();

                var latest = cursor;

                foreach (var row in orders)
                {
                    if (row is not JObject transactionRow)
                        throw new SteamPaymentsException();

                    var transaction = new SteamTransaction().Read(transactionRow);

                    if (transaction.Time == DateTime.MinValue)
                        throw new SteamPaymentsException();

                    if (latest < transaction.Time)
                        latest = transaction.Time;

                    var order = await _store.GetAsync(_options.SteamScope, transaction.OrderId, cancellationToken);

                    if (order != null)
                        await ApplyReportAsync(order, transaction, cancellationToken);
                }

                if (5000 <= orders.Count && cursor == latest)
                    throw new SteamPaymentsException();

                await _store.RecordReportSuccessAsync(_options.SteamScope, latest, cancellationToken);
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                _metrics.ProviderError(_options.SteamScope, "GetReport");

                if (cancellationToken.IsCancellationRequested == false)
                {
                    try
                    {
                        await _store.RecordReportFailureAsync(_options.SteamScope, exception.GetType().Name, cancellationToken);
                    }
                    catch (MongoException recordingException)
                    {
                        _logger.LogWarning("[Error] Steam report failure could not be persisted errorType = {ErrorType}", recordingException.GetType().Name);
                    }
                }

                _logger.LogWarning("[Error] Steam settlement report will be retried errorType = {ErrorType}", exception.GetType().Name);
            }
        }

        public async Task ReviewStaleAsync(CancellationToken cancellationToken)
        {
            try
            {
                var orders = await _store.ListStaleSteamOrdersAsync(_options.SteamScope, cancellationToken);

                for (var i = 0; i < orders.Count; i++)
                    await _store.MarkReviewAsync(orders[i], "stale_pending", "steam:pending_for_24h", cancellationToken);
            }
            catch (MongoException exception)
            {
                _logger.LogWarning("[Error] Steam stale order review will be retried errorType = {ErrorType}", exception.GetType().Name);
            }
        }

        private async Task ProcessOrderAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            try
            {
                await _payments.RefreshAsync(order, cancellationToken);
            }
            catch (PaymentSettlementException exception) when (exception.Retryable == false)
            {
                await _store.MarkReviewAsync(order, "settlement_failed", "steam:reward_grant", cancellationToken);
            }
        }

        private async Task ApplyReportAsync(PaymentOrder order, SteamTransaction transaction, CancellationToken cancellationToken)
        {
            try
            {
                await _payments.ApplyAsync(order, transaction, cancellationToken);
            }
            catch (PaymentSettlementException exception) when (exception.Retryable == false)
            {
                await _store.MarkReviewAsync(order, "settlement_failed", "steam:GetReport", cancellationToken);
            }
        }

        private bool IsRetryable(Exception exception)
        {
            return exception is SteamPaymentsException or MongoException or FormatException or OverflowException or InvalidCastException ||
                (exception is PaymentSettlementException settlement && settlement.Retryable);
        }
    }
}
