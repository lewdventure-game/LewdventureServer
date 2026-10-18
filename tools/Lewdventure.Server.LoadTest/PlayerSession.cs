using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server.LoadTest
{
    internal sealed class PlayerSession
    {
        public const string CurrentRunEndpoint = "run.current";

        private const string ActiveStatus = "active";
        private const string ForkKind = "fork";
        private const string SeedDelivery = "seed";
        private const int MaxStepsPerRun = 200;

        private readonly HttpClient _httpClient;
        private readonly LoadTestSettings _settings;
        private readonly PlayerScenarioResults _results;
        private readonly string _deviceId;
        private readonly long _measureFrom;
        private readonly Random _random;

        private string _accessToken = string.Empty;
        private string _bundleTag = string.Empty;
        private long _sessionId;
        private long _nextAnalyticsAt;

        public PlayerSession(HttpClient httpClient, LoadTestSettings settings, PlayerScenarioResults results, string deviceId, int seed, long measureFrom)
        {
            _httpClient = httpClient;
            _settings = settings;
            _results = results;
            _deviceId = deviceId;
            _measureFrom = measureFrom;
            _random = new Random(seed);
        }

        public async Task RunAsync(TimeSpan startDelay, long deadline, CancellationToken cancellationToken)
        {
            try
            {
                if (TimeSpan.Zero < startDelay)
                    await Task.Delay(startDelay, cancellationToken);

                if (await AuthenticateAsync(cancellationToken) == false)
                {
                    _results.RecordPlayerWithoutAuth();

                    return;
                }

                _sessionId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _nextAnalyticsAt = Stopwatch.GetTimestamp() + Jitter(_settings.AnalyticsIntervalSeconds * 1000);

                await SendAsync("player.profile", HttpMethod.Get, "/api/player/profile", null, cancellationToken);
                await LoadBundleAsync(cancellationToken);

                while (IsBefore(deadline, cancellationToken))
                {
                    var run = await LoadOrStartRunAsync(cancellationToken);

                    if (run == null)
                    {
                        await ThinkAsync(_settings.ThinkMilliseconds, cancellationToken);

                        continue;
                    }

                    await PlayRunAsync(run, deadline, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task<bool> AuthenticateAsync(CancellationToken cancellationToken)
        {
            var response = await SendAsync("auth.device", HttpMethod.Post, "/api/auth/device", new { deviceId = _deviceId, clientVersion = "loadtest" }, cancellationToken);

            if (response.Body == null)
                return false;

            _accessToken = response.Body.Value<string>("accessToken") ?? string.Empty;

            return _accessToken.Length != 0;
        }

        private async Task LoadBundleAsync(CancellationToken cancellationToken)
        {
            var response = await SendAsync("config.bundle", HttpMethod.Get, "/api/config/bundle", null, cancellationToken);

            _bundleTag = response.EntityTag;
        }

        private async Task<JObject?> LoadOrStartRunAsync(CancellationToken cancellationToken)
        {
            var current = await SendAsync(CurrentRunEndpoint, HttpMethod.Get, "/api/run/current", null, cancellationToken);

            if (current.Body != null && current.StatusCode == 200)
                return current.Body;

            if (current.StatusCode != 404)
                return null;

            var started = await SendAsync("run.start", HttpMethod.Post, "/api/run/start", new { storyLevelId = _settings.StoryLevelId }, cancellationToken);

            if (started.Body == null || started.StatusCode != 200)
                return null;

            _results.RecordRunStarted();

            return started.Body;
        }

        private async Task PlayRunAsync(JObject run, long deadline, CancellationToken cancellationToken)
        {
            for (int step = 0; step < MaxStepsPerRun && IsBefore(deadline, cancellationToken); step++)
            {
                if (IsActive(run) == false)
                {
                    _results.RecordRunFinished(string.Equals(run.Value<string>("status"), "completed", StringComparison.Ordinal));

                    return;
                }

                await SendAnalyticsIfDueAsync(cancellationToken);

                var next = await StepAsync(run, cancellationToken);

                if (next == null)
                    return;

                run = next;

                await ThinkAsync(_settings.ThinkMilliseconds, cancellationToken);
            }
        }

        private async Task<JObject?> StepAsync(JObject run, CancellationToken cancellationToken)
        {
            var runId = run.Value<string>("runId") ?? string.Empty;

            if (run.Value<bool>("battlePending"))
                return await FinishBattleAsync(runId, cancellationToken);

            if (run["pendingChoice"] is JObject choice)
            {
                var chosen = await SendAsync("run.choose", HttpMethod.Post, "/api/run/choose", new { runId, requestId = NewRequestId(), picks = ReadPicks(choice), battleDelivery = SeedDelivery }, cancellationToken);

                return chosen.StatusCode == 200 ? chosen.Body : null;
            }

            var advanced = await SendAsync("run.advance", HttpMethod.Post, "/api/run/advance", new { runId, requestId = NewRequestId(), battleDelivery = SeedDelivery }, cancellationToken);

            if (advanced.Body == null || advanced.StatusCode != 200)
                return null;

            if (advanced.Body.Value<bool>("battlePending") == false)
                return advanced.Body;

            _results.RecordBattle();

            await ThinkAsync(_settings.BattleWatchMilliseconds, cancellationToken);

            return await FinishBattleAsync(runId, cancellationToken);
        }

        private async Task<JObject?> FinishBattleAsync(string runId, CancellationToken cancellationToken)
        {
            var finished = await SendAsync("run.battle.finish", HttpMethod.Post, "/api/run/battle/finish", new { runId, requestId = NewRequestId() }, cancellationToken);

            return finished.StatusCode == 200 ? finished.Body : null;
        }

        private List<int> ReadPicks(JObject choice)
        {
            var picks = new List<int>();

            if (string.Equals(choice.Value<string>("kind"), ForkKind, StringComparison.Ordinal))
            {
                picks.Add(1 + _random.Next(2));

                return picks;
            }

            var options = choice["options"] as JArray;
            var count = choice.Value<int?>("choiceCount") ?? 1;

            if (options == null)
                return picks;

            for (int i = 0; i < options.Count && picks.Count < count; i++)
                picks.Add(options[i].Value<int>());

            return picks;
        }

        private async Task SendAnalyticsIfDueAsync(CancellationToken cancellationToken)
        {
            if (_settings.AnalyticsIntervalSeconds <= 0 || Stopwatch.GetTimestamp() < _nextAnalyticsAt)
                return;

            _nextAnalyticsAt = Stopwatch.GetTimestamp() + Jitter(_settings.AnalyticsIntervalSeconds * 1000);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var events = new List<object>();

            for (int i = 0; i < 5; i++)
            {
                events.Add(new Dictionary<string, object>
                {
                    ["event_type"] = "loadtest_ping",
                    ["time"] = now,
                    ["session_id"] = _sessionId,
                    ["insert_id"] = Guid.NewGuid().ToString("N"),
                    ["platform"] = "LoadTest",
                    ["app_version"] = "loadtest",
                    ["device_id"] = _deviceId,
                    ["event_properties"] = new Dictionary<string, object> { ["index"] = i },
                    ["user_properties"] = new Dictionary<string, object>(),
                });
            }

            await SendAsync("analytics.events", HttpMethod.Post, "/api/analytics/events", new { events }, cancellationToken);
        }

        private async Task<PlayerResponse> SendAsync(string endpoint, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        {
            var startedAt = Stopwatch.GetTimestamp();

            using var request = new HttpRequestMessage(method, path);

            if (_accessToken.Length != 0)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            if (string.Equals(endpoint, "config.bundle", StringComparison.Ordinal) && _bundleTag.Length != 0)
                request.Headers.TryAddWithoutValidation("If-None-Match", _bundleTag);

            if (body != null)
                request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                var statusCode = (int)response.StatusCode;
                var tag = response.Headers.ETag == null ? string.Empty : response.Headers.ETag.ToString();

                Record(endpoint, statusCode, startedAt);

                if (statusCode == 401 && string.Equals(endpoint, "auth.device", StringComparison.Ordinal) == false && await AuthenticateAsync(cancellationToken))
                    return new PlayerResponse(statusCode, null, tag);

                return new PlayerResponse(statusCode, Parse(text), tag);
            }
            catch (HttpRequestException)
            {
                Record(endpoint, 0, startedAt);

                return new PlayerResponse(0, null, string.Empty);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested == false)
            {
                Record(endpoint, 0, startedAt);

                return new PlayerResponse(0, null, string.Empty);
            }
        }

        private void Record(string endpoint, int statusCode, long startedAt)
        {
            if (startedAt < _measureFrom)
                return;

            _results.Record(endpoint, statusCode, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }

        private JObject? Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text[0] != '{')
                return null;

            try
            {
                return JObject.Parse(text);
            }
            catch (JsonReaderException)
            {
                return null;
            }
        }

        private bool IsActive(JObject run)
        {
            return string.Equals(run.Value<string>("status"), ActiveStatus, StringComparison.Ordinal);
        }

        private bool IsBefore(long deadline, CancellationToken cancellationToken)
        {
            return Stopwatch.GetTimestamp() < deadline && cancellationToken.IsCancellationRequested == false;
        }

        private async Task ThinkAsync(int milliseconds, CancellationToken cancellationToken)
        {
            if (milliseconds <= 0)
                return;

            await Task.Delay(TimeSpan.FromTicks(Jitter(milliseconds) * TimeSpan.TicksPerSecond / Stopwatch.Frequency), cancellationToken);
        }

        private long Jitter(int milliseconds)
        {
            var factor = 0.5 + _random.NextDouble();

            return (long)(milliseconds * factor / 1000d * Stopwatch.Frequency);
        }

        private string NewRequestId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
