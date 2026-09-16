using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Tests.Benchmarks
{
    internal sealed class BenchmarkConfigFactory
    {
        public IConfig Create()
        {
            var job = Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance);

            return ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(job)
                .AddDiagnoser(MemoryDiagnoser.Default);
        }
    }
}
