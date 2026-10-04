using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Server.Infrastructure.Alerts;
using Server.Infrastructure.Analytics;

namespace Server.Api.Hosting
{
    internal sealed class AnalyticsWriterService : BackgroundService
    {
        private const int ShutdownFlushSeconds = 5;

        private readonly AnalyticsContextResolver _analyticsContextResolver;
        private readonly AnalyticsQueue _analyticsQueue;
        private readonly AnalyticsSpool _analyticsSpool;
        private readonly IAlertPublisher _alertPublisher;
        private readonly ClickHouseClient _clickHouseClient;
        private readonly ILogger<AnalyticsWriterService> _logger;
        private readonly AnalyticsOptions _options;
        private readonly JsonSerializerOptions _jsonOptions = new();
        private readonly Dictionary<string, AnalyticsPlayerContext> _contexts = new(StringComparer.Ordinal);
        private readonly StringBuilder _payload = new();

        public AnalyticsWriterService(
            AnalyticsContextResolver analyticsContextResolver,
            AnalyticsQueue analyticsQueue,
            AnalyticsSpool analyticsSpool,
            IAlertPublisher alertPublisher,
            ClickHouseClient clickHouseClient,
            ILogger<AnalyticsWriterService> logger,
            IOptions<AnalyticsOptions> options)
        {
            _analyticsContextResolver = analyticsContextResolver;
            _analyticsQueue = analyticsQueue;
            _analyticsSpool = analyticsSpool;
            _alertPublisher = alertPublisher;
            _clickHouseClient = clickHouseClient;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new List<AnalyticsRow>(_options.BatchSize);

            while (stoppingToken.IsCancellationRequested == false)
            {
                await CollectAsync(batch, stoppingToken);
                ReportDropped();

                if (0 < batch.Count)
                    await FlushAsync(batch, stoppingToken);
                else
                    await RetrySpoolAsync(stoppingToken);

                batch.Clear();
            }

            await DrainOnShutdownAsync(batch);
        }

        private async Task CollectAsync(List<AnalyticsRow> batch, CancellationToken stoppingToken)
        {
            using var window = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            window.CancelAfter(TimeSpan.FromSeconds(_options.FlushIntervalSeconds));

            try
            {
                while (batch.Count < _options.BatchSize && await _analyticsQueue.Reader.WaitToReadAsync(window.Token))
                {
                    while (batch.Count < _options.BatchSize && _analyticsQueue.Reader.TryRead(out var row))
                        batch.Add(row);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task FlushAsync(List<AnalyticsRow> batch, CancellationToken cancellationToken)
        {
            await EnrichAsync(batch, cancellationToken);

            var payload = Serialize(batch);
            var result = await _clickHouseClient.InsertEventsAsync(payload, cancellationToken);

            if (result.Succeeded)
            {
                await RetrySpoolAsync(cancellationToken);

                return;
            }

            _logger.LogWarning("[Analytics] insert failed, batch spooled events = {Events} error = {Error}", batch.Count, result.Error);

            var dropped = _analyticsSpool.Write(payload);

            Alert("Analytics insert failed", $"ClickHouse insert failed, events go to spool: {result.Error}", "analytics-insert");

            if (0 < dropped)
                Alert("Analytics spool overflow", $"Spool is over limit, {dropped} oldest batches dropped.", "analytics-spool");
        }

        private async Task RetrySpoolAsync(CancellationToken cancellationToken)
        {
            if (_analyticsSpool.TryReadOldest(out var path, out var payload) == false)
                return;

            var result = await _clickHouseClient.InsertEventsAsync(payload, cancellationToken);

            if (result.Succeeded == false)
                return;

            _analyticsSpool.Delete(path);
            _logger.LogInformation("[Analytics] spooled batch delivered left = {Left}", _analyticsSpool.CountFiles());
        }

        private async Task EnrichAsync(List<AnalyticsRow> batch, CancellationToken cancellationToken)
        {
            _contexts.Clear();

            try
            {
                for (int i = 0; i < batch.Count; i++)
                {
                    var row = batch[i];

                    if (string.IsNullOrEmpty(row.UserId))
                        continue;

                    if (_contexts.TryGetValue(row.UserId, out var context) == false)
                    {
                        context = await _analyticsContextResolver.ResolveAsync(row.UserId, cancellationToken);
                        _contexts[row.UserId] = context;
                    }

                    row.ExperimentId = context.ExperimentId;
                    row.GroupId = context.GroupId;
                    row.ConfigVersion = context.ConfigVersion;

                    if (string.IsNullOrEmpty(row.Country))
                        row.Country = context.Country;
                }
            }
            catch (Exception exception) when (exception is TimeoutException || exception is MongoDB.Driver.MongoException)
            {
                _logger.LogWarning("[Analytics] player context unavailable, events written without it error = {Error}", exception.Message);
            }
        }

        private string Serialize(List<AnalyticsRow> batch)
        {
            _payload.Clear();

            for (int i = 0; i < batch.Count; i++)
                _payload.Append(JsonSerializer.Serialize(batch[i], _jsonOptions)).Append('\n');

            return _payload.ToString();
        }

        private void ReportDropped()
        {
            var dropped = _analyticsQueue.TakeDropped();

            if (dropped <= 0)
                return;

            _logger.LogWarning("[Analytics] queue full, events dropped = {Dropped}", dropped);
            Alert("Analytics queue full", $"{dropped} events dropped because the queue was full.", "analytics-queue");
        }

        private async Task DrainOnShutdownAsync(List<AnalyticsRow> batch)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ShutdownFlushSeconds));

            batch.Clear();

            while (batch.Count < _options.BatchSize && _analyticsQueue.Reader.TryRead(out var row))
                batch.Add(row);

            if (batch.Count == 0)
                return;

            try
            {
                await FlushAsync(batch, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                _analyticsSpool.Write(Serialize(batch));
            }
        }

        private void Alert(string title, string description, string key)
        {
            _alertPublisher.Publish(new AlertMessage(AlertSeverity.Warning, title, description, key));
        }
    }
}
