using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;

namespace Server.Api.Payments
{
    internal sealed class SteamPaymentWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PaymentStore _store;
        private readonly PaymentOptions _options;
        private readonly ILogger<SteamPaymentWorker> _logger;
        private readonly TimeProvider _timeProvider;

        public SteamPaymentWorker(IServiceScopeFactory scopeFactory, PaymentStore store, IOptions<PaymentOptions> options,
            ILogger<SteamPaymentWorker> logger, TimeProvider timeProvider)
        {
            _scopeFactory = scopeFactory;
            _store = store;
            _options = options.Value;
            _logger = logger;
            _timeProvider = timeProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var nextReport = DateTime.MinValue;

            while (stoppingToken.IsCancellationRequested == false)
            {
                using var scope = _scopeFactory.CreateScope();
                var payments = scope.ServiceProvider.GetRequiredService<SteamPayments>();
                var steam = scope.ServiceProvider.GetRequiredService<SteamPaymentsClient>();
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                if (nextReport <= now)
                {
                    await ReconcileReportAsync(steam, payments, stoppingToken);
                    nextReport = now.AddMinutes(1);
                }

                await CheckPendingAsync(payments, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(3), _timeProvider, stoppingToken);
            }
        }

        private async Task CheckPendingAsync(SteamPayments payments, CancellationToken cancellationToken)
        {
            try
            {
                for (var i = 0; i < 15; i++)
                {
                    var order = await _store.LeaseNextSteamOrderAsync(_options.SteamScope, cancellationToken);

                    if (order == null)
                        return;

                    await payments.RefreshAsync(order, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is SteamPaymentsException or MongoException or FormatException or OverflowException or InvalidCastException)
            {
                _logger.LogWarning("[Error] Steam pending orders will be retried errorType = {ErrorType}", exception.GetType().Name);
            }
        }

        private async Task ReconcileReportAsync(SteamPaymentsClient steam, SteamPayments payments, CancellationToken cancellationToken)
        {
            try
            {
                var cursor = await _store.GetReportCursorAsync(_options.SteamScope, cancellationToken);
                var response = await steam.GetReportAsync(cursor, cancellationToken);

                if (response.Value<string>("result") != "OK" || response["params"] is not JObject parameters)
                    throw new SteamPaymentsException();

                if (parameters.Value<int?>("count") == 0)
                    return;

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
                        await payments.ApplyAsync(order, transaction, cancellationToken);
                }

                if (cursor < latest)
                    await _store.AdvanceReportCursorAsync(_options.SteamScope, latest, cancellationToken);
                else if (5000 <= orders.Count)
                    _logger.LogError("[Error] Steam settlement report cursor did not advance at {ReportCursor}", cursor);
            }
            catch (Exception exception) when (exception is SteamPaymentsException or MongoException or FormatException or OverflowException or InvalidCastException)
            {
                _logger.LogWarning("[Error] Steam settlement report will be retried errorType = {ErrorType}", exception.GetType().Name);
            }
        }
    }
}
