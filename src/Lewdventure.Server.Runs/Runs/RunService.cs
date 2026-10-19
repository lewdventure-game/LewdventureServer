using Server.Infrastructure.GameConfigs;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Services;
using Server.Stories;

namespace Server.Runs
{
    internal sealed class RunService
    {
        private const string RunIdPrefix = "run_";
        private const int MaxAttempts = 3;

        private readonly IConfigDistributor _configDistributor;
        private readonly GameConfigSelection _gameConfigSelection;
        private readonly ILogger<RunService> _logger;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly PlayerProfileService _playerProfileService;
        private readonly PlayerRewardService _playerRewardService;
        private readonly RunAnalytics _runAnalytics;
        private readonly RunEngine _runEngine;
        private readonly RunRandomFactory _runRandomFactory;
        private readonly RunRecords _runRecords;
        private readonly RunRepository _runRepository;
        private readonly TimeProvider _timeProvider;

        public RunService(
            IConfigDistributor configDistributor,
            GameConfigSelection gameConfigSelection,
            ILogger<RunService> logger,
            PlayerProfileRepository playerProfileRepository,
            PlayerProfileService playerProfileService,
            PlayerRewardService playerRewardService,
            RunAnalytics runAnalytics,
            RunEngine runEngine,
            RunRandomFactory runRandomFactory,
            RunRecords runRecords,
            RunRepository runRepository,
            TimeProvider timeProvider)
        {
            _configDistributor = configDistributor;
            _gameConfigSelection = gameConfigSelection;
            _logger = logger;
            _playerProfileRepository = playerProfileRepository;
            _playerProfileService = playerProfileService;
            _playerRewardService = playerRewardService;
            _runAnalytics = runAnalytics;
            _runEngine = runEngine;
            _runRandomFactory = runRandomFactory;
            _runRecords = runRecords;
            _runRepository = runRepository;
            _timeProvider = timeProvider;
        }

        public async Task<RunOperationResult> GetCurrentAsync(string userId, CancellationToken cancellationToken)
        {
            var run = await _runRepository.GetActiveAsync(userId, cancellationToken);

            if (run == null)
            {
                await ReconcileFinishedRunAsync(userId, cancellationToken);

                return Failed("No active run.");
            }

            return new RunOperationResult(run, null, false, new List<string>());
        }

        public async Task<RunOperationResult> StartAsync(string userId, int storyLevelId, CancellationToken cancellationToken)
        {
            await ReconcileFinishedRunAsync(userId, cancellationToken);

            var active = await _runRepository.GetActiveAsync(userId, cancellationToken);

            if (active != null)
                return Failed($"Run {active.Id} is still active.");

            if (_configDistributor.StoryLevels.TryGet(storyLevelId, out var level) == false)
                return Failed($"Story level {storyLevelId} is missing in configs.");

            var profile = await _playerProfileService.GetOrCreateAsync(userId, _configDistributor, cancellationToken);

            if (IsUnlocked(level, profile) == false)
                return Failed($"Story level {storyLevelId} is locked.");

            if (profile.Loadout.CharacterId <= 0)
                return Failed("Loadout has no character.");

            var runId = RunIdPrefix + Guid.NewGuid().ToString("N");
            var seed = _runRandomFactory.CreateSeed();

            if (_runEngine.TryCreateRun(runId, profile, level, seed, _runRandomFactory.CreateBattleSeedKey(), _gameConfigSelection.Current.Version, out var run, out var errors) == false)
                return new RunOperationResult(null, null, false, errors);

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            run.UserId = userId;
            run.CreatedAt = now;
            run.UpdatedAt = now;
            run.Rev = 1;

            await _runRepository.InsertAsync(run, cancellationToken);

            if (await SetCurrentRunWithRetryAsync(userId, profile, run.Id, now, cancellationToken) == false)
            {
                run.Status = RunDocument.AbandonedStatus;

                await SaveAsync(run, cancellationToken);

                _logger.LogWarning("[Run] start rolled back userId = {UserId} runId = {RunId}", userId, run.Id);

                return Conflict();
            }

            _runAnalytics.TrackStarted(run);
            _logger.LogInformation("[Run] started userId = {UserId} runId = {RunId} level = {Level} stages = {Stages} config = {Config}", userId, run.Id, storyLevelId, run.Stages.Count, run.ConfigVersion);

            return new RunOperationResult(run, null, false, new List<string>());
        }

