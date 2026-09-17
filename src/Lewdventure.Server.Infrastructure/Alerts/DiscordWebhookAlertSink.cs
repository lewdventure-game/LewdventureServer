using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Server.Infrastructure.Alerts
{
    internal sealed class DiscordWebhookAlertSink : IAlertSink
    {
        private const int TitleLimit = 256;
        private const int DescriptionLimit = 4000;
        private const int FieldNameLimit = 256;
        private const int FieldValueLimit = 1000;
        private const int FieldCountLimit = 20;
        private const int MaxRetryAfterSeconds = 10;

        private readonly HttpClient _httpClient;
        private readonly ILogger<DiscordWebhookAlertSink> _logger;
        private readonly AlertsOptions _options;

        public DiscordWebhookAlertSink(HttpClient httpClient, ILogger<DiscordWebhookAlertSink> logger, IOptions<AlertsOptions> options)
        {
            _httpClient = httpClient;
            _logger = logger;
            _options = options.Value;
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.HttpTimeoutSeconds);
        }

        public async Task<bool> SendAsync(AlertMessage message, CancellationToken cancellationToken)
        {
            var payload = CreatePayload(message);

            try
            {
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    using var response = await _httpClient.PostAsync(_options.DiscordWebhookUrl, content, cancellationToken);

                    if (response.IsSuccessStatusCode)
                        return true;

                    if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 1)
                    {
                        await Task.Delay(GetRetryAfter(response), cancellationToken);

                        continue;
                    }

                    _logger.LogWarning("[Alert] discord rejected status = {StatusCode} title = {Title}", (int)response.StatusCode, message.Title);

                    return false;
                }
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException)
            {
                _logger.LogWarning("[Alert] discord send failed title = {Title} error = {Error}", message.Title, exception.Message);
            }

            return false;
        }

        public string CreatePayload(AlertMessage message)
        {
            var fields = new List<object>();
            var fieldCount = Math.Min(message.Fields.Count, FieldCountLimit);

            fields.Add(new { name = "environment", value = Truncate(string.IsNullOrEmpty(_options.EnvironmentLabel) ? "unknown" : _options.EnvironmentLabel, FieldValueLimit), inline = true });

            for (int i = 0; i < fieldCount; i++)
            {
                var field = message.Fields[i];

                fields.Add(new { name = Truncate(field.Key, FieldNameLimit), value = Truncate(string.IsNullOrEmpty(field.Value) ? "-" : field.Value, FieldValueLimit), inline = true });
            }

            var payload = new
            {
                username = "Lewdventure Server",
                embeds = new[]
                {
                    new
                    {
                        title = Truncate($"[{message.Severity}] {message.Title}", TitleLimit),
                        description = Truncate(message.Description, DescriptionLimit),
                        color = GetColor(message.Severity),
                        timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                        fields,
                    },
                },
            };

            return JsonConvert.SerializeObject(payload);
        }

        private TimeSpan GetRetryAfter(HttpResponseMessage response)
        {
            var retryAfter = response.Headers.RetryAfter;

            if (retryAfter != null && retryAfter.Delta.HasValue)
                return TimeSpan.FromSeconds(Math.Min(retryAfter.Delta.Value.TotalSeconds, MaxRetryAfterSeconds));

            return TimeSpan.FromSeconds(1);
        }

        private int GetColor(AlertSeverity severity)
        {
            switch (severity)
            {
                case AlertSeverity.Critical:
                    return 0xE74C3C;
                case AlertSeverity.Warning:
                    return 0xF1C40F;
                case AlertSeverity.Resolved:
                    return 0x2ECC71;
                default:
                    return 0x3498DB;
            }
        }

        private string Truncate(string value, int limit)
        {
            if (value.Length <= limit)
                return value;

            return value.Substring(0, limit - 1) + "…";
        }
    }
}
