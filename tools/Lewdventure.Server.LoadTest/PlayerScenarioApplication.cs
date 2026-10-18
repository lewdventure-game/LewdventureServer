using System.Diagnostics;

namespace Server.LoadTest
{
    internal sealed class PlayerScenarioApplication
    {
        public async Task<int> RunAsync(LoadTestSettings settings)
        {
            using var cancellation = new CancellationTokenSource();
            using var handler = new SocketsHttpHandler { MaxConnectionsPerServer = Math.Max(settings.Players, 1), PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(settings.Target), Timeout = TimeSpan.FromSeconds(30) };

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            var results = new PlayerScenarioResults();
            var runId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var deadline = Stopwatch.GetTimestamp() + (long)((settings.RampSeconds + settings.DurationSeconds) * (double)Stopwatch.Frequency);
            var measureFrom = Stopwatch.GetTimestamp() + (long)(settings.RampSeconds * (double)Stopwatch.Frequency);
            var players = new Task[settings.Players];

            Console.WriteLine($"scenario      players");
            Console.WriteLine($"players       {settings.Players}, ramp {settings.RampSeconds} s, measure {settings.DurationSeconds} s");
            Console.WriteLine($"think         {settings.ThinkMilliseconds} ms, battle watch {settings.BattleWatchMilliseconds} ms, analytics every {settings.AnalyticsIntervalSeconds} s");
            Console.WriteLine($"devices       {settings.DevicePrefix}-{runId}-N");

            for (int i = 0; i < players.Length; i++)
            {
                var startDelay = settings.Players <= 1 ? TimeSpan.Zero : TimeSpan.FromSeconds(settings.RampSeconds * (double)i / settings.Players);
                var session = new PlayerSession(httpClient, settings, results, $"{settings.DevicePrefix}-{runId}-{i}", i, measureFrom);

                players[i] = session.RunAsync(startDelay, deadline, cancellation.Token);
            }

            await Task.WhenAll(players);

            return new PlayerScenarioReport().Write(settings, results, TimeSpan.FromSeconds(settings.DurationSeconds)) ? 0 : 1;
        }
    }
}
