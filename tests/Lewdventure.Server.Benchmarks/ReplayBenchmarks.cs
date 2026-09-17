using BenchmarkDotNet.Attributes;
using Tests.Golden.Infrastructure;

namespace Tests.Benchmarks
{
    public class ReplayBenchmarks
    {
        private const ulong Seed = 42UL;

        private GoldenTestHost _host = null!;
        private GoldenCase _goldenCase = null!;

        [Params("001-baseline-1v1-vs-tank", "019-full-perk-kit", "029-three-summons-slot-order", "900-client-shape-full")]
        public string CaseName { get; set; } = string.Empty;

        [GlobalSetup]
        public void Setup()
        {
            _host = new GoldenTestHost();
            _goldenCase = _host.Catalog.Load(CaseName);
            _host.ReplayAsync(_goldenCase, Seed).GetAwaiter().GetResult();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _host.Dispose();
        }

        [Benchmark]
        public async Task<int> Replay()
        {
            var response = await _host.ReplayAsync(_goldenCase, Seed);

            return response.Body.Length;
        }
    }
}
