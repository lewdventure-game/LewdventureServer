using System.Globalization;

namespace Server.LoadTest
{
    internal sealed class PlayerScenarioReport
    {
        public bool Write(LoadTestSettings settings, PlayerScenarioResults results, TimeSpan measured)
        {
            var endpoints = results.GetEndpoints();
            var total = 0;
            var failed = 0;
            var throttled = 0;
            var conflicts = 0;
            var serverErrors = 0;
            var allLatencies = new List<double>();

            Console.WriteLine();
            Console.WriteLine($"target        {settings.Target}");
            Console.WriteLine($"players       {settings.Players}");
            Console.WriteLine($"measured      {Format(measured.TotalSeconds)} s after {settings.RampSeconds} s ramp");
            Console.WriteLine();
            Console.WriteLine("endpoint              requests      rps    p50 ms    p95 ms    p99 ms    max ms  statuses");

            for (int i = 0; i < endpoints.Count; i++)
            {
                var name = endpoints[i].Key;
                var endpoint = endpoints[i].Value;
                var latencies = endpoint.GetSortedLatencies();
                var outcomes = endpoint.GetOutcomes();

                total += endpoint.Total;
                failed += endpoint.Failed;
                allLatencies.AddRange(latencies);

                foreach (var outcome in outcomes)
                {
                    if (outcome.Key == "429")
                        throttled += outcome.Value;
                    else if (outcome.Key == "409")
                        conflicts += outcome.Value;
                    else if (outcome.Key == "network" || outcome.Key.StartsWith("5", StringComparison.Ordinal))
                        serverErrors += outcome.Value;
                }

                Console.WriteLine(
                    name.PadRight(20)
                    + endpoint.Total.ToString(CultureInfo.InvariantCulture).PadLeft(10)
                    + Format(endpoint.Total / Math.Max(measured.TotalSeconds, 0.001)).PadLeft(9)
                    + Format(Percentile(latencies, 0.50)).PadLeft(10)
                    + Format(Percentile(latencies, 0.95)).PadLeft(10)
                    + Format(Percentile(latencies, 0.99)).PadLeft(10)
                    + Format(Percentile(latencies, 1)).PadLeft(10)
                    + "  " + FormatOutcomes(outcomes));
            }

            allLatencies.Sort();

            var errorRate = total == 0 ? 1d : (double)failed / total;
            var p95 = Percentile(allLatencies, 0.95);

            Console.WriteLine();
            Console.WriteLine($"requests      {total}");
            Console.WriteLine($"throughput    {Format(total / Math.Max(measured.TotalSeconds, 0.001))} rps");
            Console.WriteLine($"latency p95   {Format(p95)} ms");
            Console.WriteLine($"errors        {failed} ({Format(errorRate * 100)} %), 5xx and network {serverErrors}, 429 {throttled}, 409 {conflicts}");
            Console.WriteLine($"runs          started {results.RunsStarted}, completed {results.RunsCompleted}, failed {results.RunsFailed}, battles {results.Battles}");

            if (0 < results.PlayersWithoutAuth)
                Console.WriteLine($"no auth       {results.PlayersWithoutAuth} players could not sign in");

            var passed = total != 0 && errorRate <= settings.MaxErrorRate && results.PlayersWithoutAuth == 0;

            if (0 < settings.MaxP95Milliseconds && settings.MaxP95Milliseconds < p95)
                passed = false;

            Console.WriteLine(passed ? "result        PASS" : "result        FAIL");

            return passed;
        }

        private string FormatOutcomes(Dictionary<string, int> outcomes)
        {
            var keys = new List<string>(outcomes.Keys);
            var parts = new List<string>(keys.Count);

            keys.Sort(StringComparer.Ordinal);

            for (int i = 0; i < keys.Count; i++)
                parts.Add(keys[i] + "×" + outcomes[keys[i]].ToString(CultureInfo.InvariantCulture));

            return string.Join(" ", parts);
        }

        private double Percentile(List<double> sorted, double percentile)
        {
            if (sorted.Count == 0)
                return 0;

            var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;

            return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
        }

        private string Format(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
