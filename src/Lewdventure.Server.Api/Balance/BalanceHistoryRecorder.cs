using Server.Infrastructure.Mongo.Balance;

namespace Server.Api.Balance
{
    internal sealed class BalanceHistoryRecorder
    {
        private const string RunIdPrefix = "bal_";

        private readonly BalanceRunRepository _balanceRunRepository;
        private readonly ILogger<BalanceHistoryRecorder> _logger;
        private readonly TimeProvider _timeProvider;

        public BalanceHistoryRecorder(BalanceRunRepository balanceRunRepository, ILogger<BalanceHistoryRecorder> logger, TimeProvider timeProvider)
        {
            _balanceRunRepository = balanceRunRepository;
            _logger = logger;
            _timeProvider = timeProvider;
        }

        public async Task<string> RecordAsync(string actor, string kind, string configVersion, int edits, string summary, string scenario, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var document = new BalanceRunDocument
            {
                Id = RunIdPrefix + Guid.NewGuid().ToString("N"),
                CreatedAt = now,
                UpdatedAt = now,
                Actor = actor,
                Kind = kind,
                ConfigVersion = configVersion,
                Edits = edits,
                Summary = summary,
                Payload = scenario ?? string.Empty,
            };

            try
            {
                await _balanceRunRepository.InsertAsync(document, cancellationToken);

                return document.Id;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("[Balance] history write failed actor = {Actor} kind = {Kind} error = {Error}", actor, kind, exception.Message);

                return string.Empty;
            }
        }
    }
}
