using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Server.Admin.Options;

namespace Server.Admin.Analytics
{
    internal sealed class ClickHouseQueryClient
    {
        public const string HttpClientName = "clickhouse";

        private const string UserHeader = "X-ClickHouse-User";
        private const string KeyHeader = "X-ClickHouse-Key";
        private const int MaxErrorLength = 400;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ClickHouseQueryClient> _logger;
        private readonly AdminPanelOptions _options;

        public ClickHouseQueryClient(IHttpClientFactory httpClientFactory, ILogger<ClickHouseQueryClient> logger, IOptions<AdminPanelOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _options = options.Value;
        }

        public bool IsConfigured => string.IsNullOrWhiteSpace(_options.ClickHouse.Url) == false;

        public async Task<ClickHouseQueryResult> QueryAsync(string sql, Dictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            var result = new ClickHouseQueryResult();

            if (IsConfigured == false)
            {
                result.Error = "ClickHouse для админки не настроен (AdminPanel:ClickHouse).";

                return result;
            }

            var url = new StringBuilder(_options.ClickHouse.Url.TrimEnd('/')).Append("/?default_format=JSON");

            foreach (var parameter in parameters)
                url.Append("&param_").Append(parameter.Key).Append('=').Append(Uri.EscapeDataString(parameter.Value));

            using var request = new HttpRequestMessage(HttpMethod.Post, url.ToString());

            request.Headers.Add(UserHeader, _options.ClickHouse.User);
            request.Headers.Add(KeyHeader, _options.ClickHouse.Password);
            request.Content = new StringContent(sql, Encoding.UTF8, "text/plain");

            try
            {
                using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode == false)
                {
                    result.Error = body.Length <= MaxErrorLength ? body : body.Substring(0, MaxErrorLength);
                    _logger.LogWarning("[Admin] analytics query failed status = {Status} error = {Error}", (int)response.StatusCode, result.Error);

                    return result;
                }

                ReadRows(body, result);
                result.IsSuccess = true;
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException || exception is JsonException)
            {
                result.Error = "ClickHouse недоступен: " + exception.Message;
                _logger.LogWarning("[Admin] analytics query failed error = {Error}", exception.Message);
            }

            return result;
        }

        private void ReadRows(string body, ClickHouseQueryResult result)
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("data", out var data) == false)
                return;

            foreach (var row in data.EnumerateArray())
            {
                var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

                foreach (var property in row.EnumerateObject())
                    values[property.Name] = property.Value.Clone();

                result.Rows.Add(values);
            }
        }
    }
}