        public async Task<RunOperationResult> AdvanceAsync(string userId, string runId, string requestId, CancellationToken cancellationToken)
        {
            var run = await LoadForStepAsync(userId, runId, cancellationToken);

            if (run == null)
                return Failed("No active run.");

            if (IsReplay(run, requestId))
                return Replayed(run);

            if (IsActive(run) == false)
                return Failed("No active run.");

            run.LastRequestId = requestId;

            var step = await _runEngine.AdvanceAsync(run, CreateProfileStore(userId), cancellationToken);

            return await ApplyStepAsync(run, step, cancellationToken);
        }

        public async Task<RunOperationResult> ChooseAsync(string userId, string runId, IReadOnlyList<int> picks, string requestId, CancellationToken cancellationToken)
        {
            var run = await LoadForStepAsync(userId, runId, cancellationToken);

            if (run == null)
                return Failed("No active run.");

            if (IsReplay(run, requestId))
                return Replayed(run);

            if (IsActive(run) == false)
                return Failed("No active run.");

            run.LastRequestId = requestId;

            var step = await _runEngine.ChooseAsync(run, picks, CreateProfileStore(userId), cancellationToken);

            return await ApplyStepAsync(run, step, cancellationToken);
        }

        public async Task<RunOperationResult> FinishBattleAsync(string userId, string runId, string requestId, CancellationToken cancellationToken)
        {
            var run = await LoadForStepAsync(userId, runId, cancellationToken);

            if (run == null)
                return Failed("No active run.");

            if (IsReplay(run, requestId))
                return Replayed(run);

            if (IsActive(run) == false)
                return Failed("No active run.");

            run.LastRequestId = requestId;

            var step = await _runEngine.FinishBattleAsync(run, CreateProfileStore(userId), cancellationToken);

            if (step.BattleFinish != null)
                _runAnalytics.TrackBattleFinished(run, step.BattleFinish);

            return await ApplyStepAsync(run, step, cancellationToken);
        }

        public async Task<RunOperationResult> AbandonAsync(string userId, string runId, CancellationToken cancellationToken)
        {
            var run = await LoadActiveAsync(userId, runId, cancellationToken);

            if (run == null)
                return Failed("No active run.");

            var profile = await _playerProfileService.GetOrCreateAsync(userId, _configDistributor, cancellationToken);

            if (run.PendingBattle != null)
                _logger.LogInformation("[Run] pending battle discarded on abandon runId = {RunId} stage = {Stage}", run.Id, run.StageIndex);

            run.PendingBattle = null;
            run.Status = RunDocument.AbandonedStatus;
            run.IsNewRecord = _runRecords.IsNewRecord(profile, run);

            if (await SaveAsync(run, cancellationToken) == false)
                return Conflict();

            await ClearCurrentRunAsync(userId, run, cancellationToken);
            _runAnalytics.TrackFinished(run);
            _logger.LogInformation("[Run] abandoned userId = {UserId} runId = {RunId} stage = {Stage}", userId, run.Id, run.StageIndex);

            return new RunOperationResult(run, null, false, new List<string>());
        }

        private async Task<RunOperationResult> ApplyStepAsync(RunDocument run, RunEngineStep step, CancellationToken cancellationToken)
        {
            if (step.Kind == RunEngineStepKind.Failed)
                return Failed(step.Error);

            if (step.Kind == RunEngineStepKind.Conflict)
                return Conflict();

            if (step.Kind == RunEngineStepKind.LevelEnd)
                return await CompleteAsync(run, cancellationToken);

            if (await SaveAsync(run, cancellationToken) == false)
                return Conflict();

            if (step.Kind == RunEngineStepKind.BattlePending)
            {
                _logger.LogInformation("[Run] battle pending userId = {UserId} runId = {RunId} stage = {Stage} outcome = {Outcome}", run.UserId, run.Id, run.StageIndex, run.PendingBattle!.Outcome);

                return new RunOperationResult(run, step.Outcome, false, new List<string>());
            }

            if (step.Kind == RunEngineStepKind.BattleLost)
            {
                await ClearCurrentRunAsync(run.UserId, run, cancellationToken);
                _runAnalytics.TrackFinished(run);
                _logger.LogInformation("[Run] failed userId = {UserId} runId = {RunId} stage = {Stage}", run.UserId, run.Id, run.StageIndex);

                return new RunOperationResult(run, step.Outcome, false, new List<string>());
            }

            if (step.Kind == RunEngineStepKind.PerksChosen)
            {
                if (_runEngine.IsFinished(run))
                    return await CompleteAsync(run, cancellationToken);

                return new RunOperationResult(run, null, false, new List<string>());
            }

            return await CompleteIfFinishedAsync(run, step.Outcome!, cancellationToken);
        }

