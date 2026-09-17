using System.Globalization;

namespace Server.LoadTest
{
    internal sealed class LoadTestReport
    {
        public bool Write(LoadTestSettings settings, LoadTestResults results, TimeSpan elapsed)
        {
            var latencies = results.GetSortedLatencies();
            var errorRate = results.Total == 0 ? 1d : (double)results.Failed / results.Total;
            var p95 = Percentile(latencies, 0.95);

            Console.WriteLine();
            Console.WriteLine($"target        {settings.Target}/api/battle/{settings.Endpoint}");
            Console.WriteLine($"concurrency   {settings.Concurrency}");
            Console.WriteLine($"duration      {Format(elapsed.TotalSeconds)} s");
            Console.WriteLine($"requests      {results.Total}");
            Console.WriteLine($"throughput    {Format(results.Total / Math.Max(elapsed.TotalSeconds, 0.001))} rps");
            Console.WriteLine($"errors        {results.Failed} ({Format(errorRate * 100)} %)");
            Console.WriteLine($"latency min   {Format(Percentile(latencies, 0))} ms");
            Console.WriteLine($"latency p50   {Format(Percentile(latencies, 0.50))} ms");
            Console.WriteLine($"latency p95   {Format(p95)} ms");
            Console.WriteLine($"latency p99   {Format(Percentile(latencies, 0.99))} ms");
            Console.WriteLine($"latency max   {Format(Percentile(latencies, 1))} ms");

            foreach (var outcome in results.GetOutcomes())
                Console.WriteLine($"outcome       {outcome.Key} x {outcome.Value}");

            foreach (var mismatch in results.GetMismatches())
                Console.WriteLine($"mismatch      {mismatch}");

            var passed = results.Total != 0 && errorRate <= settings.MaxErrorRate;

            if (0 < settings.MaxP95Milliseconds && settings.MaxP95Milliseconds < p95)
                passed = false;

            Console.WriteLine(passed ? "result        PASS" : "result        FAIL");

            return passed;
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
