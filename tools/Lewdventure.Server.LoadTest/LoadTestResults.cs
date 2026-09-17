namespace Server.LoadTest
{
    internal sealed class LoadTestResults
    {
        private readonly object _lock = new();
        private readonly List<double> _latencies = new();
        private readonly Dictionary<string, int> _outcomes = new(StringComparer.Ordinal);
        private readonly List<string> _mismatches = new();

        public int Total { get; private set; }

        public int Failed { get; private set; }

        public void Record(string outcome, double latencyMs, bool succeeded, string mismatch)
        {
            lock (_lock)
            {
                Total++;
                _latencies.Add(latencyMs);
                _outcomes[outcome] = _outcomes.TryGetValue(outcome, out var count) ? count + 1 : 1;

                if (succeeded == false)
                    Failed++;

                if (string.IsNullOrEmpty(mismatch) == false && _mismatches.Count < 10)
                    _mismatches.Add(mismatch);
            }
        }

        public List<double> GetSortedLatencies()
        {
            lock (_lock)
            {
                var sorted = new List<double>(_latencies);

                sorted.Sort();

                return sorted;
            }
        }

        public Dictionary<string, int> GetOutcomes()
        {
            lock (_lock)
            {
                return new Dictionary<string, int>(_outcomes, StringComparer.Ordinal);
            }
        }

        public List<string> GetMismatches()
        {
            lock (_lock)
            {
                return new List<string>(_mismatches);
            }
        }
    }
}
