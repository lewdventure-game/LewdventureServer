using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Balance;

namespace Server.Api.Balance
{
    internal sealed class BalanceJobWorker : BackgroundService
    {
        private const int IdleDelayMilliseconds = 1000;
        private const int HeartbeatMilliseconds = 1000;
        private const string StaleError = "Balance worker stopped while the job was running, start it again.";

        private readonly BalanceHistoryRecorder _balanceHistoryRecorder;
        private readonly BalanceJobRepository _balanceJobRepository;
        private readonly BalanceJobService _balanceJobService;
        private readonly BalanceOptions _balanceOptions;
        private readonly BalanceRunGate _balanceRunGate;
        private readonly ILogger<BalanceJobWorker> _logger;
        private readonly MongoOptions _mongoOptions;
        private readonly TimeProvider _timeProvider;
        private readonly string _workerId = Environment.MachineName + ":" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);

        public BalanceJobWorker(
            BalanceHistoryRecorder balanceHistoryRecorder,
            BalanceJobRepository balanceJobRepository,
            BalanceJobService balanceJobService,
            IOptions<BalanceOptions> balanceOptions,
            BalanceRunGate balanceRunGate,
            ILogger<BalanceJobWorker> logger,
            IOptions<MongoOptions> mongoOptions,
            TimeProvider timeProvider)
        {
            _balanceHistoryRecorder = balanceHistoryRecorder;
            _balanceJobRepository = balanceJobRepository;
            _balanceJobService = balanceJobService;
            _balanceOptions = balanceOptions.Value;
            _balanceRunGate = balanceRunGate;
            _logger = logger;
            _mongoOptions = mongoOptions.Value;
            _timeProvider = timeProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_balanceOptions.Enabled == false || _balanceOptions.WorkerEnabled == false || _mongoOptions.Enabled == false)
                return;

            _logger.LogInformation("[Balance] worker started worker = {WorkerId}", _workerId);

            while (stoppingToken.IsCancellationRequested == false)
            {
                try
                {
                    var now = _timeProvider.GetUtcNow().UtcDateTime;
                    var stale = await _balanceJobRepository.FailStaleAsync(now.AddSeconds(-_balanceOptions.JobStaleSeconds), StaleError, now, stoppingToken);

                    if (0 < stale)
                        _logger.LogWarning("[Balance] stale jobs failed count = {Count}", stale);

                    var job = await _balanceJobRepository.ClaimNextAsync(_workerId, now, stoppingToken);

                    if (job == null)
                    {
                        await Task.Delay(IdleDelayMilliseconds, stoppingToken);

                        continue;
                    }

                    await ProcessAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "[Error] balance worker loop failed worker = {WorkerId}", _workerId);

                    await Task.Delay(IdleDelayMilliseconds, CancellationToken.None);
                }
            }
        }

        private async Task ProcessAsync(BalanceJobDocument job, CancellationToken stoppingToken)
        {
            var progress = new BalanceJobProgress();
            var startedAt = _timeProvider.GetTimestamp();

            await _balanceRunGate.EnterAsync(stoppingToken);

            try
            {
                var work = Task.Run(() => _balanceJobService.ExecuteAsync(job.Kind, job.Request, progress, stoppingToken), stoppingToken);

                while (work.IsCompleted == false)
                {
                    await Task.WhenAny(work, Task.Delay(HeartbeatMilliseconds, stoppingToken));
                    await _balanceJobRepository.UpdateProgressAsync(job.Id, progress.Done, progress.Total, _timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
                }

                var execution = await work;
                var elapsed = (long)_timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;

                if (execution.Result == null)
                {
                    await _balanceJobRepository.FailAsync(job.Id, execution.Error, execution.StatusCode, _timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
                    _logger.LogWarning("[Balance] job rejected job = {JobId} kind = {Kind} actor = {Actor} error = {Error}", job.Id, job.Kind, job.Actor, execution.Error);

                    return;
                }

                execution.Result.RunId = await _balanceHistoryRecorder.RecordAsync(job.Actor, job.Kind, execution.ConfigVersion, execution.Edits, execution.Summary, execution.Scenario, stoppingToken);

                var result = JsonSerializer.Serialize(execution.Result, execution.Result.GetType(), _balanceJobService.JsonOptions);

                await _balanceJobRepository.CompleteAsync(job.Id, result, execution.Result.RunId, _timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
                _logger.LogInformation(
                    "[Balance] job done job = {JobId} kind = {Kind} actor = {Actor} version = {Version} edits = {Edits} summary = {Summary} elapsedMs = {ElapsedMs}",
                    job.Id,
                    job.Kind,
                    job.Actor,
                    execution.ConfigVersion,
                    execution.Edits,
                    execution.Summary,
                    elapsed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "[Error] balance job failed job = {JobId} kind = {Kind} actor = {Actor}", job.Id, job.Kind, job.Actor);

                await _balanceJobRepository.FailAsync(job.Id, "Balance job failed: " + exception.Message, StatusCodes.Status500InternalServerError, _timeProvider.GetUtcNow().UtcDateTime, CancellationToken.None);
            }
            finally
            {
                _balanceRunGate.Exit();
            }
        }
    }
}
