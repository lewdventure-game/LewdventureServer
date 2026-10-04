using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Server.Admin.Options;

namespace Server.Admin.Backend
{
    internal sealed class GameAdminClient
    {
        public const string HttpClientName = "game-admin";

        private const string AdminKeyHeader = "X-Admin-Key";
        private const string ActorHeader = "X-Admin-Actor";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GameAdminClient> _logger;
        private readonly AdminPanelOptions _options;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public GameAdminClient(IHttpClientFactory httpClientFactory, ILogger<GameAdminClient> logger, IOptions<AdminPanelOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _options = options.Value;
        }

        public async Task<GameAdminResult<T>> GetAsync<T>(string environment, string path, string actor, CancellationToken cancellationToken)
        {
            return await SendAsync<T>(environment, HttpMethod.Get, path, null, actor, cancellationToken);
        }

        public async Task<GameAdminResult<T>> SendAsync<T>(string environment, HttpMethod method, string path, object? body, string actor, CancellationToken cancellationToken)
        {
            var result = new GameAdminResult<T>();
            var target = FindEnvironment(environment);

            if (target == null)
            {
                result.Errors.Add($"Окружение {environment} не настроено.");

                return result;
            }

            using var request = new HttpRequestMessage(method, target.BaseUrl.TrimEnd('/') + path);

            request.Headers.Add(AdminKeyHeader, target.ApiKey);
            request.Headers.Add(ActorHeader, actor);

            if (body != null)
                request.Content = JsonContent.Create(body, options: _jsonOptions);

            try
            {
                using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                result.StatusCode = (int)response.StatusCode;
                result.IsSuccess = response.IsSuccessStatusCode;

                if (result.IsSuccess && 0 < content.Length)
                    result.Data = JsonSerializer.Deserialize<T>(content, _jsonOptions);

                ReadMessages(content, result);

                if (result.IsSuccess == false && result.Errors.Count == 0)
                    result.Errors.Add($"Сервер {environment} ответил {result.StatusCode}.");
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException || exception is JsonException)
            {
                _logger.LogWarning("[Admin] request failed environment = {Environment} path = {Path} error = {Error}", environment, path, exception.Message);

                result.IsSuccess = false;
                result.Errors.Add($"Сервер {environment} недоступен: {exception.Message}");
            }

            return result;
        }

        private AdminEnvironmentOptions? FindEnvironment(string environment)
        {
            for (int i = 0; i < _options.Environments.Count; i++)
            {
                if (string.Equals(_options.Environments[i].Name, environment, StringComparison.Ordinal))
                    return _options.Environments[i];
            }

            return null;
        }

        private void ReadMessages<T>(string content, GameAdminResult<T> result)
        {
            if (content.Length == 0 || content[0] != '{')
                return;

            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            ReadStrings(root, "errors", result.Errors);
            ReadStrings(root, "warnings", result.Warnings);

            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                result.Errors.Add(error.GetString()!);

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String && result.IsSuccess == false)
                result.Errors.Add(detail.GetString()!);
        }

        private void ReadStrings(JsonElement root, string propertyName, List<string> target)
        {
            if (root.TryGetProperty(propertyName, out var values) == false || values.ValueKind != JsonValueKind.Array)
                return;

            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.String)
                    target.Add(value.GetString()!);
            }
        }
    }
}
