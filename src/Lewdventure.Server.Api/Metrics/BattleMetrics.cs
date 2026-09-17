using System.Diagnostics.Metrics;

namespace Server.Api.Metrics
{
    internal sealed class BattleMetrics : IDisposable
    {
        public const string MeterName = "Lewdventure.Server";

        private readonly Meter _meter;
        private readonly Counter<long> _battles;
        private readonly Histogram<double> _duration;

        public BattleMetrics(IMeterFactory meterFactory)
        {
            _meter = meterFactory.Create(MeterName);
            _battles = _meter.CreateCounter<long>("lewdventure.battles", "{battle}", "Battle simulations handled");
            _duration = _meter.CreateHistogram<double>("lewdventure.battle.duration", "ms", "Battle simulation duration");
        }

        public void Record(string kind, int statusCode, double durationMs)
        {
            var kindTag = new KeyValuePair<string, object?>("kind", kind);
            var statusTag = new KeyValuePair<string, object?>("status", statusCode);

            _battles.Add(1, kindTag, statusTag);
            _duration.Record(durationMs, kindTag, statusTag);
        }

        public void Dispose()
        {
            _meter.Dispose();
        }
    }
}
