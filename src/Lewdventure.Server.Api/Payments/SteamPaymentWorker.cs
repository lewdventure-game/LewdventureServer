namespace Server.Api.Payments
{
    internal sealed class SteamPaymentWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _timeProvider;

        public SteamPaymentWorker(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
        {
            _scopeFactory = scopeFactory;
            _timeProvider = timeProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var nextReport = DateTime.MinValue;

            while (stoppingToken.IsCancellationRequested == false)
            {
                using var scope = _scopeFactory.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<SteamPaymentReconciler>();
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                if (nextReport <= now)
                {
                    await reconciler.ReconcileReportAsync(stoppingToken);
                    await reconciler.ReviewStaleAsync(stoppingToken);
                    nextReport = now.AddMinutes(1);
                }

                await reconciler.CheckPendingAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(3), _timeProvider, stoppingToken);
            }
        }
    }
}
