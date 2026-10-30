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

        private readonly DialogueLaunchKeys _dialogueLaunchKeys = new();
        private readonly DialoguePathValidator _dialoguePathValidator = new();

        private readonly DialogueAnalytics _dialogueAnalytics;
        private readonly GameConfigSetCache _gameConfigSetCache;
        private readonly ILogger<PlayerDialogueService> _logger;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly PlayerProfileService _playerProfileService;
        private readonly RunRepository _runRepository;
        private readonly TimeProvider _timeProvider;

        public PlayerDialogueService(
            DialogueAnalytics dialogueAnalytics,
            GameConfigSetCache gameConfigSetCache,
            ILogger<PlayerDialogueService> logger,
            PlayerProfileRepository playerProfileRepository,
            PlayerProfileService playerProfileService,
            RunRepository runRepository,
            TimeProvider timeProvider)
        {
            _dialogueAnalytics = dialogueAnalytics;
            _gameConfigSetCache = gameConfigSetCache;
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
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);

                if (IsCompleted(profile, completion.LaunchKey))
                    return new PlayerUpdateResult(profile, false, new List<string>());

                var pendingIndex = FindPending(profile, completion.LaunchKey);
                var error = Validate(profile, completion, pendingIndex, configDistributor, out var dialogueId);

                if (string.IsNullOrEmpty(error) == false)
                {
                    var lastRunConfigs = await FindLastRunConfigsAsync(userId, configDistributor, cancellationToken);

                    if (lastRunConfigs != null && string.IsNullOrEmpty(Validate(profile, completion, pendingIndex, lastRunConfigs, out dialogueId)))
                        error = string.Empty;
                }

                if (string.IsNullOrEmpty(error) == false)
                    return new PlayerUpdateResult(null, false, new List<string> { error });

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

                _dialogueAnalytics.TrackCompleted(userId, completion.LaunchKey, dialogueId, completion.Picks);
                _logger.LogInformation("[Player] dialogue completed userId = {UserId} launchKey = {LaunchKey} picks = {Picks}", userId, completion.LaunchKey, completion.Picks.Count);

                return new PlayerUpdateResult(profile, false, new List<string>());
            }

            return new PlayerUpdateResult(null, true, new List<string>());
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
