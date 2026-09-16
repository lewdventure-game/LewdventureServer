using System.Collections.Concurrent;
using System.Text;
using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    [Explicit("Сервисы боя пока singleton с состоянием в полях; тест включается в фазе 4")]
    public sealed class ParallelReplayTests
    {
        private const int TaskCount = 32;
        private const int IterationCount = 20;

        [Test]
        public async Task ParallelReplay_MatchesGolden()
        {
            using var host = new GoldenTestHost();

            var cases = host.Catalog.LoadAll();
            var seeds = host.Settings.Seeds;
            var mismatches = new ConcurrentQueue<string>();
            var tasks = new Task[TaskCount];

            for (int taskIndex = 0; taskIndex < TaskCount; taskIndex++)
                tasks[taskIndex] = RunWorkerAsync(host, cases, seeds, taskIndex, mismatches);

            await Task.WhenAll(tasks);

            Assert.That(mismatches, Is.Empty, string.Join(Environment.NewLine, mismatches));
        }

        private async Task RunWorkerAsync(
            GoldenTestHost host,
            List<GoldenCase> cases,
            IReadOnlyList<ulong> seeds,
            int taskIndex,
            ConcurrentQueue<string> mismatches)
        {
            var encoding = new UTF8Encoding(false);

            for (int iteration = 0; iteration < IterationCount; iteration++)
            {
                var goldenCase = cases[(taskIndex * 7 + iteration * 13) % cases.Count];
                var seed = seeds[(taskIndex + iteration) % seeds.Count];
                var responsePath = goldenCase.GetResponsePath(seed);

                if (File.Exists(responsePath) == false)
                    continue;

                var response = await host.ReplayAsync(goldenCase, seed);
                var expected = await File.ReadAllTextAsync(responsePath, encoding);

                if (response.StatusCode != goldenCase.GetExpectedStatusCode() || string.Equals(expected, response.Body, StringComparison.Ordinal) == false)
                    mismatches.Enqueue($"case = {goldenCase.Name} seed = {seed} task = {taskIndex} iteration = {iteration}");
            }
        }
    }
}
