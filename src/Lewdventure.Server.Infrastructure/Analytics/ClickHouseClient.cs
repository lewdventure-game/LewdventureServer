using System.Text;
using Microsoft.Extensions.Options;

namespace Server.Infrastructure.Analytics
{
    internal sealed class ClickHouseClient
    {
        private const string UserHeader = "X-ClickHouse-User";
        private const string KeyHeader = "X-ClickHouse-Key";

        private readonly HttpClient _httpClient;
        private readonly AnalyticsOptions _options;

        public ClickHouseClient(HttpClient httpClient, IOptions<AnalyticsOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<ClickHouseInsertResult> InsertEventsAsync(string payload, CancellationToken cancellationToken)
        {
            var query = Uri.EscapeDataString($"INSERT INTO {_options.Database}.events FORMAT JSONEachRow");
            var url = $"{_options.ClickHouseUrl.TrimEnd('/')}/?query={query}&input_format_skip_unknown_fields=1&date_time_input_format=best_effort";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            request.Headers.Add(UserHeader, _options.User);
            request.Headers.Add(KeyHeader, _options.Password);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/x-ndjson");

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                    return new ClickHouseInsertResult(true, string.Empty);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                return new ClickHouseInsertResult(false, $"{(int)response.StatusCode}: {Truncate(body)}");
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException)
            {
                return new ClickHouseInsertResult(false, exception.Message);
            }
        }

        private string Truncate(string value)
        {
            return value.Length <= 300 ? value : value.Substring(0, 300);
        }
    }
}
