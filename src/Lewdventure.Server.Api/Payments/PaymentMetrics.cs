using System.Diagnostics.Metrics;

namespace Server.Api.Payments
{
    internal sealed class PaymentMetrics : IDisposable
    {
        private readonly Meter _meter;
        private readonly Counter<long> _settlements;
        private readonly Counter<long> _reviews;
        private readonly Counter<long> _providerErrors;

        public PaymentMetrics(IMeterFactory meterFactory)
        {
            _meter = meterFactory.Create("Lewdventure.Server");
            _settlements = _meter.CreateCounter<long>("lewdventure.payments.settlements", "{operation}", "Payment grants and reversals committed");
            _reviews = _meter.CreateCounter<long>("lewdventure.payments.reviews", "{case}", "Payment cases opened for manual review");
            _providerErrors = _meter.CreateCounter<long>("lewdventure.payments.provider.errors", "{error}", "Payment provider operation errors");
        }

        public void Settlement(string scope, bool reverse)
        {
            _settlements.Add(1, new KeyValuePair<string, object?>("platform", Platform(scope)), new KeyValuePair<string, object?>("action", reverse ? "reversed" : "granted"));
        }

        public void Review(string scope, string reason)
        {
            _reviews.Add(1, new KeyValuePair<string, object?>("platform", Platform(scope)), new KeyValuePair<string, object?>("reason", reason));
        }

        public void ProviderError(string scope, string operation)
        {
            _providerErrors.Add(1, new KeyValuePair<string, object?>("platform", Platform(scope)), new KeyValuePair<string, object?>("operation", operation));
        }

        public void Dispose()
        {
            _meter.Dispose();
        }

        private string Platform(string scope)
        {
            var separator = scope.IndexOf(':');

            return separator < 0 ? scope : scope.Substring(0, separator);
        }
    }
}