        private RunProfileStore CreateProfileStore(string userId)
        {
            return new RunProfileStore(userId, _configDistributor, _playerProfileService, _playerRewardService);
        }

        private async Task<RunOperationResult> CompleteIfFinishedAsync(RunDocument run, RunStepOutcome outcome, CancellationToken cancellationToken)
        {
            if (_runEngine.IsFinished(run) == false)
                return new RunOperationResult(run, outcome, false, new List<string>());

            var completed = await CompleteAsync(run, cancellationToken);

            if (completed.Succeeded == false)
                return completed;

            outcome.RunCompleted = true;

            return new RunOperationResult(run, outcome, false, new List<string>());
        }

        private async Task<RunOperationResult> CompleteAsync(RunDocument run, CancellationToken cancellationToken)
        {
            var currentProfile = await _playerProfileService.GetOrCreateAsync(run.UserId, _configDistributor, cancellationToken);

            run.Status = RunDocument.CompletedStatus;
            run.IsNewRecord = _runRecords.IsNewRecord(currentProfile, run);

            if (await SaveAsync(run, cancellationToken) == false)
                return Conflict();

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(run.UserId, _configDistributor, cancellationToken);
                var expectedRev = profile.Rev;

                if (profile.Story.CompletedLevelIds.Contains(run.StoryLevelId) == false)
                    profile.Story.CompletedLevelIds.Add(run.StoryLevelId);

                _runRecords.StoreRecord(profile, run);

                if (string.Equals(profile.Story.CurrentRunId, run.Id, StringComparison.Ordinal))
                    profile.Story.CurrentRunId = string.Empty;

                profile.Rev = expectedRev + 1;
                profile.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

                if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken))
                {
                    _runAnalytics.TrackFinished(run);
                    _logger.LogInformation("[Run] completed userId = {UserId} runId = {RunId} level = {Level}", run.UserId, run.Id, run.StoryLevelId);

                    var outcome = new RunStepOutcome { RunCompleted = true, EventType = "completed" };

                    return new RunOperationResult(run, outcome, false, new List<string>());
                }
            }

            return Conflict();
        }

        private bool IsUnlocked(IStoryLevelMapper level, PlayerProfileDocument profile)
        {
            var types = level.TriggerTypes;

            for (int i = 0; i < types.Length; i++)
            {
                var value = level.TriggerValues.Length <= i ? 0 : level.TriggerValues[i];

                if (types[i] == StoryLevelTriggerType.AlwaysAvailable)
                    continue;

                if (types[i] == StoryLevelTriggerType.StoryLevelCompleted && profile.Story.CompletedLevelIds.Contains(value) == false)
                    return false;

                if (types[i] == StoryLevelTriggerType.StoryLevelNotCompleted && profile.Story.CompletedLevelIds.Contains(value))
                    return false;
            }

            return true;
        }

        private async Task<RunDocument?> LoadForStepAsync(string userId, string runId, CancellationToken cancellationToken)
        {
            var run = string.IsNullOrEmpty(runId)
                ? await _runRepository.GetActiveAsync(userId, cancellationToken)
                : await _runRepository.GetAsync(runId, cancellationToken);

            if (run == null || string.Equals(run.UserId, userId, StringComparison.Ordinal) == false)
                return null;

            return run;
        }

        private bool IsReplay(RunDocument run, string requestId)
        {
            return string.IsNullOrEmpty(requestId) == false && string.Equals(run.LastRequestId, requestId, StringComparison.Ordinal);
        }

        private bool IsActive(RunDocument run)
        {
            return string.Equals(run.Status, RunDocument.ActiveStatus, StringComparison.Ordinal);
        }

        private RunOperationResult Replayed(RunDocument run)
        {
            _logger.LogInformation("[Run] step replay ignored userId = {UserId} runId = {RunId} stage = {Stage}", run.UserId, run.Id, run.StageIndex);

            return new RunOperationResult(run, null, false, new List<string>(), true);
        }

        private async Task<RunDocument?> LoadActiveAsync(string userId, string runId, CancellationToken cancellationToken)
        {
            var run = string.IsNullOrEmpty(runId)
                ? await _runRepository.GetActiveAsync(userId, cancellationToken)
                : await _runRepository.GetAsync(runId, cancellationToken);

            if (run == null)
                return null;

            if (string.Equals(run.UserId, userId, StringComparison.Ordinal) == false)
                return null;

            if (string.Equals(run.Status, RunDocument.ActiveStatus, StringComparison.Ordinal) == false)
                return null;

            return run;
        }

        private async Task<bool> SaveAsync(RunDocument run, CancellationToken cancellationToken)
        {
            var expectedRev = run.Rev;

            run.Rev = expectedRev + 1;
            run.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

            return await _runRepository.ReplaceAsync(run, expectedRev, cancellationToken);
        }

        private async Task<bool> SetCurrentRunAsync(PlayerProfileDocument profile, string runId, DateTime now, CancellationToken cancellationToken)
        {
            var expectedRev = profile.Rev;

            profile.Story.CurrentRunId = runId;
            profile.Rev = expectedRev + 1;
            profile.UpdatedAt = now;

            return await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken);
        }

        private async Task<bool> SetCurrentRunWithRetryAsync(string userId, PlayerProfileDocument profile, string runId, DateTime now, CancellationToken cancellationToken)
        {
            if (await SetCurrentRunAsync(profile, runId, now, cancellationToken))
                return true;

            for (int attempt = 2; attempt <= MaxAttempts; attempt++)
            {
                var fresh = await _playerProfileService.GetOrCreateAsync(userId, _configDistributor, cancellationToken);

                if (string.IsNullOrEmpty(fresh.Story.CurrentRunId) == false && string.Equals(fresh.Story.CurrentRunId, runId, StringComparison.Ordinal) == false)
                    return false;

                if (await SetCurrentRunAsync(fresh, runId, now, cancellationToken))
                    return true;
            }

            return false;
        }

        private async Task ReconcileFinishedRunAsync(string userId, CancellationToken cancellationToken)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(userId, _configDistributor, cancellationToken);
                var currentRunId = profile.Story.CurrentRunId;

                if (string.IsNullOrEmpty(currentRunId))
                    return;

                var run = await _runRepository.GetAsync(currentRunId, cancellationToken);

                if (run != null && string.Equals(run.Status, RunDocument.ActiveStatus, StringComparison.Ordinal))
                    return;

                var expectedRev = profile.Rev;

                if (run != null && string.Equals(run.UserId, userId, StringComparison.Ordinal))
                {
                    if (string.Equals(run.Status, RunDocument.CompletedStatus, StringComparison.Ordinal) && profile.Story.CompletedLevelIds.Contains(run.StoryLevelId) == false)
                        profile.Story.CompletedLevelIds.Add(run.StoryLevelId);

                    _runRecords.StoreRecord(profile, run);
                }

                profile.Story.CurrentRunId = string.Empty;
                profile.Rev = expectedRev + 1;
                profile.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

                if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken))
                {
                    _logger.LogWarning("[Run] finished run reconciled userId = {UserId} runId = {RunId} status = {Status}", userId, currentRunId, run == null ? "missing" : run.Status);

                    return;
                }
            }
        }

        private async Task ClearCurrentRunAsync(string userId, RunDocument run, CancellationToken cancellationToken)
        {
            var runId = run.Id;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(userId, _configDistributor, cancellationToken);

                if (string.Equals(profile.Story.CurrentRunId, runId, StringComparison.Ordinal) == false)
                    return;

                var expectedRev = profile.Rev;

                profile.Story.CurrentRunId = string.Empty;
                _runRecords.StoreRecord(profile, run);
                profile.Rev = expectedRev + 1;
                profile.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

                if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken))
                    return;
            }

            _logger.LogWarning("[Run] current run not cleared userId = {UserId} runId = {RunId}", userId, runId);
        }

        private RunOperationResult Failed(string error)
        {
            return new RunOperationResult(null, null, false, new List<string> { error });
        }

        private RunOperationResult Conflict()
        {
            return new RunOperationResult(null, null, true, new List<string>());
        }
    }
}
