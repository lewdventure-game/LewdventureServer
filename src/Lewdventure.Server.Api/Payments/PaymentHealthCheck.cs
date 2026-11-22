using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Server.Api.Payments
{
    internal sealed class PaymentHealthCheck : IHealthCheck
    {
        private readonly PaymentStore _store;
        private readonly PaymentOptions _options;
        private readonly TimeProvider _timeProvider;

        public PaymentHealthCheck(IOptions<PaymentOptions> options, PaymentStore store, TimeProvider timeProvider)
        {
            _options = options.Value;
            _store = store;
            _timeProvider = timeProvider;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var reviewCount = await _store.CountReviewAsync(cancellationToken);
            var data = new Dictionary<string, object> { ["reviewRequired"] = reviewCount };

            if (_options.SteamAppId != 0)
            {
                var cursor = await _store.GetReportStateAsync(_options.SteamScope, cancellationToken);

                if (cursor != null)
                {
                    data["steamReportCursor"] = cursor.Time;
                    data["steamReportLastSuccessAt"] = cursor.LastSuccessAt;
                    data["steamReportLastError"] = cursor.LastError;

                    var now = _timeProvider.GetUtcNow().UtcDateTime;
                    var lastSuccess = cursor.LastSuccessAt == DateTime.MinValue ? cursor.CreatedAt : cursor.LastSuccessAt;

                    if (lastSuccess != DateTime.MinValue && lastSuccess < now.AddMinutes(-10))
                        return HealthCheckResult.Degraded("Steam settlement report is delayed.", data: data);
                }
            }

            if (0 < reviewCount)
                return HealthCheckResult.Degraded($"Payment review queue contains {reviewCount} orders.", data: data);

            return HealthCheckResult.Healthy("Payment reconciliation is current.", data);
        }
    }
}
