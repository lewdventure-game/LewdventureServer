using System.Diagnostics;
using System.Net;
using System.Text;

namespace Server.LoadTest
{
    internal sealed class LoadTestRunner
    {
        private readonly HttpClient _httpClient;
        private readonly LoadTestSettings _settings;
        private readonly List<LoadTestRequest> _requests;

        private int _nextRequestIndex = -1;

        public LoadTestRunner(HttpClient httpClient, LoadTestSettings settings, List<LoadTestRequest> requests)
        {
            _httpClient = httpClient;
            _settings = settings;
            _requests = requests;
        }

        public async Task<LoadTestResults> RunAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            var results = new LoadTestResults();
            var deadline = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
            var workers = new Task[_settings.Concurrency];

            for (int i = 0; i < workers.Length; i++)
                workers[i] = RunWorkerAsync(results, deadline, cancellationToken);

            await Task.WhenAll(workers);

            return results;
        }

        private async Task RunWorkerAsync(LoadTestResults results, long deadline, CancellationToken cancellationToken)
        {
            var interval = _settings.RequestsPerSecond <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)_settings.Concurrency / _settings.RequestsPerSecond);

            while (Stopwatch.GetTimestamp() < deadline && cancellationToken.IsCancellationRequested == false)
            {
                var startedAt = Stopwatch.GetTimestamp();

                await SendAsync(results, cancellationToken);

                if (interval <= TimeSpan.Zero)
                    continue;

                var remaining = interval - Stopwatch.GetElapsedTime(startedAt);

                if (TimeSpan.Zero < remaining)
                    await Task.Delay(remaining, cancellationToken);
            }
        }

        private async Task SendAsync(LoadTestResults results, CancellationToken cancellationToken)
        {
            var index = (int)((uint)Interlocked.Increment(ref _nextRequestIndex) % (uint)_requests.Count);
            var request = _requests[index];
            var startedAt = Stopwatch.GetTimestamp();

            try
            {
                using var content = new StringContent(request.Body, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync("/api/battle/" + _settings.Endpoint, content, cancellationToken);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var latencyMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                var succeeded = response.StatusCode == HttpStatusCode.OK;
                var mismatch = string.Empty;

                if (succeeded && _settings.Verify && Normalize(body) != Normalize(request.ExpectedResponse))
                {
                    succeeded = false;
                    mismatch = request.Name;
                }

                results.Record(mismatch.Length == 0 ? ((int)response.StatusCode).ToString() : "mismatch", latencyMs, succeeded, mismatch);
            }
            catch (Exception exception) when (exception is HttpRequestException || (exception is TaskCanceledException && cancellationToken.IsCancellationRequested == false))
            {
                results.Record(exception.GetType().Name, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, false, string.Empty);
            }
        }

        private string Normalize(string text)
        {
            return text.Replace("\r\n", "\n").TrimEnd();
        }
    }
}
