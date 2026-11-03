using Server.Infrastructure.Analytics;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Services;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerDialogueService
    {
        private const int MaxAttempts = 3;
        private const int LastRunCount = 1;
        private const int StaleReservationSeconds = 30;
        private const string CompleteAction = "dialogue_complete";

        private readonly DialogueLaunchKeys _dialogueLaunchKeys = new();
        private readonly DialoguePathValidator _dialoguePathValidator = new();

        private readonly DialogueAnalytics _dialogueAnalytics;
        private readonly GameConfigSetCache _gameConfigSetCache;
        private readonly IdempotencyRepository _idempotencyRepository;
        private readonly ILogger<PlayerDialogueService> _logger;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly PlayerProfileService _playerProfileService;
        private readonly RunRepository _runRepository;
        private readonly TimeProvider _timeProvider;

        public PlayerDialogueService(
            DialogueAnalytics dialogueAnalytics,
            GameConfigSetCache gameConfigSetCache,
            IdempotencyRepository idempotencyRepository,
            ILogger<PlayerDialogueService> logger,
            PlayerProfileRepository playerProfileRepository,
            PlayerProfileService playerProfileService,
            RunRepository runRepository,
            TimeProvider timeProvider)
        {
            _dialogueAnalytics = dialogueAnalytics;
            _gameConfigSetCache = gameConfigSetCache;
            _idempotencyRepository = idempotencyRepository;
            _logger = logger;
            _playerProfileRepository = playerProfileRepository;
            _playerProfileService = playerProfileService;
            _runRepository = runRepository;
            _timeProvider = timeProvider;
        }

        public async Task<PlayerUpdateResult> CompleteAsync(
            string userId,
            DialogueCompletion completion,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(completion.RequestId) == false)
            {
                var reservation = await _idempotencyRepository.GetAsync(userId, completion.RequestId, cancellationToken);

                if (reservation != null && 0 < reservation.ResultRev)
                    return new PlayerUpdateResult(await _playerProfileService.GetOrCreateAsync(userId, cancellationToken), false, new List<string>());

                var claimed = reservation == null
                    ? await ReserveAsync(userId, completion.RequestId, cancellationToken)
                    : await TryTakeOverAsync(reservation, cancellationToken);

                if (claimed == false)
                    return new PlayerUpdateResult(null, true, new List<string>());
            }

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);

                if (IsCompleted(profile, completion.LaunchKey))
                {
                    await CompleteRequestAsync(userId, completion.RequestId, profile.Rev, cancellationToken);

                    return new PlayerUpdateResult(profile, false, new List<string>());
                }

                var pendingIndex = FindPending(profile, completion.LaunchKey);
                var error = Validate(profile, completion, pendingIndex, configDistributor, out var dialogueId);

                if (string.IsNullOrEmpty(error) == false)
                {
                    var lastRunConfigs = await FindLastRunConfigsAsync(userId, configDistributor, cancellationToken);

                    if (lastRunConfigs != null && string.IsNullOrEmpty(Validate(profile, completion, pendingIndex, lastRunConfigs, out dialogueId)))
                        error = string.Empty;
                }

                if (string.IsNullOrEmpty(error) == false)
                {
                    await ReleaseAsync(userId, completion.RequestId, cancellationToken);

                    return new PlayerUpdateResult(null, false, new List<string> { error });
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var expectedRev = profile.Rev;

                if (0 <= pendingIndex)
                    profile.Dialogues.Pending.RemoveAt(pendingIndex);

                profile.Dialogues.Completed.Add(new PlayerDialogueCompletionDocument
                {
                    LaunchKey = completion.LaunchKey,
                    DialogueId = dialogueId,
                    Picks = new List<int>(completion.Picks),
                    CompletedAt = now,
                });
                profile.Rev = expectedRev + 1;
                profile.UpdatedAt = now;

                if (await _playerProfileRepository.ReplaceAsync(profile, expectedRev, cancellationToken) == false)
                    continue;

                await CompleteRequestAsync(userId, completion.RequestId, profile.Rev, cancellationToken);
                _dialogueAnalytics.TrackCompleted(userId, completion.LaunchKey, dialogueId, completion.Picks);
                _logger.LogInformation("[Player] dialogue completed userId = {UserId} launchKey = {LaunchKey} picks = {Picks}", userId, completion.LaunchKey, completion.Picks.Count);

                return new PlayerUpdateResult(profile, false, new List<string>());
            }

            await ReleaseAsync(userId, completion.RequestId, cancellationToken);

            return new PlayerUpdateResult(null, true, new List<string>());
        }

        private async Task<bool> ReserveAsync(string userId, string requestId, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            return await _idempotencyRepository.TryReserveAsync(new IdempotencyDocument
            {
                Id = _idempotencyRepository.CreateKey(userId, requestId),
                UserId = userId,
                RequestId = requestId,
                Action = CompleteAction,
                CreatedAt = now,
                UpdatedAt = now,
            }, cancellationToken);
        }

        private async Task<bool> TryTakeOverAsync(IdempotencyDocument reservation, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (now < reservation.UpdatedAt.AddSeconds(StaleReservationSeconds))
                return false;

            return await _idempotencyRepository.TryTakeOverAsync(reservation, now, cancellationToken);
        }

        private async Task CompleteRequestAsync(string userId, string requestId, long resultRev, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(requestId))
                return;

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            await _idempotencyRepository.CompleteAsync(userId, requestId, resultRev, now, cancellationToken);
        }

        private async Task ReleaseAsync(string userId, string requestId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(requestId))
                return;

            await _idempotencyRepository.ReleaseAsync(userId, requestId, cancellationToken);
        }

        private string Validate(
            PlayerProfileDocument profile,
            DialogueCompletion completion,
            int pendingIndex,
            IConfigDistributor configDistributor,
            out int dialogueId)
        {
            var error = ResolveDialogueId(profile, completion.LaunchKey, pendingIndex, configDistributor, out dialogueId);

            return string.IsNullOrEmpty(error) ? _dialoguePathValidator.Validate(dialogueId, completion.Picks, configDistributor) : error;
        }

        private async Task<IConfigDistributor?> FindLastRunConfigsAsync(string userId, IConfigDistributor current, CancellationToken cancellationToken)
        {
            var runs = await _runRepository.ListAsync(userId, LastRunCount, cancellationToken);

            if (runs.Count == 0)
                return null;

            var configSet = await _gameConfigSetCache.GetAsync(runs[0].ConfigVersion, cancellationToken);

            if (configSet == null || configSet.Distributor == current)
                return null;

            _logger.LogInformation("[Player] dialogue picks checked against last run configs userId = {UserId} config = {Config}", userId, runs[0].ConfigVersion);

            return configSet.Distributor;
        }

        private string ResolveDialogueId(
            PlayerProfileDocument profile,
            string launchKey,
            int pendingIndex,
            IConfigDistributor configDistributor,
            out int dialogueId)
        {
            dialogueId = 0;

            if (0 <= pendingIndex)
            {
                dialogueId = profile.Dialogues.Pending[pendingIndex].DialogueId;

                return string.Empty;
            }

            if (_dialogueLaunchKeys.IsReward(launchKey))
                return $"Dialogue launch {launchKey} is not pending.";

            if (_dialogueLaunchKeys.TryParseLevel(launchKey, out var storyLevelId, out dialogueId, out var trigger) == false)
                return $"Dialogue launch {launchKey} is not recognized.";

            if (configDistributor.StoryLevels.TryGet(storyLevelId, out var level) == false)
                return $"Story level {storyLevelId} is missing in configs.";

            var count = Math.Min(level.DialogueIds.Length, level.DialogueTriggers.Length);

            for (int i = 0; i < count; i++)
            {
                if (level.DialogueIds[i] == dialogueId && string.Equals(_dialogueLaunchKeys.NormalizeTrigger(level.DialogueTriggers[i].Source), trigger, StringComparison.Ordinal))
                    return string.Empty;
            }

            return $"Story level {storyLevelId} has no dialogue {dialogueId} on {trigger}.";
        }

        private bool IsCompleted(PlayerProfileDocument profile, string launchKey)
        {
            var completed = profile.Dialogues.Completed;

            for (int i = 0; i < completed.Count; i++)
            {
                if (string.Equals(completed[i].LaunchKey, launchKey, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private int FindPending(PlayerProfileDocument profile, string launchKey)
        {
            var pending = profile.Dialogues.Pending;

            for (int i = 0; i < pending.Count; i++)
            {
                if (string.Equals(pending[i].LaunchKey, launchKey, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }
}
