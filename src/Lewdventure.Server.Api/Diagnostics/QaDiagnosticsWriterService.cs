using MongoDB.Driver;
using Server.Infrastructure.Mongo.Qa;
using Server.Infrastructure.Qa;

namespace Server.Api.Diagnostics
{
    internal sealed class QaDiagnosticsWriterService : BackgroundService
    {
        private const int BatchSize = 500;
        private const int FlushIntervalSeconds = 2;

        private readonly ILogger<QaDiagnosticsWriterService> _logger;
        private readonly QaDiagnosticsQueue _qaDiagnosticsQueue;
        private readonly QaDiagnosticsRepository _qaDiagnosticsRepository;
        private readonly List<RequestTraceDocument> _traces = new(BatchSize);
        private readonly List<ServerErrorDocument> _errors = new(BatchSize);

        public QaDiagnosticsWriterService(
            ILogger<QaDiagnosticsWriterService> logger,
            QaDiagnosticsQueue qaDiagnosticsQueue,
            QaDiagnosticsRepository qaDiagnosticsRepository)
        {
            _logger = logger;
            _qaDiagnosticsQueue = qaDiagnosticsQueue;
            _qaDiagnosticsRepository = qaDiagnosticsRepository;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (stoppingToken.IsCancellationRequested == false)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(FlushIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                }

                await FlushAsync(CancellationToken.None);
            }
        }

        private async Task FlushAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                _traces.Clear();
                _errors.Clear();

                while (_traces.Count < BatchSize && _qaDiagnosticsQueue.Traces.TryRead(out var trace))
                    _traces.Add(trace);

                while (_errors.Count < BatchSize && _qaDiagnosticsQueue.Errors.TryRead(out var error))
                    _errors.Add(error);

                if (_traces.Count == 0 && _errors.Count == 0)
                    break;

                try
                {
                    if (0 < _traces.Count)
                        await _qaDiagnosticsRepository.InsertTracesAsync(_traces, cancellationToken);

                    if (0 < _errors.Count)
                        await _qaDiagnosticsRepository.InsertErrorsAsync(_errors, cancellationToken);
                }
                catch (Exception exception) when (exception is MongoException || exception is TimeoutException)
                {
                    _logger.LogWarning("[Qa] diagnostics write failed traces = {Traces} errors = {Errors} error = {Error}", _traces.Count, _errors.Count, exception.Message);

                    break;
                }
            }

            var dropped = _qaDiagnosticsQueue.TakeDropped();

            if (0 < dropped)
                _logger.LogWarning("[Qa] diagnostics queue overflow dropped = {Dropped}", dropped);
        }
    }
}
