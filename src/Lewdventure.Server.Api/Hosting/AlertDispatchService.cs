using Server.Infrastructure.Alerts;

namespace Server.Api.Hosting
{
    internal sealed class AlertDispatchService : BackgroundService
    {
        private readonly AlertDispatcher _alertDispatcher;
        private readonly AlertQueue _alertQueue;

        public AlertDispatchService(AlertDispatcher alertDispatcher, AlertQueue alertQueue)
        {
            _alertDispatcher = alertDispatcher;
            _alertQueue = alertQueue;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            _alertQueue.Complete();

            using var drainTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            drainTimeout.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                await _alertDispatcher.DrainAsync(drainTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _alertDispatcher.RunAsync(stoppingToken);
        }
    }
}
