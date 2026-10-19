using System.Text.Json;
using Microsoft.Extensions.Options;
using Server.Api.Endpoints;
using Server.Api.Options;
using Server.Balance;
using Server.Infrastructure.Mongo.Balance;

namespace Server.Api.Balance
{
    internal sealed class BalanceJobService
    {
        private const string JobIdPrefix = "job_";

        private readonly BalanceJobRepository _balanceJobRepository;
        private readonly BalanceOptions _balanceOptions;
        private readonly BalanceRunExecutor _balanceRunExecutor;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private readonly TimeProvider _timeProvider;

        public BalanceJobService(BalanceJobRepository balanceJobRepository, IOptions<BalanceOptions> balanceOptions, BalanceRunExecutor balanceRunExecutor, TimeProvider timeProvider)
        {
            _balanceJobRepository = balanceJobRepository;
            _balanceOptions = balanceOptions.Value;
            _balanceRunExecutor = balanceRunExecutor;
            _timeProvider = timeProvider;
        }

        public JsonSerializerOptions JsonOptions => _jsonOptions;

        public async Task<BalanceJobCreation> CreateAsync(string kind, JsonElement request, string actor, CancellationToken cancellationToken)
        {
            var normalizedKind = (kind ?? string.Empty).Trim().ToLowerInvariant();
            var raw = request.ValueKind == JsonValueKind.Object ? request.GetRawText() : string.Empty;

            if (raw.Length == 0)
                return new BalanceJobCreation(string.Empty, "Job request must be a JSON object.", StatusCodes.Status400BadRequest);

            var error = Validate(normalizedKind, raw);

            if (error.Length != 0)
                return new BalanceJobCreation(string.Empty, error, StatusCodes.Status400BadRequest);

            if (_balanceOptions.MaxPendingJobs <= await _balanceJobRepository.CountPendingAsync(cancellationToken))
                return new BalanceJobCreation(string.Empty, $"Balance queue already has {_balanceOptions.MaxPendingJobs} jobs, try again later.", StatusCodes.Status409Conflict);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var document = new BalanceJobDocument
            {
                Id = JobIdPrefix + Guid.NewGuid().ToString("N"),
                CreatedAt = now,
                UpdatedAt = now,
                Actor = actor,
                Kind = normalizedKind,
                Request = raw,
            };

            await _balanceJobRepository.InsertAsync(document, cancellationToken);

            return new BalanceJobCreation(document.Id, string.Empty, StatusCodes.Status202Accepted);
        }

        public async Task<BalanceExecution> ExecuteAsync(string kind, string raw, IBalanceProgress progress, CancellationToken cancellationToken)
        {
            var error = Validate(kind, raw);

            if (error.Length != 0)
                return new BalanceExecution(null, error, StatusCodes.Status400BadRequest, string.Empty, 0, string.Empty, string.Empty);

            if (kind == BalanceRunKinds.Series)
                return await _balanceRunExecutor.RunSeriesAsync(Read<BalanceRequest>(raw), progress, cancellationToken);

            if (kind == BalanceRunKinds.Level)
                return await _balanceRunExecutor.RunLevelAsync(Read<BalanceLevelRequest>(raw), progress, cancellationToken);

            if (kind == BalanceRunKinds.Compare)
                return await _balanceRunExecutor.RunCompareAsync(Read<BalanceCompareRequest>(raw), progress, cancellationToken);

            return await _balanceRunExecutor.RunSweepAsync(Read<BalanceSweepRequest>(raw), progress, cancellationToken);
        }

        private string Validate(string kind, string raw)
        {
            try
            {
                if (kind == BalanceRunKinds.Series)
                    return _balanceRunExecutor.ValidateSeries(Read<BalanceRequest>(raw));

                if (kind == BalanceRunKinds.Level)
                    return _balanceRunExecutor.ValidateLevel(Read<BalanceLevelRequest>(raw));

                if (kind == BalanceRunKinds.Compare)
                    return _balanceRunExecutor.ValidateCompare(Read<BalanceCompareRequest>(raw));

                if (kind == BalanceRunKinds.Sweep)
                    return _balanceRunExecutor.ValidateSweep(Read<BalanceSweepRequest>(raw));
            }
            catch (JsonException exception)
            {
                return "Job request is not valid JSON: " + exception.Message;
            }

            return $"Unknown job kind {kind}, use series, level, compare or sweep.";
        }

        private T Read<T>(string raw)
            where T : new()
        {
            return JsonSerializer.Deserialize<T>(raw, _jsonOptions) ?? new T();
        }
    }
}
