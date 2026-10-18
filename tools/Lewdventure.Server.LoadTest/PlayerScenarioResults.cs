namespace Server.LoadTest
{
    internal sealed class PlayerScenarioResults
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, LoadTestResults> _endpoints = new(StringComparer.Ordinal);

        private int _runsStarted;
        private int _runsCompleted;
        private int _runsFailed;
        private int _battles;
        private int _playersWithoutAuth;

        public int RunsStarted => _runsStarted;

        public int RunsCompleted => _runsCompleted;

        public int RunsFailed => _runsFailed;

        public int Battles => _battles;

        public int PlayersWithoutAuth => _playersWithoutAuth;

        public void Record(string endpoint, int statusCode, double latencyMs)
        {
            LoadTestResults results;

            lock (_lock)
            {
                if (_endpoints.TryGetValue(endpoint, out var existing) == false)
                {
                    existing = new LoadTestResults();
                    _endpoints[endpoint] = existing;
                }

                results = existing;
            }

            results.Record(statusCode == 0 ? "network" : statusCode.ToString(), latencyMs, IsSuccess(endpoint, statusCode), string.Empty);
        }

        public void RecordRunStarted()
        {
            Interlocked.Increment(ref _runsStarted);
        }

        public void RecordRunFinished(bool completed)
        {
            if (completed)
                Interlocked.Increment(ref _runsCompleted);
            else
                Interlocked.Increment(ref _runsFailed);
        }

        public void RecordBattle()
        {
            Interlocked.Increment(ref _battles);
        }

        public void RecordPlayerWithoutAuth()
        {
            Interlocked.Increment(ref _playersWithoutAuth);
        }

        public List<KeyValuePair<string, LoadTestResults>> GetEndpoints()
        {
            lock (_lock)
            {
                var endpoints = new List<KeyValuePair<string, LoadTestResults>>(_endpoints);

                endpoints.Sort(CompareByName);

                return endpoints;
            }
        }

        private bool IsSuccess(string endpoint, int statusCode)
        {
            if (statusCode == 404)
                return string.Equals(endpoint, PlayerSession.CurrentRunEndpoint, StringComparison.Ordinal);

            return 200 <= statusCode && statusCode < 300 || statusCode == 304;
        }

        private int CompareByName(KeyValuePair<string, LoadTestResults> left, KeyValuePair<string, LoadTestResults> right)
        {
            return string.CompareOrdinal(left.Key, right.Key);
        }
    }
}
