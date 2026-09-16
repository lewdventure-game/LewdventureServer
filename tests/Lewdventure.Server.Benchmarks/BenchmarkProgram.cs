using BenchmarkDotNet.Running;

namespace Tests.Benchmarks
{
    internal sealed class BenchmarkProgram
    {
        private static void Main(string[] args)
        {
            var config = new BenchmarkConfigFactory().Create();

            BenchmarkSwitcher.FromAssembly(typeof(BenchmarkProgram).Assembly).Run(args, config);
        }
    }
}
