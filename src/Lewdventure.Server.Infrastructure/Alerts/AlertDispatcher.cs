namespace Server.Infrastructure.Alerts
{
    internal sealed class AlertDispatcher
    {
        private readonly AlertQueue _alertQueue;
        private readonly AlertThrottle _alertThrottle;
        private readonly IAlertSink _alertSink;
        private readonly ILogger<AlertDispatcher> _logger;

        public AlertDispatcher(AlertQueue alertQueue, AlertThrottle alertThrottle, IAlertSink alertSink, ILogger<AlertDispatcher> logger)
        {
            _alertQueue = alertQueue;
            _alertThrottle = alertThrottle;
            _alertSink = alertSink;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var message in _alertQueue.Reader.ReadAllAsync(cancellationToken))
                    await DispatchAsync(message, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }

        public async Task DrainAsync(CancellationToken cancellationToken)
        {
            while (_alertQueue.Reader.TryRead(out var message))
                await DispatchAsync(message, cancellationToken);
        }

        private async Task DispatchAsync(AlertMessage message, CancellationToken cancellationToken)
        {
            if (_alertThrottle.TryAcquire(message) == false)
            {
                _logger.LogDebug("[Alert] throttled key = {DedupKey}", message.DedupKey);

                return;
            }

            try
            {
                await _alertSink.SendAsync(message, cancellationToken);
            }
            catch (Exception exception) when (exception is OperationCanceledException == false)
            {
                _logger.LogWarning("[Alert] sink failed title = {Title} error = {Error}", message.Title, exception.Message);
            }
        }
    }
}
