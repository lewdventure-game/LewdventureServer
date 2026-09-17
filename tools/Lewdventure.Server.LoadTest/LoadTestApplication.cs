using System.Diagnostics;

namespace Server.LoadTest
{
    internal sealed class LoadTestApplication
    {
        public async Task<int> RunAsync(string[] args)
        {
            LoadTestSettings settings;
            List<LoadTestRequest> requests;

            try
            {
                settings = new LoadTestArgumentsParser().Parse(args);

                var caseLoader = new LoadTestCaseLoader();
                var casesPath = caseLoader.ResolveCasesPath(settings.CasesPath);

                requests = caseLoader.Load(casesPath);

                if (requests.Count == 0)
                    throw new InvalidOperationException($"No replay cases found in {casesPath}.");
            }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is IOException || exception is InvalidOperationException)
            {
                Console.Error.WriteLine(exception.Message);

                return 2;
            }

            using var cancellation = new CancellationTokenSource();
            using var handler = new SocketsHttpHandler { MaxConnectionsPerServer = settings.Concurrency, PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(settings.Target), Timeout = TimeSpan.FromSeconds(30) };

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            Console.WriteLine($"cases         {requests.Count}");

            var runner = new LoadTestRunner(httpClient, settings, requests);

            if (0 < settings.WarmupSeconds)
            {
                Console.WriteLine($"warmup        {settings.WarmupSeconds} s");

                await runner.RunAsync(TimeSpan.FromSeconds(settings.WarmupSeconds), cancellation.Token);
            }

            var stopwatch = Stopwatch.StartNew();
            var results = await runner.RunAsync(TimeSpan.FromSeconds(settings.DurationSeconds), cancellation.Token);

            stopwatch.Stop();

            return new LoadTestReport().Write(settings, results, stopwatch.Elapsed) ? 0 : 1;
        }
    }
}
