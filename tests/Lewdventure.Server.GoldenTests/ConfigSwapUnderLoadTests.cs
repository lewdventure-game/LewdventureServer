using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Server.GameConfigs;
using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    public sealed class ConfigSwapUnderLoadTests
    {
        private const int WorkerCount = 16;
        private const int IterationCount = 15;

        [Test]
        public async Task ReplayWhileSwappingConfigSets_MatchesGolden()
        {
            using var host = new GoldenTestHost();

            if (host.Settings.IsUpdateMode)
                Assert.Ignore("[Golden] update mode");

            var services = host.Services;
            var provider = services.GetRequiredService<IGameConfigSetProvider>();
            var builder = services.GetRequiredService<GameConfigSetBuilder>();
            var fileSource = services.GetRequiredService<FileConfigSnapshotSource>();
            var snapshot = await fileSource.LoadAsync(host.Paths.FixturePath, CancellationToken.None);
            var cases = host.Catalog.LoadAll();
            var mismatches = new ConcurrentQueue<string>();
            var swapCount = 0;

            using var cancellation = new CancellationTokenSource();

            var swapTask = Task.Run(async () =>
            {
                while (cancellation.IsCancellationRequested == false)
                {
                    var result = builder.Build(snapshot, "swap-test");

                    provider.Swap(result.ConfigSet!);
                    Interlocked.Increment(ref swapCount);

                    await Task.Yield();
                }
            });

            var workers = new Task[WorkerCount];

            for (int workerIndex = 0; workerIndex < WorkerCount; workerIndex++)
                workers[workerIndex] = RunWorkerAsync(host, cases, workerIndex, mismatches);

            await Task.WhenAll(workers);

            cancellation.Cancel();

            await swapTask;

            Assert.That(swapCount, Is.GreaterThan(0));
            Assert.That(mismatches, Is.Empty, string.Join(Environment.NewLine, mismatches));
        }

        private async Task RunWorkerAsync(GoldenTestHost host, List<GoldenCase> cases, int workerIndex, ConcurrentQueue<string> mismatches)
        {
            var encoding = new UTF8Encoding(false);
            var seed = 42UL;

            for (int iteration = 0; iteration < IterationCount; iteration++)
            {
                var goldenCase = cases[(workerIndex * 5 + iteration * 11) % cases.Count];
                var responsePath = goldenCase.GetResponsePath(seed);
                var response = await host.ReplayAsync(goldenCase, seed);
                var expected = await File.ReadAllTextAsync(responsePath, encoding);

                if (response.StatusCode != goldenCase.GetExpectedStatusCode() || string.Equals(expected, response.Body, StringComparison.Ordinal) == false)
                    mismatches.Enqueue($"case = {goldenCase.Name} worker = {workerIndex} iteration = {iteration} status = {response.StatusCode}");
            }
        }
    }
}
