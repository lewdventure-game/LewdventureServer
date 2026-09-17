using Microsoft.Extensions.Options;

namespace Server.Infrastructure.Alerts
{
    internal sealed class AlertThrottle
    {
        private readonly Dictionary<string, DateTimeOffset> _lastSent = new(StringComparer.Ordinal);
        private readonly object _lock = new();
        private readonly AlertsOptions _options;
        private readonly TimeProvider _timeProvider;

        public AlertThrottle(IOptions<AlertsOptions> options, TimeProvider timeProvider)
        {
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public bool TryAcquire(AlertMessage message)
        {
            if (message.Severity == AlertSeverity.Resolved || string.IsNullOrEmpty(message.DedupKey))
                return true;

            var now = _timeProvider.GetUtcNow();
            var cooldown = TimeSpan.FromSeconds(_options.CooldownSeconds);

            lock (_lock)
            {
                if (_lastSent.TryGetValue(message.DedupKey, out var lastSent) && now - lastSent < cooldown)
                    return false;

                _lastSent[message.DedupKey] = now;

                return true;
            }
        }
    }
}
